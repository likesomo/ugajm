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
        OverlayStack overlays;
        BackNavigator back;
        ChapterIntroController chapterIntro;

        /// The board controller (tests, tools).
        public BoardController Board => board;
        /// Stage on the board and the stage list (art preview scripts read these).
        public int LevelIndex => board.LevelIndex;
        public SevenSteps.Core.Level[] Levels => board.Levels;

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

            var tiles = cfg.tileSet != null ? cfg.tileSet : assets.tileSet;
            if (tiles == null) throw new InvalidOperationException("[SevenSteps] No TileSet: set AppConfig.tileSet or GameAssets.tileSet");

            // Views
            board = gameObject.AddComponent<BoardController>();
            hud = HudView.Create(tiles); // theme prefab with its own canvas; LabelLayer is valid for board.Init below
            var result = ResultView.Create();
            var overlayCanvas = OverlayCanvas();
            var settings = SettingsPanel.Create(overlayCanvas, save, cfg.telemetryOnByDefault);
            var chapterCard = ChapterCardView.CreateOptional(overlayCanvas); // null when the theme has no card
            settingsController = new SettingsController(save, settings, cfg.telemetryOnByDefault);
            overlays = new OverlayStack();

            // Board controller, then the flow controller on top of it
            board.Init(assets, levels, save, hud, cam, tiles, overrideCamera ? cameraRig : (CameraRig?)null, Services.Get<ITelemetry>());
            // Chapter look (12.8): stage -> chapters.json artPreset -> AppConfig.Chapter; null (no chapter assets yet) = as now.
            board.UseChapters(new ChapterStage(cam, keyLight, fillLight, rimLight, transform), ChapterPresetFor(cfg), cfg.tileSet);
            flowController = new GameFlowController(flow, Services.Get<EntryTracker>(), save, board, result, LevelCatalog.MinBodies, LevelCatalog.ChapterKeyOf);
            back = new BackNavigator(overlays, flowController.Back);
            chapterIntro = new ChapterIntroController(flowController, overlays, chapterCard, LevelCatalog.ChapterCard);
            board.InputBlocked = () => flow.IsLoading || overlays.Count > 0;
            board.FlowTakes = flowController.TakesCommand;

            hud.StageSelectRequested += flowController.OpenStageSelect;
            hud.SettingsRequested += () => { if (!overlays.Contains(settings)) overlays.Push(settings); };
            result.NextRequested += flowController.Next;
            result.RetryRequested += flowController.Again;
            result.StageSelectRequested += flowController.OpenStageSelect;
            result.UndoRequested += () => board.Send(SevenSteps.Core.Command.Undo);
            result.RestartRequested += board.Restart;
        }

        void Start()
        {
            flowController.Start(launch.DirectToGame ? Bootstrap.FirstStage(launch, flow.StageCount) : 0);

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
            chapterIntro?.Dispose();
        }

        /// Flow keys. Board keys are read by the board controller (blocked while an overlay is open or a scene loads).
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            // Esc in one place: the top overlay closes first, otherwise AppFlow's back target (BackNavigator).
            if (kb.escapeKey.wasPressedThisFrame) { back.Back(); return; }
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
