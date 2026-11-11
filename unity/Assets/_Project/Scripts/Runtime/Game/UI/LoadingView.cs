using System;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The loading screen (art concept loading.png, docs/architecture.md 14.10): the menu background under a wash, the
    /// title top left, the Big Dipper progress at 45% of the screen width (stars light up in order, the newest one glows),
    /// a large "n / 7" and a tip line. Code-built so UiLayoutCheck can build it in Edit Mode; SceneLoader's fader shows it.
    /// </summary>
    public sealed class LoadingView
    {
        public const int Tips = 3;
        // Dipper box in 1280x720 reference units: 576 wide = 45% of the screen.
        const float BoxX = 352, BoxY = 230, BoxW = 576, BoxH = 262, StarSize = 44, GlowSize = 64, NumberGap = 40;

        readonly RectTransform root;
        readonly Text head, count, tip;
        readonly Text[] numbers = new Text[7];
        readonly Image[] stars = new Image[7], glows = new Image[7];
        int lit = -1;

        public RectTransform Root => root;

        public LoadingView(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = Ui.Rect("Loading", parent).Stretch();
            var solid = Ui.MakeImage(root, Ui.PanelBg, null, "Solid"); // opaque under everything: the old scene never shows
            solid.rectTransform.Stretch();
            if (Ui.MenuBackground(root) != null) Ui.Wash(root, 0.76f);
            solid.transform.SetAsFirstSibling(); // Solid, background, wash
            head = Ui.Label(root, "", 48, Ui.Ink, TextAnchor.MiddleLeft, "Head");
            head.rectTransform.TopLeft(40, 30, 1070, 72);

            var dipper = Ui.Theme.Dipper;
            Vector2 P(int i) => new Vector2(BoxX + dipper[i].x * BoxW, BoxY + dipper[i].y * BoxH);
            // Lines stop at the star edge so they never run under a star or its number.
            foreach (var (a, b) in UiTheme.DipperEdges)
            {
                Vector2 pa = P(a), pb = P(b), d = (pb - pa).normalized * (StarSize / 2 + 4);
                Ui.Line(root, pa + d, pb - d, Ui.Dim, "Edge").rectTransform.sizeDelta = new Vector2(Vector2.Distance(pa + d, pb - d), 3);
            }
            var number = Ui.Theme.FontFor(Language.En, FontRole.Number); // digits only: the number font in every language
            for (int i = 0; i < 7; i++)
            {
                var p = P(i);
                glows[i] = Ui.MakeImage(root, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.35f), Ui.Circle, "Glow " + (i + 1));
                glows[i].rectTransform.TopLeft(p.x - GlowSize / 2, p.y - GlowSize / 2, GlowSize, GlowSize);
                glows[i].enabled = false;
                stars[i] = Ui.Star(root, false, "Star " + (i + 1));
                stars[i].rectTransform.TopLeft(p.x - StarSize / 2, p.y - StarSize / 2, StarSize, StarSize);
                numbers[i] = Ui.Label(root, (i + 1).ToString(), 24, Ui.Ink, TextAnchor.MiddleCenter, "Number " + (i + 1));
                numbers[i].font = number;
                // Above or below the star, on the side no line leaves from (bowl 1-2-3-4-1, handle 4-5-6-7), clear of the glow.
                bool above = i == 0 || i == 3 || i == 4;
                numbers[i].rectTransform.TopLeft(p.x - 16, above ? p.y - NumberGap - 30 : p.y + NumberGap, 32, 30);
            }
            count = Ui.Label(root, "", 64, Ui.Gold, TextAnchor.MiddleCenter, "Count");
            count.rectTransform.TopLeft(490, 452, 300, 80);
            count.font = number;
            tip = Ui.Label(root, "", 28, Ui.Ink, TextAnchor.MiddleCenter, "Tip");
            tip.rectTransform.TopLeft(140, 560, 1000, 84);
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// Text in the current language and a tip (1..Tips), or the observer's carving instead (story.json "loading",
        /// StoryBook.LoadingLine; null = the tip); progress back to 0.
        public void Begin(Localizer loc, int tipNumber, string carving = null)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (tipNumber < 1 || tipNumber > Tips) throw new ArgumentOutOfRangeException(nameof(tipNumber));
            UiText.Set(head, loc, "loading.title", FontRole.Title);
            UiText.Set(tip, loc, "loading.tip." + tipNumber, FontRole.Body);
            if (!string.IsNullOrEmpty(carving)) tip.text = carving;
            lit = -1;
            Progress(0f);
        }

        /// Load progress 0..1: lights that many of the 7 stars, the newest one with a glow.
        public void Progress(float t)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * 7), 0, 7);
            if (n == lit) return;
            lit = n;
            for (int i = 0; i < 7; i++)
            {
                Ui.SetStar(stars[i], i < n);
                glows[i].enabled = i == n - 1;
            }
            count.text = $"{n} / 7";
        }

        /// Pulse of the newest star's glow (call every frame while shown, unscaled time).
        public void Animate(float time)
        {
            if (lit < 1) return;
            var g = glows[lit - 1];
            float s = 1f + 0.12f * Mathf.Sin(time * 5f);
            g.rectTransform.localScale = new Vector3(s, s, 1);
        }
    }
}
