using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// Game scene flow without Unity (docs/scene-flow.md S3): fake loader, fake board, fake result panel.
    /// Checks the stage entries and their telemetry via, the clear save, the result panel and the flow actions.
    public sealed class GameFlowControllerTests
    {
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
            public void Lose() => Lost?.Invoke(State);
            public void Recover() => Recovered?.Invoke();
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
            Assert.AreEqual(Status.Win, s.Status, "test level must be won with 7 steps right");
            return s;
        }

        Scenes scenes;
        AppFlow flow;
        Board board;
        Panel panel;
        SaveSystem save;
        GameFlowController ctl;

        void Make(int stageCount, bool levelFromLaunch)
        {
            scenes = new Scenes();
            flow = new AppFlow(scenes, stageCount);
            board = new Board();
            panel = new Panel();
            save = new SaveSystem(new MemoryStore());
            ctl = new GameFlowController(flow, EntryTracker.ForLaunch(levelFromLaunch), save, board, panel, stage => stage < 2 ? "" : "moss",
                stage => new ChapterInfo { id = stage < 2 ? "ruins" : "moss", planet = stage < 2 ? "폐허" : "이끼" });
        }

        [TearDown] public void TearDown() => ctl?.Dispose();

        [Test]
        public void DirectPlayEntersTheStartStageOnce()
        {
            Make(3, levelFromLaunch: false);
            ctl.Start(1);
            Assert.AreEqual(AppState.Game, flow.State);
            CollectionAssert.AreEqual(new[] { (1, TelemetryNames.Via.Boot) }, board.Loads);
            Assert.AreEqual("seven", save.Progress.lastStage, "last stage saved by name (11.3.1)");
        }

        [Test]
        public void ArrivingInGameEntersFlowStageWithUrlVia()
        {
            Make(3, levelFromLaunch: true);
            scenes.Current = AppScenes.Boot;
            flow.GoToGame(2);          // Bootstrap with ?level=3
            scenes.Finish();           // Game scene loaded, its root starts
            ctl.Start(0);
            CollectionAssert.AreEqual(new[] { (2, TelemetryNames.Via.Url) }, board.Loads);
        }

        [Test]
        public void ClearSavesThenShowsResultThenNextAndAgain()
        {
            Make(3, false);
            ctl.Start(0);
            board.Win();
            Assert.IsTrue(save.Progress.IsCleared(Seven));
            Assert.AreEqual(AppState.Result, flow.State);
            Assert.IsNotNull(panel.Shown);
            Assert.IsTrue(panel.Shown.Cleared);
            Assert.AreEqual(1, panel.Shown.BodiesUsed);
            Assert.AreEqual(1, panel.Shown.BestBodies);

            Assert.IsTrue(ctl.TakesCommand(Command.Restart), "R in Result is the flow's");
            Assert.AreEqual((0, TelemetryNames.Via.Again), board.Loads[board.Loads.Count - 1]);
            Assert.IsNull(panel.Shown, "entering a stage hides the panel");

            board.Win();
            ctl.Next();
            Assert.AreEqual((1, TelemetryNames.Via.Next), board.Loads[board.Loads.Count - 1]);
            Assert.AreEqual(AppState.Game, flow.State);
            Assert.IsFalse(ctl.TakesCommand(Command.Undo), "in Game the board takes commands");
        }

        /// ClearNews (U5 first-clear trace): each win records the stage, first clear and best before/after; a repeat with
        /// nothing new adds nothing.
        [Test]
        public void AWinRecordsItsClearNews()
        {
            Make(3, false);
            var news = new ClearNews();
            ctl.News = news;
            ctl.Start(0);
            board.Win();
            var taken = new List<ClearNote>();
            Assert.IsTrue(news.TryTake(new[] { 0 }, taken));
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual(0, taken[0].Stage);
            Assert.IsTrue(taken[0].FirstClear);
            Assert.AreEqual(0, taken[0].BestBefore);
            Assert.AreEqual(save.Progress.BestStars(Seven), taken[0].BestAfter);
            ctl.Again();
            board.Win();
            Assert.IsFalse(news.TryTake(new[] { 0 }, taken), "the same clear again: nothing new");
        }

        [Test]
        public void ClearingThePlanetsLastStageNamesThePlanet()
        {
            Make(3, false);            // chapters: stages 0-1 "", stage 2 "moss"
            ctl.Start(0);
            board.Win();
            Assert.IsNull(panel.Shown.FinishedChapter, "stage 1 of 2 in its chapter");
            ctl.Next();
            board.Win();
            Assert.AreEqual("ruins", panel.Shown.FinishedChapter?.id, "next stage is in another chapter");
            ctl.Next();
            board.Win();
            Assert.AreEqual("moss", panel.Shown.FinishedChapter?.id, "the last stage of all");
        }

        [Test]
        public void StarsFromTheAttemptAreSavedAndShown()
        {
            // Stars (design/levels/stars.md 0): the test win uses 1 of the level's 3 brothers, so 2 at most.
            Make(3, false);
            ctl.Start(0);
            board.Win();               // no hint
            Assert.AreEqual(2, panel.Shown.Stars);
            Assert.AreEqual(2, panel.Shown.BestStars);
            Assert.AreEqual(2, save.Progress.BestStars(board.State.Level));

            ctl.Again();               // a new attempt: a hint makes it 1 star, the best stays 2
            board.Attempt = new AttemptCounts(1, 0, 0);
            board.Win();
            Assert.AreEqual(1, panel.Shown.Stars);
            Assert.AreEqual(2, panel.Shown.BestStars);

            ctl.Again();               // undo and restart do not cost stars
            board.Attempt = new AttemptCounts(0, 2, 1);
            board.Win();
            Assert.AreEqual(2, panel.Shown.Stars);
        }

        [Test]
        public void ResultModelRejectsStarsItCannotHave()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(0, 3, 1, 1, false, stars: 3, bestStars: 2),
                "best below this clear");
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResultModel(0, false, 3, 0, 0, false, stars: 1));
        }

        [Test]
        public void NextAfterTheLastStageAsksForTheEnding()
        {
            Make(2, false);
            int endings = 0, clears = 0;
            ctl.EndingRequested += () => endings++;
            ctl.ClearShown += () => clears++;
            ctl.Start(1);
            board.Win();
            Assert.IsTrue(panel.Shown.IsLast);
            Assert.AreEqual(1, clears, "story save point when the clear result opens (16.5)");
            int loads = board.Loads.Count;
            ctl.Next();                // [끝까지 보기] (16.8): no stage load, the ending
            Assert.AreEqual(loads, board.Loads.Count);
            Assert.AreEqual(AppState.Result, flow.State);
            Assert.AreEqual(1, endings);
            ctl.Again();
            ctl.Next();                // not in Result: nothing
            Assert.AreEqual(1, endings);
        }

        [Test]
        public void LossShowsThePanelWithoutLeavingGame()
        {
            Make(3, false);
            ctl.Start(0);
            board.Lose();
            Assert.IsNotNull(panel.Shown);
            Assert.IsFalse(panel.Shown.Cleared);
            Assert.AreEqual(AppState.Game, flow.State);
            Assert.IsFalse(ctl.TakesCommand(Command.Undo), "undo still reaches the board after a loss");
            board.Recover();
            Assert.IsNull(panel.Shown);
        }

        [Test]
        public void ChapterHookFiresOnFirstEntryAndOnChapterChange()
        {
            Make(3, false);            // stages 0-1 chapter "", stage 2 "moss"
            var seen = new List<(string, string, int)>();
            ctl.ChapterEntering += (from, to, stage) => seen.Add((from, to, stage));
            ctl.Start(0);
            ctl.SelectStage(1);
            ctl.SelectStage(2);
            CollectionAssert.AreEqual(new[] { ((string)null, "", 0), ("", "moss", 2) }, seen);
        }

        [Test]
        public void StagePickAndBackGoThroughTheFlow()
        {
            Make(3, false);
            ctl.Start(0);
            ctl.SelectStage(2);
            Assert.AreEqual((2, TelemetryNames.Via.Select), board.Loads[board.Loads.Count - 1]);
            ctl.SelectStage(3);        // past the last stage: ignored
            ctl.SelectStage(-1);
            Assert.AreEqual(2, board.Loads.Count);

            ctl.Back();                // Esc: Game -> StageSelect (Title scene)
            Assert.AreEqual(AppState.StageSelect, flow.State);
            Assert.IsTrue(flow.IsLoading);
            Assert.IsTrue(ctl.TakesCommand(Command.Undo), "nothing reaches the board while the scene loads");
            ctl.SelectStage(1);        // a key in the same frame: ignored while loading
            ctl.OpenStageSelect();
            Assert.AreEqual(2, board.Loads.Count);
        }
    }
}
