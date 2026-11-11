using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// Small helpers for building uGUI from code (legacy Text, so no TMP setup is needed). Font and colors come from
    /// <see cref="Theme"/> (UiTheme.cs, docs/scene-flow.md S6).
    public static partial class Ui
    {

        static Sprite circle, starShape, ringShape;

        /// Domain reload is disabled in the editor: drop the previous Play's theme and generated sprite.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            theme = null;
            circle = null;
            starShape = null;
            ringShape = null;
        }

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

        /// A white five-point star (anti-aliased), generated once: the stars at the brother's feet and the choice card marks.
        public static Sprite StarShape => starShape != null ? starShape : starShape = Shape(64, (x, y) =>
        {
            // Distance-like coverage from a polar star outline: radius between inner .42 and outer 1 by angle.
            float a = Mathf.Atan2(x, y), r = Mathf.Sqrt(x * x + y * y);
            float k = Mathf.Abs(Mathf.Repeat(a / (2 * Mathf.PI) * 5f, 1f) - 0.5f) * 2f; // 1 at a point, 0 between points
            float edge = Mathf.Lerp(0.5f, 0.98f, k * k);
            return (edge - r) * 32f;
        });

        /// A white ring (outer radius 1, width 0.14), generated once: the choice ring around the brother.
        public static Sprite RingShape => ringShape != null ? ringShape : ringShape = Shape(128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return (0.07f - Mathf.Abs(r - 0.92f)) * 64f;
        });

        /// A sprite of n x n texels from a coverage function over -1..1 (clamped to 0..1).
        static Sprite Shape(int n, Func<float, float, float> coverage)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(coverage(u, v)) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
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
