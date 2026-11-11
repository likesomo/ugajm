using System;
using System.Collections.Generic;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Title scene root (states Title and StageSelect, docs/scene-flow.md 4.1, S3, S7). Composition only: bootstraps when the
    /// scene is played directly, builds the views (menu background, TitleView, PlanetMapView, the planet stage list, the
    /// language picker, settings, credits), creates the <see cref="TitleFlowController"/> and wires view events to it.
    /// Keys: Esc here (top overlay first, then AppFlow back); arrows / Enter are uGUI navigation and submit.
    /// First run (docs/architecture.md 13.3): the language picker opens over the title when the player never chose a
    /// language and the run has no -lang / ?lang=.
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
        Localizer localizer;
        Level[] levels;
        List<PlanetEntry> planets;
        TitleView titleView;
        PlanetMapView planetMap;
        PlanetStagesPanel planetStages;
        LanguagePanel languagePanel;
        SettingsPanel settings;
        CreditsPanel creditsPanel;
        TitleFlowController flowController;
        SettingsController settingsController;
        OverlayStack overlays;
        BackNavigator back;
        int openPlanet = -1;
        LanguageController language;
        GameObject menuBackground;
        // Stage select v4 (StageSelectToggle, docs/stage-select-transitions.md): null when the run uses the card map.
        bool newMap;
        PlanetSpace planetSpace;
        StageSelectViews stageViews;

        /// The v4 stage select controller (scene-flow's TitleJourney subscribes StageChosen); null when the new map is off.
        public StageSelectController StageSelect { get; private set; }
        // Journey transitions (scene-flow, docs/stage-select-transitions.md): null unless JourneyToggle is on and the new map is up.
        TitleJourney journey;

        void Awake()
        {
            if (assets == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.assets is not set (unity.sh scene-validate)");
            if (config == null) throw new MissingReferenceException($"[SevenSteps] {name}: TitleScreen.config is not set (unity.sh scene-validate)");
            if (!Bootstrap.HasRunThisPlay) Bootstrap.Run(config, assets, null, enterFirstScreen: false);
            flow = Services.Get<AppFlow>();
            save = Services.Get<SaveSystem>();
            localizer = Services.Get<Localizer>();
            Ui.Theme = assets.uiTheme;
            levels = LevelCatalog.Load(assets);

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = assets.background; }
            GameAudio.ApplyMaster(save.Settings); // the Title scene has no GameAudio instance: master volume only
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = BuildCanvas();
            menuBackground = Ui.MenuBackground(root); // null when the theme has none: the camera color shows
            newMap = StageSelectToggle.On(Services.Get<LaunchOptions>());
            titleView = new TitleView(root);
            planets = PlanetEntry.FromCatalog(levels.Length);
            planetMap = new PlanetMapView(root, planets, levels);
            planetMap.Story = Services.TryGet<StoryBook>(out var story) ? story : null; // carvings, lit dipper lines (story 0.5)
            planetMap.ReduceMotion = save.Settings.reduceMotion;
            if (story != null && story.On) titleView.ShowSky(story.Catalog.ChapterIds.Select(story.FinaleCleared).ToArray());
            planetStages = new PlanetStagesPanel(root);
            if (newMap)
            {
                if (assets.planetBodies == null) throw new MissingReferenceException($"[SevenSteps] {name}: GameAssets.planetBodies is not set (Data/PlanetBodies.asset) and the new map is on");
                if (cam == null) throw new MissingReferenceException($"[SevenSteps] {name}: the Title scene has no Main Camera (the new map draws planet bodies)");
                planetSpace = new PlanetSpace(cam, assets.background);
                var map3d = new PlanetMap3DView(root, planetSpace, assets.planetBodies, planets);
                var surface = new PlanetSurfaceView(root, planetSpace, assets.planetBodies, planets, map3d.SetBodiesVisible);
                stageViews = new StageSelectViews(map3d, surface, overlays = new OverlayStack());
            }
            languagePanel = new LanguagePanel(root);
            bool telemetryDefault = Services.Get<AppConfig>().telemetryOnByDefault;
            settings = SettingsPanel.Create(root, save, telemetryDefault, localizer);
            settingsController = new SettingsController(save, settings, telemetryDefault, localizer);
            creditsPanel = new CreditsPanel(root, credits, licenses);
            BuildLanguageBadge(root);
            if (overlays == null) overlays = new OverlayStack();
            // Text in the current language; redraw when the language changes (docs/architecture.md 13.3).
            ApplyText();
            localizer.Changed += OnLanguageChanged;

            flowController = new TitleFlowController(flow, save, this, levels, Services.Get<EntryTracker>());
            back = new BackNavigator(overlays, flowController.Back);
            titleView.BeginRequested += flowController.BeginJourney;
            titleView.ContinueRequested += flowController.ContinueJourney;
            titleView.StagesRequested += flowController.OpenStageSelect;
            titleView.SettingsRequested += () => Open(settings);
            titleView.CreditsRequested += () => Open(creditsPanel);
            titleView.LanguageRequested += OpenLanguage;
            planetMap.PlanetClicked += OpenPlanet;
            planetMap.BackRequested += flowController.Back;
            planetStages.StageClicked += flowController.PlayStage;
            if (newMap)
            {
                var stages = planets.Select(p => p.Stages).ToList();
                StageSelect = new StageSelectController(stageViews, BuildStageSelectModel, stages, flow.CanGoToGame, flowController.PlayStage,
                    Services.TryGet<JourneyReturn>(out var journeyReturn) ? journeyReturn : null);
                stageViews.Map.BackRequested += flowController.Back;
            }
            if (StageSelect != null && JourneyToggle.On(Services.Get<LaunchOptions>()))
            {
                var runner = JourneyRunner.Create(transform);
                runner.ReduceMotion = save.Settings.reduceMotion;
                journey = new TitleJourney(StageSelect, runner, Services.Get<JourneyDive>(), Services.Get<JourneyReturn>(), flow.CanGoToGame);
            }
            language = new LanguageController(languagePanel, overlays, localizer, save, Services.Get<LaunchOptions>().Language);
            settingsController.OpenLanguage = OpenLanguage; // the settings language button opens the picker
            overlays.Changed += OnOverlaysChanged;
        }

        void Open(IOverlay overlay)
        {
            if (!overlays.Contains(overlay)) overlays.Push(overlay);
        }

        void Start()
        {
            flowController.Start();
            journey?.ReturnIfPending(); // planet complete: pull back from the planet to the map, then the trail lights
            var launch = Services.Get<LaunchOptions>();
            if (!save.LanguageChosen && launch.Language == null && flow.State == AppState.Title) OpenLanguage();
            if (launch.OpenSettings) Open(settings); // -settings / ?settings=1 (development, screenshots)
            if (launch.TitleCheck != "" && !launch.DirectToGame) StartCoroutine(TitleCheck(launch.TitleCheck));
            if (launch.JourneyCheck == "complete" && StageSelect != null)
            {
                // -journeycheck complete (scene-flow): the planet-complete return arrived here; its own screenshots and quit.
                StartCoroutine(JourneyCheck.RunTitle(flow, StageSelect, stageViews.Map, Services.Get<JourneyReturn>(), launch.Screenshot));
                return;
            }
            // Smoke test without -level: "-screenshot title.png" [-stageselect] [-credits] opens that panel first.
            if (!string.IsNullOrEmpty(launch.Screenshot))
            {
                if (launch.OpenStageSelect) flowController.OpenStageSelect();
                if (launch.OpenCredits) Open(creditsPanel);
                StartCoroutine(ScreenshotOnce.Run(launch.Screenshot, () => $"{flow.State}, cleared {save.Progress.ClearedCount}"));
            }
        }

        /// -titlecheck (S8): press the real buttons of one Title path; the Game scene logs where it arrived. A path that
        /// does not leave the Title scene logs FAIL here.
        System.Collections.IEnumerator TitleCheck(string path)
        {
            yield return new WaitForSecondsRealtime(1f);
            while (overlays.Count > 0) overlays.Pop(); // e.g. the first-run language picker
            string fail = null;
            switch (path)
            {
                case "begin": titleView.BeginButton.onClick.Invoke(); break;
                case "continue":
                    if (!titleView.ContinueButton.interactable) fail = "Continue is off (no last played stage)";
                    else titleView.ContinueButton.onClick.Invoke();
                    break;
                case "map":
                    titleView.StagesButton.onClick.Invoke();
                    yield return null;
                    int planet = Mathf.Min(1, planets.Count - 1);
                    if (newMap)
                    {
                        // The planet plays its first unfinished stage, or (complete) opens the surface path: press place 1 there.
                        stageViews.Map.Target(planet).onClick.Invoke();
                        yield return null;
                        if (stageViews.Surface.Visible) stageViews.Surface.Place(0).onClick.Invoke();
                        // A journey dive (scene-flow) takes 1.5 s before the scene changes: the 10 s wait below covers it.
                        break;
                    }
                    planetMap.Card(planet).onClick.Invoke();
                    yield return null;
                    planetStages.Card(0).onClick.Invoke();
                    break;
            }
            float t0 = Time.unscaledTime;
            while (fail == null && Time.unscaledTime - t0 < 10f) yield return null; // the Game scene takes over (this object goes)
            Debug.Log($"[SevenSteps] Title check {path}: FAIL: {fail ?? $"still in {flow.State} after 10 s"}");
            Application.Quit();
        }

        void OpenPlanet(int planet)
        {
            if (overlays.Count > 0) return;
            planetStages.Bind(planets[planet], save.Progress, levels, localizer, TruthOf(planet));
            openPlanet = planet;
            Open(planetStages);
        }

        void OpenLanguage() => language.Open();

        /// An overlay opened or closed: when none is left, give the keyboard focus back to the screen under it.
        void OnOverlaysChanged()
        {
            if (overlays.Count > 0) return;
            if (newMap && flow.State == AppState.StageSelect)
            {
                stageViews.Map.Frame(); // the surface path had the camera
                if (stageViews.LastSurfacePlanet >= 0) stageViews.Map.Focus(stageViews.LastSurfacePlanet);
                stageViews.ForgetSurface();
            }
            else if (flow.State == AppState.StageSelect && openPlanet >= 0) planetMap.Focus(openPlanet);
            else if (!flow.IsLoading) Show(flow.State);
            openPlanet = -1;
        }

        /// "EN / KO" at the top right: switches to the next language and saves it (SettingsController).
        void BuildLanguageBadge(Transform root)
        {
            var badge = Ui.MakeButton(root, "EN / KO", 20, () => settingsController.ToggleLanguage(), out var text, "LanguageBadge");
            ((Image)badge.targetGraphic).color = new Color(0, 0, 0, 0);
            ((RectTransform)badge.transform).Place(Vector2.one, Vector2.one, new Vector2(-30, -18), new Vector2(120, 34)); // from the right edge (1280x800, 4:3)
            text.color = Ui.Gold;
            text.alignment = TextAnchor.MiddleRight;
            text.font = Ui.Theme.FontFor(localizer.Current, FontRole.Number); // Latin only: the number font in every language
        }

        void ApplyText()
        {
            titleView.Apply(localizer, Services.TryGet<StoryBook>(out var story) ? story.TitleLine(localizer.Current) : null);
            planetMap.Apply(localizer);
            if (newMap)
            {
                stageViews.Map.Apply(localizer);
                stageViews.Surface.Apply(localizer);
            }
            languagePanel.Apply(localizer);
            creditsPanel.Apply(localizer);
            if (overlays.Contains(planetStages) && openPlanet >= 0)
                planetStages.Bind(planets[openPlanet], save.Progress, levels, localizer, TruthOf(openPlanet));
        }

        void OnLanguageChanged(Language lang) => ApplyText();

        /// A planet's truth for its stage list once its finale is cleared (story 0.5 #3), else null (the tagline).
        string TruthOf(int planet) =>
            Services.TryGet<StoryBook>(out var story) && story.FinaleCleared(planets[planet].Key) ? story.FinaleLine(planets[planet].Key, localizer.Current) : null;

        void OnDestroy()
        {
            flowController?.Dispose();
            StageSelect?.Dispose();
            journey?.Dispose();
            settingsController?.Dispose();
            language?.Dispose();
            if (localizer != null) localizer.Changed -= OnLanguageChanged;
            if (overlays != null) overlays.Changed -= OnOverlaysChanged;
        }

        /// ITitlePanels: the flow controller shows the panel for the state.
        public void Show(AppState state)
        {
            if (titleView == null) throw new InvalidOperationException("[SevenSteps] TitleScreen.Show before Awake");
            if (state == AppState.Title) titleView.Show(flowController.CanContinue); else titleView.Hide();
            bool select = state == AppState.StageSelect;
            if (newMap)
            {
                planetMap.Hide();
                if (menuBackground != null) menuBackground.SetActive(!select); // the planet space draws it behind the bodies
                if (select) StageSelect.Show(); else stageViews.Map.Hide();
            }
            else if (select) planetMap.Show(save.Progress, -1); else planetMap.Hide();
        }

        /// Esc in one place: the top overlay closes first, otherwise AppFlow's back target (BackNavigator). Arrows and Enter
        /// are uGUI navigation / submit on the selected button (every screen selects one when it shows).
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            if (journey != null && journey.Blocks) return; // a transition plays, or this press skipped / cancelled one
            if (kb.escapeKey.wasPressedThisFrame) back.Back();
        }

        /// The v4 stage select model for the save now: cleared stages, the last played one, and the star-trail edges
        /// (story.json "dipper" when the story is on, else the Dipper lines).
        StageSelectModel BuildStageSelectModel()
        {
            var keys = planets.Select(p => p.Key).ToList();
            List<(int from, int to)> trails;
            if (Services.TryGet<StoryBook>(out var story) && story.On)
                trails = StageSelectModel.TrailsByKey(keys, story.Catalog.Dipper.Select(d => (d.from, d.to)));
            else
                trails = UiTheme.DipperEdges.Where(e => e.a < keys.Count && e.b < keys.Count).Select(e => (e.a, e.b)).ToList();
            int last = string.IsNullOrEmpty(save.Progress.lastStage) ? -1 : save.Progress.LastStageIndex(levels);
            return StageSelectModel.From(keys, planets.Select(p => p.Stages).ToList(), s => save.Progress.IsCleared(levels[s]), last, trails);
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
