using System;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// Menu skin helpers (UiTheme "Menu skin", docs/scene-flow.md S7): framed panels, cards, sprite-swap buttons, stars,
    /// the Dipper and top-left placement in 1280x720 reference pixels (the art layouts are measured that way).
    /// Every helper falls back to the flat theme colors when the theme has no sprite, so a theme without the skin still works.
    public static partial class Ui
    {
        /// Places a rect by its top-left corner in reference pixels from the parent's top-left (art layouts: x right, y down).
        public static RectTransform TopLeft(this RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        /// The theme's menu background prefab stretched over <paramref name="parent"/>, or null when the theme has none.
        public static GameObject MenuBackground(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var prefab = Theme.menuBackground;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            ((RectTransform)go.transform).Stretch();
            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            go.transform.SetAsFirstSibling();
            return go;
        }

        /// Full-screen color wash over the background (the art darkens it per screen).
        public static Image Wash(Transform parent, float alpha, string name = "Wash")
        {
            var img = MakeImage(parent, new Color(0.04f, 0.08f, 0.15f, alpha), null, name);
            img.rectTransform.Stretch();
            return img;
        }

        static Image Framed(Transform parent, Sprite sprite, Color flat, string name)
        {
            var img = MakeImage(parent, sprite != null ? Color.white : flat, sprite, name);
            if (sprite != null) img.type = Image.Type.Sliced;
            return img;
        }

        /// A large framed panel (title menu, stage list): the theme's panel sprite, or the flat panel color.
        public static Image Panel(Transform parent, string name = "Panel") => Framed(parent, Theme.panel, PanelBg, name);

        /// A small framed card (planets, stages), or the flat button color.
        public static Image Card(Transform parent, string name = "Card")
        {
            var img = Framed(parent, Theme.card, ButtonBg, name);
            if (img.sprite != null) img.pixelsPerUnitMultiplier = Mathf.Max(1f, img.sprite.border.w / CardBorder); // 14.4
            return img;
        }

        /// Card frame border in reference pixels: the card sprite (panel_wood, border 32 with a grain line 23-25 px in) is
        /// drawn smaller so its border is 12 and the grain stays inside the border, clear of the card text (docs/architecture.md 14.4).
        public const float CardBorder = 12f;

        /// Applies the theme's button sprites (sprite swap, 9-sliced) to a button made by MakeButton, then
        /// <see cref="FitButtonSkin"/>. No skin: unchanged.
        public static void Skin(Button b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            var t = Theme;
            if (!t.HasButtonSkin) return;
            var img = (Image)b.targetGraphic;
            img.sprite = t.buttonNormal;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = t.buttonHover, selectedSprite = t.buttonHover,
                pressedSprite = t.buttonPressed, disabledSprite = t.buttonDisabled,
            };
            FitButtonSkin(b);
        }

        /// Button frame scale: the atlas button (512 x 80, border 20 holding both frame lines) drawn at 1/2.5, so the border
        /// is 8 reference pixels and the inner line sits about 4 in from the edge on any button height (docs/architecture.md 14.4).
        public const float ButtonFrameScale = 2.5f;
        /// Clear space between the frame's border and the label (14.4 padding).
        public const float LabelPadding = 8f;

        /// Frame border of a sliced image in reference pixels (left, bottom, right, top); zero when not sliced.
        public static Vector4 FrameBorder(Image img)
        {
            if (img == null) throw new ArgumentNullException(nameof(img));
            if (img.sprite == null || img.type != Image.Type.Sliced) return Vector4.zero;
            // Image draws the border at sprite pixels / (Image.pixelsPerUnit = sprite PPU / canvas reference PPU, x multiplier),
            // the same formula UiLayoutCheck uses (architect N1).
            float scale = img.pixelsPerUnit * img.pixelsPerUnitMultiplier;
            return img.sprite.border / Mathf.Max(scale, 0.0001f);
        }

        /// The art button frame at <see cref="ButtonFrameScale"/>, and its label (the first Text child) stretched inside the
        /// frame's border plus <see cref="LabelPadding"/>, so the frame lines never cross the words (ui-layout-fix "common").
        /// Buttons need height >= 2 x (8 + 8) + the label's line height (60 for 22 px text). Icons, if any, are left alone.
        public static void FitButtonSkin(Button b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (!(b.targetGraphic is Image img)) return;
            // Also on a flat base-prefab button: an art variant that only swaps the sprite keeps this scale.
            img.pixelsPerUnitMultiplier = ButtonFrameScale;
            var label = b.GetComponentInChildren<Text>(true);
            if (label == null || label.transform.parent != b.transform) return;
            var border = FrameBorder(img);
            var skin = Theme != null ? Theme.buttonNormal : null;
            if (border == Vector4.zero && skin != null) // flat base button: room for the theme frame the variant puts on
            {
                float refPpu = img.canvas != null && img.canvas.referencePixelsPerUnit > 0 ? img.canvas.referencePixelsPerUnit : 100f;
                border = skin.border / Mathf.Max(skin.pixelsPerUnit / refPpu * ButtonFrameScale, 0.0001f);
            }
            var r = label.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(Mathf.Max(r.offsetMin.x, border.x + LabelPadding), border.y + LabelPadding);
            r.offsetMax = new Vector2(Mathf.Min(r.offsetMax.x, -(border.z + LabelPadding)), -(border.w + LabelPadding));
        }

        /// A text plate (14.4): a flat panel-colour image at alpha 0.9, no frame sprite, not a raycast target.
        public static Image Plate(Transform parent, string name = "Plate")
        {
            var c = PanelBg;
            var img = MakeImage(parent, new Color(c.r, c.g, c.b, PlateAlpha), null, name);
            img.raycastTarget = false;
            return img;
        }

        /// Plate alpha: 0.9 keeps gold and dim text above 4.5:1 even composited over white (14.3).
        public const float PlateAlpha = 0.9f;

        /// A menu button: MakeButton + the theme skin.
        public static Button MenuButton(Transform parent, int fontSize, Action onClick, out Text text, string name = "Button")
        {
            var b = MakeButton(parent, "", fontSize, onClick, out text, name);
            Skin(b);
            return b;
        }

        /// A star icon: the theme's sprites, or a gold / dim dot.
        public static Image Star(Transform parent, bool filled, string name = "Star")
        {
            var t = Theme;
            var sprite = t.starFilled != null ? (filled ? t.starFilled : t.starEmpty) : Circle;
            var color = t.starFilled != null ? Color.white : (filled ? Gold : Dim);
            var img = MakeImage(parent, color, sprite, name);
            img.preserveAspect = true;
            return img;
        }

        public static void SetStar(Image star, bool filled)
        {
            var t = Theme;
            if (t.starFilled != null) star.sprite = filled ? t.starFilled : t.starEmpty;
            else star.color = filled ? Gold : Dim;
        }

        /// A 2px line from a to b (top-left reference pixels), e.g. the Dipper edges.
        public static Image Line(Transform parent, Vector2 a, Vector2 b, Color color, string name = "Line")
        {
            var img = MakeImage(parent, color, null, name);
            var r = img.rectTransform.TopLeft(a.x, a.y, Vector2.Distance(a, b), 2);
            r.pivot = new Vector2(0, 0.5f);
            r.anchoredPosition = new Vector2(a.x, -a.y);
            r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            return img;
        }

        /// A label in a role font of a language (the font is set again by UiText.Set when the text is a table string).
        public static Text RoleLabel(Transform parent, Language lang, FontRole role, int size, Color color, TextAnchor align, string name)
        {
            var t = Label(parent, "", size, color, align, name);
            t.font = Theme.FontFor(lang, role);
            return t;
        }
    }
}
