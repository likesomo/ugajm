using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Settings overlay shared by the Title and Game scenes: master/sfx volume and the play-data (telemetry) consent.
    /// Edits <see cref="SaveSystem.Settings"/> and saves on close (docs/architecture.md 11.3), which raises SettingsChanged
    /// so audio and telemetry react. Volume is previewed live through AudioListener while open.
    /// </summary>
    public sealed class SettingsPanel
    {
        const float VolumeStep = 0.1f;

        readonly SaveSystem save;
        readonly GameObject root;
        readonly Text masterText, sfxText, telemetryText, telemetryNote;

        public bool IsOpen => root.activeSelf;

        public SettingsPanel(Transform parent, SaveSystem save)
        {
            this.save = save;
            var dim = Ui.MakeImage(parent, new Color(0, 0, 0, 0.6f), null, "Settings");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            root = dim.gameObject;

            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 380));
            var title = Ui.Label(panel.transform, "설정", 34, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(480, 44));

            masterText = VolumeRow(panel.transform, "전체 음량", -90, d => Change(ref save.Settings.masterVolume, d));
            sfxText = VolumeRow(panel.transform, "효과음", -150, d => Change(ref save.Settings.sfxVolume, d));

            var tLabel = Ui.Label(panel.transform, "플레이 기록 보내기", 20, Ui.Ink, TextAnchor.MiddleLeft, "TelemetryLabel");
            tLabel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, -210), new Vector2(240, 40));
            var tb = Ui.MakeButton(panel.transform, "", 18, ToggleTelemetry, out telemetryText, "TelemetryToggle");
            ((RectTransform)tb.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, -210), new Vector2(140, 40));
            telemetryNote = Ui.Label(panel.transform, "", 14, Ui.Dim, TextAnchor.MiddleCenter, "TelemetryNote");
            telemetryNote.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -256), new Vector2(480, 40));

            var close = Ui.MakeButton(panel.transform, "닫기", 20, Close, out _, "Close");
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(200, 44));

            root.SetActive(false);
        }

        public void Open()
        {
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            Redraw();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            save.SaveSettings(); // raises SettingsChanged: volume + telemetry on/off
        }

        void Change(ref float value, float delta)
        {
            value = Mathf.Clamp01(Mathf.Round((value + delta) * 10f) / 10f);
            AudioListener.volume = save.Settings.masterVolume;
            Redraw();
        }

        void ToggleTelemetry()
        {
            var s = save.Settings;
            bool on = s.telemetryChoice != 2 && (s.telemetryChoice == 1 || TelemetryDefault());
            s.telemetryChoice = on ? 2 : 1;
            if (s.telemetryChoice == 2) s.playerId = ""; // opt-out forgets the anonymous id (11.3)
            Redraw();
        }

        static bool TelemetryDefault() =>
            Services.TryGet<AppConfig>(out var cfg) && cfg.telemetryOnByDefault;

        void Redraw()
        {
            var s = save.Settings;
            masterText.text = $"{Mathf.RoundToInt(s.masterVolume * 100)}%";
            sfxText.text = $"{Mathf.RoundToInt(s.sfxVolume * 100)}%";
            bool on = s.telemetryChoice == 1 || (s.telemetryChoice == 0 && TelemetryDefault());
            telemetryText.text = on ? "켜짐" : "꺼짐";
            telemetryText.color = on ? Ui.Gold : Ui.Dim;
            telemetryNote.text = "익명 플레이 기록(이동·클리어)만 보냅니다. 이름이나 계정은 보내지 않습니다.";
        }

        static Text VolumeRow(Transform parent, string label, float y, System.Action<float> change)
        {
            var l = Ui.Label(parent, label, 20, Ui.Ink, TextAnchor.MiddleLeft, label);
            l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, y), new Vector2(240, 40));
            var minus = Ui.MakeButton(parent, "−", 22, () => change(-VolumeStep), out _, label + " -");
            ((RectTransform)minus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, y), new Vector2(44, 40));
            var value = Ui.Label(parent, "", 20, Ui.Gold, TextAnchor.MiddleCenter, label + " value");
            value.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(140, y), new Vector2(80, 40));
            var plus = Ui.MakeButton(parent, "+", 22, () => change(VolumeStep), out _, label + " +");
            ((RectTransform)plus.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(210, y), new Vector2(44, 40));
            return value;
        }
    }
}
