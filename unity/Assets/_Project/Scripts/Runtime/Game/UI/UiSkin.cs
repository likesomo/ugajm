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
        public static Image Card(Transform parent, string name = "Card") => Framed(parent, Theme.card, ButtonBg, name);

        /// Applies the theme's button sprites (sprite swap, 9-sliced) to a button made by MakeButton. No skin: unchanged.
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
        }

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
