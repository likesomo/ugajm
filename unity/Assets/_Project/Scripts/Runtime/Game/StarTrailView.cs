using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SevenSteps.Game
{
    /// <summary>
    /// Star trail view (design/art/star-trail.md, docs/architecture.md 15). On every board draw it works the trail out from
    /// the State (<see cref="StarTrail.Of"/>) and
    /// - fills the global board texture _StarTrailMap and its globals (15.1-15.3, 15.5) for the shared tile shader (art):
    ///   the final look, zero extra draws;
    /// - draws the same marks with an interim combined quad mesh (one draw, 15.5 interim render): body-coloured rims and
    ///   lines, small stars for steps 1-6 at the cell corners (the centre 40 % stays clear for numbers), the Big Dipper
    ///   glyph on the 7th step's cell and a star on each statue. Texture made in code, built-in Sprites/Default shader,
    ///   alpha blended so overlaps never add up past a mark's own brightness. Turned off once the art shader draws the map.
    /// Sizes, opacities and seconds come from the chapter's <see cref="StarTrailPreset"/> (empty = Default).
    /// </summary>
    public sealed class StarTrailView : MonoBehaviour
    {
        // Interim quad look; the preset gives the numbers it shares with the shader.
        const float Lift = 0.012f;                 // above the tile top, in cells
        const float StarSlot = 0.33f;              // corner slot distance from the centre, in cells
        const float RimAlpha = 0.55f, GlyphAlpha = 0.8f, StoneAlpha = 0.8f;
        const float LineTrim = 0.22f;              // lines stop outside the centre 40 % (numbers)
        const float RimInset = 0.07f;              // each further body's rim sits this much further in
        const float GlyphSize = 0.6f;
        // Ring of seven stars at the walking body's feet (steps left; star 7 pulses after the 6th step).
        const float RingLift = 0.1f, RingRadius = 0.46f, RingSize = 0.2f, RingLastSize = 0.28f, RingSpentSize = 0.14f, RingAlpha = 0.85f, RingSpentAlpha = 0.6f;
        const float AppearSeconds = 0.18f, FellDim = 0.35f;
        /// Quad size per core diameter: the star texture's core is about a sixth of the quad (the rest is rays and glow).
        const float StarQuadPerCore = 6f;
        static readonly Color StarColor = new Color(1f, 0.93f, 0.74f);
        static readonly Color GlyphColor = new Color(1f, 0.86f, 0.55f);

        /// The star texture's core is this opaque; star alpha divides by it so the core reaches the preset opacity.
        const float CoreOpacity = 0.6f;

        /// Corner slots first (TL, TR, BR, BL as seen from the camera), then edge middles; more stars wrap around.
        static readonly Vector2[] Slots =
        {
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
            new Vector2(0, 1.1f), new Vector2(1.1f, 0), new Vector2(0, -1.1f), new Vector2(-1.1f, 0),
        };

        sealed class Item
        {
            public TrailMark Mark;
            public Color Color;          // rgb + full alpha
            public float From, To, Start, Duration; // alpha factor tween
            public float Pulse;          // extra brightness for the 7th step's awakening / the finale, over Duration
            public bool Leaving;
        }

        BoardGrid grid;
        TileSet set;
        Camera cam;
        StarTrailPreset preset;
        Mesh mesh;
        MeshRenderer rend;
        StarTrailMap map;
        Texture2D mapTexture;
        readonly List<Item> items = new List<Item>();
        readonly Dictionary<TrailMark, Item> byMark = new Dictionary<TrailMark, Item>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        // StepResults the session presented, so a body that ended inside a move keeps the cells it really took.
        readonly System.Runtime.CompilerServices.ConditionalWeakTable<State, StepResult> results =
            new System.Runtime.CompilerServices.ConditionalWeakTable<State, StepResult>();
        bool animating;
        // Shader globals animated by the view (15.1): newest-cell pulse, seventh awaken, Restart fade, finale.
        float newestStart = -1f, awakenStart = -1f, fadeStart = -1f, finale;
        StarTrail pendingRefill; // Restart: the old map fades out first, then this is written
        int currentBody, phase, used;
        bool lastStepPulse; // one step left: ring star 7 pulses (and _StarTrailPulse.z)

        /// Interim quad renderer on. Turn off when the art tile shader draws _StarTrailMap, or the trail is drawn twice
        /// (architect N2 on 26f01e0); the map and globals are written either way.
        public static bool UseQuad = true;
        /// Finale speed-up for dev runs (replay, smoke, flow check), set by GameController; 1 in play.
        public static float FinaleSpeed = 1f;

        static Material material;
        static Texture2D atlas;

        public int MarkCount => items.Count;
        public StarTrailPreset Preset => preset;
        /// The board data the shader reads (15.2), as last written.
        public StarTrailMap Map => map;

        /// <param name="style">The chapter's preset (ChapterPreset.starTrail); null = StarTrailPreset.Default.</param>
        public void Init(BoardGrid grid, TileSet set, Camera cam, StarTrailPreset style)
        {
            this.grid = grid;
            this.set = set;
            this.cam = cam;
            preset = style != null ? style : StarTrailPreset.Default;
            mesh = new Mesh { name = "Star Trail" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rend = gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = Material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            map = new StarTrailMap(grid.W, grid.H);
            mapTexture = new Texture2D(map.TextureWidth, map.TextureHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Star Trail Map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            StarTrailPreset.SetGlobals(preset);
            Shader.SetGlobalTexture(StarTrailMap.MapId, mapTexture);
            Shader.SetGlobalMatrix(StarTrailMap.WorldToCellId, StarTrailMap.WorldToCell(grid));
            var c = new Vector4[StarTrailMap.MaxBodies];
            for (int b = 0; b < c.Length; b++)
            {
                var col = set != null ? set.BodyColor(b) : Color.white;
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear;
                c[b] = new Vector4(col.r, col.g, col.b, 1f);
            }
            Shader.SetGlobalVectorArray(StarTrailMap.ColorsId, c);
            SetParams(1f);
            Shader.SetGlobalVector(StarTrailMap.PulseId, Vector4.zero);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            // Nothing to draw without the view (title, art review scenes): enabled = 0.
            if (map != null) Shader.SetGlobalVector(StarTrailMap.ParamsId, Vector4.zero);
            if (mapTexture != null) Destroy(mapTexture);
        }

        void SetParams(float fade) => Shader.SetGlobalVector(StarTrailMap.ParamsId, new Vector4(map.W, map.H, 1f, Mathf.Clamp01(fade)));

        void SetState() => Shader.SetGlobalVector(StarTrailMap.StateId, StarTrailMap.StateVector(currentBody, phase, finale, used));

        void Upload(StarTrail trail, bool newest)
        {
            trail.Write(map, newest);
            mapTexture.SetPixelData(map.Bytes, 0);
            mapTexture.Apply(false);
        }

        /// The session presented r (BoardView.Present): remember the cells it walked for <see cref="StarTrail.Of"/>.
        public void Remember(StepResult r)
        {
            if (r?.State == null) return;
            results.Remove(r.State);
            results.Add(r.State, r);
        }

        StepResult Known(State s) => results.TryGetValue(s, out var r) ? r : null;

        /// Draws the trail of s. animate (after Present): new marks light up, the 7th awakens, a fall's star dims, the
        /// newest cell pulses. Otherwise (Rebuild, Snap: Undo, Restart, replay) the picture snaps to s and marks s no
        /// longer has fade out (Undo: undoSeconds; Restart: restartSeconds, the shader map fades the same way first).
        public void Show(State s, bool animate)
        {
            var trail = StarTrail.Of(s, Known);
            // The ring at the feet is drawn in screen space by BoardMarksView (never behind a tile); the map still has the cell.
            var marks = trail.Marks().FindAll(m => m.Kind != TrailMarkKind.Ring);
            float now = Time.time;
            bool restart = s.Prev == null && items.Count > 0;
            var keep = new HashSet<TrailMark>(marks);

            currentBody = Mathf.Clamp(BodyIndex.Current(s), 0, StarTrailMap.MaxBodies - 1);
            phase = (int)s.Status;
            used = s.Used;
            lastStepPulse = trail.Ring && trail.Used == Rules.Steps - 1;
            finale = 0f;
            SetState();
            if (restart && !animate)
            {
                pendingRefill = trail; // afterglow: the old map fades, then the empty board is written
                fadeStart = now;
                animating = true;
            }
            else
            {
                pendingRefill = null;
                fadeStart = -1f;
                SetParams(1f);
                Upload(trail, animate);
            }
            newestStart = animate ? now : -1f;

            foreach (var it in items)
            {
                if (it.Leaving || keep.Contains(it.Mark)) continue;
                it.Leaving = true;
                Tween(it, Alpha(it), 0f, now, animate ? AppearSeconds : restart ? preset.restartSeconds : preset.undoSeconds);
            }

            var next = new List<Item>(marks.Count);
            foreach (var m in marks)
            {
                if (byMark.TryGetValue(m, out var it) && !it.Leaving) { next.Add(it); continue; }
                it = new Item { Mark = m, Color = ColorOf(m) };
                byMark[m] = it;
                if (!animate) Tween(it, 1f, 1f, now, 0f);
                else if (m.Dim) Tween(it, 1f, 1f, now, m.Kind == TrailMarkKind.Seventh ? preset.awakenSeconds : preset.failSeconds); // lit, then lets go
                else
                {
                    Tween(it, 0f, 1f, now, m.Kind == TrailMarkKind.Seventh ? preset.awakenSeconds : AppearSeconds);
                    if (m.Kind == TrailMarkKind.Seventh) { it.Pulse = 1f; awakenStart = now; }
                }
                next.Add(it);
            }
            // Leaving marks stay (drawn first) until they have faded.
            foreach (var it in items) if (it.Leaving && it.Duration > 0 && !next.Contains(it)) next.Insert(0, it);
            items.Clear();
            items.AddRange(next);
            byMark.Clear();
            foreach (var it in items) if (!it.Leaving) byMark[it.Mark] = it;
            if (newestStart >= 0 || awakenStart >= 0 || lastStepPulse) animating = true;
            Rebuild(now);
        }

        /// Win finale (finaleSeconds): each body's path brightens in turn and _StarTrailState.z rises 0 -> 1, so the three
        /// colours join into one constellation before the result. Enter, Space, a click or a tap skips to the end (N1).
        public IEnumerator Finale(float speed)
        {
            bool Skip()
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
                var touch = Touchscreen.current;
                return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
            }
            int bodies = 0;
            foreach (var it in items) bodies = Mathf.Max(bodies, it.Mark.Body + 1);
            float total = preset.finaleSeconds / Mathf.Max(speed * FinaleSpeed, 0.01f);
            float per = total / Mathf.Max(bodies + 1, 1);
            float start = Time.time;
            for (int b = 0; b < bodies; b++)
            {
                float now = Time.time;
                foreach (var it in items)
                    if (!it.Leaving && !it.Mark.Dim && it.Mark.Body == b && it.Mark.Kind != TrailMarkKind.Rim)
                    {
                        it.From = it.To; // keep its alpha, only pulse
                        it.Pulse = 1f; it.Start = now; it.Duration = per * 2f;
                    }
                animating = true;
                for (float t = 0; t < per; t += Time.deltaTime)
                {
                    finale = Mathf.Clamp01((Time.time - start) / total);
                    SetState();
                    yield return null;
                    if (Skip()) goto done;
                }
            }
            for (float t = 0; t < per; t += Time.deltaTime)
            {
                finale = Mathf.Clamp01((Time.time - start) / total);
                SetState();
                yield return null;
                if (Skip()) break;
            }
            done:
            finale = 1f;
            SetState();
        }

        void Tween(Item it, float from, float to, float now, float duration)
        {
            it.From = from; it.To = to; it.Start = now; it.Duration = duration;
            if (duration > 0) animating = true;
        }

        float Alpha(Item it)
        {
            float k = it.Duration > 0 ? Mathf.Clamp01((Time.time - it.Start) / it.Duration) : 1f;
            float a = Mathf.Lerp(it.From, it.To, k);
            // A fall's star dims as it lets go; a bridge's 7th star sinks (bridgeSink).
            if (it.Mark.Dim && it.Mark.Kind != TrailMarkKind.Ring) a *= Mathf.Lerp(1f, it.Mark.Kind == TrailMarkKind.Seventh ? 1f - 0.8f * preset.bridgeSink : FellDim, k);
            return a;
        }

        /// 0..1, about one beat a second: the last step's star breathing.
        static float RingPulse(float now) => 0.5f + 0.5f * Mathf.Sin(now * 2f * Mathf.PI / RingBeat);
        const float RingBeat = 0.9f;

        static float PulseNow(Item it)
        {
            if (it.Pulse <= 0 || it.Duration <= 0) return 0;
            float k = Mathf.Clamp01((Time.time - it.Start) / it.Duration);
            return it.Pulse * Mathf.Sin(k * Mathf.PI);
        }

        void Update()
        {
            if (!animating || mesh == null) return;
            float now = Time.time;
            bool still = false, dropped = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                bool done = it.Duration <= 0 || now - it.Start >= it.Duration;
                if (!done) { still = true; continue; }
                it.Pulse = 0;
                if (it.Leaving) { items.RemoveAt(i); dropped = true; }
            }

            // Shader globals: pulse (newest cell, seventh awaken) and the Restart afterglow.
            float newest = 0, awaken = 0;
            if (newestStart >= 0)
            {
                float k = (now - newestStart) / (2f * preset.awakenSeconds);
                if (k < 1) { newest = 1 - k; still = true; } else newestStart = -1;
            }
            if (awakenStart >= 0)
            {
                float k = (now - awakenStart) / Mathf.Max(preset.awakenSeconds, 0.001f);
                if (k < 1) { awaken = Mathf.Sin(k * Mathf.PI); still = true; } else awakenStart = -1;
            }
            float ring = lastStepPulse ? RingPulse(now) : 0f;
            if (lastStepPulse) still = true;
            Shader.SetGlobalVector(StarTrailMap.PulseId, new Vector4(newest, awaken, ring, 0));
            if (fadeStart >= 0)
            {
                float k = (now - fadeStart) / Mathf.Max(preset.restartSeconds, 0.001f);
                if (k < 1) { SetParams(1 - k); still = true; }
                else
                {
                    fadeStart = -1;
                    if (pendingRefill != null) { Upload(pendingRefill, false); pendingRefill = null; }
                    SetParams(1f);
                }
            }

            animating = still;
            if (dropped) Rebuild(now);
            else WriteColors();
        }

        static float StepT(int step) => (Mathf.Clamp(step, 1, 6) - 1) / 5f;
        /// Star quad size (cells) for step 1..6: the core grows dotSizeMin -> dotSizeMax of the tile.
        float StarSize(int step) => Mathf.Lerp(preset.dotSizeMin, preset.dotSizeMax, StepT(step)) * StarQuadPerCore;
        /// Star alpha for step 1..6 so the core reaches opacityMin -> opacityMax.
        float StarAlpha(int step) => Mathf.Lerp(preset.opacityMin, preset.opacityMax, StepT(step)) / CoreOpacity;

        // ---------- Mesh ----------

        void Rebuild(float now)
        {
            verts.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
            foreach (var it in items) AddQuad(it);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            rend.enabled = UseQuad && items.Count > 0;
        }

        void WriteColors()
        {
            colors.Clear();
            foreach (var it in items)
            {
                var c = ColorNow(it);
                colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
            }
            if (colors.Count == mesh.vertexCount) mesh.SetColors(colors);
        }

        Color ColorNow(Item it)
        {
            var c = it.Color;
            float gain = 1f + 0.25f * PulseNow(it);
            // The last step's ring star breathes between 60 % and full.
            if (lastStepPulse && it.Mark.Kind == TrailMarkKind.Ring && it.Mark.Aux == Rules.Steps && !it.Mark.Dim) gain = 0.6f + 0.4f * RingPulse(Time.time);
            c.a = Mathf.Clamp01(c.a * Alpha(it) * gain);
            return c;
        }

        void AddQuad(Item it)
        {
            var m = it.Mark;
            float cs = grid.CellSize;
            Vector3 center = grid.CellToWorld(m.Cell) + Vector3.up * (Lift * cs);
            Vector3 right = Vector3.right, fwd = Vector3.forward; // flat on the board
            float w, h;
            Rect uv;
            switch (m.Kind)
            {
                case TrailMarkKind.Rim:
                    w = h = 1f - 2f * RimInset * m.Aux;
                    uv = Region(m.Body % 3, 1);
                    break;
                case TrailMarkKind.Line:
                {
                    Vector3 a = grid.CellToWorld(m.Cell), b = grid.CellToWorld(m.Aux);
                    Vector3 d = b - a;
                    float len = d.magnitude / cs;
                    if (len < 0.001f) { w = h = 0; uv = Region(2, 0); break; }
                    right = d.normalized;
                    fwd = Vector3.Cross(right, Vector3.up);
                    center = (a + b) * 0.5f + Vector3.up * (Lift * 1.5f * cs);
                    w = Mathf.Max(0, len - 2f * LineTrim);
                    h = preset.lineWidth;
                    uv = Region(2, 0);
                    break;
                }
                case TrailMarkKind.Seventh:
                    center += Vector3.up * (Lift * cs);
                    w = h = GlyphSize;
                    uv = Region(1, 0);
                    break;
                case TrailMarkKind.Star:
                {
                    var slot = Slots[m.Aux % Slots.Length] * StarSlot;
                    center += new Vector3(slot.x, Lift * 2f, slot.y) * cs;
                    w = h = StarSize(m.Step);
                    uv = Region(0, 0);
                    break;
                }
                case TrailMarkKind.Ring:
                {
                    // Seven stars round the feet, star 1 at the top (away from the camera), clockwise.
                    float a = Mathf.PI * 0.5f - (m.Aux - 1) * 2f * Mathf.PI / Rules.Steps;
                    center += new Vector3(Mathf.Cos(a) * RingRadius, RingLift, Mathf.Sin(a) * RingRadius) * cs;
                    w = h = m.Dim ? RingSpentSize : m.Aux == Rules.Steps ? RingLastSize : RingSize;
                    uv = Region(0, 0);
                    break;
                }
                default: // StoneStar: faces the camera on top of the statue
                {
                    var v = set != null ? set.StatueFor(m.Body) : default;
                    float top = v.LabelOffset(false).y > 0.1f ? v.LabelOffset(false).y : 1f;
                    center = grid.CellToWorld(m.Cell) + v.offset * cs + Vector3.up * ((top + 0.08f) * cs);
                    if (cam != null) { right = cam.transform.right; fwd = cam.transform.up; }
                    else fwd = Vector3.up;
                    w = h = preset.statueStarSize * StarQuadPerCore * 0.5f;
                    uv = Region(0, 0);
                    break;
                }
            }
            Vector3 rx = right * (w * 0.5f * cs), fz = fwd * (h * 0.5f * cs);
            int i0 = verts.Count;
            verts.Add(center - rx - fz); uvs.Add(new Vector2(uv.xMin, uv.yMin));
            verts.Add(center - rx + fz); uvs.Add(new Vector2(uv.xMin, uv.yMax));
            verts.Add(center + rx + fz); uvs.Add(new Vector2(uv.xMax, uv.yMax));
            verts.Add(center + rx - fz); uvs.Add(new Vector2(uv.xMax, uv.yMin));
            var c = ColorNow(it);
            colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        Color ColorOf(TrailMark m)
        {
            Color body = set != null ? set.BodyColor(m.Body) : Color.white;
            switch (m.Kind)
            {
                case TrailMarkKind.Rim: body.a = RimAlpha; return body;
                case TrailMarkKind.Line: { var c = Color.Lerp(body, Color.white, 0.35f); c.a = preset.lineOpacity; return c; }
                case TrailMarkKind.Seventh: { var c = GlyphColor; c.a = GlyphAlpha; return c; }
                case TrailMarkKind.Star: { var c = StarColor; c.a = Mathf.Clamp01(StarAlpha(m.Step)); return c; }
                case TrailMarkKind.Ring:
                {
                    // Lit = a step left, in the star colour; spent = the brother's colour, faint.
                    var c = m.Dim ? body : StarColor;
                    c.a = m.Dim ? RingSpentAlpha : RingAlpha;
                    return c;
                }
                default: { var c = StarColor; c.a = StoneAlpha; return c; }
            }
        }

        // ---------- Shared material and atlas (made in code, no image asset) ----------

        const int Cell = 128, Cols = 4, Rows = 2;

        /// Atlas cell (col, row) with a 4 px inset, so mipmaps do not bleed between cells.
        static Rect Region(int col, int row)
        {
            float pw = 1f / (Cell * Cols), ph = 1f / (Cell * Rows);
            return Rect.MinMaxRect((col * Cell + 4) * pw, (row * Cell + 4) * ph, ((col + 1) * Cell - 4) * pw, ((row + 1) * Cell - 4) * ph);
        }

        static Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "Star Trail", mainTexture = Atlas, hideFlags = HideFlags.HideAndDontSave };
                material.renderQueue = 3000;
                return material;
            }
        }

        public static Texture2D Atlas
        {
            get
            {
                if (atlas != null) return atlas;
                atlas = new Texture2D(Cell * Cols, Cell * Rows, TextureFormat.RGBA32, true)
                {
                    name = "Star Trail Atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave,
                };
                var px = new Color32[Cell * Cols * Cell * Rows];
                Paint(px, 0, 0, StarAt);
                Paint(px, 1, 0, DipperAt);
                Paint(px, 2, 0, (x, y) => Mathf.Exp(-y * y / (2f * 0.3f * 0.3f)));
                for (int k = 0; k < 3; k++) { int breaks = k + 1; Paint(px, k, 1, (x, y) => RimAt(x, y, breaks)); }
                atlas.SetPixels32(px);
                atlas.Apply(true, true);
                return atlas;
            }
        }

        /// Fills atlas cell (col, row) with white and alpha f(x, y), x and y in -1..1 across the cell's inner area.
        static void Paint(Color32[] px, int col, int row, Func<float, float, float> f)
        {
            int stride = Cell * Cols;
            for (int j = 0; j < Cell; j++)
            for (int i = 0; i < Cell; i++)
            {
                float x = (i + 0.5f - 4) / (Cell - 8) * 2f - 1f, y = (j + 0.5f - 4) / (Cell - 8) * 2f - 1f;
                bool inside = i >= 4 && j >= 4 && i < Cell - 4 && j < Cell - 4;
                byte a = inside ? (byte)Mathf.RoundToInt(Mathf.Clamp01(f(x, y)) * 255f) : (byte)0;
                px[(row * Cell + j) * stride + col * Cell + i] = new Color32(255, 255, 255, a);
            }
        }

        /// Four-point star: a soft core (about a quarter of the quad wide) and thin rays.
        static float StarAt(float x, float y)
        {
            float r2 = x * x + y * y;
            float core = CoreOpacity * Mathf.Exp(-r2 / (2f * 0.11f * 0.11f)) + 0.4f * Mathf.Exp(-r2 / (2f * 0.3f * 0.3f));
            float rays = Mathf.Exp(-Mathf.Abs(x) / 0.025f) * Mathf.Exp(-Mathf.Abs(y) / 0.35f) + Mathf.Exp(-Mathf.Abs(y) / 0.025f) * Mathf.Exp(-Mathf.Abs(x) / 0.35f);
            float edge = Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 4f);
            return Mathf.Min(1f, core + 0.5f * rays) * edge;
        }

        /// The Big Dipper as a shallow engraving: bowl 1-2-3-4-1, handle 4-5-6-7 (star-trail.md).
        static readonly Vector2[] Dipper =
        {
            new Vector2(-0.80f, 0.10f), new Vector2(-0.74f, 0.62f), new Vector2(-0.20f, 0.66f), new Vector2(-0.16f, 0.16f),
            new Vector2(0.24f, -0.06f), new Vector2(0.55f, -0.24f), new Vector2(0.86f, -0.52f),
        };
        static readonly int[] DipperEdges = { 0, 1, 1, 2, 2, 3, 3, 0, 3, 4, 4, 5, 5, 6 };

        static float DipperAt(float x, float y)
        {
            var p = new Vector2(x, y);
            float dots = 0, lines = 0;
            foreach (var d in Dipper) dots = Mathf.Max(dots, Mathf.Exp(-(p - d).sqrMagnitude / (2f * 0.055f * 0.055f)));
            for (int e = 0; e < DipperEdges.Length; e += 2)
            {
                Vector2 a = Dipper[DipperEdges[e]], b = Dipper[DipperEdges[e + 1]];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                float dist = (p - (a + (b - a) * t)).magnitude;
                lines = Mathf.Max(lines, 0.45f * Mathf.Exp(-dist * dist / (2f * 0.018f * 0.018f)));
            }
            return Mathf.Max(dots, lines);
        }

        /// A rounded-square ring near the cell edge with 1, 2 or 3 breaks (colour is not the only cue for the body).
        static float RimAt(float x, float y, int breaks)
        {
            const float half = 0.9f, radius = 0.18f, width = 0.03f;
            float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
            float ring = Mathf.Exp(-outside * outside / (2f * width * width));
            float turn = (Mathf.Atan2(y, x) / (2f * Mathf.PI) + 1.25f) % 1f; // 0 at the top, clockwise
            for (int b = 0; b < breaks; b++)
            {
                float d = Mathf.Abs(Mathf.Repeat(turn - (b + 0.5f) / breaks + 0.5f, 1f) - 0.5f);
                if (d < 0.025f) return 0;
            }
            return ring;
        }
    }
}
