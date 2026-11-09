using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Settings overlay shared by the Title and Game scenes (view only, docs/scene-flow.md 4.7, S4): master/sfx volume and the
    /// play-data (telemetry) consent. Reads <see cref="SaveSystem.Settings"/> to draw and raises events; the
    /// <see cref="SettingsController"/> changes and saves the values. Opened and closed through the OverlayStack.
    /// </summary>
    public sealed class SettingsPanel : IOverlay
    {
        public const float VolumeStep = 0.1f;

        readonly SaveSystem save;
        readonly bool telemetryOnByDefault;
        readonly GameObject root;
        readonly Text masterText, sfxText, telemetryText, telemetryNote;

        /// +/- on a volume row: the step (+0.1 or -0.1).
        public event Action<float> MasterVolumeStepped, SfxVolumeStepped;
        public event Action TelemetryToggled;
        public event Action CloseRequested;

        /// <param name="telemetryOnByDefault">AppConfig.telemetryOnByDefault: what "no choice yet" means.</param>
        public SettingsPanel(Transform parent, SaveSystem save, bool telemetryOnByDefault)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.telemetryOnByDefault = telemetryOnByDefault;
            var dim = Ui.MakeImage(parent, new Color(0, 0, 0, 0.6f), null, "Settings");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            root = dim.gameObject;

            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 380));
            var title = Ui.Label(panel.transform, "설정", 34, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(480, 44));

            masterText = VolumeRow(panel.transform, "전체 음량", -90, d => MasterVolumeStepped?.Invoke(d));
            sfxText = VolumeRow(panel.transform, "효과음", -150, d => SfxVolumeStepped?.Invoke(d));

            var tLabel = Ui.Label(panel.transform, "플레이 기록 보내기", 20, Ui.Ink, TextAnchor.MiddleLeft, "TelemetryLabel");
            tLabel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, -210), new Vector2(240, 40));
            var tb = Ui.MakeButton(panel.transform, "", 18, () => TelemetryToggled?.Invoke(), out telemetryText, "TelemetryToggle");
            ((RectTransform)tb.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, -210), new Vector2(140, 40));
            telemetryNote = Ui.Label(panel.transform, "", 14, Ui.Dim, TextAnchor.MiddleCenter, "TelemetryNote");
            telemetryNote.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -256), new Vector2(480, 40));

            var close = Ui.MakeButton(panel.transform, "닫기 (Esc)", 20, () => CloseRequested?.Invoke(), out _, "Close");
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(200, 44));

            root.SetActive(false);
        }

        public void Show()
        {
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            Redraw();
        }

        public void Hide() => root.SetActive(false);

        /// The settings changed (the controller calls this after it saved).
        public void Redraw()
        {
            var s = save.Settings;
            masterText.text = $"{Mathf.RoundToInt(s.masterVolume * 100)}%";
            sfxText.text = $"{Mathf.RoundToInt(s.sfxVolume * 100)}%";
            bool on = SettingsController.TelemetryOn(s, telemetryOnByDefault);
            telemetryText.text = on ? "켜짐" : "꺼짐";
            telemetryText.color = on ? Ui.Gold : Ui.Dim;
            telemetryNote.text = "익명 플레이 기록(이동·클리어)만 보냅니다. 이름이나 계정은 보내지 않습니다.";
        }

        static Text VolumeRow(Transform parent, string label, float y, Action<float> step)
        {
            var l = Ui.Label(parent, label, 20, Ui.Ink, TextAnchor.MiddleLeft, label);
            l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, y), new Vector2(240, 40));
            var minus = Ui.MakeButton(parent, "−", 22, () => step(-VolumeStep), out _, label + " -");
            ((RectTransform)minus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, y), new Vector2(44, 40));
            var value = Ui.Label(parent, "", 20, Ui.Gold, TextAnchor.MiddleCenter, label + " value");
            value.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(140, y), new Vector2(80, 40));
            var plus = Ui.MakeButton(parent, "+", 22, () => step(VolumeStep), out _, label + " +");
            ((RectTransform)plus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(210, y), new Vector2(44, 40));
            return value;
        }
    }

    /// <summary>
    /// Settings panel controller (docs/scene-flow.md 4.7, S4): changes <see cref="SaveSystem.Settings"/> and saves at every
    /// change, so the value is kept however the panel closes (Esc, button, scene change). Saving raises SettingsChanged:
    /// audio and telemetry react. Until the architect adds SaveSystem commands (SetVolume ...), it edits the fields directly.
    /// </summary>
    public sealed class SettingsController : IDisposable
    {
        readonly SaveSystem save;
        readonly SettingsPanel view;
        readonly bool telemetryOnByDefault;

        public SettingsController(SaveSystem save, SettingsPanel view, bool telemetryOnByDefault)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.telemetryOnByDefault = telemetryOnByDefault;
            view.MasterVolumeStepped += StepMaster;
            view.SfxVolumeStepped += StepSfx;
            view.TelemetryToggled += ToggleTelemetry;
        }

        public void Dispose()
        {
            view.MasterVolumeStepped -= StepMaster;
            view.SfxVolumeStepped -= StepSfx;
            view.TelemetryToggled -= ToggleTelemetry;
        }

        /// What the consent button shows: an explicit choice (1 on, 2 off), or the build default when none was made.
        public static bool TelemetryOn(SettingsData s, bool onByDefault) =>
            s.telemetryChoice == 1 || (s.telemetryChoice == 0 && onByDefault);

        void StepMaster(float delta) { save.Settings.masterVolume = Step(save.Settings.masterVolume, delta); Saved(); }
        void StepSfx(float delta) { save.Settings.sfxVolume = Step(save.Settings.sfxVolume, delta); Saved(); }

        void ToggleTelemetry()
        {
            var s = save.Settings;
            bool on = TelemetryOn(s, telemetryOnByDefault);
            s.telemetryChoice = on ? 2 : 1;
            if (s.telemetryChoice == 2) s.playerId = ""; // opt-out forgets the anonymous id (11.3)
            Saved();
        }

        static float Step(float value, float delta)
        {
            if (Math.Abs(Math.Abs(delta) - SettingsPanel.VolumeStep) > 1e-4f) throw new ArgumentOutOfRangeException(nameof(delta), delta, "one volume step");
            return Mathf.Clamp01(Mathf.Round((value + delta) * 10f) / 10f);
        }

        void Saved()
        {
            AudioListener.volume = save.Settings.masterVolume; // live preview in scenes without an audio controller (Title)
            save.SaveSettings(); // raises SettingsChanged: volume + telemetry on/off
            view.Redraw();
        }
    }
}
