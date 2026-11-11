using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Game scene root (states Game and Result, docs/scene-flow.md 4.1, S3). Composition only: bootstraps when the scene is
    /// played directly, builds the views (HUD, result panel), adds the board controller and creates the
    /// <see cref="GameFlowController"/>, then wires view events to them. Reads the flow keys ([ ] Enter Esc); board keys are
    /// the board controller's. Inspector fields are the scene's start values (startLevel / replay act like -level / -replay
    /// when Game.unity is played without Boot) and the camera override.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("Data/GameAssets.asset")]
        public GameAssets assets;
        [Tooltip("Data/AppConfig.asset. Used when this scene is played without Boot.")]
        public AppConfig config;
        [Tooltip("Editor only, when played without Boot: 1-based stage (like -level)")]
        public int startLevel = 1;
        [Tooltip("Editor only, when played without Boot: replay letters U D L R K X (like -replay)")]
        public string replay = "";

        [Header("Camera (docs/architecture.md 12.2)")]
        [Tooltip("Use cameraRig below instead of the TileSet's own preset. Off by default: the placeholder walls are ~1 cell tall and hide the cell behind them at 50 deg (walls must be <= 0.6, 12.3). Turn on to preview plan A.")]
        public bool overrideCamera = false;
        [Tooltip("Plan A, front 3/4 view: pitch 50, yaw 0, orthographic. Keep yaw 0: input is not rotated.")]
        public CameraRig cameraRig = new CameraRig { orthographic = true, pitch = 50f, yaw = 0f, fieldOfView = 30f, distance = 30f, padding = 0.6f };

        [Header("Chapter lights (docs/architecture.md 12.8): set per chapter by ChapterStage")]
        [Tooltip("Key light, the only one with shadows (WebGL Mobile budget)")]
        public Light keyLight;
        public Light fillLight;
        public Light rimLight;

        AppFlow flow;
        LaunchOptions launch;
        BoardController board;
        HudView hud;
        GameFlowController flowController;
        SettingsController settingsController;
        ChapterStage chapterStage;
        ResultView result;
        Func<int, ChapterPreset> presetFor;
        LanguageController languageController;
        OverlayStack overlays;
        BackNavigator back;
        ChapterIntroController chapterIntro;
        Localizer localizer;
        Action<Language> applyResultLanguage;
        SettingsPanel settingsPanel;
        IChapterCard chapterCard;
        MenuPanel menuPanel;
        MenuController menu;
        TutorialTracker tutorial;

        bool OnlyChapterCard() => chapterCard != null && overlays.Count == 1 && overlays.Top == chapterCard;

        /// The board controller (tests, tools).
        public BoardController Board => board;
        /// Stage on the board and the stage list (art preview scripts read these).
        public int LevelIndex => board.LevelIndex;
        public SevenSteps.Core.Level[] Levels => board.Levels;
        /// One-time guides of the minimal UI (move guide, KEEP / SWITCH guide, choice result lines). The HUD and the choice
        /// cards read it and redraw on Changed (gameplay-ui).
        public TutorialTracker Tutorial => tutorial;
        /// The in-game menu (settings, stages, solution, controls). The HUD menu button calls Menu.Toggle.
        public MenuController Menu => menu;

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (assets == null) throw new MissingReferenceException($"[SevenSteps] {name}: GameController.assets is not set (unity.sh scene-validate)");
            if (config == null) throw new MissingReferenceException($"[SevenSteps] {name}: GameController.config is not set (unity.sh scene-validate)");
            if (keyLight == null || fillLight == null || rimLight == null)
                throw new MissingReferenceException($"[SevenSteps] {name}: GameController key / fill / rim light is not set (Seven Steps > Scene Flow > Build Game Lights)");
            if (!Bootstrap.HasRunThisPlay)
            {
                // Played directly in the editor: same bootstrap as the Boot scene, with this object's start values.
                var opts = Bootstrap.ReadLaunchOptions();
                bool levelFromInspector = opts.UseEditorStart(Mathf.Max(1, startLevel), replay);
                Bootstrap.Run(config, assets, opts, enterFirstScreen: false, levelFromEditor: levelFromInspector);
            }
            flow = Services.Get<AppFlow>();
            launch = Services.Get<LaunchOptions>();
            var save = Services.Get<SaveSystem>();
            var cfg = Services.Get<AppConfig>();

            Ui.Theme = assets.uiTheme; // before any view or the board label layer is built
            var levels = LevelCatalog.Load(assets);
            var cam = Camera.main;
            if (cam == null) throw new MissingReferenceException("[SevenSteps] Game scene has no MainCamera-tagged camera (unity.sh scene-validate)");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = assets.background;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // The set the board starts with (12.8 order: AppConfig.tileSet → first stage's chapter → GameAssets.tileSet);
            // each stage then swaps to its chapter's set. The HUD reads only body colours from it (the same in every set).
            var tiles = BoardController.TileSetFor(cfg.tileSet, ChapterPresetFor(cfg)(0), assets.tileSet);

            // Views
            board = gameObject.AddComponent<BoardController>();
            var localizer = Services.Get<Localizer>();
            hud = HudView.Create(tiles, localizer); // theme prefab with its own canvas; LabelLayer is valid for board.Init below
            var result = ResultView.Create();
            result.Apply(localizer);
            this.localizer = localizer;
            var overlayCanvas = OverlayCanvas();
            var settings = settingsPanel = SettingsPanel.Create(overlayCanvas, save, cfg.telemetryOnByDefault, localizer);
            var languagePanel = new LanguagePanel(overlayCanvas); // opened from the settings language button
            languagePanel.Apply(localizer);
            var menuPanel = this.menuPanel = new MenuPanel(overlayCanvas);
            menuPanel.Apply(localizer);
            applyResultLanguage = _ => { result.Apply(localizer); languagePanel.Apply(localizer); menuPanel.Apply(localizer); }; // the HUD and board redraw in BoardController
            localizer.Changed += applyResultLanguage;
            // The theme's card prefab, else the code-built chapter entry screen (art concept chapter-entry). None in smoke /
            // replay runs: the screenshot must show the board.
            IChapterCard chapterCard = launch.SmokeView != SmokeView.None || (!string.IsNullOrEmpty(launch.Replay) && !launch.FlowCheck) ? null
                : (IChapterCard)ChapterCardView.CreateOptional(overlayCanvas) ?? ChapterEntryView.Create(overlayCanvas, localizer);
            this.chapterCard = chapterCard;
            // Win finale (star trail) at a quarter of its length in dev runs, so replays and smoke reach the result quickly.
            StarTrailView.FinaleSpeed = launch.SmokeView != SmokeView.None || launch.FlowCheck || !string.IsNullOrEmpty(launch.Replay) ? 4f : 1f;
            settingsController = new SettingsController(save, settings, cfg.telemetryOnByDefault, localizer);
            overlays = new OverlayStack();
            languageController = new LanguageController(languagePanel, overlays, localizer, save, launch.Language);
            settingsController.OpenLanguage = languageController.Open;

            // Board controller, then the flow controller on top of it
            board.Init(assets, levels, save, hud, cam, tiles, overrideCamera ? cameraRig : (CameraRig?)null, Services.Get<ITelemetry>(), localizer);
            // Chapter look (12.8): stage -> chapters.json artPreset -> AppConfig.Chapter; null (no chapter assets yet) = as now.
            // -smoke board|empty (smoke-webgl): no sky/planet and a magenta clear; empty also hides the board (negative control).
            chapterStage = new ChapterStage(cam, keyLight, fillLight, rimLight, transform, launch.SmokeView);
            board.UseChapters(chapterStage, ChapterPresetFor(cfg), cfg.tileSet);
            if (launch.SmokeView == SmokeView.Empty) board.HideBoardForSmoke();
            if (launch.SmokeView != SmokeView.None)
            {
                cam.backgroundColor = ChapterStage.SmokeClear;
                // Screen UI off (core request): only the 3D board and its world labels (on the HUD's label layer) draw.
                hud.HideForSmoke();
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.gameObject != hud.gameObject) c.enabled = false;
            }
            flowController = new GameFlowController(flow, Services.Get<EntryTracker>(), save, board, result, LevelCatalog.MinBodies, LevelCatalog.ChapterKeyOf, LevelCatalog.ChapterOf);
            back = new BackNavigator(overlays, flowController.Back);
            chapterIntro = new ChapterIntroController(flowController, overlays, chapterCard, key => LevelCatalog.ChapterCard(key, localizer.Current));
            this.result = result;
            presetFor = ChapterPresetFor(cfg);
            // The chapter card alone does not hold the board: a board key closes it and plays (Update below).
            board.InputBlocked = () => flow.IsLoading || (overlays.Count > 0 && !OnlyChapterCard());
            board.FlowTakes = flowController.TakesCommand;

            // One-time guides (ui-minimal). Dev runs (replay, flow / title checks, smoke) keep the profile untouched and start
            // with every guide seen, so their screenshots show the plain HUD.
            bool devRun = launch.SmokeView != SmokeView.None || !string.IsNullOrEmpty(launch.Replay) || launch.FlowCheck || launch.TitleCheck != "";
            tutorial = new TutorialTracker(devRun ? new MemoryTutorialStore(allSeen: true) : (ITutorialStore)new SaveTutorialStore(save));
            board.Session.Updated += tutorial.Apply;
            board.StageEntered += StopControlsGuide;
            menu = new MenuController(overlays, menuPanel,
                openSettings: () => { if (!overlays.Contains(settings)) overlays.Push(settings); },
                openStages: flowController.OpenStageSelect,
                showSolution: board.ShowHint,
                playControls: tutorial.PlayControls);

            hud.StageSelectRequested += flowController.OpenStageSelect;
            // Until the minimal HUD has its own menu button (gameplay-ui), the settings button opens the menu.
            hud.SettingsRequested += menu.Toggle;
            result.NextRequested += flowController.Next;
            result.RetryRequested += flowController.Again;
            result.StageSelectRequested += flowController.OpenStageSelect;
            result.UndoRequested += () => board.Send(SevenSteps.Core.Command.Undo);
            result.RestartRequested += board.Restart;
        }

        void Start()
        {
            flowController.Start(launch.DirectToGame ? Bootstrap.FirstStage(launch, flow.StageCount) : 0);
            if (launch.OpenSettings && !overlays.Contains(settingsPanel)) overlays.Push(settingsPanel); // -settings / ?settings=1

            if (launch.TitleCheck != "" && !launch.DirectToGame)
            {
                // -titlecheck (S8): arrived from the Title buttons. Save progress like a return to stage select would, so the
                // next run's Continue has this stage, then log and quit.
                Services.Get<SaveSystem>().SaveProgress();
                Debug.Log($"[SevenSteps] Title check {launch.TitleCheck}: arrived stage {flow.Stage + 1} via {flowController.LastVia}");
                Application.Quit();
                return;
            }
            if (launch.FlowCheck && !string.IsNullOrEmpty(launch.Replay) && launch.DirectToGame)
            {
                // -flowcheck: the replay wins for real, then Result -> Next -> chapter entry are checked (S8).
                var check = new FlowCheck(flow, board, flowController, overlays, result, chapterStage, chapterIntro, localizer, presetFor);
                StartCoroutine(check.Run(launch.TakeReplay(), launch.Screenshot));
                return;
            }
            if (!string.IsNullOrEmpty(launch.Replay) && launch.DirectToGame && flow.Stage == launch.Level - 1)
                board.Replay(launch.TakeReplay()); // once per launch

            if (!string.IsNullOrEmpty(launch.Screenshot))
                StartCoroutine(ScreenshotOnce.Run(launch.Screenshot, () =>
                {
                    var s = board.State;
                    return $"level {flow.Stage + 1}, status {s.Status}, used {s.Used}, bodies {s.Bodies}";
                }));
        }

        void OnDestroy()
        {
            flowController?.Dispose();
            settingsController?.Dispose();
            languageController?.Dispose();
            chapterIntro?.Dispose();
            menu?.Dispose();
            if (board != null && tutorial != null)
            {
                if (board.Session != null) board.Session.Updated -= tutorial.Apply;
                board.StageEntered -= StopControlsGuide;
            }
            if (localizer != null) localizer.Changed -= applyResultLanguage;
        }

        void StopControlsGuide(int stage, string via) => tutorial.StopControls();

        /// Flow keys. Board keys are read by the board controller (blocked while an overlay is open or a scene loads).
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            // Esc in one place: the top overlay closes first, otherwise AppFlow's back target (BackNavigator).
            if (kb.escapeKey.wasPressedThisFrame) { back.Back(); return; }
            // Chapter card: a board key dismisses it and still counts as a move (rehearsal 2026-11-10: the first arrow was lost).
            if (OnlyChapterCard() && BoardController.AnyBoardKeyPressed(kb)) back.Back();
            if (overlays.Count > 0) return;
            if (kb.leftBracketKey.wasPressedThisFrame) flowController.SelectStage(flow.Stage - 1);
            else if (kb.rightBracketKey.wasPressedThisFrame) flowController.SelectStage(flow.Stage + 1);
            else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && flow.State == AppState.Result) flowController.Next();
        }

        /// Stage -> its chapter's art preset: levels.json chapter -> chapters.json artPreset -> AppConfig.Chapter.
        /// Null when there is no chapters.json or AppConfig has no chapters yet (the scene keeps its own look).
        static Func<int, ChapterPreset> ChapterPresetFor(AppConfig cfg) => stage =>
        {
            var info = LevelCatalog.ChapterOf(stage);
            return info != null ? cfg.Chapter(info.ArtPreset) : null;
        };

        /// Canvas for overlays above the HUD (0) and the result panel (5).
        static Transform OverlayCanvas()
        {
            var canvasGo = new GameObject("Overlays", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvasGo.transform;
        }
    }
}
