using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// Small helpers for building uGUI from code (legacy Text with the Jua font, so no TMP setup is needed).
    public static class Ui
    {
        public static Font Font;
        public static readonly Color Ink = new Color32(0xec, 0xe6, 0xd6, 0xff);
        public static readonly Color Dim = new Color32(0x7d, 0x76, 0x90, 0xff);
        public static readonly Color Gold = new Color32(0xf2, 0xc1, 0x4e, 0xff);
        public static readonly Color Stone = new Color32(0x9a, 0x94, 0x8a, 0xff);
        public static readonly Color ButtonBg = new Color32(0x2a, 0x26, 0x35, 0xff);
        public static readonly Color PanelBg = new Color32(0x1f, 0x1c, 0x2a, 0xf2);
        public static readonly Color PlateA = new Color32(0xe7, 0x6f, 0x51, 0xff);
        public static readonly Color PlateB = new Color32(0x2a, 0x9d, 0x8f, 0xff);
        public static readonly Color PlateC = new Color32(0x8e, 0x7d, 0xbe, 0xff);

        static Sprite circle;

        /// A soft-edged white circle, generated once (no texture asset needed).
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    byte a = (byte)(Mathf.Clamp01(n / 2f - 1 - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
                return circle;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// Place a rect: anchor point (0..1) on the parent, pivot, offset in reference pixels, size.
        public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, string name = "Label")
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Image MakeImage(Transform parent, Color color, Sprite sprite = null, string name = "Image")
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static Button MakeButton(Transform parent, string label, int fontSize, Action onClick, out Text text, string name = "Button")
        {
            var img = MakeImage(parent, ButtonBg, null, name);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            text = Label(img.transform, label, fontSize, Ink);
            text.rectTransform.Stretch();
            return b;
        }

        public static void AddOutline(Graphic g, Color color, float dist = 1.5f)
        {
            var o = g.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
        }
    }
}
