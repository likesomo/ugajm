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

    /// <summary>
    /// Every key of <paramref name="inner"/> with "#suffix" appended (LaunchOptions.SaveKey, development / smoke only): runs
    /// with different suffixes keep separate progress, settings and telemetry queue in the same PlayerPrefs.
    /// </summary>
    public sealed class SuffixedStore : ISaveStore
    {
        readonly ISaveStore inner;
        readonly string suffix;
        public SuffixedStore(ISaveStore inner, string suffix)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (!SaveStores.IsValidSuffix(suffix)) throw new ArgumentException("1-32 of A-Z a-z 0-9 _ -", nameof(suffix));
            this.suffix = "#" + suffix;
        }
        public string Get(string key, string fallback = "") => inner.Get(key + suffix, fallback);
        public void Set(string key, string value) => inner.Set(key + suffix, value);
        public void Delete(string key) => inner.Delete(key + suffix);
        public void Flush() => inner.Flush();
    }

    /// The store a run uses: PlayerPrefs, with LaunchOptions.SaveKey as a key suffix when it is set. Every ISaveStore of a
    /// run (SaveSystem, telemetry queue) comes from here so they agree.
    public static class SaveStores
    {
        public static ISaveStore For(LaunchOptions launch) =>
            launch == null || string.IsNullOrEmpty(launch.SaveKey) ? new PlayerPrefsStore() : (ISaveStore)new SuffixedStore(new PlayerPrefsStore(), launch.SaveKey);

        public static bool IsValidSuffix(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 32) return false;
            foreach (char c in s)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            return true;
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
        /// The player picked the language (first-run picker or the settings toggle), not only the system default (13.3).
        /// Saves from before this field read as false: the picker shows once for them too.
        public bool languageChosen;
        /// Accessibility (11.3.2): show the step count as digits (numbered footprints, steps left). Default off: the seven
        /// foot stars carry it. Saves from before this field read as false.
        public bool showStepNumbers;
        /// Accessibility (11.3.2): no pulses, awaken / finale rays or long slides; a double outline or the end picture
        /// instead. Default off.
        public bool reduceMotion;
    }

    /// <summary>
    /// Progress and settings as JSON in two PlayerPrefs keys. Both survive a missing or broken value (defaults).
    /// Progress v0 (key "seven-steps-cleared") and v1 (stage numbers) are migrated to v2 (stage names) on load and saved
    /// at once; the original is kept under <see cref="ProgressBackupKey"/> (docs/architecture.md 11.3.1). A progress
    /// save from a newer build is not overwritten: this run starts from defaults and does not save progress.
    /// </summary>
    public sealed class SaveSystem
    {
        public const string ProgressKey = "seven-steps.progress";
        public const string SettingsKey = "seven-steps.settings";
        public const string ProgressBackupKey = "seven-steps.progress.v1";
        /// One-time guides and the KEEP / SWITCH confirm count (TutorialData, 11.3.2).
        public const string TutorialKey = "seven-steps.tutorial";
        /// Story lines shown, ending / carvings / final truth seen (StoryProgress, docs/architecture.md 16.5).
        public const string StoryKey = "seven-steps.story";
        const string LegacyClearedKey = "seven-steps-cleared";

        readonly ISaveStore store;
        /// The stored progress is from a newer build: keep it, do not write over it.
        bool progressReadOnly; // set on load; StartNewPlayer clears it
        /// Same for the tutorial data.
        bool tutorialReadOnly;
        /// Same for the story data.
        bool storyReadOnly;

        public ProgressData Progress { get; private set; }
        public SettingsData Settings { get; private set; }
        public TutorialData Tutorial { get; private set; }
        /// Story presentation memory (16.5). Change it through its Mark methods, then call SaveStory at the save points.
        public StoryProgress Story { get; private set; }

        /// Raised after SaveSettings, so audio/telemetry can react to volume or opt-out changes.
        public event Action<SettingsData> SettingsChanged;

        public SaveSystem(ISaveStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Progress = LoadProgress(out progressReadOnly);
            Tutorial = LoadTutorial(out tutorialReadOnly);
            Story = LoadStory(out storyReadOnly);
            Settings =Read<SettingsData>(SettingsKey);
            if (Settings == null)
            {
                // First run (13.3): the UI language follows the system (Korean → ko, otherwise en).
                Settings = new SettingsData { language = Languages.Code(Languages.FromSystem(Application.systemLanguage)) };
            }
        }

        public void SaveProgress()
        {
            if (progressReadOnly) return; // a newer build's save stays as it is (warned on load)
            store.Set(ProgressKey, JsonUtility.ToJson(Progress));
            store.Flush();
        }

        /// Playtest facilitator: the next tester starts fresh (-newplayer / ?newplayer=1, LaunchOptions.NewPlayer).
        /// Empty progress (cleared stages, best bodies and stars, attempt counts) and no playerId (telemetry makes a new
        /// one), no finished guides (TutorialData: the next tester sees them again) and no story memory (StoryProgress: the
        /// next tester does not start on the after-ending branch); volumes, consent, language and the accessibility options
        /// stay. Overrides a newer build's read-only progress / tutorial / story data: this is deliberate.
        public void StartNewPlayer()
        {
            Progress = new ProgressData();
            progressReadOnly = false;
            SaveProgress();
            Tutorial = new TutorialData();
            tutorialReadOnly = false;
            SaveTutorial();
            Story = new StoryProgress();
            storyReadOnly = false;
            SaveStory();
            Settings.playerId = "";
            SaveSettings();
        }

        // ---------- One-time guides (11.3.2). Counted only from applied commands (SessionUpdate Present), never from Snap,
        // Rebuild or Replay; language change, Undo and Restart leave them as they are. Not part of any replay / QA hash. ----------

        /// This guide (TutorialIds, "name@version") was finished.
        public bool TutorialSeen(string id) => Tutorial.IsSeen(id);

        /// The guide was finished (its first successful use). Saves when it changed. Invalid id: ArgumentException.
        public void MarkTutorialSeen(string id)
        {
            if (Tutorial.MarkSeen(id)) SaveTutorial();
        }

        /// Confirmed KEEP / SWITCH choices so far, 0..TutorialData.ChoiceExplainCount.
        public int ChoiceConfirms => Tutorial.choiceConfirms;

        /// The choice cards still explain both outcomes (fewer than TutorialData.ChoiceExplainCount confirmed choices).
        public bool ExplainChoice => Tutorial.ExplainChoice;

        /// A KEEP / SWITCH was applied (SessionUpdate Present with a Keep or Switch command). Saves while under the cap.
        public void CountChoiceConfirm()
        {
            if (Tutorial.CountChoice()) SaveTutorial();
        }

        void SaveTutorial()
        {
            if (tutorialReadOnly) return;
            store.Set(TutorialKey, JsonUtility.ToJson(Tutorial));
            store.Flush();
        }

        TutorialData LoadTutorial(out bool readOnly)
        {
            readOnly = false;
            var t = Read<TutorialData>(TutorialKey);
            if (t == null) return new TutorialData();
            if (t.version > TutorialData.Version)
            {
                Debug.LogWarning($"[SevenSteps] Tutorial save version {t.version} is newer than this build ({TutorialData.Version}): not loaded, not overwritten");
                readOnly = true;
                return new TutorialData();
            }
            if (t.seen == null) t.seen = new System.Collections.Generic.List<string>();
            t.choiceConfirms = Mathf.Clamp(t.choiceConfirms, 0, TutorialData.ChoiceExplainCount);
            return t;
        }

        // ---------- Story (16.5): Mark* on Story change memory only; write at the save points ----------

        /// Write the story memory: after the result panel opens, at the end of the ending, after a star-map carving
        /// appears, and on app quit. Not per line (WebGL: every save is an IndexedDB write).
        public void SaveStory()
        {
            if (storyReadOnly) return;
            store.Set(StoryKey, JsonUtility.ToJson(Story));
            store.Flush();
        }

        StoryProgress LoadStory(out bool readOnly)
        {
            readOnly = false;
            var s = Read<StoryProgress>(StoryKey);
            if (s == null)
            {
                if (!string.IsNullOrEmpty(store.Get(StoryKey))) Debug.LogWarning("[SevenSteps] Story save is not valid JSON: starting from nothing seen");
                return new StoryProgress();
            }
            if (s.version > StoryProgress.Version)
            {
                Debug.LogWarning($"[SevenSteps] Story save version {s.version} is newer than this build ({StoryProgress.Version}): not loaded, not overwritten");
                readOnly = true;
                return new StoryProgress();
            }
            s.Repair();
            return s;
        }

        /// Accessibility: step count as digits (11.3.2). Saves and raises SettingsChanged.
        public void SetShowStepNumbers(bool on) { Settings.showStepNumbers = on; SaveSettings(); }

        /// Accessibility: reduce motion (11.3.2). Saves and raises SettingsChanged.
        public void SetReduceMotion(bool on) { Settings.reduceMotion = on; SaveSettings(); }

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

        /// UI language from the settings. A value this build does not know (newer save, hand edit) reads as the base language.
        public Language Language => Languages.TryParse(Settings.language, out var lang) ? lang : Languages.Base;

        /// The player has picked a language (13.3). False on a first run and for older saves: Title shows the language
        /// picker first, unless this run has a -lang / ?lang= override (LaunchOptions.Language).
        public bool LanguageChosen => Settings.languageChosen;

        /// The player's language choice (first-run picker or settings toggle, 13.3): sets <see cref="LanguageChosen"/>,
        /// saves and raises SettingsChanged (views redraw through the Localizer). The system default never goes through here.
        public void SetLanguage(Language language)
        {
            Settings.language = Languages.Code(language);
            Settings.languageChosen = true;
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

        [Serializable]
        sealed class VersionProbe { public int version; }

        ProgressData LoadProgress(out bool readOnly)
        {
            readOnly = false;
            string json = store.Get(ProgressKey);
            if (string.IsNullOrEmpty(json))
            {
                string v0 = store.Get(LegacyClearedKey);
                if (string.IsNullOrEmpty(v0)) return new ProgressData();
                return Migrated(ProgressMigration.FromV0(v0), v0);
            }
            VersionProbe probe;
            try { probe = JsonUtility.FromJson<VersionProbe>(json); }
            catch (ArgumentException) { probe = null; }
            if (probe == null)
            {
                Debug.LogWarning("[SevenSteps] Progress save is not valid JSON: starting from no progress");
                return new ProgressData();
            }
            if (probe.version == ProgressData.Version) return Read<ProgressData>(ProgressKey) ?? new ProgressData();
            if (probe.version > ProgressData.Version)
            {
                Debug.LogWarning($"[SevenSteps] Progress save version {probe.version} is newer than this build ({ProgressData.Version}): not loaded, not overwritten");
                readOnly = true;
                return new ProgressData();
            }
            var v1 = Read<ProgressMigration.V1>(ProgressKey);
            return v1 == null ? new ProgressData() : Migrated(ProgressMigration.FromV1(v1), json);
        }

        /// Keep the original, write v2 now.
        ProgressData Migrated(ProgressData p, string original)
        {
            store.Set(ProgressBackupKey, original);
            store.Set(ProgressKey, JsonUtility.ToJson(p));
            store.Flush();
            return p;
        }
    }
}
