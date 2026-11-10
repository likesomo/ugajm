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

        /// level_clear.stars in the game: levels.json expected.minBodies (no solving at clear time; the export checks it
        /// matches the solver, which is what the golden test's default uses).
        static int LevelsJsonMinBodies(int levelNumber, Core.Level level)
        {
            if (!ReferenceEquals(LevelCatalog.At(levelNumber - 1), level))
                throw new InvalidOperationException($"TelemetryRunner: level {levelNumber} is not the stage in levels.json");
            return LevelCatalog.MinBodies(levelNumber - 1);
        }

        /// Bootstrap: the ITelemetry to register (docs/architecture.md 11.4). Endpoint = ?telemetry=<url> / -telemetry <url>,
        /// else AppConfig.telemetryEndpoint; none, or telemetry=off, gives NullTelemetry. Otherwise an HttpTelemetry driven
        /// by a new runner, enabled by the saved consent (SettingsData.telemetryChoice).
        public static ITelemetry CreateTelemetry(AppConfig config, LaunchOptions launch, SaveSystem save)
        {
            var old = GameObject.Find(ObjectName);
            if (old != null) DestroyImmediate(old); // the jslib calls this object by name: never two at once
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
            var t = new HttpTelemetry(ctx, SaveStores.For(launch), level => save.Progress.CountStart(LevelCatalog.At(level - 1)), LevelsJsonMinBodies);
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<TelemetryRunner>().telemetry = t;
            // lang = the screen language (lead 2026-11-10): Localizer.Current and its Changed event. Until the bootstrap
            // registers the Localizer (scene-flow 8481e5c), the same rule it uses: -lang / ?lang override, else the setting.
            bool localized = Services.TryGet<Localizer>(out var loc);
            t.Tracker.SetLang(Languages.Code(localized ? loc.Current : launch.Language ?? save.Language));
            if (localized) loc.Changed += lang => t.Tracker.SetLang(Languages.Code(lang));
            t.SetEnabled(allowed);
            if (allowed) t.Tracker.SessionStart(firstTime, save.Progress.ClearedCount);

            // Consent changes in Settings apply right away (11.3). Opting in again starts a session with a fresh id.
            save.SettingsChanged += s =>
            {
                if (!localized) t.Tracker.SetLang(Languages.Code(launch.Language ?? save.Language)); // else Localizer.Changed
                bool on = save.TelemetryAllowed(config);
                if (on == t.Enabled) return;
                bool fresh = on && string.IsNullOrEmpty(s.playerId);
                if (fresh) s.playerId = Guid.NewGuid().ToString("D");
                t.Context.PlayerId = s.playerId;
                t.SetEnabled(on);
                if (fresh) save.SaveSettings();
                if (on) t.Tracker.SessionStart(fresh, save.Progress.ClearedCount);
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
        public void OnPageVisible() => telemetry?.OnPageVisible();
    }
}
