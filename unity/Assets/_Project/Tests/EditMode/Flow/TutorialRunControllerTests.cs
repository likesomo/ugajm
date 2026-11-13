using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using UnityEditor;

namespace SevenSteps.Tests
{
    /// <summary>The tutorial run (docs/tutorial.md 2.4) on the shipped boards T1..T3, headless. Owner: gameplay-ui.</summary>
    public sealed class TutorialRunControllerTests
    {
        /// The board on a headless GameSession: no presenter, so a win is "presented" as it is applied.
        sealed class Board : ITutorialBoard
        {
            public readonly GameSession Session = new GameSession();
            public int Restarts, Loaded = -1, Ring = -1;
            public Board() => Session.Updated += u =>
            {
                Updated?.Invoke(u);
                if (u.Kind == SessionUpdateKind.Present && u.State.Status == Status.Win) WinPresented?.Invoke(u.State);
                if (u.Kind == SessionUpdateKind.Present && u.State.Status == Status.Lose) Lost?.Invoke(u.State);
            };
            public void LoadTutorial(int index) { Loaded = index; Session.Load(LevelCatalog.Tutorials[index].level.ToLevel()); }
            public State State => Session.State;
            public void Restart() { Restarts++; Session.Send(Command.Restart); }
            public void Highlight(int cell) => Ring = cell;
            public event Action<SessionUpdate> Updated;
            public event Action<State> WinPresented;
            public event Action<State> Lost;
            public void Play(string letters)
            {
                foreach (char c in letters)
                {
                    Assert.IsTrue(Rules.TryParseLetter(c, out var act), c.ToString());
                    Session.Send(Command.FromAct(act));
                }
            }
        }

        sealed class View : ITutorialCueView
        {
            public string Line = "";
            public bool Shown;
            public event Action SkipRequested;
            public void ShowCue(string line, int board, int boards) => Line = line;
            public void Show() => Shown = true;
            public void Hide() => Shown = false;
            public void Skip() => SkipRequested?.Invoke();
        }

        IReadOnlyList<TutorialDto> boards;
        Board board;
        View view;
        TutorialRunController run;
        Language lang;
        readonly List<string> done = new List<string>();
        readonly List<(float s, Action a)> later = new List<(float, Action)>();
        readonly List<bool> ends = new List<bool>();

