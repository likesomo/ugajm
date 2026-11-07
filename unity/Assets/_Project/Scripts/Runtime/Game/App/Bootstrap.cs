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
        public static LaunchOptions ReadLaunchOptions() =>
            LaunchOptions.Parse(Environment.GetCommandLineArgs(), Application.absoluteURL);

        /// <param name="enterFirstScreen">Boot scene: go to Game (?level=) or Title now. Direct scene play: false, the scene enters itself.</param>
        public static void Run(AppConfig config, LaunchOptions launch = null, bool enterFirstScreen = true)
        {
            if (config == null)
            {
                Debug.LogWarning("[SevenSteps] Bootstrap without AppConfig: using defaults (telemetry off).");
                config = ScriptableObject.CreateInstance<AppConfig>();
            }
            launch ??= ReadLaunchOptions();

            Services.Clear();
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

            if (!enterFirstScreen) return;
            if (launch.DirectToGame) flow.GoToGame(Mathf.Max(0, launch.Level - 1));
            else flow.Go(AppState.Title);
        }
    }
}
