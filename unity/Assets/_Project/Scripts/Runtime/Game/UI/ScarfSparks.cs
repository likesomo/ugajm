using System;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// U5 boot / loading treatment (concept U5-boot.png; UiStyleToggle on): three small sparks in the brothers' scarf
    /// colours at the bottom centre, lighting one after another, and nothing else (no title, progress or tip). Shown by
    /// SceneLoader's fader in place of <see cref="LoadingView"/> on the same delay. View only, code-built (UiLayoutCheck).
    /// </summary>
    public sealed class ScarfSparks
    {
        public const float Size = 12, Pitch = 22, CenterY = 642, Period = 1.2f, DimAlpha = 0.3f;

        readonly RectTransform root;
        readonly Image[] sparks = new Image[3];

        /// The sparks' root (the fader fades it as a whole).
        public RectTransform Root => root;
        /// The sparks, left to right (tests).
        public Image Spark(int k) => sparks[k];

        public ScarfSparks(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = Ui.Rect("Scarf sparks", parent).Stretch();
            for (int k = 0; k < sparks.Length; k++)
            {
                var c = PlanetMap3DView.Scarves[k];
                sparks[k] = Ui.MakeImage(root, new Color(c.r, c.g, c.b, DimAlpha), Ui.Circle, "Spark " + (k + 1));
                sparks[k].raycastTarget = false;
                sparks[k].rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2((k - 1) * Pitch, 720 - CenterY), new Vector2(Size, Size));
            }
        }

        /// One spark at a time lights (brightest 1, the others dim), left to right every <see cref="Period"/> / 3 s.
        public void Animate(float time)
        {
            float phase = Mathf.Repeat(time / Period, 1f) * sparks.Length;
            for (int k = 0; k < sparks.Length; k++)
            {
                float d = Mathf.Abs(phase - (k + 0.5f));
                d = Mathf.Min(d, sparks.Length - d); // wraps around
                float a = Mathf.Lerp(1f, DimAlpha, Mathf.Clamp01(d));
                var c = sparks[k].color;
                sparks[k].color = new Color(c.r, c.g, c.b, a);
                sparks[k].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(d));
            }
        }
    }
}
