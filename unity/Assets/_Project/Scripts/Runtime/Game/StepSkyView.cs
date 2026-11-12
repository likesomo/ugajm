using System;
using SevenSteps.Core;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// The step constellation (user decision 2026-11-12 "C", plan-2026-11-12 item 3): seven stars on a shallow arch in
    /// the sky above the board, lit one per step and joined by a line as the steps add up; after the 6th step the 7th
    /// star calls in gold (reduce motion: a steady double outline). Replaces the stars at the brother's feet.
    /// Screen space on the HUD's world label layer (like BoardMarksView, so HudView.Conceal hides it with the board marks),
    /// placed in the band between the HUD top band and the top of the playable cells: it is never drawn over a playable
    /// cell (<see cref="Layout"/> asserts it). View only: BoardView feeds it StepSky.Of(state) and the board box.
    /// </summary>
    public sealed class StepSkyView : MonoBehaviour
    {
        /// Star size range (canvas units at 1280 x 720; Readable = smallest size from the cell size on big boards), gaps and
        /// the arch rise. The star shrinks (down to MinStar) and the
        /// arch flattens before the constellation would touch the HUD top band.
        public const float Star = 26f, Readable = 20f, MinStar = 14f, Gap = 10f, Rise = 18f, PulseGrow = 1.3f, PulsePeriod = 1.4f, Lift = 0.6f;
        /// Bottom of the U5 HUD top band (pause, title, scarf dots, icon actions) in the 1280 x 720 reference, from the top
        /// (gameplay-ui 2026-11-12: keep the arc below y 80).
        public const float HudTopBand = 80f;
        const float Inner = 0.62f, Shadow = 1.3f, Line = 3f;
        static readonly Color EmptyFill = new Color(0.06f, 0.10f, 0.13f, 1f), EmptyRim = new Color(0.85f, 0.88f, 0.95f, 0.85f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.6f), LineColor = new Color(1f, 0.86f, 0.45f, 0.9f);

        /// Where the stars go. Points are star centres on the label layer (canvas units, centre origin, y up).
        public readonly struct Placement
        {
            public readonly Vector2[] Points;
            public readonly float Size;
            public Placement(Vector2[] points, float size) { Points = points; Size = size; }
            /// Lowest star edge (canvas y): stays above the playable cells.
            public float Bottom { get { float b = float.MaxValue; foreach (var p in Points) b = Mathf.Min(b, p.y); return b - Size * 0.5f; } }
            /// Highest star edge including the calling pulse.
            public float Top { get { float t = float.MinValue; foreach (var p in Points) t = Mathf.Max(t, p.y); return t + Size * PulseGrow * 0.5f; } }
        }

        /// Pure layout. <paramref name="playableTop"/>: highest canvas y of the playable cells (with the actors standing on
        /// them); <paramref name="centreX"/>: the board's centre; <paramref name="ceiling"/>: canvas y the stars stay
        /// under (the HUD top band); <paramref name="cellPx"/>: a cell's size on the canvas.
        /// Above the playable cells always wins: if the band is too thin even for flat MinStar stars, they sit just above
        /// the playable cells and may reach into the HUD band (never over the board).
        public static Placement Layout(float playableTop, float centreX, float ceiling, float cellPx) =>
            Layout(playableTop, centreX, ceiling, cellPx, new Vector2[Rules.Steps]);

        /// Same layout written into <paramref name="points"/> (length Rules.Steps), which the placement then holds: the
        /// view reuses one array, no allocation per layout (architect R1, 2026-11-12).
        public static Placement Layout(float playableTop, float centreX, float ceiling, float cellPx, Vector2[] points)
        {
            if (float.IsNaN(playableTop) || float.IsNaN(centreX) || float.IsNaN(ceiling)) throw new ArgumentException("NaN layout input");
            if (!(cellPx > 0)) throw new ArgumentOutOfRangeException(nameof(cellPx), cellPx, "positive");
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length != Rules.Steps) throw new ArgumentException($"{Rules.Steps} points", nameof(points));
            float margin = Mathf.Max(6f, 0.08f * cellPx);
            float size = Mathf.Clamp(0.34f * cellPx, Readable, Star);
            float rise = Rise;
            float floor = playableTop + margin; // star bottoms stay above this
            float room = ceiling - floor;       // band height for size * (1/2 + PulseGrow/2) + rise
            float need = size * (0.5f + 0.5f * PulseGrow) + rise;
            if (room < need) { rise = Mathf.Max(0f, room - size * (0.5f + 0.5f * PulseGrow)); need = size * (0.5f + 0.5f * PulseGrow) + rise; }
            if (room < need) size = Mathf.Max(MinStar, room / (0.5f + 0.5f * PulseGrow));
            // Lift into the free band (half the spare room, at most Lift cells): sky, not a rim on the wall.
            float spare = room - (size * (0.5f + 0.5f * PulseGrow) + rise);
            float baseY = floor + size * 0.5f + Mathf.Clamp(spare * 0.5f, 0f, Lift * cellPx);
            float step = size + Gap;
            for (int k = 0; k < Rules.Steps; k++)
            {
                float t = k / (Rules.Steps - 1f);
                points[k] = new Vector2(centreX + (t - 0.5f) * step * (Rules.Steps - 1), baseY + rise * Mathf.Sin(Mathf.PI * t));
            }
            return new Placement(points, size);
        }

        RectTransform layer;
        Func<Vector3, Vector2> toCanvas;
        readonly Image[] shadow = new Image[Rules.Steps], outer = new Image[Rules.Steps], inner = new Image[Rules.Steps];
        readonly Image[] links = new Image[Rules.Steps - 1];
        readonly Vector2[] points = new Vector2[Rules.Steps], bandPoints = new Vector2[Rules.Steps];
        Image calm;
        StepSky sky;
        bool reduceMotion;
        Bounds board;
        float cellPx = 77f;
        Placement placed;

        // What the drawn marks were built from: the marks are rewritten only when one of these changes (architect R1).
        bool drawnOn, dirty = true;
        StepSky drawnSky;
        bool drawnReduce;
        float drawnTop, drawnCx, drawnCeiling, drawnCell;

        /// The current sky and where its stars are (tests, captures).
        public StepSky Sky => sky;
        public bool Shown => outer[0] != null && outer[0].enabled;
        public Placement Placed => placed;
        public bool StarLit(int k) => inner[k].color == Ui.Gold;
        public bool LinkShown(int k) => links[k].enabled;
        /// Times the marks were rewritten (tests: a still frame rewrites nothing).
        public int Rebuilds { get; private set; }

        /// <param name="toCanvas">World point → anchored position on <paramref name="layer"/> (canvas units).</param>
        public void Init(RectTransform layer, Func<Vector3, Vector2> toCanvas)
        {
            this.layer = layer != null ? layer : throw new ArgumentNullException(nameof(layer));
            this.toCanvas = toCanvas ?? throw new ArgumentNullException(nameof(toCanvas));
            for (int k = 0; k < links.Length; k++) links[k] = Make($"Sky Link {k + 1}", null);
            for (int k = 0; k < Rules.Steps; k++)
            {
                shadow[k] = Make($"Sky Star {k + 1} Shadow", Ui.StarShape);
                outer[k] = Make($"Sky Star {k + 1}", Ui.StarShape);
                inner[k] = Make($"Sky Star {k + 1} Fill", Ui.StarShape);
            }
            calm = Make("Sky Star 7 Calm", Ui.StarShape);
            calm.transform.SetSiblingIndex(shadow[Rules.Steps - 1].transform.GetSiblingIndex());
            dirty = true;
            Refresh();
        }

        Image Make(string name, Sprite sprite)
        {
            var img = Ui.MakeImage(layer, Color.white, sprite, name);
            img.raycastTarget = false;
            img.enabled = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        /// <param name="playable">World box of the playable cells with the actors' height (BoardView).</param>
        public void Show(StepSky sky, Bounds playable, bool reduceMotion, float cellPx)
        {
            this.sky = sky;
            board = playable;
            this.reduceMotion = reduceMotion;
            this.cellPx = cellPx > 0 ? cellPx : 77f;
            Refresh();
        }

        public void Clear()
        {
            sky = default;
            Refresh();
        }

        void LateUpdate() => Refresh();

        /// The playable box projected now: its top (canvas y) and the board centre's x. Covers camera moves and screen size.
        void Project(out float top, out float cx)
        {
            top = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                var p = toCanvas(board.center + Vector3.Scale(board.extents,
                    new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                top = Mathf.Max(top, p.y);
            }
            cx = toCanvas(board.center).x;
        }

        float Ceiling => layer.rect.height * 0.5f - HudTopBand;

        /// The box the constellation takes on the HUD canvas (canvas units, top-left origin, the calling pulse and the star
        /// outlines included, grown by 8), or a zero rect when no sky is shown. UI that must not cover it avoids it
        /// (gameplay-ui: the KEEP / SWITCH choice, 2026-11-12).
        public Rect Band()
        {
            if (layer == null || !sky.Shown || board.size == Vector3.zero) return default;
            Project(out float top, out float cx);
            var p = Layout(top, cx, Ceiling, cellPx, bandPoints);
            float half = 0.5f * p.Size * Mathf.Max(PulseGrow, Shadow) + 8f;
            float x0 = p.Points[0].x - half, x1 = p.Points[Rules.Steps - 1].x + half;
            float w = layer.rect.width, h = layer.rect.height;
            return Rect.MinMaxRect(x0 + w * 0.5f, h * 0.5f - (p.Top + 8f), x1 + w * 0.5f, h * 0.5f - (p.Bottom - 8f));
        }

        static bool Same(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        void Refresh()
        {
            if (layer == null) return;
            bool on = sky.Shown && board.size != Vector3.zero;
            float top = 0f, cx = 0f, ceiling = 0f;
            if (on) { Project(out top, out cx); ceiling = Ceiling; }
            bool changed = dirty || on != drawnOn || (on && (!sky.Equals(drawnSky) || reduceMotion != drawnReduce
                || !Same(top, drawnTop) || !Same(cx, drawnCx) || !Same(ceiling, drawnCeiling) || !Same(cellPx, drawnCell)));
            if (changed)
            {
                dirty = false;
                drawnOn = on; drawnSky = sky; drawnReduce = reduceMotion;
                drawnTop = top; drawnCx = cx; drawnCeiling = ceiling; drawnCell = cellPx;
                Rebuild(on, top, cx, ceiling);
            }
            // While the 7th star calls, only its size changes per frame.
            if (on && sky.Calling && !reduceMotion)
            {
                float size = placed.Size * Mathf.Lerp(1f, PulseGrow, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / PulsePeriod));
                int k = Rules.Steps - 1;
                shadow[k].rectTransform.sizeDelta = Vector2.one * (size * Shadow);
                outer[k].rectTransform.sizeDelta = Vector2.one * size;
                inner[k].rectTransform.sizeDelta = Vector2.one * (size * Inner);
            }
        }

        void Rebuild(bool on, float top, float cx, float ceiling)
        {
            Rebuilds++;
            for (int k = 0; k < Rules.Steps; k++) shadow[k].enabled = outer[k].enabled = inner[k].enabled = on;
            for (int k = 0; k < links.Length; k++) links[k].enabled = on && k < sky.Links;
            calm.enabled = on && sky.Calling && reduceMotion;
            if (!on) return;

            placed = Layout(top, cx, ceiling, cellPx, points);
            for (int k = 0; k < links.Length; k++)
            {
                if (!links[k].enabled) continue;
                Vector2 a = points[k], b = points[k + 1], d = b - a;
                Put(links[k], (a + b) * 0.5f, new Vector2(d.magnitude, Line), LineColor, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            for (int k = 0; k < Rules.Steps; k++)
            {
                var p = points[k];
                float size = placed.Size;
                bool calling = sky.Calling && k == Rules.Steps - 1;
                bool lit = k < sky.Lit;
                Put(shadow[k], p, Vector2.one * (size * Shadow), ShadowColor);
                Put(outer[k], p, Vector2.one * size, lit || calling ? Ui.Gold : EmptyRim);
                Put(inner[k], p, Vector2.one * (size * Inner), lit ? Ui.Gold : EmptyFill);
                if (calling && calm.enabled) Put(calm, p, Vector2.one * (size * 1.6f), new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.7f));
            }
        }

        static void Put(Image img, Vector2 p, Vector2 rect, Color c, float angle = 0f)
        {
            var r = img.rectTransform;
            r.anchoredPosition = p;
            r.sizeDelta = rect;
            r.localRotation = Quaternion.Euler(0, 0, angle);
            img.color = c;
        }
    }
}
