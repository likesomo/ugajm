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
        public void JourneyAdvancesInsideThePlanetWithoutTheResultPanel()
        {
            Make();
            ctl.Journey = true;
            var advancing = new List<int>();
            int clearShown = 0;
            ctl.Advancing += advancing.Add;
            ctl.PlanetCompleting += _ => Assert.Fail("stage 1 of moss is not its last");
            ctl.ClearShown += () => clearShown++;
            ctl.Start(0);
            board.Win();
            CollectionAssert.AreEqual(new[] { 1 }, advancing);
            Assert.AreEqual(AppState.Game, flow.State, "no Result state");
            Assert.IsNull(panel.Shown);
            Assert.AreEqual(1, clearShown, "the story save point still happens");
            Assert.IsTrue(save.Progress.IsCleared(Seven), "the clear is saved as before");

            ctl.ContinueTo(1);
            Assert.AreEqual((1, TelemetryNames.Via.Next), board.Loads[board.Loads.Count - 1]);
            Assert.AreEqual(1, flow.Stage);
            Assert.Throws<ArgumentOutOfRangeException>(() => ctl.ContinueTo(3), "only the next stage");
        }

        [Test]
        public void JourneyReturnsToTheMapAtThePlanetsEndAndKeepsTheResultForTheLastStage()
        {
            Make();
            ctl.Journey = true;
            var completing = new List<int>();
            ctl.Advancing += _ => { };
            ctl.PlanetCompleting += completing.Add;
            ctl.Start(1);
            board.Win();
            CollectionAssert.AreEqual(new[] { 1 }, completing);
            Assert.AreEqual(AppState.Game, flow.State);
            ctl.OpenStageSelect();
            Assert.AreEqual(AppState.StageSelect, flow.State);

            Make();
            ctl.Journey = true;
            ctl.Advancing += _ => Assert.Fail("last stage");
            ctl.PlanetCompleting += _ => Assert.Fail("last stage");
            ctl.Start(4);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State, "the last stage of all: the result panel and its ending button");
        }

        [Test]
        public void JourneyWithoutAHandlerThrowsAndJourneyOffKeepsTheResult()
        {
            Make();
            ctl.Journey = true;
            ctl.Start(0);
            Assert.Throws<InvalidOperationException>(() => board.Win(), "journey on needs GameJourney");

            Make();
            ctl.Start(0);
            board.Win();
            Assert.AreEqual(AppState.Result, flow.State);
            Assert.IsNotNull(panel.Shown);
            Assert.Throws<InvalidOperationException>(() => ctl.ContinueTo(1), "ContinueTo is the advance transition's, from Game only");
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
