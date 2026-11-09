using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Settings overlay shared by the Title and Game scenes (view only, docs/scene-flow.md 4.7, S4, S6): master/sfx volume and
    /// the play-data (telemetry) consent. Prefab UI/Prefabs/SettingsPanel.prefab (referenced by UiTheme.settingsPanel), placed
    /// under an overlay canvas by the scene root and given the SaveSystem with <see cref="Init"/>. Reads
    /// <see cref="SaveSystem.Settings"/> to draw and raises events; the <see cref="SettingsController"/> changes and saves.
    /// Opened and closed through the OverlayStack.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour, IOverlay
    {
        public const float VolumeStep = 0.1f;

        public Text masterText, sfxText, telemetryText, telemetryNote;
        public Button masterDown, masterUp, sfxDown, sfxUp, telemetryToggle, close;
        [Tooltip("Optional until the prefab has it: language toggle (docs/architecture.md 13.3)")]
        public Button languageToggle;
        [Tooltip("Optional: the current language's own name, drawn in that language's body font (13.4)")]
        public Text languageText;

        SaveSystem save;
        bool telemetryOnByDefault;

        /// +/- on a volume row: the step (+0.1 or -0.1).
        public event Action<float> MasterVolumeStepped, SfxVolumeStepped;
        public event Action TelemetryToggled;
        public event Action LanguageToggled;
        public event Action CloseRequested;

        /// Required references missing in the prefab, one line each (OnValidate, Awake, SceneFlowValidator).
        public void Validate(List<string> issues)
        {
            void Need(Object o, string field) { if (o == null) issues.Add($"SettingsPanel {name}: {field} is not set"); }
            Need(masterText, nameof(masterText)); Need(sfxText, nameof(sfxText));
            Need(telemetryText, nameof(telemetryText)); Need(telemetryNote, nameof(telemetryNote));
            Need(masterDown, nameof(masterDown)); Need(masterUp, nameof(masterUp));
            Need(sfxDown, nameof(sfxDown)); Need(sfxUp, nameof(sfxUp));
            Need(telemetryToggle, nameof(telemetryToggle)); Need(close, nameof(close));
        }

        void OnValidate()
        {
            var issues = new List<string>();
            Validate(issues);
            foreach (var i in issues) Debug.LogError("[SevenSteps] " + i, this);
        }

        bool wired;

        void Awake() => Wire();

        /// Checks the references and connects the buttons once (also from Init: the prefab root is saved inactive, and
        /// Awake does not run in edit-mode tests).
        void Wire()
        {
            if (wired) return;
            var issues = new List<string>();
            Validate(issues);
            if (issues.Count > 0) throw new MissingReferenceException("[SevenSteps] " + string.Join("; ", issues));
            masterDown.onClick.AddListener(() => MasterVolumeStepped?.Invoke(-VolumeStep));
            masterUp.onClick.AddListener(() => MasterVolumeStepped?.Invoke(VolumeStep));
            sfxDown.onClick.AddListener(() => SfxVolumeStepped?.Invoke(-VolumeStep));
            sfxUp.onClick.AddListener(() => SfxVolumeStepped?.Invoke(VolumeStep));
            telemetryToggle.onClick.AddListener(() => TelemetryToggled?.Invoke());
            if (languageToggle != null) languageToggle.onClick.AddListener(() => LanguageToggled?.Invoke());
            close.onClick.AddListener(() => CloseRequested?.Invoke());
            wired = true;
        }

        /// Called once by the scene root right after Instantiate. <paramref name="telemetryOnByDefault"/> = AppConfig.telemetryOnByDefault.
        Localizer localizer;

        /// <param name="localizer">Current UI language for the language toggle label (null in tests without one).</param>
        public void Init(SaveSystem save, bool telemetryOnByDefault, Localizer localizer = null)
        {
            if (this.save != null) throw new InvalidOperationException("SettingsPanel.Init twice");
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.telemetryOnByDefault = telemetryOnByDefault;
            this.localizer = localizer;
            Wire();
        }

        public void Show()
        {
            if (save == null) throw new InvalidOperationException("SettingsPanel.Show before Init");
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Redraw();
        }

        public void Hide() => gameObject.SetActive(false);

        /// The settings changed (the controller calls this after it saved).
        public void Redraw()
        {
            var s = save.Settings;
            masterText.text = $"{Mathf.RoundToInt(s.masterVolume * 100)}%";
            sfxText.text = $"{Mathf.RoundToInt(s.sfxVolume * 100)}%";
            bool on = save.TelemetryOn(telemetryOnByDefault);
            telemetryText.text = on ? "켜짐" : "꺼짐";
            telemetryText.color = on ? Ui.Gold : Ui.Dim;
            if (languageText != null && localizer != null)
            {
                // The language's own name in its own body font, whatever the UI language is (13.4).
                languageText.text = NativeName(localizer.Current);
                languageText.font = Ui.Theme.FontFor(localizer.Current, FontRole.Body);
            }
        }

        /// Places the theme's settings prefab under <paramref name="parent"/> (an overlay canvas) and initializes it.
        /// A language's name in that language (not a table string: it must read the same in every UI language).
        public static string NativeName(Language lang) => lang switch
        {
            Language.Ko => "한국어",
            Language.En => "English",
            _ => throw new ArgumentOutOfRangeException(nameof(lang), lang, "add its native name"),
        };

        public static SettingsPanel Create(Transform parent, SaveSystem save, bool telemetryOnByDefault, Localizer localizer = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var prefab = Ui.Theme.settingsPanel != null ? Ui.Theme.settingsPanel
                : throw new MissingReferenceException($"[SevenSteps] UiTheme {Ui.Theme.name}: settingsPanel is not set (Seven Steps > Scene Flow > Build UI)");
            var panel = Instantiate(prefab, parent, false);
            panel.name = prefab.name;
            panel.Init(save, telemetryOnByDefault, localizer);
            return panel;
        }
    }

    /// <summary>
    /// Settings panel controller (docs/scene-flow.md 4.7, S4): turns panel events into SaveSystem commands (SetMasterVolume,
    /// SetSfxVolume, SetTelemetryChoice, docs/architecture.md 11.3), which save at once, so the value is kept however the
    /// panel closes (Esc, button, scene change). Saving raises SettingsChanged: audio and telemetry react.
    /// </summary>
    public sealed class SettingsController : IDisposable
    {
        readonly SaveSystem save;
        readonly SettingsPanel view;
        readonly bool telemetryOnByDefault;

        readonly Localizer localizer;

        /// <param name="localizer">The run's UI language (Services); the toggle saves the choice and switches it at once.</param>
        public SettingsController(SaveSystem save, SettingsPanel view, bool telemetryOnByDefault, Localizer localizer)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.telemetryOnByDefault = telemetryOnByDefault;
            this.localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            view.MasterVolumeStepped += StepMaster;
            view.SfxVolumeStepped += StepSfx;
            view.TelemetryToggled += ToggleTelemetry;
            view.LanguageToggled += ToggleLanguage;
        }

        public void Dispose()
        {
            view.MasterVolumeStepped -= StepMaster;
            view.SfxVolumeStepped -= StepSfx;
            view.TelemetryToggled -= ToggleTelemetry;
            view.LanguageToggled -= ToggleLanguage;
        }

        void StepMaster(float delta) { save.SetMasterVolume(save.Settings.masterVolume + Step(delta)); Saved(); }
        void StepSfx(float delta) { save.SetSfxVolume(save.Settings.sfxVolume + Step(delta)); Saved(); }
        void ToggleTelemetry() { save.SetTelemetryChoice(!save.TelemetryOn(telemetryOnByDefault)); Saved(); }

        /// Next language that has a table (Ko -> En -> Ko). Saved (SetLanguage) and applied now (Localizer.Changed redraws
        /// the views and updates telemetry lang); a -lang launch override is replaced by the player's choice.
        void ToggleLanguage()
        {
            var next = NextLanguage(localizer.Current);
            save.SetLanguage(next);
            localizer.Set(next);
            Saved();
        }

        Language NextLanguage(Language current)
        {
            var all = Languages.All;
            int at = Array.IndexOf(all, current);
            for (int k = 1; k <= all.Length; k++)
            {
                var lang = all[(at + k) % all.Length];
                if (localizer.Has(lang)) return lang;
            }
            return current;
        }

        static float Step(float delta)
        {
            if (Math.Abs(Math.Abs(delta) - SettingsPanel.VolumeStep) > 1e-4f) throw new ArgumentOutOfRangeException(nameof(delta), delta, "one volume step");
            return delta;
        }

        /// The SaveSystem command already saved and raised SettingsChanged (audio and telemetry react).
        void Saved()
        {
            GameAudio.ApplyMaster(save.Settings); // live preview in scenes without a GameAudio instance (Title)
            view.Redraw();
        }
    }
}
