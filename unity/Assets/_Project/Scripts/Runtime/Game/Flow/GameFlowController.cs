using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.UI;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Screen flow inside the Game scene (states Game and Result, docs/scene-flow.md 4.3, S3). Pure C#: turns AppFlow changes
    /// into stage loads on the board and the result panel, saves a clear, and takes the flow actions (stage pick, next,
    /// again, stage select, back). Moved out of GameController. View events and keys call the public methods; a method the
    /// flow cannot take now (scene loading, no next stage) does nothing, it is not an error.
    /// Chapter transitions hook in through <see cref="ChapterEntering"/>.
    /// </summary>
    public sealed class GameFlowController : IDisposable
    {
        readonly AppFlow flow;
        readonly EntryTracker entries;
        readonly SaveSystem save;
        readonly IStageBoard board;
        readonly IResultPanel result;
        readonly Func<int, int> minBodies;
        readonly Func<int, string> chapterOf;
        string enteredChapter;

        /// A stage of another chapter is being entered (fromChapter is null for the first stage this scene enters).
        /// Raised before the board loads the stage. S4/S6 hook: chapter transition card (an IOverlay), chapter theme
        /// preset (UiTheme, lighting, sky, docs/architecture.md 12.8). Chapter ids are levels.json "chapter", "" = first chapter.
        public event Action<string, string, int> ChapterEntering;

        /// <param name="minBodies">Solver's fewest bodies per stage, 0 = unknown (LevelCatalog.MinBodies).</param>
        /// <param name="chapterOf">Chapter id per stage (LevelCatalog.ChapterIdOf).</param>
        public GameFlowController(AppFlow flow, EntryTracker entries, SaveSystem save, IStageBoard board, IResultPanel result, Func<int, int> minBodies, Func<int, string> chapterOf)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.minBodies = minBodies ?? throw new ArgumentNullException(nameof(minBodies));
            this.chapterOf = chapterOf ?? throw new ArgumentNullException(nameof(chapterOf));
            flow.Changed += OnFlowChanged;
            board.Won += OnWon;
            board.WinPresented += OnWinPresented;
            board.Lost += OnLost;
            board.Recovered += OnRecovered;
        }

        public void Dispose()
        {
            flow.Changed -= OnFlowChanged;
            board.Won -= OnWon;
            board.WinPresented -= OnWinPresented;
            board.Lost -= OnLost;
            board.Recovered -= OnRecovered;
        }

        /// The Game scene has started. Boot (editor direct play): go to the start stage, which enters it through Changed.
        /// Game (Boot -> Game for ?level=, or StageSelect -> Game): the flow is already there, enter the stage now.
        public void Start(int firstStage)
        {
            if (flow.State == AppState.Boot) flow.GoToGame(firstStage);
            else if (flow.State == AppState.Game) Enter(flow.Stage);
            else throw new InvalidOperationException($"[SevenSteps] Game scene started in state {flow.State}");
        }

        // ---------- Player actions ----------

        /// Stage list buttons, [ and ].
        public void SelectStage(int stage)
        {
            if (!flow.CanGoToGame(stage)) return;
            entries.Announce(TelemetryNames.Via.Select);
            flow.GoToGame(stage);
        }

        /// Result panel "다음 스테이지" / Enter in Result.
        public void Next()
        {
            if (flow.State != AppState.Result || !flow.CanGoToGame(flow.Stage + 1)) return;
            entries.Announce(TelemetryNames.Via.Next);
            flow.GoToGame(flow.Stage + 1);
        }

        /// Result panel "다시 하기" / R in Result: a fresh attempt at the same stage.
        public void Again()
        {
            if (flow.State != AppState.Result || !flow.CanGoToGame(flow.Stage)) return;
            entries.Announce(TelemetryNames.Via.Again);
            flow.GoToGame(flow.Stage);
        }

        /// "스테이지" button, result panel "스테이지 선택", Esc.
        public void OpenStageSelect()
        {
            if (flow.IsLoading || !AppFlow.CanGo(flow.State, AppState.StageSelect)) return;
            save.SaveProgress();
            flow.Go(AppState.StageSelect);
        }

        /// Esc with no overlay open: AppFlow's back target (Game / Result -> StageSelect).
        public void Back()
        {
            if (!flow.CanBack || !flow.TryGetBackTarget(out var target)) return;
            if (target == AppState.StageSelect) OpenStageSelect();
            else flow.Go(target);
        }

        /// The flow takes this board command instead of the board: in Result (after a clear) only Restart means something,
        /// and it starts a new attempt. While a scene loads, nothing reaches the board.
        public bool TakesCommand(Command cmd)
        {
            if (flow.IsLoading) return true;
            if (flow.State != AppState.Result) return false;
            if (cmd.Type == CommandType.Restart) Again();
            return true;
        }

        // ---------- Flow and board events ----------

        void OnFlowChanged(AppState from, AppState to)
        {
            // A transition that loads another scene belongs to that scene: its root enters the stage in Start.
            if (flow.IsLoading) return;
            if (to == AppState.Game) Enter(flow.Stage);
            else if (to == AppState.Result) ShowClear();
        }

        void Enter(int stage)
        {
            result.Hide();
            string chapter = chapterOf(stage) ?? "";
            if (enteredChapter == null || chapter != enteredChapter) ChapterEntering?.Invoke(enteredChapter, chapter, stage);
            enteredChapter = chapter;
            board.LoadStage(stage, entries.TakeVia());
            save.Progress.SetLastStage(board.State.Level); // saved with the next progress save (clear, stage select)
        }

        void OnWon(State s)
        {
            if (flow.State != AppState.Game) return;
            save.Progress.MarkCleared(s.Level, BodiesUsed(s));
            save.SaveProgress();
        }

        void OnWinPresented(State s)
        {
            if (s.Status == Status.Win && flow.State == AppState.Game && !flow.IsLoading) flow.Go(AppState.Result);
        }

        void OnLost(State s) => result.Show(ResultModel.Loss(flow.Stage, s.Level.Bodies, IsLast));

        void OnRecovered()
        {
            if (flow.State == AppState.Game) result.Hide();
        }

        void ShowClear()
        {
            var s = board.State ?? throw new InvalidOperationException("Result without a board state");
            int stage = flow.Stage, bodies = s.Level.Bodies;
            int min = minBodies(stage);
            int best = save.Progress.BestBodies(s.Level); // 0 = none, other map or out of range (11.3.1)
            // The solver's number is external to this build's stage: out of range = unknown.
            if (min > bodies) min = 0;
            result.Show(ResultModel.Clear(stage, bodies, BodiesUsed(s), min, best, IsLast));
        }

        bool IsLast => flow.Stage == flow.StageCount - 1;

        static int BodiesUsed(State s) => s.Level.Bodies - s.Bodies + 1;
    }
}
