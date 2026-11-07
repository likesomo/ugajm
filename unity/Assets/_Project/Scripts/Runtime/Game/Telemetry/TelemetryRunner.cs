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
        [DllImport("__Internal")] static extern void TelemetryRegisterVisibility(string objectName);
#endif

        /// Bootstrap: the ITelemetry to register (docs/architecture.md 11.4). Endpoint = ?telemetry=<url> / -telemetry <url>,
        /// else AppConfig.telemetryEndpoint; none, or telemetry=off, gives NullTelemetry. Otherwise an HttpTelemetry driven
        /// by a new runner, enabled by the saved consent (SettingsData.telemetryChoice).
        public static ITelemetry CreateTelemetry(AppConfig config, LaunchOptions launch, SaveSystem save)
        {
            var old = GameObject.Find(ObjectName);
            if (old != null) Destroy(old);
            string endpoint = !string.IsNullOrEmpty(launch.TelemetryUrl) ? launch.TelemetryUrl : config != null ? config.telemetryEndpoint : "";
            if (string.IsNullOrEmpty(endpoint) || launch.TelemetryOff) return new NullTelemetry();

            bool firstTime = string.IsNullOrEmpty(save.Settings.playerId);
            bool allowed = save.TelemetryAllowed(config);
            if (allowed && firstTime)
            {
                save.Settings.playerId = Guid.NewGuid().ToString("D");
                save.SaveSettings();
            }

            string build = Application.version + (Application.isEditor ? "+editor" : "");
            var ctx = new TelemetryContext
            {
                Endpoint = endpoint,
                PlayerId = save.Settings.playerId,
                SessionId = Guid.NewGuid().ToString("D"),
#if UNITY_WEBGL && !UNITY_EDITOR
                Client = TelemetryContext.ClientWebGL,
#else
                Client = TelemetryContext.ClientDesktop,
#endif
                Build = build.Length > 40 ? build.Substring(0, 40) : build,
            };
            var t = new HttpTelemetry(ctx, new PlayerPrefsStore(), level => save.Progress.CountStart(level - 1));
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<TelemetryRunner>().telemetry = t;
            t.SetEnabled(allowed);
            if (allowed) t.Tracker.SessionStart(firstTime, save.Progress.cleared.Count);

            // Consent changes in Settings apply right away (11.3). Opting in again starts a session with a fresh id.
            save.SettingsChanged += s =>
            {
                bool on = save.TelemetryAllowed(config);
                if (on == t.Enabled) return;
                bool fresh = on && string.IsNullOrEmpty(s.playerId);
                if (fresh) s.playerId = Guid.NewGuid().ToString("D");
                t.Context.PlayerId = s.playerId;
                t.SetEnabled(on);
                if (fresh) save.SaveSettings();
                if (on) t.Tracker.SessionStart(fresh, save.Progress.cleared.Count);
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            TelemetryRegisterVisibility(ObjectName);
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
        public void OnPageVisible() => telemetry?.OnPageVisible();
    }
}
