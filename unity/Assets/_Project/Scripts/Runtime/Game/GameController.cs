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
        StoryBook story;
        Story.StoryDirector storyLines;
        EndingView endingView;
        EndingController ending;
        GameJourney journey; // stage select v4 transitions; null when JourneyToggle is off

        bool OnlyChapterCard() => chapterCard != null && overlays.Count == 1 && overlays.Top == chapterCard;

        /// The board controller (tests, tools).
        public BoardController Board => board;
        /// Stage on the board and the stage list (art preview scripts read these).
        public int LevelIndex => board.LevelIndex;
        public SevenSteps.Core.Level[] Levels => board.Levels;
        /// One-time guides of the minimal UI (move guide, KEEP / SWITCH guide, choice result lines). The HUD and the choice
        /// cards read it and redraw on Changed (gameplay-ui).
        public TutorialTracker Tutorial => tutorial;
        /// Stage select v4 journey transitions (docs/stage-select-transitions.md); null when JourneyToggle is off. PlayMode tests read it.
        public GameJourney Journey => journey;
        /// The stage flow (Next, map, again from the clear; the journey after the clear). PlayMode tests and captures read it.
        public GameFlowController Flow => flowController;
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
            // U5 (docs/ui-u5-kit.md 2): asked once here. The current HUD stays for the board labels, message, statues, guide
            // and story; U5 hides its own buttons, title, dots and cards and draws the top band and the choice glyphs.
            if (UiStyleToggle.U5(launch))
            {
                if (assets.uiIcons == null) throw new MissingReferenceException("[SevenSteps] GameAssets.uiIcons is not set (Data/UiIconSet.asset) and U5 is on");
                u5Hud = UI.U5.U5GameHud.Build(assets.uiIcons, tiles, localizer);
                UI.U5.U5GameHud.HideClassic(hud);
            }
            var result = ResultView.Create();
            result.Apply(localizer);
            this.localizer = localizer;
            var overlayCanvas = OverlayCanvas();
            var settings = settingsPanel = SettingsPanel.Create(overlayCanvas, save, cfg.telemetryOnByDefault, localizer);
            var languagePanel = new LanguagePanel(overlayCanvas); // opened from the settings language button
            languagePanel.Apply(localizer);
            var menuPanel = this.menuPanel = new MenuPanel(overlayCanvas);
            menuPanel.Apply(localizer);
            // Story ending (docs/architecture.md 16.8): four cards after the last stage's [끝까지 보기].
            story = Services.TryGet<StoryBook>(out var book) ? book : StoryBook.Off();
            var endingView = this.endingView = new EndingView(overlayCanvas, assets.storyImages, localizer);
            applyResultLanguage = _ => { result.Apply(localizer); languagePanel.Apply(localizer); menuPanel.Apply(localizer); endingView.Apply(localizer); if (u5Hud != null) u5Hud.Apply(localizer); if (u5Clear != null) u5Clear.Apply(localizer); u5Pause?.Apply(localizer); u5Settings?.Apply(localizer); }; // the HUD and board redraw in BoardController
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
            if (u5Hud != null) WireU5(u5Hud);
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
            // U5: clears on the U5 clear (star stones, Next by the exit); losses still on the current panel until the failure step.
            Flow.IResultPanel resultPanel = result;
            if (u5Hud != null)
            {
                u5Clear = UI.U5.U5ClearView.Build(assets.uiIcons, localizer);
                u5Clear.ExitPoint = () => board.View.BrotherOnCanvas();
                u5Clear.BoardArea = () => board.View.ProjectedArea();
                // The three world star stones stand behind the brother on the exit (R1 on 1f90617).
                u5Clear.ExitWorld = () => board.View.Player.transform.position;
                u5Clear.WorldParent = () => board.View.BoardRoot;
                u5Clear.WorldCamera = () => cam;
                u5Clear.CellSize = () => board.View.Grid.CellSize;
                resultPanel = u5Clear;
            }
            flowController = new GameFlowController(flow, Services.Get<EntryTracker>(), save, board, resultPanel, LevelCatalog.ChapterKeyOf, LevelCatalog.ChapterOf);
            back = new BackNavigator(overlays, flowController.Back);
            chapterIntro = new ChapterIntroController(flowController, overlays, chapterCard, key => LevelCatalog.ChapterCard(key, localizer.Current));
            this.result = result;
            presetFor = ChapterPresetFor(cfg);
            // The chapter card alone does not hold the board: a board key closes it and plays (Update below).
            // Stage select v4 journey (docs/stage-select-transitions.md): clears lead on to the next board or back to the map.
            if (JourneyToggle.On(launch))
            {
                var runner = JourneyRunner.Create(transform);
                runner.ReduceMotion = save.Settings.reduceMotion;
                journey = new GameJourney(flowController, board, cam, runner, Services.Get<JourneyReturn>(), Services.Get<JourneyDive>(), chapterStage,
                    stage => JourneyRoute.PlanetIndex(ChaptersWithStages(), LevelCatalog.ChapterKeyOf(stage)), LevelCatalog.ChapterKeyOf);
            }
            if (journey != null)
            {
                // No chapter card on the dive's arrival: the dive is the planet entry (lead 2026-11-11). Continue, [ ] and ?level= keep it.
                var dive = Services.Get<JourneyDive>();
                chapterIntro.Skips = stage => dive.Pending && dive.Stage == stage;
            }
            board.InputBlocked = () => flow.IsLoading || (journey != null && journey.Blocks) || (overlays.Count > 0 && !OnlyChapterCard());
            board.FlowTakes = flowController.TakesCommand;

            // One-time guides (ui-minimal). Dev runs (replay, flow / title checks, smoke) keep the profile untouched and start
            // with every guide seen, so their screenshots show the plain HUD.
            bool devRun = launch.SmokeView != SmokeView.None || !string.IsNullOrEmpty(launch.Replay) || launch.FlowCheck || launch.TitleCheck != "";
            // "-guides fresh" (captures of the first-choice cards, v3-choose-first): a dev run with nothing seen yet, still in memory.
            // Read here until scene-flow moves it into LaunchOptions.
            bool freshGuides = Array.IndexOf(Environment.GetCommandLineArgs(), "-guides") is int gi && gi >= 0
                && gi + 1 < Environment.GetCommandLineArgs().Length && Environment.GetCommandLineArgs()[gi + 1] == "fresh";
            tutorial = new TutorialTracker(devRun ? new MemoryTutorialStore(allSeen: !freshGuides) : (ITutorialStore)new SaveTutorialStore(save));
            board.Session.Updated += tutorial.Apply;
            board.StageEntered += StopControlsGuide;
            // U5: the pause row is the in-game menu and its settings icon opens the U5 settings row (its "more" opens the
            // full current panel: volumes, play data, step numbers).
            Flow.IMenuPanel menuView = menuPanel;
            Action openSettingsView = () => { if (!overlays.Contains(settings)) overlays.Push(settings); };
            if (u5Hud != null)
            {
                u5Pause = new UI.U5.U5PauseView(assets.uiIcons, localizer);
                u5Settings = new UI.U5.U5SettingsView(assets.uiIcons, localizer);
                var full = openSettingsView;
                u5SettingsController = new UI.U5.U5SettingsController(save, u5Settings, languageController.Open, full);
                menuView = u5Pause;
                openSettingsView = () => { if (!overlays.Contains(u5Settings)) overlays.Push(u5Settings); };
            }
            menu = new MenuController(overlays, menuView,
                openSettings: openSettingsView,
                openStages: flowController.OpenStageSelect,
                showSolution: board.ShowHint,
                playControls: tutorial.PlayControls,
                // "시작 줄 다시 보기" (story-integration.md 0.2): the row only when the stage has a start line and no result is up
                // (review ebdb653 N1). storyLines is made later in Awake: lambdas.
                canReplayLine: () => storyLines != null && flow.State != AppState.Result && StartLineHere().Length > 0,
                replayLine: () => ReplayStartLine());
            hud.MenuRequested += menu.Toggle; // the HUD menu button (settings, stages, solution, controls)
            // Move guide (first play, or Menu > controls) in the HUD's left margin; the KEEP / SWITCH guide goes with the choice cards.
            hud.Guide = tutorial.ShowMoveGuide;
            // Choice cards: both result lines for the first confirms, the guide line the first time (shown after Presented, 4.4).
            board.Choice.ExplainChoices = () => tutorial.ExplainChoices;
            board.Choice.ShowGuide = () => tutorial.ShowChoiceGuide;
            tutorial.Changed += () => { hud.Guide = tutorial.ShowMoveGuide; board.Choice.Refresh(); };
            ending = new EndingController(overlays, endingView);
            ending.Finished += OnEndingFinished;
            flowController.EndingRequested += BeginEnding;
            flowController.ClearShown += story.Save; // 16.5: the finale line just shown is counted
            result.NextRequested += flowController.Next;
            result.RetryRequested += flowController.Again;
            result.StageSelectRequested += flowController.OpenStageSelect;
            result.UndoRequested += () => board.Send(SevenSteps.Core.Command.Undo);
            result.RestartRequested += board.Restart;
            // Clear over the board (docs 14.7): the HUD hides while the result shows.
            result.HudHidden += hud.Conceal;
            if (u5Hud != null)
            {
                u5Clear.NextRequested += flowController.Next;
                u5Clear.RetryRequested += flowController.Again;
                u5Clear.StageSelectRequested += flowController.OpenStageSelect;
                u5Clear.UndoRequested += () => board.Send(SevenSteps.Core.Command.Undo);
                u5Clear.RestartRequested += board.Restart;
                u5Hud.PauseRequested += menu.Toggle;
                u5Pause.RestartRequested += () => { menu.Toggle(); board.Restart(); };
                // Both HUDs hide while a U5 result or row is up (14.7: a 0.25 scrim needs the HUD hidden), from one place.
                result.HudHidden += _ => RefreshU5Huds();
                u5Clear.HudHidden += _ => RefreshU5Huds();
                overlays.Changed += RefreshU5Huds;
            }

            // Story on the board (docs 16.7 / 16.8, story-integration.md 0.2): the stage's start line and a planet's finale
            // line on the HUD's passing line. Quiet in dev runs and art perf, like the guides.
            var chapterKeys = new string[levels.Length];
            for (int i = 0; i < chapterKeys.Length; i++) chapterKeys[i] = LevelCatalog.ChapterKeyOf(i);
            bool FinaleHere() => story.On && StoryTruths.IsFinaleStage(chapterKeys, board.LevelIndex);
            storyLines = new Story.StoryDirector(board.Session, hud, devRun || launch.ArtPerf,
                startLine: StartLineHere,
                finaleLine: () => FinaleHere() ? story.FinaleLine(chapterKeys[board.LevelIndex], localizer.Current) : null,
                finaleShown: () => story.Progress.MarkShown(story.Catalog.Find(StoryRules.Finale(chapterKeys[board.LevelIndex])).id),
                covered: () => overlays.Count > 0);
            board.FinaleStarting = storyLines.FinaleStarting;
            overlays.Changed += storyLines.CoverChanged;
        }

        void Start()
        {
            flowController.Start(launch.DirectToGame ? Bootstrap.FirstStage(launch, flow.StageCount) : 0);
            journey?.ArriveIfDiving(flow.Stage); // the second half of the map → board dive
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
            if (launch.JourneyCheck != "" && !string.IsNullOrEmpty(launch.Replay) && launch.DirectToGame)
            {
                // -journeycheck advance|complete (stage select v4 journey, smoke): the replay wins, the transition is checked.
                StartCoroutine(JourneyCheck.RunGame(launch.JourneyCheck, flow, board, journey, result, overlays, launch.TakeReplay(), launch.Screenshot, flowController.Next));
                return;
            }
            if (launch.FlowCheck && !string.IsNullOrEmpty(launch.Replay) && launch.DirectToGame)
            {
                // -flowcheck: the replay wins for real, then Result -> Next -> chapter entry are checked (S8).
                var check = new FlowCheck(flow, board, flowController, overlays, result, chapterStage, chapterIntro, localizer, presetFor, ending, endingView);
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

        /// The current stage's start line in the current language ("" = none).
        string StartLineHere() => Story.StoryDirector.StartText(LevelCatalog.TextOf(board.LevelIndex, localizer.Current), LevelCatalog.StartLineOf(board.LevelIndex));

        /// Menu "다시 보기" (story-integration.md 0.2): the stage's start line again once the menu closes. False when the
        /// stage has none (the menu item can say so or stay quiet), and over a result (its HUD is hidden, review N1 on
        /// ebdb653). The menu item and its wiring are scene-flow's.
        public bool ReplayStartLine() => storyLines != null && flow.State != AppState.Result && storyLines.ReplayStartLine();

        void OnDestroy()
        {
            storyLines?.Dispose();
            flowController?.Dispose();
            settingsController?.Dispose();
            languageController?.Dispose();
            u5SettingsController?.Dispose();
            chapterIntro?.Dispose();
            menu?.Dispose();
            ending?.Dispose();
            journey?.Dispose();
            if (board != null && tutorial != null)
            {
                if (board.Session != null) board.Session.Updated -= tutorial.Apply;
                board.StageEntered -= StopControlsGuide;
            }
            if (localizer != null) localizer.Changed -= applyResultLanguage;
        }

        void StopControlsGuide(int stage, string via) => tutorial.StopControls();

        /// The ending for the won last stage, or straight to the star map when the story is off.
        void BeginEnding()
        {
            var cards = board.State != null ? story.Ending(board.State, localizer.Current) : null;
            if (cards == null) flowController.OpenStageSelect();
            else ending.Begin(cards);
        }

        void OnEndingFinished(EndingModel cards, int seen, bool skipped)
        {
            story.EndingFinished(cards, seen);
            flowController.OpenStageSelect();
        }

        /// The ending is the top overlay (its keys: Enter / Space / Right = next; Esc skips through the BackNavigator).
        bool EndingOnTop => ending != null && ending.IsOpen && overlays.Top == endingView;

        /// Flow keys. Board keys are read by the board controller (blocked while an overlay is open or a scene loads).
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow.IsLoading) return;
            if (journey != null && journey.Blocks) return; // a transition plays, or this press skipped one
            // Esc in one place: the top overlay closes first; on the clear result the menu opens over it (14.7: the result
            // has no stage select button); otherwise AppFlow's back target (BackNavigator).
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (overlays.Count == 0 && flow.State == AppState.Result) menu.Open();
                else back.Back();
                return;
            }
            if (EndingOnTop && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
            {
                ending.Next();
                return;
            }
            // Chapter card: a board key dismisses it and still counts as a move (rehearsal 2026-11-10: the first arrow was lost).
            if (OnlyChapterCard() && BoardController.AnyBoardKeyPressed(kb)) back.Back();
            if (overlays.Count > 0) return;
            if (kb.leftBracketKey.wasPressedThisFrame) flowController.SelectStage(flow.Stage - 1);
            else if (kb.rightBracketKey.wasPressedThisFrame) flowController.SelectStage(flow.Stage + 1);
            else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && flow.State == AppState.Result) flowController.Next();
            else if (flow.State == AppState.Result && result.StarRowShown)
            {
                // What each star asks, by keyboard (14.7 focus); Left / Right never reach the board here (TakesCommand).
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (kb.tabKey.wasPressedThisFrame) result.FocusStar(shift ? -1 : 1);
                else if (kb.rightArrowKey.wasPressedThisFrame) result.FocusStar(1);
                else if (kb.leftArrowKey.wasPressedThisFrame) result.FocusStar(-1);
            }
        }

        /// Chapters that have stages, in chapters.json order: the star map's planets (JourneyRoute.PlanetIndex).
        static System.Collections.Generic.List<string> ChaptersWithStages()
        {
            var keys = new System.Collections.Generic.List<string>();
            foreach (var c in LevelCatalog.Chapters)
                if (LevelCatalog.StagesOf(c.id).Length > 0) keys.Add(c.id);
            return keys;
        }

        /// Stage -> its chapter's art preset: levels.json chapter -> chapters.json artPreset -> AppConfig.Chapter.
        /// Null when there is no chapters.json or AppConfig has no chapters yet (the scene keeps its own look).
        static Func<int, ChapterPreset> ChapterPresetFor(AppConfig cfg) => stage =>
        {
            var info = LevelCatalog.ChapterOf(stage);
            return info != null ? cfg.Chapter(info.ArtPreset) : null;
        };

        /// Canvas for overlays above the HUD (0) and the result panel (5).
        UI.U5.U5GameHud u5Hud;
        UI.U5.U5ClearView u5Clear;
        UI.U5.U5PauseView u5Pause;
        UI.U5.U5SettingsView u5Settings;
        UI.U5.U5SettingsController u5SettingsController;

        void RefreshU5Huds()
        {
            bool hide = u5Clear.IsShown || u5Pause.Visible || u5Settings.Visible;
            hud.Conceal(hide);
            u5Hud.Conceal(hide);
        }

        /// U5 HUD -> the same controller calls as the current HUD (docs/ui-u5-kit.md 7).
        void WireU5(UI.U5.U5GameHud u5)
        {
            board.UseChoiceView(u5);
            board.HudRefreshed += u5.Refresh;
            u5.BoardArea = () => board.View.ProjectedArea();
            u5.UndoRequested += () => board.Send(SevenSteps.Core.Command.Undo);
            u5.RestartRequested += board.Restart;
            u5.HintRequested += board.ShowHint;
            u5.KeepRequested += () => board.Send(SevenSteps.Core.Command.Keep);
            u5.SwitchRequested += () => board.Send(SevenSteps.Core.Command.Switch);
        }

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
