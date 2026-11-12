using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The small "this attempt earns at most ★1" mark (user decision 2026-11-12-b (2), option A): a gold star with a dark
    /// "1" on it. Shown while the attempt opened a costing hint (AttemptCounts.Hinted), restarts included. The current HUD
    /// puts it right of the stage title, U5 on the hint button's corner (whose focus tip then says why). View only: the
    /// owner sets <see cref="SetShown"/> from the board's attempt; this decides nothing.
    /// </summary>
    public static class StarCapMark
    {
        public const float Size = 24;
        public const int DigitSize = 13;
        public const string Name = "Star cap";
        static readonly Color Digit = new Color(0.12f, 0.1f, 0.16f, 1f);

        /// Builds the mark (hidden) under <paramref name="parent"/>, out of any layout group, centred on the anchor point
        /// <paramref name="anchor"/> of the parent plus <paramref name="offset"/>. Never catches the pointer.
        public static RectTransform Build(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 offset)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (parent.Find(Name) != null) throw new InvalidOperationException($"{parent.name} already has a {Name} mark");
            var star = Ui.MakeImage(parent, Ui.Gold, Ui.StarShape, Name);
            star.raycastTarget = false;
            var r = star.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = offset;
            r.sizeDelta = new Vector2(Size, Size);
            star.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var one = Ui.Label(r, "1", DigitSize, Digit, TextAnchor.MiddleCenter, "Digit");
            one.rectTransform.Stretch();
            one.rectTransform.anchoredPosition = new Vector2(0, -1); // the star's body sits a little low in its square
            star.gameObject.SetActive(false);
            return r;
        }

        /// Shows or hides the mark built by <see cref="Build"/>.
        public static void SetShown(RectTransform mark, bool shown)
        {
            if (mark == null) throw new ArgumentNullException(nameof(mark));
            if (mark.name != Name) throw new ArgumentException($"not a {Name} mark: {mark.name}", nameof(mark));
            if (mark.gameObject.activeSelf != shown) mark.gameObject.SetActive(shown);
        }
    }
}
