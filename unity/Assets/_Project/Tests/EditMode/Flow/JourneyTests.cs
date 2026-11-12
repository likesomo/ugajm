using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// Stage select v4 journey, pure parts (docs/stage-select-transitions.md): transition clock, clear routing, the dive and
    /// return hand-offs, GameFlowController's journey branch and the launch toggle. The camera / board motion (GameJourney,
    /// JourneyRunner) needs the Game scene and is checked in play.
    public sealed class JourneyTests
    {
        // ---------- TransitionClock ----------

        [Test]
        public void ClockEasesFromZeroToOneAndSkipSettles()
        {
            var c = new TransitionClock(1.5f);
            Assert.AreEqual(0f, c.Eased);
            c.Tick(0.75f);
            Assert.AreEqual(0.5f, c.T, 1e-5f);
            Assert.AreEqual(0.5f, c.Eased, 1e-5f, "SmoothStep is symmetric about the middle");
            Assert.IsFalse(c.Done);
            c.Tick(5f);
            Assert.AreEqual(1.5f, c.Elapsed, "ticks past the end clamp");
            Assert.IsTrue(c.Done);

            var s = new TransitionClock(1.5f);
            s.Skip();
            Assert.IsTrue(s.Done);
            Assert.AreEqual(1f, s.Eased);
        }

        [Test]
        public void ZeroLengthClockIsDoneAtOnceAndBadTimesThrow()
        {
            var z = new TransitionClock(0f);
            Assert.IsTrue(z.Done);
            Assert.AreEqual(1f, z.T);
            Assert.Throws<ArgumentOutOfRangeException>(() => new TransitionClock(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TransitionClock(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TransitionClock(1f).Tick(-0.1f));
        }

        [Test]
        public void WindowStaggersPieces()
        {
            Assert.AreEqual(0f, TransitionClock.Window(0.1f, 0.2f, 1f));
            Assert.AreEqual(0.5f, TransitionClock.Window(0.6f, 0.2f, 1f), 1e-5f);
            Assert.AreEqual(1f, TransitionClock.Window(1f, 0f, 0.67f));
            Assert.Throws<ArgumentException>(() => TransitionClock.Window(0.5f, 0.5f, 0.5f));
        }

        // ---------- JourneyRoute ----------

        static string Chapters(int stage) => stage < 2 ? "moss" : stage < 4 ? "hollow" : "carved";

        [Test]
        public void ClearLeadsOnInsideAPlanetAndBackToTheMapAtItsEnd()
        {
            Assert.AreEqual(ClearExit.Advance, JourneyRoute.After(true, 0, 5, Chapters));
            Assert.AreEqual(ClearExit.PlanetComplete, JourneyRoute.After(true, 1, 5, Chapters));
            Assert.AreEqual(ClearExit.Advance, JourneyRoute.After(true, 2, 5, Chapters));
            Assert.AreEqual(ClearExit.Result, JourneyRoute.After(true, 4, 5, Chapters), "the last stage keeps the result panel (ending)");
            Assert.AreEqual(ClearExit.Result, JourneyRoute.After(false, 0, 5, Chapters), "journey off: the result panel");
            Assert.Throws<ArgumentOutOfRangeException>(() => JourneyRoute.After(true, 5, 5, Chapters));
            Assert.Throws<ArgumentNullException>(() => JourneyRoute.After(true, 0, 5, null));
        }

        [Test]
        public void PlanetIndexFollowsTheMapOrder()
        {
            var keys = new[] { "moss", "hollow", "carved" };
            Assert.AreEqual(1, JourneyRoute.PlanetIndex(keys, "hollow"));
            Assert.AreEqual(-1, JourneyRoute.PlanetIndex(keys, "frost"));
            Assert.AreEqual(0, JourneyRoute.PlanetIndex(new string[0], ""), "no chapters.json: one planet");
        }

        // ---------- Hand-offs ----------

        [Test]
        public void DiveArrivesOnceOnItsStage()
        {
            var d = new JourneyDive();
            Assert.IsFalse(d.Arrive(3), "nothing pending: a normal entry");
            d.Begin(3);
            Assert.Throws<InvalidOperationException>(() => d.Begin(4), "one dive at a time");
            Assert.IsTrue(d.Arrive(3));
            Assert.IsFalse(d.Pending);
            Assert.IsFalse(d.Arrive(3), "arrives once");

            d.Begin(2);
            Assert.Throws<InvalidOperationException>(() => d.Arrive(5), "the Game scene entered another stage");
            d.Cancel();
            Assert.Throws<InvalidOperationException>(() => d.Cancel(), "nothing to cancel");
        }

        [Test]
        public void ReturnSettlesOnceAndTellsTheMap()
        {
            var r = new JourneyReturn();
            int settled = 0;
            r.Settled += () => settled++;
            Assert.Throws<InvalidOperationException>(() => r.Settle());
            r.Begin(2, "carved");
            Assert.IsTrue(r.Pending);
            Assert.AreEqual(2, r.Planet);
            Assert.AreEqual("carved", r.PlanetKey);
            Assert.Throws<InvalidOperationException>(() => r.Begin(3, "frost"));
            r.Settle();
            Assert.AreEqual(1, settled);
            Assert.IsFalse(r.Pending);
            Assert.AreEqual(-1, r.Planet);
            Assert.Throws<ArgumentOutOfRangeException>(() => r.Begin(-1, ""));
            Assert.Throws<ArgumentNullException>(() => r.Begin(0, null));
        }

        // ---------- GameFlowController, journey on ----------

        sealed class Scenes : ISceneLoader
        {
            public string Current { get; set; } = AppScenes.Game;
            public string Pending;
            public event Action<string> Loaded;
            public void Load(string sceneName) { Pending = sceneName; }
            public void Finish() { Current = Pending; Pending = null; Loaded?.Invoke(Current); }
        }

        sealed class Board : IStageBoard
        {
            public readonly List<(int stage, string via)> Loads = new List<(int, string)>();
            public State State { get; set; }
            public void LoadStage(int stage, string via) { Loads.Add((stage, via)); State = Rules.NewState(Seven); }
            public AttemptCounts Attempt { get; set; }
            public event Action<State> Won, WinPresented, Lost;
            public event Action Recovered;
            public void Win() { State = WinState(); Won?.Invoke(State); WinPresented?.Invoke(State); }
            public void Touch() { Lost?.Invoke(State); Recovered?.Invoke(); }
        }

        sealed class Panel : IResultPanel
        {
            public ResultModel Shown;
            public void Show(ResultModel model) => Shown = model;
            public void Hide() => Shown = null;
        }

        static readonly Level Seven = Level.Parse("seven", "", 3, new[] { "##########", "#S......E#", "##########" });

        static State WinState()
        {
            var s = Rules.NewState(Seven);
            for (int k = 0; k < Rules.Steps; k++) s = Rules.Step(s, Dir.Right).State;
            return s;
        }

        Scenes scenes;
        AppFlow flow;
        Board board;
        Panel panel;
        SaveSystem save;
        GameFlowController ctl;

        void Make()
        {
            scenes = new Scenes();
            flow = new AppFlow(scenes, 5);
            board = new Board();
            panel = new Panel();
            save = new SaveSystem(new MemoryStore());
            ctl = new GameFlowController(flow, EntryTracker.ForLaunch(false), save, board, panel, Chapters);
        }

        [TearDown] public void TearDown() => ctl?.Dispose();

        [Test]
        public void ClearShowsTheResultThenNextAdvancesInsideThePlanet()
        {
            Make();
            ctl.Journey = true;
            var advancing = new List<int>();
            int clearShown = 0;
            ctl.Advancing += advancing.Add;
            ctl.PlanetCompleting += _ => Assert.Fail("stage 1 of moss is not its last");
            ctl.Returning += _ => Assert.Fail("Next does not go back to the map");
            ctl.ClearShown += () => clearShown++;
            ctl.Start(0);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State, "the journey never skips the result (lead 2026-11-12)");
            Assert.IsNotNull(panel.Shown);
            Assert.AreEqual(1, clearShown, "the story save point: the result opened");
            Assert.IsTrue(save.Progress.IsCleared(Seven), "the clear is saved as before");
            Assert.IsEmpty(advancing, "nothing moves before Next");
            Assert.Throws<InvalidOperationException>(() => ctl.ContinueTo(1), "ContinueTo only after Next asked for the advance");

            int loads = board.Loads.Count;
            ctl.Next();
            CollectionAssert.AreEqual(new[] { 1 }, advancing);
            Assert.IsNull(panel.Shown, "the clear hides before the advance");
            Assert.IsTrue(ctl.Leaving);
            Assert.AreEqual(loads, board.Loads.Count, "no reload: the transition builds the next board");
            ctl.Next();
            ctl.Again();
            ctl.OpenStageSelect();
            CollectionAssert.AreEqual(new[] { 1 }, advancing, "a second press starts nothing (Leaving)");
            Assert.AreEqual(AppState.Result, flow.State, "the map press during the advance does nothing");
            Assert.Throws<InvalidOperationException>(() => ctl.FinishReturn(), "an advance is not a return");

            Assert.Throws<ArgumentOutOfRangeException>(() => ctl.ContinueTo(3), "only the next stage");
            ctl.ContinueTo(1);
            Assert.AreEqual((1, TelemetryNames.Via.Next), board.Loads[board.Loads.Count - 1], "via next, once (in ContinueTo)");
            Assert.AreEqual(1, flow.Stage);
            Assert.AreEqual(AppState.Game, flow.State);
            Assert.IsFalse(ctl.Leaving, "entering the next stage ends it");
        }

        [Test]
        public void NextOnAPlanetsLastStageReturnsWithTheTrailAndTheLastStageKeepsTheEnding()
        {
            Make();
            ctl.Journey = true;
            var completing = new List<int>();
            ctl.Advancing += _ => Assert.Fail("the planet's last stage");
            ctl.Returning += _ => Assert.Fail("Next on the last stage is the planet complete return");
            ctl.PlanetCompleting += completing.Add;
            ctl.Start(1);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State);
            ctl.Next();
            CollectionAssert.AreEqual(new[] { 1 }, completing);
            Assert.IsNull(panel.Shown);
            ctl.FinishReturn(); // the pullback's end (GameJourney)
            Assert.AreEqual(AppState.StageSelect, flow.State);

            Make();
            ctl.Journey = true;
            int ending = 0;
            ctl.Advancing += _ => Assert.Fail("last stage");
            ctl.PlanetCompleting += _ => Assert.Fail("last stage");
            ctl.EndingRequested += () => ending++;
            ctl.Start(4);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State);
            ctl.Next();
            Assert.AreEqual(1, ending, "the last stage of all: [끝까지 보기]");
        }

        [Test]
        public void MapFromAFinaleClearLightsTheTrailLikeNext()
        {
            // Lead 2026-11-12 (gameplay-ui found it): a Back return from a finale showed the trail already lit, no light-up.
            Make();
            ctl.Journey = true;
            var completing = new List<int>();
            ctl.Advancing += _ => Assert.Fail("map is not Next");
            ctl.Returning += _ => Assert.Fail("a finale's map takes the planet complete return");
            ctl.PlanetCompleting += completing.Add;
            ctl.Start(1); // moss's last stage
            board.Win();
            ctl.OpenStageSelect(); // the clear's map button / Esc
            CollectionAssert.AreEqual(new[] { 1 }, completing);
            ctl.OpenStageSelect();
            CollectionAssert.AreEqual(new[] { 1 }, completing, "a second press starts nothing");
            ctl.FinishReturn();
            Assert.AreEqual(AppState.StageSelect, flow.State);
        }

        [Test]
        public void MapFromTheClearReturnsWithoutLightingAndAgainReloads()
        {
            Make();
            ctl.Journey = true;
            var returning = new List<int>();
            ctl.Advancing += _ => Assert.Fail("map is not Next");
            ctl.PlanetCompleting += _ => Assert.Fail("map is the back return");
            ctl.Returning += returning.Add;
            ctl.Start(0);
            board.Win();
            ctl.OpenStageSelect(); // the clear's map button / Esc
            CollectionAssert.AreEqual(new[] { 0 }, returning);
            Assert.AreEqual(AppState.Result, flow.State, "the pullback plays first");
            ctl.OpenStageSelect();
            CollectionAssert.AreEqual(new[] { 0 }, returning, "a second press starts nothing");
            ctl.FinishReturn(); // the pullback's end
            Assert.AreEqual(AppState.StageSelect, flow.State);

            Make();
            ctl.Journey = true;
            ctl.Start(0);
            board.Win();
            int loads = board.Loads.Count;
            ctl.Again();
            Assert.AreEqual((0, TelemetryNames.Via.Again), board.Loads[loads], "again: the same stage, reloaded, as before");
        }

        [Test]
        public void JourneyWithoutAHandlerThrowsAndJourneyOffKeepsTheResult()
        {
            Make();
            ctl.Journey = true;
            ctl.Start(0);
            board.Win();
            Assert.Throws<InvalidOperationException>(() => ctl.Next(), "journey on needs GameJourney");

            Make();
            ctl.Start(0);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State);
            Assert.IsNotNull(panel.Shown);
            ctl.Next();
            Assert.AreEqual((1, TelemetryNames.Via.Next), board.Loads[board.Loads.Count - 1], "journey off: Next reloads");
            Assert.Throws<InvalidOperationException>(() => ctl.ContinueTo(2), "ContinueTo is the advance transition's");
        }

        [Test]
        public void ReturnKindIsKeptForTheMap()
        {
            var r = new JourneyReturn();
            ReturnKind seen = ReturnKind.PlanetComplete;
            r.Settled += () => seen = r.LastKind;
            r.Begin(1, "hollow", ReturnKind.Back);
            Assert.AreEqual(ReturnKind.Back, r.LastKind);
            r.Settle();
            Assert.AreEqual(ReturnKind.Back, seen, "a Settled handler reads the kind");
            r.Begin(1, "hollow");
            Assert.AreEqual(ReturnKind.PlanetComplete, r.LastKind, "the default kind");
            Assert.Throws<ArgumentOutOfRangeException>(() => new JourneyReturn().Begin(0, "moss", (ReturnKind)7));
        }

        // ---------- Toggle ----------

        [Test]
        public void JourneyRunsOnlyWithTheNewMapUnlessTurnedOff()
        {
            LaunchOptions L(string[] args, string url = "") => LaunchOptions.Parse(args, url);
            Assert.AreEqual(StageSelectToggle.DefaultOn, JourneyToggle.On(L(new string[0])), "default follows the map toggle");
            Assert.IsTrue(JourneyToggle.On(L(new[] { "x.exe", "-newmap" })));
            Assert.IsFalse(JourneyToggle.On(L(new[] { "x.exe", "-oldmap" })));
            Assert.IsFalse(JourneyToggle.On(L(new[] { "x.exe", "-newmap", "-journey", "off" })));
            Assert.IsTrue(JourneyToggle.On(L(new[] { "x.exe", "-newmap", "-journey", "on" })));
            Assert.IsFalse(JourneyToggle.On(L(new string[0], "https://x/?newmap=1&journey=0")));
            Assert.IsTrue(JourneyToggle.On(L(new string[0], "https://x/?newmap=1&journey=1")));
            Assert.IsFalse(JourneyToggle.On(L(new[] { "x.exe", "-newmap", "-flowcheck" })), "flowcheck checks the result path");
            var bad = L(new string[0], "https://x/?journey=maybe");
            Assert.IsNull(bad.Journey);
            Assert.AreEqual(1, bad.Warnings.Count);
        }
    }
}
