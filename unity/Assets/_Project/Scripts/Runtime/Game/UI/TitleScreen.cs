using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Title scene root: states Title (start, settings) and StageSelect (stage list with clear marks and best bodies).
    /// Switching between the two only toggles panels (AppFlow.Changed); choosing a stage goes to the Game scene.
    /// Played directly in the editor, it runs the bootstrap first.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
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
        Level[] levels;
        GameObject titlePanel, selectPanel;
        SettingsPanel settings;
        CreditsPanel creditsPanel;
        Button[] stageButtons;
        Text[] stageTexts;
        Button startButton;

        void Awake()
        {
            if (assets == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.assets is not set (unity.sh scene-validate)");
            if (config == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.config is not set (unity.sh scene-validate)");
            if (!Bootstrap.HasRunThisPlay) Bootstrap.Run(config, assets, null, enterFirstScreen: false);
            flow = Services.Get<AppFlow>();
            save = Services.Get<SaveSystem>();
            Ui.Font = assets.font;
            levels = LevelCatalog.Load(assets);

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = assets.background; }
            AudioListener.volume = save.Settings.masterVolume;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            BuildUi();
            flow.Changed += OnFlowChanged;
        }

        void Start()
        {
            if (flow.State == AppState.Boot) flow.Go(AppState.Title);
            else Show(flow.State);
            // Smoke test without -level: "-screenshot title.png" [-stageselect] [-credits] opens that panel first.
            var launch = Services.Get<LaunchOptions>();
            if (!string.IsNullOrEmpty(launch.Screenshot))
            {
                if (launch.OpenStageSelect && flow.State == AppState.Title) flow.Go(AppState.StageSelect);
                if (launch.OpenCredits) creditsPanel.Open();
                StartCoroutine(ScreenshotOnce.Run(launch.Screenshot, () => $"{flow.State}, cleared {save.Progress.cleared.Count}"));
            }
        }

        void OnDestroy()
        {
            if (flow != null) flow.Changed -= OnFlowChanged;
        }

        void OnFlowChanged(AppState from, AppState to) => Show(to);

        // Player actions: ignored while a scene is loading or when the flow does not allow them from the current state.
        void Go(AppState to)
        {
            if (flow.IsLoading || !AppFlow.CanGo(flow.State, to)) return;
            flow.Go(to);
        }

        void PlayStage(int stage)
        {
            if (flow.CanGoToGame(stage)) flow.GoToGame(stage);
        }

        void Show(AppState s)
        {
            if (titlePanel == null) throw new InvalidOperationException("[SevenSteps] TitleScreen.Show before BuildUi (or after the scene unloaded without unsubscribing)");
            titlePanel.SetActive(s == AppState.Title);
            selectPanel.SetActive(s == AppState.StageSelect);
            if (s == AppState.StageSelect) RefreshStages();
        }

        void Update()
        {
            if (settings.IsOpen)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) settings.Close();
                return;
            }
            if (creditsPanel.IsOpen)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) creditsPanel.Close();
                return;
            }
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            if (flow.State == AppState.Title && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Go(AppState.StageSelect);
            else if (flow.State == AppState.StageSelect && kb.escapeKey.wasPressedThisFrame && flow.TryGetBackTarget(out var back))
                Go(back);
            else if (flow.State == AppState.StageSelect && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                PlayStage(Mathf.Clamp(save.Progress.lastStage, 0, levels.Length - 1)); // save data is external input: clamp
        }

        void BuildUi()
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
            var root = canvasGo.transform;

            // Title
            titlePanel = Ui.Rect("Title", root).Stretch().gameObject;
            var t = titlePanel.transform;
            var name = Ui.Label(t, "일곱 걸음", 96, Ui.Gold, TextAnchor.MiddleCenter, "Name");
            name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(800, 120));
            var sub = Ui.Label(t, "Seven Steps", 26, Ui.Dim, TextAnchor.MiddleCenter, "Sub");
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 76), new Vector2(400, 36));
            var tag = Ui.Label(t, "한 몸에 딱 일곱 걸음. 일곱 번째 걸음은 행운이다.", 20, Ui.Ink, TextAnchor.MiddleCenter, "Tagline");
            tag.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 30));
            startButton = Ui.MakeButton(t, "시작 (Enter)", 24, () => Go(AppState.StageSelect), out _, "Start");
            ((RectTransform)startButton.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(260, 54));
            var set = Ui.MakeButton(t, "설정", 20, () => settings.Open(), out _, "Settings");
            ((RectTransform)set.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(260, 44));
            var cr = Ui.MakeButton(t, "크레딧", 20, () => creditsPanel.Open(), out _, "Credits");
            ((RectTransform)cr.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -182), new Vector2(260, 44));
            var ver = Ui.Label(t, Application.version, 14, Ui.Dim, TextAnchor.LowerRight, "Version");
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(300, 20));

            // Stage select
            selectPanel = Ui.Rect("StageSelect", root).Stretch().gameObject;
            var s = selectPanel.transform;
            var head = Ui.Label(s, "스테이지 선택", 40, Ui.Gold, TextAnchor.MiddleCenter, "Head");
            head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 56));
            var grid = Ui.Rect("Grid", s);
            grid.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 420));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(232, 84);
            g.spacing = new Vector2(14, 14);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            stageButtons = new Button[levels.Length];
            stageTexts = new Text[levels.Length];
            for (int k = 0; k < levels.Length; k++)
            {
                int idx = k;
                stageButtons[k] = Ui.MakeButton(grid, "", 18, () => PlayStage(idx), out stageTexts[k], $"Stage {k + 1}");
                stageTexts[k].horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            var back = Ui.MakeButton(s, "뒤로 (Esc)", 18, () => Go(AppState.Title), out _, "Back");
            ((RectTransform)back.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 40), new Vector2(180, 44));
            var set2 = Ui.MakeButton(s, "설정", 18, () => settings.Open(), out _, "Settings");
            ((RectTransform)set2.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-110, 40), new Vector2(180, 44));

            settings = new SettingsPanel(root, save);
            creditsPanel = new CreditsPanel(root, credits, licenses);
            titlePanel.SetActive(false);
            selectPanel.SetActive(false);
        }

        void RefreshStages()
        {
            var p = save.Progress;
            for (int k = 0; k < levels.Length; k++)
            {
                bool done = p.IsCleared(k);
                int b = p.bestStage.IndexOf(k);
                string best = b >= 0 ? $"  · 몸 {p.bestBodies[b]}" : "";
                stageTexts[k].text = $"{k + 1}. {levels[k].Name}\n<size=14>{(done ? "클리어" + best : "")}</size>";
                stageTexts[k].color = done ? Ui.Gold : Ui.Ink;
                if (k == p.lastStage) stageButtons[k].Select();
            }
        }
    }
}
