using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.Telemetry;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Registers the app services (docs/architecture.md 11.2). Called by the Boot scene, or by GameController/TitleScreen when
    /// their scene is played directly in the editor. The only DontDestroyOnLoad root it makes is the telemetry runner,
    /// and only when telemetry is on (the other DDOL root, SceneFader, comes with the first scene load; docs/scene-flow.md 2).
    /// </summary>
    public static class Bootstrap
    {
        /// True once Run has finished in this Play (or player run). A scene played directly in the editor bootstraps when it is false.
        /// Not Services.Has: with domain reload disabled that could see the previous Play's services (docs/scene-flow.md 3.1).
        public static bool HasRunThisPlay { get; private set; }

        static Action quitSave;

        /// Launch options for the next Bootstrap.Run, set by PlayMode tests and the Play Mode capture (docs/scene-flow.md 9.1):
        /// a -runTests run cannot pass -level / -telemetry off / -savekey per test. Taken once by ReadLaunchOptions.
        static LaunchOptions nextRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlay()
        {
            HasRunThisPlay = false;
            nextRun = null;
        }

        /// Tests and the Play Mode capture: the next ReadLaunchOptions (Boot scene, or a scene played directly) returns
        /// <paramref name="launch"/> instead of the command line and page URL. Set it inside Play Mode, after entering it
        /// (entering clears it). Throws when one is already waiting.
        public static void UseLaunchOptionsForNextRun(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            if (nextRun != null) throw new InvalidOperationException("[SevenSteps] launch options for the next run are already set");
            nextRun = launch;
        }

        /// PlayMode tests between runs in one Play session: forget this Play's bootstrap and services, as entering Play Mode
        /// does (SubsystemRegistration). The caller destroys the DontDestroyOnLoad roots (TelemetryRunner, SceneFader).
        public static void ResetForTest()
        {
            ResetForPlay();
            Services.ResetForPlay();
        }

        /// The command line and page URL, or the options a test set (<see cref="UseLaunchOptionsForNextRun"/>), once.
        public static LaunchOptions ReadLaunchOptions()
        {
            if (nextRun != null)
            {
                var o = nextRun;
                nextRun = null;
                return o;
            }
            return LaunchOptions.Parse(Environment.GetCommandLineArgs(), Application.absoluteURL);
        }

        /// <param name="assets">Stage list (levels.json): AppFlow needs the stage count.</param>
        /// <param name="enterFirstScreen">Boot scene: go to Game (?level=) or Title now. Direct scene play: false, the scene enters itself.</param>
        /// <param name="levelFromEditor">The launch level came from the Game scene's startLevel (LaunchOptions.UseEditorStart), not the URL.</param>
        public static void Run(AppConfig config, GameAssets assets, LaunchOptions launch = null, bool enterFirstScreen = true, bool levelFromEditor = false)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "[SevenSteps] Bootstrap needs Data/AppConfig.asset: set config on the scene root (unity.sh scene-validate)");
            if (assets == null)
                throw new ArgumentNullException(nameof(assets), "[SevenSteps] Bootstrap needs Data/GameAssets.asset: set assets on the scene root (unity.sh scene-validate)");
            if (HasRunThisPlay) throw new InvalidOperationException("[SevenSteps] Bootstrap already ran in this Play");
            launch ??= ReadLaunchOptions();
            foreach (var w in launch.Warnings) Debug.LogWarning("[SevenSteps] Launch option " + w);
            int stageCount = LevelCatalog.Load(assets).Length;

            Services.Clear(); // a Run that threw halfway left HasRunThisPlay false; start clean

            var store = SaveStores.For(launch); // -savekey / ?savekey=: suffixed keys (docs/architecture.md 11)
            var save = new SaveSystem(store);
            NewPlayerLink.Apply(launch, save); // -newplayer / ?newplayer=1: fresh progress and playerId before telemetry starts
            // UI language for this run (docs/architecture.md 13.3): -lang / ?lang= wins over the saved setting, and is not saved.
            // Registered before telemetry: TelemetryRunner reports Localizer.Current as lang and follows Localizer.Changed.
            Services.Register(Localizer.FromAssets(assets.strings, launch.Language ?? save.Language));
            var telemetry = TelemetryRunner.CreateTelemetry(config, launch, save);
            UI.Ui.Theme = assets.uiTheme; // the scene fader's loading screen is built before any scene root sets it
            var flow = new AppFlow(new SceneLoader(), stageCount);

            Services.Register(config);
            Services.Register(launch);
            Services.Register<ISaveStore>(store);
            Services.Register(save);
            Services.Register(telemetry);
            Services.Register(flow);
            Services.Register(EntryTracker.ForLaunch(launch.DirectToGame && !levelFromEditor));
            Services.Register(new JourneyReturn()); // planet complete → map hand-off (docs/stage-select-transitions.md)
            Services.Register(new ClearNews()); // win -> U5 surface first-clear trace (stage-select-transitions.md 4); empty at boot, not saved
            Services.Register(new JourneyDive()); // map → board dive hand-off (same doc)
            // Music + the shared effects (user request 2026-11-13): one DontDestroyOnLoad player; the app state picks the music.
            GameSound.Create(assets.sounds, save);
            flow.Changed += (_, to) => GameSound.Music(SoundIds.MusicFor(to));
            // Story (docs/architecture.md 16): off when story.json is missing or broken (A8). Dev runs do not touch its save.
            bool devRun = launch.SmokeView != SmokeView.None || !string.IsNullOrEmpty(launch.Replay) || launch.FlowCheck || launch.TitleCheck != "";
            var story = StoryLoad.FromAssets(assets, save, devRun);
            Services.Register(story);
            // 16.5 "앱 종료": story memory marked since the last save point (review 6e8f3cd M3). One hook per Play (no domain reload).
            if (quitSave != null) Application.quitting -= quitSave;
            quitSave = story.Save;
            Application.quitting += quitSave;
            HasRunThisPlay = true;

            if (!enterFirstScreen) return;
            if (launch.DirectToGame) flow.GoToGame(FirstStage(launch, stageCount));
            else if (FirstTutorialBoard(launch) is int board && board >= 0) flow.GoToTutorial(board, LevelCatalog.Tutorials.Count);
            else flow.Go(AppState.Title);
        }

        /// 0-based tutorial board for a -tutorial N / ?tutorial=N launch (docs/tutorial.md 2.4), -1 for none. A number past
        /// the boards in levels.json is external input: warned and ignored (the title opens). -level wins over it.
        public static int FirstTutorialBoard(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            if (launch.Tutorial <= 0 || launch.DirectToGame) return -1;
            int count = LevelCatalog.Tutorials.Count;
            if (launch.Tutorial <= count) return launch.Tutorial - 1;
            Debug.LogWarning($"[SevenSteps] Launch option tutorial {launch.Tutorial} is past the tutorial boards ({count}); ignored");
            return -1;
        }

        /// 0-based start stage for a -level / ?level= launch. A level past the last stage is external input: warn and use the last.
        public static int FirstStage(LaunchOptions launch, int stageCount)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            if (!launch.DirectToGame) throw new InvalidOperationException("no launch level");
            if (launch.Level <= stageCount) return launch.Level - 1;
            Debug.LogWarning($"[SevenSteps] Launch option level {launch.Level} is past the last stage ({stageCount}); starting at {stageCount}");
            return stageCount - 1;
        }
    }
}
