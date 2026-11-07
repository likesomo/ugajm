using System;
using SevenSteps.Contracts;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// The one DontDestroyOnLoad object (docs/architecture.md 8, 11.5). Drives <see cref="HttpTelemetry"/> every frame,
    /// flushes on pause/quit, and receives the WebGL page events from SevenStepsTelemetry.jslib:
    /// SendMessage("TelemetryRunner", "OnPageHidden", "hidden"|"unload") and SendMessage("TelemetryRunner", "OnPageVisible").
    /// The object name is fixed because the jslib calls it by name.
    /// </summary>
    public sealed class TelemetryRunner : MonoBehaviour
    {
        public const string ObjectName = "TelemetryRunner";

        HttpTelemetry telemetry;
        float saveAt;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SevenSteps_RegisterVisibility(string objectName);
#endif

        /// Bootstrap: the ITelemetry to register. NullTelemetry when the endpoint is empty or ?telemetry=0 / -telemetry 0;
        /// otherwise an HttpTelemetry driven by a new runner, enabled by the saved consent.
        public static ITelemetry CreateTelemetry(AppConfig config, LaunchOptions launch, SaveSystem save)
        {
            var old = GameObject.Find(ObjectName);
            if (old != null) Destroy(old);
            if (config == null || string.IsNullOrEmpty(config.telemetryEndpoint) || launch.TelemetryOff) return new NullTelemetry();

            bool firstTime = string.IsNullOrEmpty(save.Settings.playerId);
            bool allowed = save.TelemetryAllowed(config);
            if (allowed && firstTime)
            {
                save.Settings.playerId = Guid.NewGuid().ToString("D");
                save.SaveSettings();
            }

            var t = new HttpTelemetry(config, new PlayerPrefsStore(), save.Settings.playerId);
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<TelemetryRunner>();
            runner.telemetry = t;
            t.SetEnabled(allowed);
            if (allowed) t.Tracker.SessionStart(firstTime, save.Progress.cleared.Count);

            // Consent changes in Settings apply right away (11.3). Opting in later starts a session with a fresh id.
            save.SettingsChanged += s =>
            {
                bool on = save.TelemetryAllowed(config);
                if (on == t.Enabled) return;
                if (on && string.IsNullOrEmpty(s.playerId))
                {
                    s.playerId = Guid.NewGuid().ToString("D");
                    t.Context.PlayerId = s.playerId;
                    t.SetEnabled(true);
                    save.SaveSettings();
                    t.Tracker.SessionStart(true, save.Progress.cleared.Count);
                    return;
                }
                t.SetEnabled(on);
                if (on) { t.Context.PlayerId = s.playerId; t.Tracker.SessionStart(false, save.Progress.cleared.Count); }
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            SevenSteps_RegisterVisibility(ObjectName);
#endif
            return t;
        }

        void Update()
        {
            if (telemetry == null) return;
            float now = Time.unscaledTime;
            telemetry.Tick(now);
            if (now >= saveAt) { saveAt = now + 5f; telemetry.SaveQueue(false); }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && telemetry != null) { telemetry.Flush(); telemetry.SaveQueue(true); }
        }

        void OnApplicationQuit() => telemetry?.OnQuit();

        // Called from SevenStepsTelemetry.jslib.
        public void OnPageHidden(string reason) => telemetry?.OnPageHidden(string.IsNullOrEmpty(reason) ? "hidden" : reason);
        public void OnPageVisible() => telemetry?.OnPageVisible(0);
    }
}
