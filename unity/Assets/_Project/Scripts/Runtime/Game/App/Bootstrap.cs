using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.Telemetry;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Registers the app services (docs/architecture.md 11.2). Called by the Boot scene, or by GameController/TitleScreen when
    /// their scene is played directly in the editor. The only DontDestroyOnLoad object it makes is the telemetry runner.
    /// </summary>
    public static class Bootstrap
    {
        /// True once Run has finished in this Play (or player run). A scene played directly in the editor bootstraps when it is false.
        /// Not Services.Has: with domain reload disabled that could see the previous Play's services (docs/scene-flow.md 3.1).
        public static bool HasRunThisPlay { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlay() => HasRunThisPlay = false;

        public static LaunchOptions ReadLaunchOptions() =>
            LaunchOptions.Parse(Environment.GetCommandLineArgs(), Application.absoluteURL);

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

            var store = new PlayerPrefsStore();
            var save = new SaveSystem(store);
            var telemetry = TelemetryRunner.CreateTelemetry(config, launch, save);
            var flow = new AppFlow(new SceneLoader(), stageCount);

            Services.Register(config);
            Services.Register(launch);
            Services.Register<ISaveStore>(store);
            Services.Register(save);
            Services.Register(telemetry);
            Services.Register(flow);
            Services.Register(EntryTracker.ForLaunch(launch.DirectToGame && !levelFromEditor));
            // UI language for this run (docs/architecture.md 13.3): -lang / ?lang= wins over the saved setting, and is not saved.
            Services.Register(Localizer.FromAssets(assets.strings, launch.Language ?? save.Language));
            HasRunThisPlay = true;

            if (!enterFirstScreen) return;
            if (launch.DirectToGame) flow.GoToGame(FirstStage(launch, stageCount));
            else flow.Go(AppState.Title);
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
