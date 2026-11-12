using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// The one explanation plate of a canvas (docs/ui-u5-kit.md 1, 6): one line (two at most, MaxLines) of secondary text (18) on a plate of
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
            if (on) model.Enter(source, b, b.TipKey, b.Shortcut, b.TipArgs);
            else model.Exit(source, b);
        }

        /// Words of a tip: the string + " · " + shortcut (docs 4).
        public static string Words(Localizer loc, FocusTip.Tip tip)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (tip.IsEmpty) return "";
            var s = tip.Args != null ? loc.Format(tip.Key, tip.Args) : loc.Get(tip.Key);
            return string.IsNullOrEmpty(tip.Shortcut) ? s : s + " · " + tip.Shortcut;
        }

        /// Places the shown tip again (its owner moved, or the canvas changed size).
        public void Refresh() => Redraw();

        void Redraw()
        {
            var tip = model.Current;
            if (tip.IsEmpty) { gameObject.SetActive(false); return; }
            text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            text.text = Words(loc, tip);
            var pad = PaddingOf(plate);
            var size = SizeFor(text, text.text, pad);
            // A tip longer than one 320 line wraps inside the padding (2 lines at most); a shorter one keeps its one line.
            bool wrap = size.x >= MaxWidth && LineCount(text, text.text, MaxWidth - pad.x - pad.z) > 1;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.rectTransform.offsetMin = new Vector2(wrap ? pad.x : 0, 0);
            text.rectTransform.offsetMax = new Vector2(wrap ? -pad.z : 0, 0);
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

        /// Plate padding per side: max(8, the caption plate's 9-slice border) - the art plate is a filled rounded plate with no
        /// frame line, so its border counts as padding (docs/ui-u5-kit.md 1.1, architect 2026-11-12).
        public static Vector4 PaddingOf(Image img)
        {
            var b = img != null ? Ui.FrameBorder(img) : Vector4.zero;
            return new Vector4(Mathf.Max(Padding, b.x), Mathf.Max(Padding, b.y), Mathf.Max(Padding, b.z), Mathf.Max(Padding, b.w));
        }

        /// Lines a tip may wrap to at <see cref="MaxWidth"/> (lead 2026-11-12: long words such as the hinted-attempt tip
        /// wrap to 2 lines instead of shrinking).
        public const int MaxLines = 2;

        /// Plate size for <paramref name="words"/> in <paramref name="t"/>'s font: width 250..320, height lines + padding.
        /// Words wider than one 320 line wrap at 320; more than <see cref="MaxLines"/> lines logs an error (shorten the
        /// string; LocalizationCheck / tests catch it first).
        public static Vector2 SizeFor(Text t, string words, Vector4 pad)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var gen = t.cachedTextGeneratorForLayout;
            var settings = t.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow; // one line's width, whatever the text's mode now
            float ppu = t.pixelsPerUnit > 0 ? t.pixelsPerUnit : 1f;
            float w = gen.GetPreferredWidth(words, settings) / ppu;
            float need = Mathf.Ceil(w) + pad.x + pad.z;
            float padY = Mathf.Max(pad.y, pad.w);
            if (need <= MaxWidth)
            {
                float width = Mathf.Max(need, BaseWidth);
                return new Vector2(width, PlateHeight(t, words, width, padY));
            }
            float inner = MaxWidth - pad.x - pad.z;
            int lines = LineCount(t, words, inner);
            if (lines > MaxLines) Debug.LogError($"[SevenSteps] FocusPlate: \"{words}\" needs {lines} lines > {MaxLines} at {MaxWidth} (shorten the string)");
            return new Vector2(MaxWidth, PlateHeight(t, words, inner, padY, wrap: true));
        }

        /// Lines <paramref name="words"/> take in <paramref name="t"/>'s font wrapped at <paramref name="width"/>.
        public static int LineCount(Text t, string words, float width)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width), width, "> 0");
            if (string.IsNullOrEmpty(words)) return 0;
            var gen = new TextGenerator();
            var settings = t.GetGenerationSettings(new Vector2(width, 1000f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            gen.Populate(words, settings);
            return gen.lineCount;
        }

        /// Height of a plate whose (vertically centred) text keeps <paramref name="pad"/> clear above and below its drawn
        /// glyphs: twice the glyphs' larger reach from the centre, plus the padding. The Jua glyph quads are taller than the
        /// font size and sit low (14.4 "glyphs + 8 inside", scene-flow UiLayoutCheck finding on 1f90617), so the line
        /// height alone is not enough. Empty text: the font size.
        /// <paramref name="wrap"/>: the words wrap at <paramref name="width"/> (the text area); else <paramref name="t"/>'s own
        /// wrap mode applies.
        public static float PlateHeight(Text t, string words, float width, float pad, bool wrap = false)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            float reach = t.fontSize / 2f;
            if (!string.IsNullOrEmpty(words))
            {
                var gen = new TextGenerator();
                var settings = t.GetGenerationSettings(new Vector2(Mathf.Max(width, 1f), 1000f));
                settings.textAnchor = TextAnchor.MiddleCenter;
                if (wrap) { settings.horizontalOverflow = HorizontalWrapMode.Wrap; settings.verticalOverflow = VerticalWrapMode.Overflow; }
                gen.Populate(words, settings);
                float ppu = t.pixelsPerUnit > 0 ? t.pixelsPerUnit : 1f;
                foreach (var v in gen.verts) reach = Mathf.Max(reach, Mathf.Abs(v.position.y / ppu));
            }
            return Mathf.Ceil(2 * reach + 2 * pad);
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
