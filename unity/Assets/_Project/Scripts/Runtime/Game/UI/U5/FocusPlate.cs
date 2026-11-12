using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// The one explanation plate of a canvas (docs/ui-u5-kit.md 1, 6): one line of secondary text (18) on a plate of
    /// text height + 16, 250 wide, growing to 320 for a longer line (longer: shorten the words, the text is never
    /// shrunk). It shows <see cref="FocusTip.Current"/> next to its owner: under a button in the upper half of the
    /// canvas, over one in the lower half, kept inside the safe area. Buttons join with <see cref="Track"/>.
    /// A second plate on the same canvas is an error.
    /// </summary>
    public sealed class FocusPlate : MonoBehaviour
    {
        public const float BaseWidth = 250, MaxWidth = 320, Padding = 8, Gap = 8, Safe = 24, Alpha = 0.85f;
        public const int FontSize = 18;

        [SerializeField] Image plate;
        [SerializeField] Text text;

        readonly FocusTip model = new FocusTip();
        readonly List<IconButton> tracked = new List<IconButton>();
        Localizer loc;
        /// Placement override per owner (the choice glyphs: above / below the pair, not each glyph).
        readonly Dictionary<object, RectTransform> anchors = new Dictionary<object, RectTransform>();

        public FocusTip Model => model;
        public Text Text => text;
        public Image Plate => plate;
        public bool Showing => gameObject.activeSelf;

        /// Builds the plate on <paramref name="parent"/>'s root canvas (Edit Mode too). Throws if that canvas has one.
        public static FocusPlate Build(Transform parent, Localizer localizer, UiIconSet set)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (localizer == null) throw new ArgumentNullException(nameof(localizer));
            var canvas = parent.GetComponentInParent<Canvas>();
            if (canvas == null) throw new ArgumentException("FocusPlate needs a canvas above it", nameof(parent));
            var root = canvas.rootCanvas.transform;
            if (root.GetComponentInChildren<FocusPlate>(true) != null)
                throw new InvalidOperationException($"canvas {root.name} already has a FocusPlate (docs/ui-u5-kit.md 1: one per canvas)");

            var bg = set != null ? set.captionPlate : null;
            var c = Ui.PanelBg;
            var img = Ui.MakeImage(parent, bg != null ? new Color(1, 1, 1, Alpha) : new Color(c.r, c.g, c.b, Alpha), bg, "Focus plate");
            if (bg != null && bg.border != Vector4.zero) img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);

            var t = Ui.Label(r, "", FontSize, Ui.Ink, TextAnchor.MiddleCenter, "Text");
            Ui.Secondary(t);
            t.rectTransform.Stretch();

            var p = img.gameObject.AddComponent<FocusPlate>();
            p.plate = img;
            p.text = t;
            p.loc = localizer;
            p.model.Changed += p.Redraw;
            localizer.Changed += p.OnLanguage;
            img.gameObject.SetActive(false);
            return p;
        }

        void OnDestroy()
        {
            if (loc != null) loc.Changed -= OnLanguage;
            foreach (var b in tracked) if (b != null) b.FocusChanged -= OnFocus;
        }

        void OnLanguage(Language _) => Redraw();

        /// <paramref name="b"/>'s hover / focus / hold now show here.
        public void Track(IconButton b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (tracked.Contains(b)) return;
            tracked.Add(b);
            b.FocusChanged += OnFocus;
        }

        /// Places <paramref name="owner"/>'s tip by <paramref name="anchor"/> instead of the owner itself (null: back to the owner).
        public void AnchorTo(IconButton owner, RectTransform anchor)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (anchor == null) anchors.Remove(owner); else anchors[owner] = anchor;
            Redraw();
        }

        void OnFocus(IconButton b, TipSource source, bool on)
        {
            if (on) model.Enter(source, b, b.TipKey, b.Shortcut);
            else model.Exit(source, b);
        }

        /// Words of a tip: the string + " · " + shortcut (docs 4).
        public static string Words(Localizer loc, FocusTip.Tip tip)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (tip.IsEmpty) return "";
            var s = loc.Get(tip.Key);
            return string.IsNullOrEmpty(tip.Shortcut) ? s : s + " · " + tip.Shortcut;
        }

        void Redraw()
        {
            var tip = model.Current;
            if (tip.IsEmpty) { gameObject.SetActive(false); return; }
            text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            text.text = Words(loc, tip);
            var size = SizeFor(text, text.text, PaddingOf(plate));
            var r = (RectTransform)transform;
            r.sizeDelta = size;
            var canvas = ((RectTransform)r.parent).rect.size;
            RectTransform owner = anchors.TryGetValue(tip.Owner, out var a) && a != null ? a
                : tip.Owner is Component c ? (RectTransform)c.transform : null;
            var spot = owner != null ? Place(OwnerRect(owner, (RectTransform)r.parent), size, canvas, Avoid?.Invoke()) : new Rect((canvas.x - size.x) / 2, Safe, size.x, size.y);
            r.anchoredPosition = new Vector2(spot.x, -spot.y);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// Plate padding: 8, or the 9-slice border of the caption plate sprite + 8 when larger (docs 1.1).
        public static Vector4 PaddingOf(Image img)
        {
            var b = img != null ? Ui.FrameBorder(img) : Vector4.zero;
            return new Vector4(Mathf.Max(Padding, b.x + Padding), Mathf.Max(Padding, b.y + Padding), Mathf.Max(Padding, b.z + Padding), Mathf.Max(Padding, b.w + Padding));
        }

        /// Plate size for <paramref name="words"/> in <paramref name="t"/>'s font: width 250..320, height line + padding.
        /// A line wider than 320 logs an error (shorten the string; LocalizationCheck / tests catch it first).
        public static Vector2 SizeFor(Text t, string words, Vector4 pad)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var gen = t.cachedTextGeneratorForLayout;
            var settings = t.GetGenerationSettings(Vector2.zero);
            float ppu = t.pixelsPerUnit > 0 ? t.pixelsPerUnit : 1f;
            float w = gen.GetPreferredWidth(words, settings) / ppu, h = gen.GetPreferredHeight(words, settings) / ppu;
            float need = Mathf.Ceil(w) + pad.x + pad.z;
            if (need > MaxWidth) Debug.LogError($"[SevenSteps] FocusPlate: \"{words}\" needs {need:0} > {MaxWidth} (shorten the string)");
            return new Vector2(Mathf.Clamp(need, BaseWidth, MaxWidth), Mathf.Ceil(Mathf.Max(h, t.fontSize)) + pad.y + pad.w);
        }

        /// <paramref name="owner"/>'s rect on the canvas <paramref name="space"/>, top-left origin, y down.
        static Rect OwnerRect(RectTransform owner, RectTransform space)
        {
            var corners = new Vector3[4];
            owner.GetWorldCorners(corners);
            var min = space.InverseTransformPoint(corners[0]);
            var max = space.InverseTransformPoint(corners[2]);
            var rs = space.rect;
            return Rect.MinMaxRect(min.x - rs.xMin, rs.yMax - max.y, max.x - rs.xMin, rs.yMax - min.y);
        }

        /// Where the plate goes for an owner at <paramref name="owner"/> (canvas, top-left origin): centred under it in the
        /// upper half, over it in the lower half, <see cref="Gap"/> away, inside the safe area (pure; tests).
        public static Rect Place(Rect owner, Vector2 size, Vector2 canvas) => Place(owner, size, canvas, null);

        /// <see cref="Place(Rect, Vector2, Vector2)"/> clear of <paramref name="avoid"/> (the board area, other visible
        /// buttons; review N1): centred on the preferred side, centred on the other side, then slid just left / right of
        /// what it crosses while it still touches the owner's column (it must read as that button's words). Nothing clear:
        /// the plain preferred spot (a passing tip may then cover the board edge).
        public static Rect Place(Rect owner, Vector2 size, Vector2 canvas, IReadOnlyList<Rect> avoid)
        {
            float maxX = Mathf.Max(Safe, canvas.x - Safe - size.x), maxY = Mathf.Max(Safe, canvas.y - Safe - size.y);
            bool under = owner.center.y < canvas.y / 2;
            float centred = Mathf.Clamp(owner.center.x - size.x / 2, Safe, maxX);
            var ys = new List<float>();
            foreach (bool below in new[] { under, !under })
            {
                float y = below ? owner.yMax + Gap : owner.yMin - Gap - size.y;
                if (y >= Safe - 0.01f && y <= maxY + 0.01f) ys.Add(y);
            }
            var first = new Rect(centred, ys.Count > 0 ? ys[0] : Mathf.Clamp(under ? owner.yMax + Gap : owner.yMin - Gap - size.y, Safe, maxY), size.x, size.y);
            foreach (float y in ys)
            {
                var r = new Rect(centred, y, size.x, size.y);
                if (Clear(r, avoid)) return r;
            }
            if (avoid == null) return first;
            foreach (float y in ys)
            {
                Rect? best = null;
                float bestD = float.MaxValue;
                foreach (var a in avoid)
                    foreach (float x in new[] { a.xMin - Gap - size.x, a.xMax + Gap, Safe, maxX })
                    {
                        if (x < Safe - 0.01f || x > maxX + 0.01f) continue;
                        var r = new Rect(x, y, size.x, size.y);
                        if (r.xMax < owner.xMin || r.xMin > owner.xMax || !Clear(r, avoid)) continue; // still under / over the owner
                        float d = Mathf.Abs(r.center.x - owner.center.x);
                        if (d < bestD) { bestD = d; best = r; }
                    }
                if (best.HasValue) return best.Value;
            }
            return first;
        }

        static bool Clear(Rect r, IReadOnlyList<Rect> avoid)
        {
            if (avoid == null) return true;
            foreach (var a in avoid) if (a.width > 0 && a.height > 0 && a.Overlaps(r)) return false;
            return true;
        }

        /// Areas the plate keeps clear of (canvas units, top-left origin), asked each time it moves: the board area and
        /// the buttons shown around it. Set by the screen that owns the plate.
        public Func<IReadOnlyList<Rect>> Avoid;
    }
}
