using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Story;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// The board's passing story line under U5 (stage start lines, planet finale lines; StoryDirector). The current HUD's
    /// left-margin guide plate that carried it is hidden under U5 (U5GameHud.HideClassic, lead 2026-11-12), which left
    /// every start line invisible (found 2026-11-14). A plate at the bottom centre, above the message line, for its time.
    /// </summary>
    public sealed class U5StoryLine : MonoBehaviour, ITransientLine
    {
        public const int Size = 22;
        Image plate;
        Text text;
        Localizer loc;
        float until;

        public static U5StoryLine Build(Transform hudRoot, Localizer loc)
        {
            if (hudRoot == null) throw new ArgumentNullException(nameof(hudRoot));
            var plate = Ui.Plate(hudRoot, "Story line");
            var line = plate.gameObject.AddComponent<U5StoryLine>();
            line.plate = plate;
            line.loc = loc;
            plate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 84), new Vector2(560, 44));
            line.text = Ui.Label(plate.transform, "", Size, Ui.Gold, TextAnchor.MiddleCenter, "Line");
            line.text.rectTransform.Stretch();
            plate.gameObject.SetActive(false);
            return line;
        }

        public bool LineShown => plate != null && plate.gameObject.activeSelf;
        public string Text => LineShown ? text.text : "";

        public void ShowLine(string line, float seconds)
        {
            if (string.IsNullOrEmpty(line)) throw new ArgumentException("empty line", nameof(line));
            if (!(seconds > 0)) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "> 0");
            if (loc != null) text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            text.text = line;
            float w = text.cachedTextGeneratorForLayout.GetPreferredWidth(line, text.GetGenerationSettings(Vector2.zero)) / Mathf.Max(0.01f, text.pixelsPerUnit);
            plate.rectTransform.sizeDelta = new Vector2(Mathf.Min(1000, Mathf.Ceil(w) + 40), 44);
            until = Time.unscaledTime + seconds;
            plate.gameObject.SetActive(true);
        }

        public void HideLine() { if (plate != null) plate.gameObject.SetActive(false); }

        void Update()
        {
            if (Time.unscaledTime >= until) HideLine();
        }
    }
}
