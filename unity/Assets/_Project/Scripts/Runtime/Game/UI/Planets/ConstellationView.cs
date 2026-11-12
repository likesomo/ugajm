using System;
using System.Collections.Generic;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The U5 surface's stage constellation (D2b Orbit sparks; d2-refine IMPLEMENT.md): view only, filled from
    /// <see cref="StageSelectModel"/> through <see cref="Bind"/>. Each stage is an 80 x 80 Button (the node root, fixed hit
    /// box) with its glyph under "Glyph" (scaled by focus / press, the hit box never shrinks): 78 soft halo, 43 dark backing,
    /// 54 rim, the four-ray star (34 gold cleared, 27 cream with a dark centre untouched), 0-3 round score sparks at 25 px
    /// (top, lower right, lower left), the current stage's three scarf arcs + heart + top pointer, focus's three white
    /// accents, and the finale aureole when every stage has three stars. Links between nodes stop 40 short of the centres:
    /// future 1 px cool .20, drawn 1.5 px gold .65 (+4 px gold .10 on a perfect planet). Anchors follow the canvas size
    /// (reflow per axis, targets keep 80). Angles are y-up (the review's y-down ones mirrored).
    /// </summary>
    public sealed class ConstellationView
    {
        public const float Target = 80, Halo = 78, Rim = 54, Backing = 43, StarCleared = 34, StarUntouched = 27, SparkOrbit = 25, LinkGap = 40;
        public static readonly float[] SparkAngles = { 90, -30, -150 };
        /// The current stage's scarf arcs (d2-refine: #568C88 / #B99A57 / #B96C59; the halo takes the first).
        public static readonly Color[] Scarf = { new Color32(0x56, 0x8C, 0x88, 255), new Color32(0xB9, 0x9A, 0x57, 255), new Color32(0xB9, 0x6C, 0x59, 255) };
        static readonly Color Cream = new Color32(238, 231, 208, 255), Gold = new Color32(237, 194, 115, 255), Dim = new Color32(137, 159, 169, 255), Ink = new Color32(19, 26, 36, 255);

        readonly RectTransform layer, links;
        readonly List<Node> nodes = new List<Node>();
        readonly List<(Image line, Image glow, int a, int b)> linkImages = new List<(Image, Image, int, int)>();
        readonly List<(Image glow, Image line)> pool = new List<(Image, Image)>();
        ConstellationFigure figure;
        Vector2 fittedSize;
        int focused = -1, hovered = -1, pressed = -1;
        float time;

        /// Steady glows and immediate state changes (settings reduce motion).
        public bool ReduceMotion { get; set; }
        public int Count { get; private set; }
        public Button Button(int slot) => nodes[Check(slot)].Button;
        /// Node centre in the layer's local units (tests, UiLayoutCheck).
        public Vector2 Centre(int slot) => nodes[Check(slot)].Rect.anchoredPosition;
        /// The node's state as built ("cleared 2", "untouched", with " current" / " perfect"), for tests and names.
        public string State(int slot) => nodes[Check(slot)].State;
        public float GlyphScale(int slot) => nodes[Check(slot)].Glyph.localScale.x;
        public bool LinkDrawn(int edge) => linkImages[edge].line.color.a > 0.5f;
        public int LinkCount => linkImages.Count;
        public RectTransform Layer => layer;

        public ConstellationView(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            layer = Ui.Rect("Constellation", parent).Stretch();
            // Its own canvas: the pulse and twinkle change colours every frame and rebuild only this batch, not the surface
            // canvas (architect N2, 2ed1665). A nested canvas raycasts with its own GraphicRaycaster.
            layer.gameObject.AddComponent<Canvas>();
            layer.gameObject.AddComponent<GraphicRaycaster>();
            links = Ui.Rect("Links", layer).Stretch();
        }

        int Check(int slot) => slot >= 0 && slot < Count ? slot : throw new ArgumentOutOfRangeException(nameof(slot), slot, $"slot of {Count}");

        /// Builds the planet's nodes and links for its state. Nodes are reused across planets (their buttons keep listeners).
        public void Bind(StageSelectModel model, int planet, ConstellationFigure fig)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            figure = fig ?? throw new ArgumentNullException(nameof(fig));
            Count = model.StageCount(planet);
            if (figure.Anchors.Count != Count) throw new ArgumentException($"figure has {figure.Anchors.Count} anchors for {Count} stages", nameof(fig));
            while (nodes.Count < Count) nodes.Add(new Node(layer, nodes.Count));
            for (int i = 0; i < nodes.Count; i++) nodes[i].Rect.gameObject.SetActive(i < Count);
            bool perfect = model.Perfect(planet);
            int current = ConstellationFigure.Current(model, planet);
            for (int s = 0; s < Count; s++) nodes[s].Apply(model.IsCleared(planet, s), model.Best(planet, s), s == current, perfect);

            // Link strips are pooled (a glow under each line), reused across planets.
            linkImages.Clear();
            for (int k = 0; k < figure.Edges.Count; k++)
            {
                var e = figure.Edges[k];
                while (pool.Count <= k) pool.Add((Link("Perfect glow"), Link("Link")));
                var (glow, line) = pool[k];
                bool drawn = ConstellationFigure.Drawn(model, planet, e);
                line.gameObject.name = drawn ? "Drawn link" : "Future link";
                line.color = drawn ? WithAlpha(Gold, .65f) : WithAlpha(Dim, .20f);
                line.rectTransform.sizeDelta = new Vector2(0, drawn ? 1.5f : 1f);
                glow.color = WithAlpha(Gold, .10f);
                glow.rectTransform.sizeDelta = new Vector2(0, 4);
                glow.gameObject.SetActive(perfect && drawn);
                line.gameObject.SetActive(true);
                linkImages.Add((line, perfect && drawn ? glow : null, e.a, e.b));
            }
            for (int k = figure.Edges.Count; k < pool.Count; k++) { pool[k].glow.gameObject.SetActive(false); pool[k].line.gameObject.SetActive(false); }
            focused = hovered = pressed = -1;
            fittedSize = Vector2.zero;
            Layout();
        }

        /// Places nodes and links for the current canvas size (call when it changes; Tick does).
        public void Layout()
        {
            var size = layer.rect.size;
            if (size == fittedSize || figure == null) return;
            fittedSize = size;
            for (int s = 0; s < Count; s++) nodes[s].Rect.anchoredPosition = ToLocal(figure.Anchors[s], size);
            foreach (var (line, glow, a, b) in linkImages)
            {
                var pa = ToLocal(figure.Anchors[a], size);
                var pb = ToLocal(figure.Anchors[b], size);
                Place(line, pa, pb);
                if (glow != null) Place(glow, pa, pb);
            }
        }

        /// A reference anchor (1280 x 720, top-left origin) in the layer's centred local units, stretched per axis.
        public static Vector2 ToLocal(Vector2 anchor, Vector2 size) =>
            new Vector2((anchor.x / ConstellationFigure.RefW - .5f) * size.x, (.5f - anchor.y / ConstellationFigure.RefH) * size.y);

        static void Place(Image strip, Vector2 a, Vector2 b)
        {
            var d = b - a;
            float len = Mathf.Max(0, d.magnitude - 2 * LinkGap);
            var r = strip.rectTransform;
            r.anchoredPosition = (a + b) / 2;
            r.sizeDelta = new Vector2(len, r.sizeDelta.y);
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            strip.enabled = len > 0;
        }

        /// A route strip: faint by design (d2-refine: future 1 px at .20, drawn 1.5 px at .65), the nodes carry the state.
        Image Link(string name)
        {
            var im = Strip(links, name, 1, Gold);
            Ui.Exempt(im.gameObject, UiLayoutRule.Decoration, "D2b constellation route line (faint by design; the node glyphs carry the stage state)");
            return im;
        }

        static Image Strip(RectTransform parent, string name, float width, Color color)
        {
            var im = Ui.MakeImage(parent, color, null, name);
            im.raycastTarget = false;
            var r = im.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(0, width);
            return im;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// Focus (keyboard / selected), hover and press; -1 = none. Shows focus accents on the focused or hovered node.
        public void SetInput(int focus, int hover, int press)
        {
            focused = focus; hovered = hover;
            if (pressed >= 0 && press < 0 && pressed < Count) nodes[pressed].Release(ReduceMotion);
            pressed = press;
            for (int s = 0; s < Count; s++) nodes[s].SetFocus(s == hover || (hover < 0 && s == focus));
            if (ReduceMotion) Tick(0);
        }

        /// Eases scales and runs the current pulse and the twinkle (unscaled time, no allocations).
        public void Tick(float dt)
        {
            Layout();
            time += dt;
            for (int s = 0; s < Count; s++)
            {
                float target = s == pressed ? .91f : s == hovered ? 1.17f : s == focused && hovered < 0 ? 1.12f : 1f;
                nodes[s].Animate(target, dt, time, ReduceMotion, ReduceMotion || pressed >= 0);
            }
        }

        /// One stage node: the Button root (80 x 80, transparent hit image) and its glyph.
        public sealed class Node
        {
            public readonly RectTransform Rect, Glyph;
            public readonly Button Button;
            public string State { get; private set; } = "";
            readonly int slot;
            readonly Image halo, starRim, star, centre, heart, aureole;
            readonly UiArc rim, aureoleArc;
            readonly UiArc[] scarf = new UiArc[3], accents = new UiArc[3];
            readonly Image[] sparks = new Image[3], sparkGlows = new Image[3];
            readonly Image[] pointer = new Image[2];
            readonly RectTransform scarfRoot;
            Color haloColour;
            float haloAlpha, scale = 1, twinkle;
            bool current;

            public Node(RectTransform layer, int slot)
            {
                this.slot = slot;
                var hit = Ui.MakeImage(layer, new Color(1, 1, 1, 0), null, $"Stage {slot + 1}");
                hit.raycastTarget = true;
                Rect = hit.rectTransform;
                Rect.anchorMin = Rect.anchorMax = Rect.pivot = new Vector2(.5f, .5f);
                Rect.sizeDelta = new Vector2(Target, Target);
                Button = hit.gameObject.AddComponent<Button>();
                Button.transition = Selectable.Transition.None;
                Glyph = Ui.Rect("Glyph", Rect);
                Glyph.anchorMin = Glyph.anchorMax = Glyph.pivot = new Vector2(.5f, .5f);
                Glyph.sizeDelta = new Vector2(Target, Target);
                twinkle = 3f + (slot * 0.73f) % 2f; // staggered 3-5 s

                aureole = Img("Finale aureole", ConstellationGlyphs.Halo, 80, WithAlpha(Gold, .35f));
                halo = Img("Soft halo", ConstellationGlyphs.Halo, Halo, Cream);
                Img("Contrast backing", Ui.Circle, Backing, WithAlpha(Ink, .78f));
                rim = UiArc.Create(Glyph, "Rim", Rim / 2, 1.2f, 0, 360, Dim);
                aureoleArc = UiArc.Create(Glyph, "Finale ring", 31, 1.2f, 0, 360, WithAlpha(Gold, .8f));
                starRim = Img("Star dark rim", ConstellationGlyphs.Star, 41, Ink);
                star = Img("Stage star", ConstellationGlyphs.Star, StarCleared, Gold);
                centre = Img("Untouched centre", Ui.Circle, 7, Ink);
                for (int k = 0; k < 3; k++)
                {
                    float a = SparkAngles[k] * Mathf.Deg2Rad;
                    var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SparkOrbit;
                    sparkGlows[k] = Img("Score spark glow " + (k + 1), ConstellationGlyphs.Halo, 18, WithAlpha(Gold, .9f), at);
                    sparks[k] = Img("Score spark " + (k + 1), Ui.Circle, 7, Gold, at);
                }
                scarfRoot = Ui.Rect("Current", Glyph);
                scarfRoot.anchorMin = scarfRoot.anchorMax = scarfRoot.pivot = new Vector2(.5f, .5f);
                scarfRoot.sizeDelta = new Vector2(Target, Target);
                float[] from = { -5, -125, -245 };
                for (int k = 0; k < 3; k++) scarf[k] = UiArc.Create(scarfRoot, "Scarf arc " + (k + 1), 33, 3.2f, from[k], from[k] + 90, Scarf[k]);
                heart = Img("Scarf heart", Ui.Circle, 8, Scarf[2]);
                heart.transform.SetParent(scarfRoot, false);
                pointer[0] = Stroke(scarfRoot, "Pointer left", new Vector2(-6, 34), new Vector2(0, 39));
                pointer[1] = Stroke(scarfRoot, "Pointer right", new Vector2(0, 39), new Vector2(6, 34));
                foreach (var pt in pointer) Ui.Exempt(pt.gameObject, UiLayoutRule.Decoration, "D2b current pointer (1.8 px chevron over the scarf arcs and heart, which mark current on their own)");
                float[] accent = { -65, -185, -305 };
                for (int k = 0; k < 3; k++) accents[k] = UiArc.Create(Glyph, "Focus accent " + (k + 1), 35, 2, accent[k], accent[k] + 40, Cream);
                SetFocus(false);
            }

            Image Img(string name, Sprite sprite, float size, Color color, Vector2 at = default)
            {
                var im = Ui.MakeImage(Glyph, color, sprite, name);
                im.raycastTarget = false;
                var r = im.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = at;
                return im;
            }

            static Image Stroke(RectTransform parent, string name, Vector2 a, Vector2 b)
            {
                var im = Strip(parent, name, 1.8f, Cream);
                var d = b - a;
                im.rectTransform.anchoredPosition = (a + b) / 2;
                im.rectTransform.sizeDelta = new Vector2(d.magnitude + 1, 1.8f);
                im.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                return im;
            }

            public void Apply(bool cleared, int best, bool isCurrent, bool perfect)
            {
                if (best < 0 || best > StageSelectModel.MaxStars) throw new ArgumentOutOfRangeException(nameof(best), best, "0..3");
                current = isCurrent;
                star.color = cleared ? Gold : Cream;
                star.rectTransform.sizeDelta = Vector2.one * (cleared ? StarCleared : StarUntouched);
                centre.enabled = !cleared;
                rim.color = cleared ? WithAlpha(Gold, .45f) : WithAlpha(Dim, .52f);
                for (int k = 0; k < 3; k++) sparks[k].enabled = sparkGlows[k].enabled = cleared && k < best;
                scarfRoot.gameObject.SetActive(isCurrent);
                aureole.enabled = aureoleArc.enabled = perfect;
                haloColour = isCurrent ? Scarf[0] : cleared ? Gold : Cream;
                haloAlpha = isCurrent ? .95f : cleared ? .70f : .43f;
                halo.color = WithAlpha(haloColour, haloAlpha);
                State = (cleared ? "cleared " + best : "untouched") + (isCurrent ? " current" : "") + (perfect ? " perfect" : "");
                Rect.gameObject.name = $"Stage {slot + 1} {State}";
            }

            public void SetFocus(bool on)
            {
                foreach (var a in accents) a.enabled = on;
            }

            public void Release(bool reduce)
            {
                if (!reduce) scale = 1.09f; // the rebound, then it settles
            }

            /// <param name="immediate">Reduce motion: the scale jumps.</param>
            /// <param name="still">No pulse or twinkle (reduce motion, or a touch is held).</param>
            public void Animate(float target, float dt, float time, bool immediate, bool still)
            {
                scale = immediate || dt <= 0 ? target : Mathf.Lerp(scale, target, 1 - Mathf.Exp(-dt / 0.05f));
                Glyph.localScale = Vector3.one * scale;
                float glow = haloAlpha;
                if (!still)
                {
                    glow += .10f * Mathf.Sin(time * Mathf.PI * 2 / twinkle + slot);
                    if (current)
                    {
                        float k = .5f - .5f * Mathf.Cos(time * Mathf.PI * 2 / 2.4f); // eased 2.4 s cycle
                        scarfRoot.localScale = Vector3.one * Mathf.Lerp(1f, 35f / 33f, k);
                        float a = Mathf.Lerp(.7f, 1f, k);
                        for (int i = 0; i < 3; i++) scarf[i].color = WithAlpha(Scarf[i], a);
                        glow = haloAlpha * Mathf.Lerp(.7f, 1f, k);
                    }
                }
                else if (current) scarfRoot.localScale = Vector3.one;
                halo.color = WithAlpha(haloColour, Mathf.Clamp01(glow));
            }
        }
    }
}