        [SetUp]
        public void Make()
        {
            LevelCatalog.Load(AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset"));
            boards = LevelCatalog.Tutorials;
            Assume.That(boards.Count, Is.EqualTo(3), "levels.json has T1..T3 (core 4a76921)");
            board = new Board();
            view = new View();
            lang = Language.Ko;
            done.Clear(); later.Clear(); ends.Clear();
            run = new TutorialRunController(boards, board, view, () => lang, done.Add, (s, a) => later.Add((s, a)), i => run.Begin(i), ends.Add);
        }

        [TearDown]
        public void Clean() => run.Dispose();

        void RunLater()
        {
            var now = later.ToList();
            later.Clear();
            foreach (var (_, a) in now) a();
        }

        string CueLine(int b, string on, int n = 0) =>
            boards[b].cues.Last(c => c.on == on && (on != TutorialCues.OnStep || c.n == n)).Line(Languages.Code(lang));

        [Test]
        public void T1ShowsItsCuesAndTheRingThenLeadsToT2()
        {
            run.Begin(0);
            Assert.IsTrue(view.Shown);
            Assert.AreEqual(0, board.Loaded);
            Assert.AreEqual(CueLine(0, TutorialCues.OnStart), view.Line);
            board.Play("R");
            Assert.AreEqual(CueLine(0, TutorialCues.OnStep, 1), view.Line);
            board.Play("RRDD");
            board.Play("L"); // step 6
            var six = boards[0].cues.Last(c => c.on == TutorialCues.OnStep && c.n == 6);
            Assert.AreEqual(six.Line("ko"), view.Line);
            Assert.AreEqual(six.Y * board.State.Level.W + six.X, board.Ring, "the cue's cell ring");
            board.Play("L"); // the seventh step on the exit
            Assert.AreEqual(Status.Win, board.State.Status);
            Assert.AreEqual(CueLine(0, TutorialCues.OnWin), view.Line, "the win cue stays while the run waits");
            Assert.AreEqual(new[] { "board-t1@1" }, done, "saved as done on the win");
            Assert.AreEqual(1, later.Count);
            Assert.AreEqual(TutorialRunController.WinSeconds, later[0].s);
            RunLater();
            Assert.AreEqual(1, run.Index, "T2 next");
            Assert.AreEqual(1, board.Loaded);
            Assert.AreEqual(CueLine(1, TutorialCues.OnStart), view.Line);
        }

        /// The shortest moves (KEEP / SWITCH too: on T3 the door is past the first brother's seven steps) from the
        /// board's start that end in a Bump into a door shut now, or null.
        static string WalkIntoShutDoor(State start)
        {
            var seen = new HashSet<string> { Rules.Key(start) };
            var queue = new Queue<(State s, string path)>();
            queue.Enqueue((start, ""));
            while (queue.Count > 0)
            {
                var (st, path) = queue.Dequeue();
                foreach (char c in "UDLRKX")
                {
                    Rules.TryParseLetter(c, out var act);
                    var r = Rules.Dispatch(st, Command.FromAct(act));
                    if (st.Status == Status.Play)
                        foreach (var e in r.Events)
                            if (e.Type == GameEventType.Bump && e.At >= 0 && Rules.IsDoor(Rules.TileAt(st, e.At)) && !Rules.DoorOpen(st, Rules.TileAt(st, e.At)))
                                return path + c;
                    if (ReferenceEquals(r.State, st) || (r.State.Status != Status.Play && r.State.Status != Status.Choose)) continue;
                    if (seen.Add(Rules.Key(r.State))) queue.Enqueue((r.State, path + c));
                }
            }
            return null;
        }

        [Test]
        public void ABumpIntoTheShutDoorReachesTheDoorShutCue()
        {
            // core f5bb105: doorShut fires on a refused move (SessionUpdate Feedback); the run passes Feedback on to Match.
            for (int b = 0; b < boards.Count; b++)
            {
                if (!boards[b].cues.Any(c => c.on == TutorialCues.OnDoorShut)) continue;
                run.Begin(b);
                var path = WalkIntoShutDoor(board.State);
                Assert.IsNotNull(path, boards[b].id + ": a door to bump into");
                board.Play(path);
                Assert.AreEqual(CueLine(b, TutorialCues.OnDoorShut), view.Line, $"{boards[b].id} after {path}");
                run.Skip();
            }
        }

        [Test]
        public void EveryBoardsSolverPathWinsAndTheLastEndsTheRun()
        {
            run.Begin(0);
            for (int i = 0; i < boards.Count; i++)
            {
                Assert.AreEqual(i, run.Index);
                board.Play(boards[i].level.expected.path);
                Assert.AreEqual(Status.Win, board.State.Status, boards[i].id);
                RunLater();
            }
            Assert.IsFalse(run.Active);
            Assert.AreEqual(new[] { false }, ends, "read through");
            Assert.AreEqual(boards.Select(TutorialBoards.SaveId), done);
            Assert.IsFalse(view.Shown);
            Assert.AreEqual(-1, board.Ring);
        }

        [Test]
        public void ALossShowsTheLoseCueThenRestartsByItself()
        {
            run.Begin(0);
            board.Play("RLRLRLR"); // seven steps that end off the exit: the only brother is spent
            Assert.AreEqual(Status.Lose, board.State.Status);
            Assert.AreEqual(CueLine(0, TutorialCues.OnLose), view.Line);
            Assert.AreEqual(TutorialRunController.LoseSeconds, later.Single().s);
            RunLater();
            Assert.AreEqual(1, board.Restarts);
            Assert.AreEqual(Status.Play, board.State.Status);
            Assert.AreEqual(CueLine(0, TutorialCues.OnRestart), view.Line);
            Assert.IsEmpty(done);
        }

        [Test]
        public void SkipEndsAtOnceAndSavesNothingAndOldTimersDoNothing()
        {
            run.Begin(0);
            board.Play(boards[0].level.expected.path);
            Assert.AreEqual(1, later.Count, "the win waits");
            view.Skip();
            Assert.AreEqual(new[] { true }, ends);
            Assert.IsFalse(run.Active || view.Shown);
            RunLater(); // the win's timer from before the skip
            Assert.IsFalse(run.Active, "no next board after a skip");
            Assert.AreEqual(1, done.Count, "only the board that was won");
        }

        [Test]
        public void TheLanguageRewordsTheCue()
        {
            run.Begin(0);
            lang = Language.En;
            run.Redraw();
            Assert.AreEqual(CueLine(0, TutorialCues.OnStart), view.Line);
            StringAssert.DoesNotContain("방향", view.Line);
        }

        [Test]
        public void WhenTheRunComesAndWhereItLeads()
        {
            var none = new HashSet<string>();
            Assert.IsTrue(TutorialRunController.OnNewGame(boards, none.Contains, 0));
            Assert.IsFalse(TutorialRunController.OnNewGame(boards, none.Contains, 1), "a stage is cleared");
            var all = new HashSet<string>(boards.Select(TutorialBoards.SaveId));
            Assert.IsFalse(TutorialRunController.OnNewGame(boards, all.Contains, 0), "all boards done");
            Assert.IsFalse(TutorialRunController.OnNewGame(new TutorialDto[0], none.Contains, 0), "no boards");
            Assert.IsTrue(TutorialRunController.ToFirstStage(0));
            Assert.IsFalse(TutorialRunController.ToFirstStage(3), "a replay from the map goes back to the map");
        }

        [Test]
        public void AssertsItsInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Begin(3));
            Assert.Throws<ArgumentNullException>(() => new TutorialRunController(null, board, view, () => lang, done.Add, (s, a) => { }, i => { }, ends.Add));
        }

        // ---------- AppFlow: a tutorial board is a Game state that is not a stage ----------

        sealed class Scenes : ISceneLoader
        {
            public string Current { get; private set; } = AppScenes.Boot;
            public void Load(string sceneName) { Current = sceneName; Loaded?.Invoke(sceneName); }
            public event Action<string> Loaded;
        }

        [Test]
        public void AppFlowGoesToATutorialBoardAndBackToStages()
        {
            var flow = new AppFlow(new Scenes(), 50);
            flow.Go(AppState.Title);
            Assert.IsFalse(flow.InTutorial);
            Assert.IsTrue(flow.CanGoToTutorial(0, 3));
            Assert.IsFalse(flow.CanGoToTutorial(3, 3));
            flow.GoToTutorial(0, 3);
            Assert.AreEqual((AppState.Game, 0, true), (flow.State, flow.Tutorial, flow.InTutorial));
            flow.GoToTutorial(2, 3); // Game -> Game in the scene
            Assert.AreEqual(2, flow.Tutorial);
            flow.GoToGame(0);
            Assert.AreEqual((AppState.Game, 0, -1), (flow.State, flow.Stage, flow.Tutorial));
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.GoToTutorial(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.GoToTutorial(-1, 3));
        }
    }
}
