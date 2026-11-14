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

        /// The clear result has opened (story save point, docs/architecture.md 16.5 "결과 패널이 열린 뒤"). Every clear opens it,
        /// journey or not (the journey runs from the clear's buttons).
        public event Action ClearShown;

        /// Stage select v4 journey (docs/stage-select-transitions.md; architect review 2026-11-12, lead: the journey never
        /// skips the result). A clear always shows the result (U5 clear or the current panel); from it, Next on the same planet
        /// plays the advance (<see cref="Advancing"/>), Next on a planet's last stage the return with its trail lit
        /// (<see cref="PlanetCompleting"/>), and the map / Esc the return without lighting (<see cref="Returning"/>). The last
        /// stage of all keeps [끝까지 보기]. Off = Next reloads, the map loads the Title scene. Set by the scene root.
        public bool Journey { get; set; }

        /// Journey on, Next from the clear, the next stage (argument, 0-based) is on the same planet. The handler (GameJourney)
        /// detaches the board and calls <see cref="ContinueTo"/>. Must have a handler while <see cref="Journey"/> is on.
        public event Action<int> Advancing;

        /// Journey on, Next from the clear of a planet's last stage (argument, 0-based). The handler pulls the camera back,
        /// begins JourneyReturn (PlanetComplete) and calls <see cref="FinishReturn"/>. Must have a handler while
        /// <see cref="Journey"/> is on.
        public event Action<int> PlanetCompleting;

        /// Journey on, the map (or Esc) from the clear of a stage that is not a planet's finale (argument, 0-based; a finale takes
        /// <see cref="PlanetCompleting"/> so its trail lights once). The handler pulls the camera back, begins
        /// JourneyReturn (Back) and calls <see cref="FinishReturn"/>. Must have a handler while <see cref="Journey"/> is on.
        public event Action<int> Returning;

        /// A journey transition was asked for from the clear and the scene is leaving the result: Next, Again and the map do
        /// nothing until the next stage is entered (a second press would start a second transition).
        public bool Leaving { get; private set; }

        /// <param name="chapterOf">Chapter key per stage (LevelCatalog.ChapterKeyOf).</param>
        /// <param name="chapterInfoOf">Optional: the chapters.json planet of a stage (LevelCatalog.ChapterOf), for the result
        /// panel when the last stage of a planet is cleared. Null = never shown.</param>
        public GameFlowController(AppFlow flow, EntryTracker entries, SaveSystem save, IStageBoard board, IResultPanel result, Func<int, string> chapterOf,
            Func<int, ChapterInfo> chapterInfoOf = null)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.result = result ?? throw new ArgumentNullException(nameof(result));
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
            else if (flow.State == AppState.Game && !flow.InTutorial) Enter(flow.Stage); // a tutorial board: TutorialRunController
            else throw new InvalidOperationException($"[SevenSteps] Game scene started in state {flow.State}");
        }

        // ---------- Player actions ----------

        /// Planet interlude (user request 2026-11-14): asked with (stage, then) when the player leaves a won stage's result
        /// (Next, map, Esc). True = it plays the interlude now and calls <c>then</c> when it ends (so the leave runs again,
        /// with the interlude marked seen); false = nothing to play. Null = no interludes (tests, dev runs).
        public Func<int, Action, bool> Interlude { get; set; }

        bool InterludeFirst(Action then) =>
            Interlude != null && flow.State == AppState.Result && !flow.InTutorial && Interlude(flow.Stage, then);

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
            if (flow.State != AppState.Result || flow.IsLoading || Leaving) return;
            if (IsLast) { EndingRequested?.Invoke(); return; }
            if (InterludeFirst(Next)) return; // a planet's first finale clear: its interlude, then Next again
            if (!flow.CanGoToGame(flow.Stage + 1)) return;
            switch (JourneyRoute.After(Journey, flow.Stage, flow.StageCount, chapterOf))
            {
                case ClearExit.Advance:
                    Leave(Advancing, nameof(Advancing), flow.Stage + 1, false); // telemetry "next" once, in ContinueTo
                    return;
                case ClearExit.PlanetComplete:
                    Leave(PlanetCompleting, nameof(PlanetCompleting), flow.Stage, true);
                    return;
            }
            entries.Announce(TelemetryNames.Via.Next);
            flow.GoToGame(flow.Stage + 1);
        }

        /// The advance transition enters the next stage (telemetry via "next", like the result panel's Next). Only from the
        /// clear (Result) after Next asked for the advance; anything else is a broken transition and throws.
        public void ContinueTo(int stage)
        {
            if (flow.State != AppState.Result || !Leaving) throw new InvalidOperationException($"GameFlowController.ContinueTo in state {flow.State} (leaving {Leaving})");
            if (stage != flow.Stage + 1) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"ContinueTo: the next stage is {flow.Stage + 1}");
            if (!flow.CanGoToGame(stage)) throw new InvalidOperationException($"GameFlowController.ContinueTo({stage}): the flow cannot go there now");
            entries.Announce(TelemetryNames.Via.Next);
            flow.GoToGame(stage);
        }

        /// Result panel "다시 하기" / R in Result: a fresh attempt at the same stage.
        public void Again()
        {
            if (flow.State != AppState.Result || Leaving || !flow.CanGoToGame(flow.Stage)) return;
            entries.Announce(TelemetryNames.Via.Again);
            flow.GoToGame(flow.Stage);
        }

        /// "스테이지" button, result panel "스테이지 선택", Esc.
        public void OpenStageSelect()
        {
            if (flow.IsLoading || !AppFlow.CanGo(flow.State, AppState.StageSelect)) return;
            if (!Leaving && InterludeFirst(OpenStageSelect)) return;
            if (Journey && flow.State == AppState.Result)
            {
                // From the clear: the journey return first (FinishReturn when the pullback is over); nothing while leaving.
                // A planet's finale takes the planet complete return, as Next does, so its trail lights once (lead 2026-11-12).
                if (Leaving) return;
                if (JourneyRoute.After(Journey, flow.Stage, flow.StageCount, chapterOf) == ClearExit.PlanetComplete)
                    Leave(PlanetCompleting, nameof(PlanetCompleting), flow.Stage, true);
                else Leave(Returning, nameof(Returning), flow.Stage, true);
                return;
            }
            save.SaveProgress();
            flow.Go(AppState.StageSelect);
        }

        /// The journey's pullback is over (GameJourney): the star map loads. Only after Next on a planet's last stage or the
        /// map from the clear asked for a return; anything else is a broken transition and throws.
        public void FinishReturn()
        {
            if (!returning || flow.State != AppState.Result) throw new InvalidOperationException($"GameFlowController.FinishReturn in state {flow.State} (returning {returning})");
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
            if (flow.InTutorial) return; // a tutorial board: the scene root's TutorialRunController has it (docs/tutorial.md)
            if (to == AppState.Game) Enter(flow.Stage);
            else if (to == AppState.Result) ShowClear();
        }

        /// Hides the clear and hands the scene to a journey transition (<paramref name="handler"/>), once.
        void Leave(Action<int> handler, string name, int stage, bool toMap)
        {
            if (handler == null) throw new InvalidOperationException($"GameFlowController: journey on but nothing handles {name}");
            Leaving = true;
            returning = toMap;
            result.Hide();
            handler(stage);
        }

        bool returning;

        void Enter(int stage)
        {
            Leaving = false;
            returning = false;
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

        /// Where each win's change goes for the U5 surface's first-clear trace (Services ClearNews); null = not recorded.
        public ClearNews News { get; set; }

        /// Stars of the clear being shown (1..3), set on Won for the result panel.
        int clearStars;

        void OnWon(State s)
        {
            if (flow.State != AppState.Game || flow.InTutorial) return; // a tutorial board: no progress, no stars (docs/tutorial.md 2.5)
            // 3 stars (docs/architecture.md 6.5, design/levels/stars.md 0): judged by Core from the board's attempt hints and
            // the bodies used against the level's bodies.
            clearStars = Stars.Of(BodiesUsed(s), s.Level.Bodies, board.Attempt.Hints);
            bool firstClear = !save.Progress.IsCleared(s.Level);
            int bestBefore = save.Progress.BestStars(s.Level);
            save.Progress.MarkCleared(s.Level, BodiesUsed(s), clearStars);
            save.SaveProgress();
            News?.Record(flow.Stage, firstClear, bestBefore, save.Progress.BestStars(s.Level));
        }

        void OnWinPresented(State s)
        {
            if (s.Status != Status.Win || flow.State != AppState.Game || flow.IsLoading || flow.InTutorial) return;
            flow.Go(AppState.Result); // always the result first, journey or not (lead 2026-11-12)
        }

        void OnLost(State s)
        {
            if (flow.InTutorial) return; // the tutorial shows its lose cue and restarts by itself (docs/tutorial.md 2.4)
            result.Show(ResultModel.Loss(flow.Stage, s.Level.Bodies, IsLast));
        }

        void OnRecovered()
        {
            if (flow.State == AppState.Game && !flow.InTutorial) result.Hide();
        }

        void ShowClear()
        {
            var s = board.State ?? throw new InvalidOperationException("Result without a board state");
            int stage = flow.Stage, bodies = s.Level.Bodies;
            int best = save.Progress.BestBodies(s.Level); // 0 = none, other map or out of range (11.3.1)
            result.Show(ResultModel.Clear(stage, bodies, BodiesUsed(s), best, IsLast,
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
