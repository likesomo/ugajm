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
        [Tooltip("Opens the language picker (scene-flow S7c); its label says settings.language_change")]
        public Button languageToggle;
        [Tooltip("The current language's own name, small beside the button, in that language's body font (13.4)")]
        public Text languageText;
        [Tooltip("Row label for the language button (settings.language)")]
        public Text languageLabel;
        [Tooltip("Fixed labels worded from the string table (settings.*, 13.2)")]
        public Text titleText, masterLabel, sfxLabel, telemetryLabel;

        SaveSystem save;
        bool telemetryOnByDefault;

        /// +/- on a volume row: the step (+0.1 or -0.1).
        public event Action<float> MasterVolumeStepped, SfxVolumeStepped;
        public event Action TelemetryToggled;
        public event Action LanguageToggled;
        /// Accessibility rows (design/art/ui-minimal.md): "걸음 숫자 표시" and "움직임 줄이기", both off by default.
        public event Action StepNumbersToggled, ReduceMotionToggled;

        /// The accessibility rows, built in code as copies of the play-data row (no prefab change).
        Text stepNumbersLabel, stepNumbersText, reduceMotionLabel, reduceMotionText;
        Button stepNumbersToggle, reduceMotionToggle;
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
            Need(titleText, nameof(titleText)); Need(masterLabel, nameof(masterLabel));
            Need(sfxLabel, nameof(sfxLabel)); Need(telemetryLabel, nameof(telemetryLabel));
            Need(languageToggle, nameof(languageToggle)); Need(languageText, nameof(languageText)); Need(languageLabel, nameof(languageLabel));
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
            // Facilitator line under the consent note (playtest-1-prep E): a copy of the note, one line lower.
            var copy = Instantiate(telemetryNote.gameObject, telemetryNote.transform.parent, false);
            copy.name = "TelemetryStatus";
            telemetryStatus = copy.GetComponent<Text>();
            var r = telemetryStatus.rectTransform;
            r.anchoredPosition -= new Vector2(0, telemetryNote.rectTransform.rect.height + 2);
            telemetryStatus.text = "";
            (stepNumbersLabel, stepNumbersToggle, stepNumbersText) = CopyToggleRow("StepNumbers", () => StepNumbersToggled?.Invoke());
            (reduceMotionLabel, reduceMotionToggle, reduceMotionText) = CopyToggleRow("ReduceMotion", () => ReduceMotionToggled?.Invoke());
            resetButton = CopyButton(close, "ResetProgress", close.transform.parent, () => ShowResetConfirm(true));
            BuildResetConfirm();
            Layout();
            wired = true;
        }

        /// A copy of the play-data row (label + on/off button) with its own click.
        (Text label, Button toggle, Text value) CopyToggleRow(string name, Action click)
        {
            var label = Instantiate(telemetryLabel.gameObject, telemetryLabel.transform.parent, false).GetComponent<Text>();
            label.name = name + "Label";
            var toggle = Instantiate(telemetryToggle.gameObject, telemetryToggle.transform.parent, false).GetComponent<Button>();
            toggle.name = name + "Toggle";
            toggle.onClick = new Button.ButtonClickedEvent(); // the copy must not also toggle play data
            toggle.onClick.AddListener(() => click());
            return (label, toggle, toggle.GetComponentInChildren<Text>(true));
        }

        // ---------- "처음부터 다시" / "Reset progress" (user request 2026-11-14): a button beside Close and an in-panel confirm ----------

        /// The player confirmed the reset (the confirm panel's [처음부터 다시] / [Reset]). The controller erases and leaves.
        public event Action ResetConfirmed;

        Button resetButton, resetYes, resetNo;
        GameObject resetConfirm;
        Text resetMessage;

        /// The confirm panel is up (the settings Esc closes it first: SettingsController.Back is not needed, Hide closes both).
        public bool ResetConfirmOpen => resetConfirm != null && resetConfirm.activeSelf;
        public Button ResetButton => resetButton;
        public Button ResetYes => resetYes;
        public Button ResetNo => resetNo;

        Button CopyButton(Button source, string name, Transform panelParent, Action click)
        {
            var b = Instantiate(source.gameObject, panelParent, false).GetComponent<Button>();
            b.name = name;
            b.onClick = new Button.ButtonClickedEvent(); // the copy must not also close the panel
            b.onClick.AddListener(() => click());
            return b;
        }

        /// A dim over the whole settings panel with a box: the message and [처음부터 다시] [취소]. Built in code from the
        /// panel's own skin (box image, Close button, label), so it is never a browser dialog and follows the theme.
        void BuildResetConfirm()
        {
            var root = new GameObject("ResetConfirm", typeof(RectTransform), typeof(Image));
            var rr = (RectTransform)root.transform;
            rr.SetParent(transform, false);
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.6f);
            dim.raycastTarget = true; // blocks the settings rows under it
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var br = (RectTransform)box.transform;
            br.SetParent(rr, false);
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(600, 300);
            var boxImage = box.GetComponent<Image>();
            if (masterLabel.transform.parent.TryGetComponent<Image>(out var panelImage))
            {
                boxImage.sprite = panelImage.sprite; boxImage.type = panelImage.type; boxImage.color = panelImage.color;
                boxImage.pixelsPerUnitMultiplier = panelImage.pixelsPerUnitMultiplier;
            }
            else boxImage.color = new Color(0.1f, 0.12f, 0.2f, 0.98f);
            boxImage.raycastTarget = true;
            resetMessage = Instantiate(telemetryLabel.gameObject, br, false).GetComponent<Text>();
            resetMessage.name = "Message";
            ((RectTransform)resetMessage.transform).TopLeft(40, 40, 520, 140);
            resetMessage.fontSize = 26;
            resetMessage.alignment = TextAnchor.MiddleCenter;
            resetMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
            resetMessage.verticalOverflow = VerticalWrapMode.Overflow;
            resetMessage.resizeTextForBestFit = false;
            resetMessage.color = Ui.Ink;
            resetYes = CopyButton(close, "ResetYes", br, () => { ShowResetConfirm(false); ResetConfirmed?.Invoke(); });
            resetNo = CopyButton(close, "ResetNo", br, () => ShowResetConfirm(false));
            ((RectTransform)resetYes.transform).TopLeft(52, 204, 240, 64);
            ((RectTransform)resetNo.transform).TopLeft(308, 204, 240, 64);
            foreach (var b in new[] { resetYes, resetNo })
            {
                Ui.FitButtonSkin(b);
                foreach (var t in b.GetComponentsInChildren<Text>(true)) { t.resizeTextForBestFit = false; t.fontSize = 24; }
            }
            root.SetActive(false);
            resetConfirm = root;
        }

        /// Opens (Cancel focused, the safe choice) or closes the confirm panel.
        public void ShowResetConfirm(bool open)
        {
            if (resetConfirm == null) return;
            resetConfirm.transform.SetAsLastSibling();
            resetConfirm.SetActive(open);
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(open ? resetNo.gameObject : resetButton.gameObject);
        }

        /// Row layout in code over the prefab's rects (docs/architecture.md 14): 680x672 panel, six 64-pitch rows with 24-size
        /// text, the consent note and the status line as 18-size secondary text, everything 44 inside the panel edge.
        void Layout()
        {
            if (!(masterLabel.transform.parent is RectTransform panel)) return;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(680, 672);
            void Put(Component c, float x, float y, float w, float h)
            {
                if (c == null) return;
                if (c.transform.parent != panel) c.transform.SetParent(panel, false);
                ((RectTransform)c.transform).TopLeft(x, y, w, h);
            }
            void Size(Text t, int size) { if (t != null) t.fontSize = size; }
            void ButtonText(Button b, int size)
            {
                if (b == null) return;
                Ui.FitButtonSkin(b); // frame border 8 + label padding 8 (gameplay-ui, 14.4)
                foreach (var t in b.GetComponentsInChildren<Text>(true)) { t.resizeTextForBestFit = false; t.fontSize = size; }
            }
            Put(titleText, 44, 36, 592, 56); Size(titleText, 40); titleText.alignment = TextAnchor.MiddleCenter;
            const float Pitch = 64, Row = 62;
            float y = 102;
            foreach (var (label, down, value, up) in new[] { (masterLabel, masterDown, masterText, masterUp), (sfxLabel, sfxDown, sfxText, sfxUp) })
            {
                Put(label, 44, y + 7, 330, 48); Size(label, 24);
                Put(down, 388, y, 64, Row); ButtonText(down, 24);
                Put(value, 456, y + 7, 116, 48); Size(value, 24); value.alignment = TextAnchor.MiddleCenter;
                Put(up, 576, y, 64, Row); ButtonText(up, 24);
                y += Pitch;
            }
            foreach (var (label, toggle) in new[] { (telemetryLabel, telemetryToggle), (stepNumbersLabel, stepNumbersToggle), (reduceMotionLabel, reduceMotionToggle) })
            {
                Put(label, 44, y + 7, 330, 48); Size(label, 24);
                Put(toggle, 388, y, 252, Row); ButtonText(toggle, 24);
                y += Pitch;
            }
            Put(languageLabel, 44, y + 7, 200, 48); Size(languageLabel, 24);
            Put(languageText, 240, y + 7, 140, 48); Size(languageText, 22);
            if (languageText != null) { languageText.alignment = TextAnchor.MiddleRight; languageText.color = Ui.Ink; } // Dim was 3.7:1
            Put(languageToggle, 388, y - 1, 252, Row + 2); ButtonText(languageToggle, 22); // 64: "Change language" needs 8 inside
            y += Row + 6;
            Put(telemetryNote, 44, y, 592, 48); Size(telemetryNote, 18);
            telemetryNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            telemetryNote.alignment = TextAnchor.MiddleCenter;
            Ui.Secondary(telemetryNote);
            Put(telemetryStatus, 44, y + 48, 592, 28); Size(telemetryStatus, 18);
            Ui.Secondary(telemetryStatus);
            // [처음부터 다시] beside [닫기]: 232 + 16 + 232 centered in the 680 panel.
            Put(resetButton, 100, 572, 232, 70); ButtonText(resetButton, 24); // 70: "Reset progress" descenders need 8 inside
            Put(close, 348, 572, 232, 70); ButtonText(close, 24);
        }

        Text telemetryStatus;
        float statusAt;

        /// While open: the sending status once a second (events sent and waiting, last reply, short player id).
        void Update()
        {
            if (localizer == null || save == null || Time.unscaledTime < statusAt) return;
            statusAt = Time.unscaledTime + 1f;
            telemetryStatus.text = StatusLine(localizer, Services.TryGet<ITelemetry>(out var t) ? t as Telemetry.HttpTelemetry : null);
            telemetryStatus.font = Ui.Theme.FontFor(localizer.Current, FontRole.Body);
        }

        /// "Sent 12, waiting 0, last reply 200, ID 3f2a9c1e" or "Not sending" (settings.status*).
        public static string StatusLine(Localizer loc, Telemetry.HttpTelemetry t)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (t == null || !t.Enabled) return loc.Get("settings.status_off");
            string reply = t.LastStatus < 0 ? "-" : t.LastStatus == 0 ? loc.Get("settings.status_no_connection") : t.LastStatus.ToString();
            string id = t.Context.PlayerId ?? "";
            return loc.Format("settings.status", t.SentEvents, t.PendingEvents, reply, id.Length > 8 ? id.Substring(0, 8) : id);
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

        public void Hide()
        {
            if (resetConfirm != null) resetConfirm.SetActive(false); // Esc closes the settings and its confirm together
            gameObject.SetActive(false);
        }

        /// The settings changed (the controller calls this after it saved).
        public void Redraw()
        {
            var s = save.Settings;
            masterText.text = $"{Mathf.RoundToInt(s.masterVolume * 100)}%";
            sfxText.text = $"{Mathf.RoundToInt(s.sfxVolume * 100)}%";
            bool on = save.TelemetryOn(telemetryOnByDefault);
            telemetryText.color = on ? Ui.Gold : Ui.Dim;
            stepNumbersText.color = s.showStepNumbers ? Ui.Gold : Ui.Dim;
            reduceMotionText.color = s.reduceMotion ? Ui.Gold : Ui.Dim;
            if (localizer == null) return; // tests without a language keep the prefab's words
            // Words and role fonts in the current language (settings.*, 13.2 / 13.4); Redraw runs on every switch.
            var body = Ui.Theme.FontFor(localizer.Current, FontRole.Body);
            void Set(Text t, string key) { t.text = localizer.Get(key); t.font = body; }
            Set(titleText, "settings.title");
            titleText.font = Ui.Theme.FontFor(localizer.Current, FontRole.Title);
            Set(masterLabel, "settings.master");
            Set(sfxLabel, "settings.sfx");
            Set(telemetryLabel, "settings.telemetry");
            Set(telemetryNote, "settings.telemetry_note");
            Set(telemetryText, on ? "settings.on" : "settings.off");
            Set(stepNumbersLabel, "settings.step_numbers");
            Set(reduceMotionLabel, "settings.reduce_motion");
            Set(stepNumbersText, s.showStepNumbers ? "settings.on" : "settings.off");
            Set(reduceMotionText, s.reduceMotion ? "settings.on" : "settings.off");
            Set(close.GetComponentInChildren<Text>(true), "settings.close");
            Set(resetButton.GetComponentInChildren<Text>(true), "settings.reset");
            Set(resetMessage, "settings.reset_confirm");
            Set(resetYes.GetComponentInChildren<Text>(true), "settings.reset_yes");
            Set(resetNo.GetComponentInChildren<Text>(true), "settings.reset_no");
            if (languageLabel != null) Set(languageLabel, "settings.language");
            // The button opens the language picker (scene-flow S7c): it says what it does, the current language sits beside it.
            if (languageToggle != null) Set(languageToggle.GetComponentInChildren<Text>(true), "settings.language_change");
            if (languageText != null)
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
            view.LanguageToggled += OnLanguageButton;
            view.StepNumbersToggled += ToggleStepNumbers;
            view.ReduceMotionToggled += ToggleReduceMotion;
            view.ResetConfirmed += ResetProgress;
        }

        /// The settings language button opens the language picker (lead decision 2026-11-10) when the scene root sets this;
        /// without it the button switches to the next language (ToggleLanguage).
        public Action OpenLanguage { get; set; }

        /// After a confirmed reset (save erased and flushed, ClearNews emptied): the scene root closes its overlays and
        /// returns to the title, so New Game plays prologue → T1..T3 → stage 1 as on a fresh install.
        public Action AfterReset { get; set; }

        /// The confirm panel's [처음부터 다시]: progress, guides and story erased, settings kept (SaveSystem.ResetProgress).
        public void ResetProgress()
        {
            save.ResetProgress();
            if (Services.TryGet<ClearNews>(out var news)) news.Clear(); // its notes no longer match the save (review M2)
            Debug.Log("[SevenSteps] Progress reset from settings");
            AfterReset?.Invoke();
        }

        void OnLanguageButton()
        {
            if (OpenLanguage != null) OpenLanguage();
            else ToggleLanguage();
        }

        public void Dispose()
        {
            view.MasterVolumeStepped -= StepMaster;
            view.SfxVolumeStepped -= StepSfx;
            view.TelemetryToggled -= ToggleTelemetry;
            view.LanguageToggled -= OnLanguageButton;
            view.StepNumbersToggled -= ToggleStepNumbers;
            view.ReduceMotionToggled -= ToggleReduceMotion;
            view.ResetConfirmed -= ResetProgress;
        }

        void StepMaster(float delta) { save.SetMasterVolume(save.Settings.masterVolume + Step(delta)); Saved(); }
        void StepSfx(float delta) { save.SetSfxVolume(save.Settings.sfxVolume + Step(delta)); Saved(); }
        void ToggleTelemetry() { save.SetTelemetryChoice(!save.TelemetryOn(telemetryOnByDefault)); Saved(); }
        /// Accessibility (ui-minimal): saved at once; SettingsChanged tells the HUD and the star trail (gameplay-ui).
        void ToggleStepNumbers() { save.SetShowStepNumbers(!save.Settings.showStepNumbers); Saved(); }
        void ToggleReduceMotion() { save.SetReduceMotion(!save.Settings.reduceMotion); Saved(); }

        /// Next language that has a table (Ko -> En -> Ko). Saved (SetLanguage) and applied now (Localizer.Changed redraws
        /// the views and updates telemetry lang); a -lang launch override is replaced by the player's choice.
        public void ToggleLanguage()
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
