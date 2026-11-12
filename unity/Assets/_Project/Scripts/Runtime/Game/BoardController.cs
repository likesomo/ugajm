using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.Telemetry;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Board progression in the Game scene (docs/scene-flow.md S3): owns the <see cref="GameSession"/>, the board view, messages,
    /// hint, sound and the telemetry recorder. Board keys and HUD board buttons become Commands sent to the session.
    /// Knows nothing about AppFlow: <see cref="Flow.GameFlowController"/> tells it which stage to load (<see cref="IStageBoard"/>)
    /// and listens to Won / WinPresented / Lost / Recovered. Added at runtime by the scene root <see cref="GameController"/>.
    /// Messages, hint text and sound live in MessagePresenter, HintProvider and GameAudio; this class wires them to the session.
    /// </summary>
    public sealed class BoardController : MonoBehaviour, IStageBoard
    {
        Level[] levels;
        GameSession session;
        SaveSystem save;
        GameAssets assets;
        MessagePresenter messages;
        HintProvider hints;
        Localizer loc;
        HudView hud;
        BoardView board;
        GameAudio audio;
        Camera cam;
        int loadedStage = -1;
        bool lost;
        AttemptCounts attempt; // this attempt since LoadStage (docs 6.5)
        TelemetryRecorder recorder;

        public GameSession Session => session;
        /// The win finale starts; returns seconds to keep it up (the story's finale line, StoryDirector.FinaleStarting).
        public Func<float> FinaleStarting { set => board.FinaleStarting = value ?? throw new ArgumentNullException(nameof(value)); }
        public State State => session?.State;
        /// IStageBoard: hints / real undos / restarts after moving, since the last LoadStage (docs/architecture.md 6.5).
        public AttemptCounts Attempt => attempt;
        /// 0-based stage on the board (-1 before the first LoadStage).
        public int LevelIndex => loadedStage;
        public int LevelCount => levels.Length;
        public Level[] Levels => levels;

        /// Raised when a stage is loaded, with how it was entered (boot, url, next, select, again).
        public event Action<int, string> StageEntered;
        /// Raised just before a stage is loaded, with the 0-based stage about to start (Session.State is still the old one).
        public event Action<int> StageLeaving;
        /// Raised for HUD-only actions that do not touch the session (hint).
        public event Action HintShown;

        // IStageBoard (docs/scene-flow.md S3): the game flow controller listens.
        public event Action<State> Won;
        public event Action<State> WinPresented;
        public event Action<State> Lost;
        public event Action Recovered;

        /// Set by the scene root: true while board input must be ignored (an overlay is open, a scene is loading).
        public Func<bool> InputBlocked = () => false;
        /// Set by the scene root: the flow takes a command instead of the board (Result: R = again). True = taken.
        public Func<Command, bool> FlowTakes = _ => false;

        /// Called once by GameController.Awake (the scene root) after the bootstrap. The board side: session, board view, HUD, sound, telemetry.
        /// <paramref name="loc"/>: the UI language (messages, hint, stage texts, board exit label); a switch redraws them.
        public void Init(GameAssets assets, Level[] levels, SaveSystem save, HudView hud, Camera cam, TileSet tiles, CameraRig? cameraOverride, ITelemetry telemetry,
            Localizer loc)
        {
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            if (session != null) throw new InvalidOperationException("BoardController.Init twice");
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.cam = cam != null ? cam : throw new ArgumentNullException(nameof(cam));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));

            audio = new GameAudio(gameObject, assets, save);
            messages = new MessagePresenter(loc);
            hints = new HintProvider(levels, loc, LevelCatalog.PathOf); // the stored route: the staged hint's goals agree with it

            hud.UndoRequested += () => Send(Command.Undo);
            hud.RestartRequested += Restart;
            hud.KeepRequested += () => Send(Command.Keep);
            hud.SwitchRequested += () => Send(Command.Switch);

            board = gameObject.AddComponent<BoardView>();
            board.Init(tiles, hud.LabelLayer, cam, cameraOverride);
            board.ExitLabel = loc.Get("board.exit");
            loc.Changed += OnLanguageChanged;

            session = new GameSession(board);
            board.Hurry = () => session.QueuedCount > 0;
            session.Updated += OnSessionUpdated;
            session.Presented += OnPresented;
            if (telemetry is HttpTelemetry http) recorder = new TelemetryRecorder(this, session, http);
            choiceView = hud;
            choice = new ChoiceController(session, hud, board, Send);
            // Accessibility (docs 11.3.2): step numbers and reduce motion follow the settings, also when changed while playing.
            board.StepNumbers = hud.StepNumbers = save.Settings.showStepNumbers;
            board.ReduceMotion = save.Settings.reduceMotion;
            save.SettingsChanged += OnSettingsChanged;
        }

        /// Settings language toggle (docs/architecture.md 13.3): HUD words and fonts, the message line, the exit label.
        void OnLanguageChanged(Language lang)
        {
            hud.Apply(loc);
            board.ExitLabel = loc.Get("board.exit");
            if (session.State != null && !board.IsAnimating) board.Snap(session.State); // else the next draw picks it up
            if (session.State != null) Refresh();
        }

        void OnDestroy()
        {
            if (loc != null) loc.Changed -= OnLanguageChanged;
            if (save != null) save.SettingsChanged -= OnSettingsChanged;
            choice?.Dispose();
            recorder?.Dispose();
            audio?.Dispose();
            chapters?.Dispose();
        }

        void OnSettingsChanged(SettingsData settings)
        {
            board.ReduceMotion = settings.reduceMotion;
            if (settings.showStepNumbers != StepNumbers) StepNumbers = settings.showStepNumbers;
        }

        ChoiceController choice;
        /// The KEEP / SWITCH cards, ghosts and focus (docs 4.4). The scene root sets its tutorial funcs.
        public ChoiceController Choice => choice;
        IChoiceView choiceView;

        /// U5 (docs/ui-u5-kit.md 3): the 7th-step choice on <paramref name="view"/> instead of the HUD cards. Call once after
        /// Init, before the scene root sets Choice.ExplainChoices / ShowGuide (a new ChoiceController is made).
        public void UseChoiceView(IChoiceView view)
        {
            if (view == null || (view is UnityEngine.Object o && o == null)) throw new ArgumentNullException(nameof(view));
            if (choice == null) throw new InvalidOperationException("UseChoiceView before Init");
            if (ReferenceEquals(view, choiceView)) return;
            choice.Dispose();
            choiceView = view;
            choice = new ChoiceController(session, view, board, Send);
        }

        /// After the HUD redrew from the state (stage, its text in the UI language): a second HUD (U5) follows it.
        public event Action<State, int, LevelTextDto> HudRefreshed;

        /// The board view, for the journey transitions' hooks (BoardView.CameraHeld, DetachSnapshot, BoardRoot; scene-flow).
        public BoardView View => board;

        /// Accessibility "show step numbers" (SettingsData.showStepNumbers, off by default; the toggle is scene-flow's):
        /// numeric footprints and the steps-left number on the board. Redraws at once.
        public bool StepNumbers
        {
            get => board.StepNumbers;
            set
            {
                board.StepNumbers = hud.StepNumbers = value;
                if (session.State != null && !board.IsAnimating) board.Snap(session.State);
            }
        }

        /// Smoke negative control (LaunchOptions.SmokeView.Empty): the board plays as usual but draws nothing.
        public void HideBoardForSmoke()
        {
            if (board == null) throw new InvalidOperationException("BoardController.HideBoardForSmoke before Init");
            board.Hidden = true;
        }

        ChapterStage chapters;
        Func<int, ChapterPreset> chapterFor;
        TileSet tileSetOverride;

        /// Chapters (docs/architecture.md 12.8), set once by the scene root after Init. Without this call the board keeps
        /// the Init tile set for every stage (today's behaviour).
        /// <param name="chapterFor">Chapter preset of a 0-based stage (LevelCatalog id → ChapterInfo → AppConfig.Chapter), null = none.</param>
        /// <param name="tileSetOverride">AppConfig.tileSet: when set, every chapter draws with it (debug / transition).</param>
        public void UseChapters(ChapterStage stage, Func<int, ChapterPreset> chapterFor, TileSet tileSetOverride)
        {
            if (session == null) throw new InvalidOperationException("BoardController.UseChapters before Init");
            if (chapters != null) throw new InvalidOperationException("BoardController.UseChapters twice");
            chapters = stage ?? throw new ArgumentNullException(nameof(stage));
            this.chapterFor = chapterFor ?? throw new ArgumentNullException(nameof(chapterFor));
            this.tileSetOverride = tileSetOverride;
        }

        /// 12.8 order: AppConfig.tileSet override → the chapter's tileSet → GameAssets.tileSet.
        public static TileSet TileSetFor(TileSet tileSetOverride, ChapterPreset chapter, TileSet fallback)
        {
            var t = tileSetOverride != null ? tileSetOverride : chapter != null && chapter.tileSet != null ? chapter.tileSet : fallback;
            return t != null ? t : throw new InvalidOperationException("[SevenSteps] No TileSet for this chapter: set ChapterPreset.tileSet, AppConfig.tileSet or GameAssets.tileSet");
        }

        /// Before the stage's Rebuild: a new chapter swaps sky, planet, lights and the board's tile set and materials.
        void EnterChapter(int stage)
        {
            if (chapters == null) return;
            var c = chapterFor(stage);
            if (chapters.Enter(c)) board.UseChapter(TileSetFor(tileSetOverride, c, assets.tileSet), c);
        }

        /// IStageBoard: load a stage the flow chose.
        public void LoadStage(int stage, string via)
        {
            if (session == null) throw new InvalidOperationException("BoardController.LoadStage before Init");
            if (stage < 0 || stage >= levels.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage outside levels.json");
            if (string.IsNullOrEmpty(via)) throw new ArgumentException("telemetry via is required", nameof(via));
            StageLeaving?.Invoke(stage);
            loadedStage = stage;
            lost = false;
            attempt = default; // a new attempt: select, next, again, [ ]
            hintSteps = null; // the staged hint starts again (contract review (3))
            hintWarned = false; // a new attempt asks again before its first costing hint (QA-2)
            LastHint = null;
            board.ShowHintGoal(-1);
            // One line per stage load for performance checks (WebGL console, player log): where a planet change spends time.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            EnterChapter(stage);
            long chapterMs = clock.ElapsedMilliseconds;
            session.Load(levels[stage]); // Rebuild runs inside Load
            long boardMs = clock.ElapsedMilliseconds - chapterMs;
            chapters?.Fit(board.Grid);
            long fitMs = clock.ElapsedMilliseconds - chapterMs - boardMs;
            Debug.Log($"[SevenSteps] Stage {stage + 1} loaded in {clock.ElapsedMilliseconds} ms (chapter {chapterMs}, board {boardMs}, fit {fitMs}); " +
                      $"memory {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() >> 20} MB reserved, {System.GC.GetTotalMemory(false) >> 20} MB managed");
            if (isActiveAndEnabled) StartCoroutine(LogFirstFrames(stage));
            StageEntered?.Invoke(stage, via);
        }

        /// The slowest of the first frames after a stage load (first draws of a planet's shaders and textures happen here).
        IEnumerator LogFirstFrames(int stage)
        {
            float worst = 0;
            for (int k = 0; k < 5; k++)
            {
                yield return null;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            Debug.Log($"[SevenSteps] Stage {stage + 1} first frames: slowest {Mathf.RoundToInt(worst * 1000)} ms");
        }

        /// Replay a move string on the current stage (-replay / editor replay).
        public void Replay(string moves) => session.Replay(moves);

        // ---------- Input (board keys; flow keys are read by GameController, the scene root) ----------

        /// A board key went down this frame (the keys Update reads). The scene root closes the chapter card on one, so the
        /// key both dismisses the card and plays (playtest rehearsal 2026-11-10: the first arrow after entering a planet
        /// was swallowed by the card).
        public static bool AnyBoardKeyPressed(Keyboard kb) =>
            kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame
                || kb.rightArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame
                || kb.dKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame
                || kb.backspaceKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame);

        void Update()
        {
            if (session == null || InputBlocked()) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Send(Command.Move(Dir.Up));
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Send(Command.Move(Dir.Down));
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Send(Command.Move(Dir.Left));
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Send(Command.Move(Dir.Right));
                else if (kb.tabKey.wasPressedThisFrame && choice.Shown) choice.ToggleFocus();
                else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && choice.Shown) choice.Confirm();
                else if (kb.kKey.wasPressedThisFrame) Send(Command.Keep);
                else if (kb.xKey.wasPressedThisFrame) Send(Command.Switch);
                else if (kb.zKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Send(Command.Undo);
                else if (kb.rKey.wasPressedThisFrame) Restart();
                else if (kb.hKey.wasPressedThisFrame) ShowHint();
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUi())
                ClickCell(mouse.position.ReadValue());
        }

        static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void ClickCell(Vector2 screen)
        {
            int cell = board.Grid.RayToCell(cam.ScreenPointToRay(screen));
            var s = session.State;
            if (cell < 0 || s == null) return;
            var l = s.Level;
            int dx = l.X(cell) - l.X(s.Pos), dy = l.Y(cell) - l.Y(s.Pos);
            if (dx == 0 && dy == -1) Send(Command.Move(Dir.Up));
            else if (dx == 0 && dy == 1) Send(Command.Move(Dir.Down));
            else if (dx == -1 && dy == 0) Send(Command.Move(Dir.Left));
            else if (dx == 1 && dy == 0) Send(Command.Move(Dir.Right));
        }

        /// Every input goes here. The flow may take it (Result); otherwise the session decides whether it applies now,
        /// is queued, or is dropped.
        public void Send(Command cmd)
        {
            if (FlowTakes(cmd)) return;
            session.Send(cmd);
        }

        public void Restart() => Send(Command.Restart);

        /// H (and the menu's solution item): the next tier of the staged hint (QA-1 (A), contract review
        /// 2026-11-11-staged-hint-contract.md): the principle (the level's hint text, unless the stage started with it),
        /// then each body's goal (a gold ring on the board, where and how its run ends), then the whole route. Opened
        /// tiers stay through Undo; a new attempt (LoadStage) starts again. A tier that costs (HintRules.Costs) counts as a
        /// hint for the stars (6.5). A stage without a stored route gets the route at once, as before.
        public void ShowHint()
        {
            if (session.Level == null) return;
            int stage = loadedStage;
            if (hintSteps == null || hintStage != stage)
            {
                hintStage = stage;
                hintSteps = StepsFor(stage);
            }
            bool costs = hintSteps == null || hintSteps.NextCosts(session.State);
            if (HintRules.AsksFirst(costs, attempt.Hints, hintWarned))
            {
                hintWarned = true; // the next H (or the menu's solution again) opens it
                messages.Show(() => loc.Get("hint.confirm"));
                return;
            }
            if (hintSteps == null)
            {
                LastHint = null;
                attempt = attempt.WithHint();
                messages.Show(() => hints.For(stage));
                board.ShowHintGoal(-1);
            }
            else
            {
                var t = hintSteps.Open(session.State);
                LastHint = t;
                if (t.Costs) attempt = attempt.WithHint();
                bool warn = HintRules.FirstTierFree && !t.Costs && attempt.Hints == 0 && t.Tier < t.Tiers && hintSteps.NextCosts(session.State);
                var steps = hintSteps; // captured: LoadStage clears the field before the Rebuild clears the line (review N1 on c5a3657)
                messages.Show(() => HintText(stage, steps, t, warn));
                board.ShowHintGoal(t.What == HintTier.Shows.Goal ? t.Goal.Cell : -1);
            }
            HintShown?.Invoke();
            Refresh();
        }

        /// What the last H opened (telemetry tier fields), null for a stage without a stored route.
        public HintTier? LastHint { get; private set; }

        HintSteps hintSteps;
        /// The "a hint caps this attempt at 1 star" warning was shown in this attempt (HintRules.AsksFirst).
        bool hintWarned;
        int hintStage = -1;

        /// The staged hint of a stage, or null (the whole route at once) when it has no stored route or the route does
        /// not follow (bad data: logged, the H still helps; review M1 on 4024c25).
        HintSteps StepsFor(int stage)
        {
            string path = LevelCatalog.PathOf(stage);
            if (path.Length == 0) return null;
            HintPlan plan;
            try { plan = HintPlan.From(levels[stage], path); }
            catch (ArgumentException e)
            {
                Debug.LogError($"[SevenSteps] Staged hint off for stage {stage + 1}: {e.Message}");
                return null;
            }
            bool principle = LevelCatalog.TextOf(stage, loc.Current).hint.Length > 0 && LevelCatalog.StartLineOf(stage) != "hint";
            return new HintSteps(plan, principle);
        }

        /// The tier's line on the message line, worded on each read (a language change rewords it).
        string HintText(int stage, HintSteps steps, HintTier t, bool nextCosts)
        {
            string body = t.What switch
            {
                HintTier.Shows.Principle => LevelCatalog.TextOf(stage, loc.Current).hint,
                HintTier.Shows.Goal => loc.Format("hint.goal", loc.Get($"hint.brother{t.Goal.Body % 3 + 1}"), GoalWords(t.Goal)),
                _ => hints.For(stage),
            };
            string head = loc.Format("hint.tier", steps.Shown(t.Tier), steps.ShownTiers);
            if (t.FromStart) head += " " + loc.Get("hint.from_start");
            string line = loc.Format("hint.line", head, body);
            return nextCosts ? line + " " + loc.Get("hint.next_costs") : line;
        }

        string GoalWords(HintGoal g)
        {
            string kind = loc.Get(g.Kind switch
            {
                HintGoalKind.Keep => "hint.kind_keep",
                HintGoalKind.Switch => "hint.kind_switch",
                HintGoalKind.Bridge => "hint.kind_bridge",
                HintGoalKind.Seal => "hint.kind_seal",
                HintGoalKind.Stone => "hint.kind_stone",
                HintGoalKind.Vanish => "hint.kind_vanish",
                _ => "hint.kind_fall",
            });
            return g.Lever ? loc.Format("hint.with_lever", kind) : kind;
        }

        // ---------- Session updates ----------

        void OnSessionUpdated(SessionUpdate u)
        {
            messages.Apply(u);
            CountAttempt(u);
            if (u.Kind == SessionUpdateKind.Feedback)
            {
                if (u.Result != null && u.Result.Has(GameEventType.Bump))
                {
                    audio.Bump();
                    board.Bump(FirstAt(u.Result, GameEventType.Bump));
                }
                else if (MessagePresenter.NeedsChoice(u)) choiceView.NudgeChoice();
            }
            else if (u.Kind != SessionUpdateKind.Rebuild && u.Result != null) audio.Play(u.Result);

            var s = u.State;
            if (s.Status == Status.Win && u.Kind == SessionUpdateKind.Present)
            {
                board.ShowHintGoal(-1); // the result goes over the board
                Won?.Invoke(s); // the flow saves the clear before the HUD redraws; the result panel waits for OnPresented
            }
            Refresh();
            if (s.Status == Status.Lose && !lost) { lost = true; Lost?.Invoke(s); }
            else if (s.Status != Status.Lose && lost) { lost = false; Recovered?.Invoke(); }
        }

        /// docs 6.5 counting rule: AttemptCounts.After (shared with the QA replay).
        void CountAttempt(SessionUpdate u) => attempt = attempt.After(u);

        /// A presentation finished (docs/architecture.md 4.2). A clear shows the Result panel only now.
        void OnPresented(State s)
        {
            if (s.Status == Status.Win) WinPresented?.Invoke(s);
        }

        static int FirstAt(StepResult r, GameEventType t)
        {
            foreach (var e in r.Events) if (e.Type == t) return e.At;
            return -1;
        }

        void Refresh()
        {
            var text = LevelCatalog.TextOf(loadedStage, loc.Current); // stage name and hint in the UI language (13.5)
            hud.Refresh(session.State, loadedStage, text, messages.Current);
            HudRefreshed?.Invoke(session.State, loadedStage, text);
        }
    }
}
