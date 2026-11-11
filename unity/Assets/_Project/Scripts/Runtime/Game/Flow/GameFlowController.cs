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
        readonly Func<int, ChapterInfo> chapterInfoOf;
        string enteredChapter;

        /// A stage of another chapter is being entered (fromChapter is null for the first stage this scene enters).
        /// Raised before the board loads the stage. S4/S6 hook: chapter transition card (an IOverlay), chapter theme
        /// preset (UiTheme, lighting, sky, docs/architecture.md 12.8). Chapter ids are levels.json "chapter", "" = first chapter.
        public event Action<string, string, int> ChapterEntering;

        /// [끝까지 보기] on the cleared last stage (docs/architecture.md 16.8): the story ending instead of another stage
        /// (GameController: EndingController, or the star map when the story is off).
        public event Action EndingRequested;

        /// The clear result has opened (story save point, docs/architecture.md 16.5 "결과 패널이 열린 뒤"). With the journey on,
        /// also raised when a clear leads on without the panel (advance, planet complete): the same save point.
        public event Action ClearShown;

        /// Stage select v4 journey (docs/stage-select-transitions.md): a clear leads on without the result panel, to the next
        /// stage of the same planet (<see cref="Advancing"/>) or back to the star map (<see cref="PlanetCompleting"/>). The last
        /// stage of all keeps the result panel ([끝까지 보기]). Off = the result panel after every clear. Set by the scene root.
        public bool Journey { get; set; }

        /// Journey on, a stage was cleared and the next one (argument, 0-based) is on the same planet. The handler (GameJourney)
        /// detaches the board and calls <see cref="ContinueTo"/>. Must have a handler while <see cref="Journey"/> is on.
        public event Action<int> Advancing;

        /// Journey on, the last stage of a planet (argument, 0-based) was cleared. The handler pulls the camera back, begins
        /// JourneyReturn and calls <see cref="OpenStageSelect"/>. Must have a handler while <see cref="Journey"/> is on.
        public event Action<int> PlanetCompleting;

        /// <param name="minBodies">Solver's fewest bodies per stage, 0 = unknown (LevelCatalog.MinBodies).</param>
        /// <param name="chapterOf">Chapter key per stage (LevelCatalog.ChapterKeyOf).</param>
        /// <param name="chapterInfoOf">Optional: the chapters.json planet of a stage (LevelCatalog.ChapterOf), for the result
        /// panel when the last stage of a planet is cleared. Null = never shown.</param>
        public GameFlowController(AppFlow flow, EntryTracker entries, SaveSystem save, IStageBoard board, IResultPanel result, Func<int, int> minBodies, Func<int, string> chapterOf,
            Func<int, ChapterInfo> chapterInfoOf = null)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.minBodies = minBodies ?? throw new ArgumentNullException(nameof(minBodies));
            this.chapterOf = chapterOf ?? throw new ArgumentNullException(nameof(chapterOf));
            this.chapterInfoOf = chapterInfoOf;
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

        /// Result panel "다음 스테이지" / Enter in Result. On the last stage: [끝까지 보기], the ending (<see cref="EndingRequested"/>).
        public void Next()
        {
            if (flow.State != AppState.Result || flow.IsLoading) return;
            if (IsLast) { EndingRequested?.Invoke(); return; }
            if (!flow.CanGoToGame(flow.Stage + 1)) return;
            entries.Announce(TelemetryNames.Via.Next);
            flow.GoToGame(flow.Stage + 1);
        }

        /// The advance transition enters the next stage (telemetry via "next", like the result panel's Next). Only from Game
        /// right after a journey clear; anything else is a broken transition and throws.
        public void ContinueTo(int stage)
        {
            if (flow.State != AppState.Game) throw new InvalidOperationException($"GameFlowController.ContinueTo in state {flow.State}");
            if (stage != flow.Stage + 1) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"ContinueTo: the next stage is {flow.Stage + 1}");
            if (!flow.CanGoToGame(stage)) throw new InvalidOperationException($"GameFlowController.ContinueTo({stage}): the flow cannot go there now");
            entries.Announce(TelemetryNames.Via.Next);
            flow.GoToGame(stage);
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
            clearStars = 0; // a new attempt: no stars until it is won (architect note on 7028a00)
            string chapter = chapterOf(stage) ?? "";
            if (enteredChapter == null || chapter != enteredChapter) ChapterEntering?.Invoke(enteredChapter, chapter, stage);
            enteredChapter = chapter;
            LastVia = entries.TakeVia();
            board.LoadStage(stage, LastVia);
            save.Progress.SetLastStage(board.State.Level); // saved with the next progress save (clear, stage select)
        }

        /// level_start.via of the last stage entry (-titlecheck logs it).
        public string LastVia { get; private set; } = "";

        /// Stars of the clear being shown (0 = unknown minimum), set on Won for the result panel.
        int clearStars;

        void OnWon(State s)
        {
            if (flow.State != AppState.Game) return;
            // 3 stars (docs/architecture.md 6.5): judged by Core, counts from the board's attempt.
            var a = board.Attempt;
            clearStars = Stars.Of(BodiesUsed(s), MinBodies(s), a.Hints, a.Undos, a.Restarts);
            if (clearStars > 0) save.Progress.MarkCleared(s.Level, BodiesUsed(s), clearStars);
            else save.Progress.MarkCleared(s.Level, BodiesUsed(s)); // unknown minimum: no stars
            save.SaveProgress();
        }

        /// The solver's fewest bodies for the stage on the board; 0 (unknown) when it does not fit this stage's bodies.
        int MinBodies(State s)
        {
            int min = minBodies(flow.Stage);
            return min > s.Level.Bodies ? 0 : min;
        }

        void OnWinPresented(State s)
        {
            if (s.Status != Status.Win || flow.State != AppState.Game || flow.IsLoading) return;
            switch (JourneyRoute.After(Journey, flow.Stage, flow.StageCount, chapterOf))
            {
                case ClearExit.Advance:
                    if (Advancing == null) throw new InvalidOperationException("GameFlowController: journey on but nothing handles Advancing");
                    ClearShown?.Invoke();
                    Advancing(flow.Stage + 1);
                    break;
                case ClearExit.PlanetComplete:
                    if (PlanetCompleting == null) throw new InvalidOperationException("GameFlowController: journey on but nothing handles PlanetCompleting");
                    ClearShown?.Invoke();
                    PlanetCompleting(flow.Stage);
                    break;
                default:
                    flow.Go(AppState.Result);
                    break;
            }
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
            int best = save.Progress.BestBodies(s.Level); // 0 = none, other map or out of range (11.3.1)
            result.Show(ResultModel.Clear(stage, bodies, BodiesUsed(s), MinBodies(s), best, IsLast,
                clearStars, save.Progress.BestStars(s.Level), FinishedChapter(stage)));
            ClearShown?.Invoke();
        }

        /// The planet a cleared stage finishes: the last stage of its chapter (the next stage is in another chapter) or the
        /// last stage of all (architect review 6e1a6ca F2). Null when not the last, or there is no chapter data.
        ChapterInfo FinishedChapter(int stage)
        {
            if (chapterInfoOf == null) return null;
            bool last = stage == flow.StageCount - 1 || chapterOf(stage) != chapterOf(stage + 1);
            return last ? chapterInfoOf(stage) : null;
        }

        bool IsLast => flow.Stage == flow.StageCount - 1;

        static int BodiesUsed(State s) => Stars.BodiesUsed(s);
    }
}
