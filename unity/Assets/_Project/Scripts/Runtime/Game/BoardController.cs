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
            hints = new HintProvider(levels, loc);

            hud.UndoRequested += () => Send(Command.Undo);
            hud.RestartRequested += Restart;
            hud.HintRequested += ShowHint;
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
            recorder?.Dispose();
            audio?.Dispose();
            chapters?.Dispose();
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
            EnterChapter(stage);
            session.Load(levels[stage]); // Rebuild runs inside Load
            chapters?.Fit(board.Grid);
            StageEntered?.Invoke(stage, via);
        }

        /// Replay a move string on the current stage (-replay / editor replay).
        public void Replay(string moves) => session.Replay(moves);

        // ---------- Input (board keys; flow keys are read by GameController, the scene root) ----------

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

        /// H: the shortest route from the stage start (HintProvider), shown on the message line.
        public void ShowHint()
        {
            if (session.Level == null) return;
            attempt = attempt.WithHint();
            int stage = loadedStage;
            messages.Show(() => hints.For(stage));
            HintShown?.Invoke();
            Refresh();
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
                else if (MessagePresenter.NeedsChoice(u)) hud.NudgeChoice();
            }
            else if (u.Kind != SessionUpdateKind.Rebuild && u.Result != null) audio.Play(u.Result);

            var s = u.State;
            if (s.Status == Status.Win && u.Kind == SessionUpdateKind.Present)
                Won?.Invoke(s); // the flow saves the clear before the HUD redraws; the result panel waits for OnPresented
            Refresh();
            if (s.Status == Status.Lose && !lost) { lost = true; Lost?.Invoke(s); }
            else if (s.Status != Status.Lose && lost) { lost = false; Recovered?.Invoke(); }
        }

        /// docs 6.5: an Undo counts when the core went back a state (its Undo event; also after Lose). A Restart counts when
        /// the state before it was not the stage start (the start state has no Prev: NewState, or undone back to it).
        void CountAttempt(SessionUpdate u)
        {
            if (u.Result == null) return;
            if (u.Result.Has(GameEventType.Undo)) attempt = attempt.WithUndo();
            if (u.Result.Has(GameEventType.Restart) && u.Prev != null && u.Prev.Prev != null) attempt = attempt.WithRestart();
        }

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
        }
    }
}
