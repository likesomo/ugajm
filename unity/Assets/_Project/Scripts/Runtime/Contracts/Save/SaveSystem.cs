using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>String key-value storage. PlayerPrefs on every platform (WebGL: IndexedDB behind PlayerPrefs). Memory in tests.</summary>
    public interface ISaveStore
    {
        string Get(string key, string fallback = "");
        void Set(string key, string value);
        void Delete(string key);
        /// Write to disk now (call after a meaningful change, not every frame).
        void Flush();
    }

    public sealed class PlayerPrefsStore : ISaveStore
    {
        public string Get(string key, string fallback = "") => PlayerPrefs.GetString(key, fallback);
        public void Set(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Delete(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }

    public sealed class MemoryStore : ISaveStore
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public string Get(string key, string fallback = "") => Data.TryGetValue(key, out var v) ? v : fallback;
        public void Set(string key, string value) => Data[key] = value;
        public void Delete(string key) => Data.Remove(key);
        public void Flush() { }
    }

    /// Stage progress. Index = 0-based stage in levels.json order.
    [Serializable]
    public sealed class ProgressData
    {
        public int version = 1;
        public List<int> cleared = new List<int>();
        /// Fewest bodies used to clear each stage; parallel to bestStage.
        public List<int> bestStage = new List<int>();
        public List<int> bestBodies = new List<int>();
        public int lastStage;
        /// How many times each stage was started on this device (level_start.attempt). Index = stage.
        public List<int> starts = new List<int>();

        /// Count one more start and return the attempt number (1 = first).
        public int CountStart(int stage)
        {
            while (starts.Count <= stage) starts.Add(0);
            return ++starts[stage];
        }

        public bool IsCleared(int stage) => cleared.Contains(stage);

        public void MarkCleared(int stage, int bodiesUsed)
        {
            if (!cleared.Contains(stage)) { cleared.Add(stage); cleared.Sort(); }
            int k = bestStage.IndexOf(stage);
            if (k < 0) { bestStage.Add(stage); bestBodies.Add(bodiesUsed); }
            else if (bodiesUsed < bestBodies[k]) bestBodies[k] = bodiesUsed;
        }
    }

    /// User settings.
    [Serializable]
    public sealed class SettingsData
    {
        public int version = 1;
        [Range(0, 1)] public float masterVolume = 1f;
        [Range(0, 1)] public float sfxVolume = 1f;
        [Range(0, 1)] public float musicVolume = 0.8f;
        /// 0 = not asked yet (use AppConfig.telemetryOnByDefault), 1 = on, 2 = off (opt-out).
        public int telemetryChoice;
        /// Anonymous UUID v4 for telemetry (playerId). Created on first run, cleared when the player opts out.
        public string playerId = "";
        public string language = "ko";
    }

    /// <summary>
    /// Progress and settings as JSON in two PlayerPrefs keys. Both survive a missing or broken value (defaults).
    /// v0 save key "seven-steps-cleared" (comma list) is migrated on first load.
    /// </summary>
    public sealed class SaveSystem
    {
        public const string ProgressKey = "seven-steps.progress";
        public const string SettingsKey = "seven-steps.settings";
        const string LegacyClearedKey = "seven-steps-cleared";

        readonly ISaveStore store;

        public ProgressData Progress { get; private set; }
        public SettingsData Settings { get; private set; }

        /// Raised after SaveSettings, so audio/telemetry can react to volume or opt-out changes.
        public event Action<SettingsData> SettingsChanged;

        public SaveSystem(ISaveStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Progress = Read<ProgressData>(ProgressKey) ?? MigrateLegacy();
            Settings = Read<SettingsData>(SettingsKey) ?? new SettingsData();
        }

        public void SaveProgress() { store.Set(ProgressKey, JsonUtility.ToJson(Progress)); store.Flush(); }

        public void SaveSettings()
        {
            store.Set(SettingsKey, JsonUtility.ToJson(Settings));
            store.Flush();
            SettingsChanged?.Invoke(Settings);
        }

        // ---------- Settings commands (docs/architecture.md 11.3): change one value and save at once ----------

        /// Master volume 0..1, rounded to 0.1 and clamped (a + past 1 stays 1). Saves and raises SettingsChanged.
        public void SetMasterVolume(float volume) { Settings.masterVolume = Volume(volume); SaveSettings(); }

        /// Sound-effect volume 0..1, rounded to 0.1 and clamped. Saves and raises SettingsChanged.
        public void SetSfxVolume(float volume) { Settings.sfxVolume = Volume(volume); SaveSettings(); }

        /// Play-data consent from the settings panel: on = 1, off = 2 and the anonymous playerId is forgotten (11.3).
        /// A new id is made when telemetry starts again. Saves and raises SettingsChanged.
        public void SetTelemetryChoice(bool on)
        {
            Settings.telemetryChoice = on ? 1 : 2;
            if (!on) Settings.playerId = "";
            SaveSettings();
        }

        /// What the consent toggle shows: the explicit choice, or the build default when none was made (telemetryChoice 0).
        public bool TelemetryOn(bool onByDefault) =>
            Settings.telemetryChoice == 1 || (Settings.telemetryChoice == 0 && onByDefault);

        static float Volume(float v)
        {
            if (float.IsNaN(v)) throw new ArgumentException("volume is NaN", nameof(v));
            return Mathf.Clamp01(Mathf.Round(v * 10f) / 10f);
        }

        public bool TelemetryAllowed(AppConfig config) =>
            Settings.telemetryChoice == 1 || (Settings.telemetryChoice == 0 && config != null && config.telemetryOnByDefault);

        T Read<T>(string key) where T : class
        {
            string json = store.Get(key);
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }

        ProgressData MigrateLegacy()
        {
            var p = new ProgressData();
            foreach (var part in store.Get(LegacyClearedKey).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out int k) && !p.cleared.Contains(k)) p.cleared.Add(k);
            p.cleared.Sort();
            return p;
        }
    }
}
