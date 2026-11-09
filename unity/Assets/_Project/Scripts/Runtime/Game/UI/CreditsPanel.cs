using System.Text;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Credits overlay on the Title scene. Shows the credit lines (Credits.txt, exported from design/art/CREDITS.md before
    /// every build) and, below them in a scroll view, the full license texts that must ship with the assets (Jua: SIL OFL 1.1).
    /// </summary>
    public sealed class CreditsPanel : SevenSteps.Game.Flow.IOverlay
    {
        readonly GameObject root;
        readonly ScrollRect scroll;
        readonly Text title, lines, closeLabel;
        readonly string creditLines;

        /// The close button. The OverlayStack closes the panel (docs/scene-flow.md S4).
        public event System.Action CloseRequested;

        public CreditsPanel(Transform parent, TextAsset credits, TextAsset[] licenses)
        {
            var dim = Ui.MakeImage(parent, new Color(0, 0, 0, 0.7f), null, "Credits");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            root = dim.gameObject;

            var opaque = Ui.PanelBg; opaque.a = 1f; // the Title text behind must not show through
            var panel = Ui.MakeImage(dim.transform, opaque, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 600));
            title = Ui.Label(panel.transform, "", 34, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(860, 44));

            creditLines = credits != null ? credits.text.Trim() : "";
            lines = Ui.Label(panel.transform, "", 18, Ui.Ink,
                TextAnchor.UpperCenter, "Lines");
            lines.horizontalOverflow = HorizontalWrapMode.Wrap;
            lines.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(840, 140));

            // License texts in a scroll view (the OFL asks for the full text to travel with the font).
            var view = Ui.MakeImage(panel.transform, new Color32(0x16, 0x14, 0x1e, 0xff), null, "Licenses");
            view.raycastTarget = true;
            view.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 84), new Vector2(840, 290));
            view.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect("Content", view.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(12, 0);
            content.offsetMax = new Vector2(-12, 0);
            var sb = new StringBuilder();
            if (licenses != null)
                foreach (var l in licenses)
                    // ASCII separators: Jua has no box-drawing U+2500, so "──" drew nothing in WebGL.
                    if (l != null) sb.Append("-- ").Append(l.name).Append(" --\n").Append(l.text.Replace("\r\n", "\n").Trim()).Append("\n\n");
            var body = content.gameObject.AddComponent<Text>();
            body.font = Ui.Font;
            body.fontSize = 13;
            body.color = Ui.Dim;
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.raycastTarget = false;
            body.text = sb.ToString();
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var close = Ui.MakeButton(panel.transform, "", 20, () => CloseRequested?.Invoke(), out closeLabel, "Close");
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(200, 44));
            root.SetActive(false);
        }

        /// Title, game name and close button in the current language (credits.*, 13.2), role fonts (13.4). Call once, then
        /// on Localizer.Changed. The credit lines and license texts stay as written (names, URLs, license terms).
        public void Apply(Localizer loc)
        {
            if (loc == null) throw new System.ArgumentNullException(nameof(loc));
            var body = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            title.text = loc.Get("credits.title");
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            lines.text = loc.Get("credits.game") + "\n\n" + creditLines;
            lines.font = body;
            closeLabel.text = loc.Get("credits.close");
            closeLabel.font = body;
        }

        public void Show()
        {
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide() => root.SetActive(false);
    }
}
