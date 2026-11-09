using System;
using SevenSteps.Contracts;
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

        /// <param name="enterFirstScreen">Boot scene: go to Game (?level=) or Title now. Direct scene play: false, the scene enters itself.</param>
        public static void Run(AppConfig config, LaunchOptions launch = null, bool enterFirstScreen = true)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "[SevenSteps] Bootstrap needs Data/AppConfig.asset: set config on the scene root (unity.sh scene-validate)");
            if (HasRunThisPlay) throw new InvalidOperationException("[SevenSteps] Bootstrap already ran in this Play");
            launch ??= ReadLaunchOptions();
            foreach (var w in launch.Warnings) Debug.LogWarning("[SevenSteps] Launch option " + w);

            Services.Clear(); // a Run that threw halfway left HasRunThisPlay false; start clean

            var store = new PlayerPrefsStore();
            var save = new SaveSystem(store);
            var telemetry = TelemetryRunner.CreateTelemetry(config, launch, save);
            var flow = new AppFlow(new SceneLoader());

            Services.Register(config);
            Services.Register(launch);
            Services.Register<ISaveStore>(store);
            Services.Register(save);
            Services.Register(telemetry);
            Services.Register(flow);
            GameController.FirstEntryPending = true;
            HasRunThisPlay = true;

            if (!enterFirstScreen) return;
            if (launch.DirectToGame) flow.GoToGame(Mathf.Max(0, launch.Level - 1));
            else flow.Go(AppState.Title);
        }
    }
}
