using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Title scene root (states Title and StageSelect, docs/scene-flow.md 4.1, S3). Composition only: bootstraps when the scene
    /// is played directly, builds the views (TitleView, StageSelectView, settings, credits), creates the
    /// <see cref="TitleFlowController"/> and wires view events to it. Reads the flow keys (Enter / Space / Esc).
    /// Settings and credits stay here until S4 (OverlayStack + BackNavigator).
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour, ITitlePanels
    {
        public GameAssets assets;
        [Tooltip("Data/AppConfig.asset. Used when this scene is played without Boot.")]
        public AppConfig config;
        [Tooltip("UI/Credits.txt, exported from design/art/CREDITS.md before each build (CreditsExport)")]
        public TextAsset credits;
        [Tooltip("Full license texts shipped with the assets, e.g. Shared/Fonts/Jua-OFL.txt")]
        public TextAsset[] licenses;

        AppFlow flow;
        SaveSystem save;
        TitleView titleView;
        StageSelectView stageSelectView;
        SettingsPanel settings;
        CreditsPanel creditsPanel;
        TitleFlowController flowController;
        SettingsController settingsController;
        OverlayStack overlays;
        BackNavigator back;

        void Awake()
        {
            if (assets == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.assets is not set (unity.sh scene-validate)");
            if (config == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.config is not set (unity.sh scene-validate)");
            if (!Bootstrap.HasRunThisPlay) Bootstrap.Run(config, assets, null, enterFirstScreen: false);
            flow = Services.Get<AppFlow>();
            save = Services.Get<SaveSystem>();
            Ui.Theme = assets.uiTheme;
            var levels = LevelCatalog.Load(assets);

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = assets.background; }
            GameAudio.ApplyMaster(save.Settings); // the Title scene has no GameAudio instance: master volume only
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = BuildCanvas();
            titleView = new TitleView(root);
            stageSelectView = new StageSelectView(root, levels);
            bool telemetryDefault = Services.Get<AppConfig>().telemetryOnByDefault;
            settings = SettingsPanel.Create(root, save, telemetryDefault);
            settingsController = new SettingsController(save, settings, telemetryDefault);
            creditsPanel = new CreditsPanel(root, credits, licenses);
            overlays = new OverlayStack();

            flowController = new TitleFlowController(flow, save, this);
            back = new BackNavigator(overlays, flowController.Back);
            titleView.StartRequested += flowController.OpenStageSelect;
            titleView.SettingsRequested += () => Open(settings);
            titleView.CreditsRequested += () => Open(creditsPanel);
            stageSelectView.StageClicked += flowController.PlayStage;
            stageSelectView.BackRequested += flowController.Back;
            stageSelectView.SettingsRequested += () => Open(settings);
        }

        void Open(IOverlay overlay)
        {
            if (!overlays.Contains(overlay)) overlays.Push(overlay);
        }

        void Start()
        {
            flowController.Start();
            // Smoke test without -level: "-screenshot title.png" [-stageselect] [-credits] opens that panel first.
            var launch = Services.Get<LaunchOptions>();
            if (!string.IsNullOrEmpty(launch.Screenshot))
            {
                if (launch.OpenStageSelect) flowController.OpenStageSelect();
                if (launch.OpenCredits) Open(creditsPanel);
                StartCoroutine(ScreenshotOnce.Run(launch.Screenshot, () => $"{flow.State}, cleared {save.Progress.cleared.Count}"));
            }
        }

        void OnDestroy()
        {
            flowController?.Dispose();
            settingsController?.Dispose();
        }

        /// ITitlePanels: the flow controller shows the panel for the state.
        public void Show(AppState state)
        {
            if (titleView == null) throw new InvalidOperationException("[SevenSteps] TitleScreen.Show before Awake");
            if (state == AppState.Title) titleView.Show(); else titleView.Hide();
            if (state == AppState.StageSelect) stageSelectView.Show(save.Progress); else stageSelectView.Hide();
        }

        /// Flow keys. Esc in one place: the top overlay closes first, otherwise AppFlow's back target (BackNavigator).
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            if (kb.escapeKey.wasPressedThisFrame) { back.Back(); return; }
            if (overlays.Count > 0) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            if (flow.State == AppState.Title && (enter || kb.spaceKey.wasPressedThisFrame)) flowController.OpenStageSelect();
            else if (flow.State == AppState.StageSelect && enter) flowController.PlayLastStage();
        }

        static Transform BuildCanvas()
        {
            var canvasGo = new GameObject("TitleUI", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }
    }
}
