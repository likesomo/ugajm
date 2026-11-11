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
    /// Star trail view (design/art/star-trail.md "별 흔적 v2", docs/architecture.md 15). On every board draw it works the
    /// trail out from the State (<see cref="StarTrail.Of"/>) and
    /// - fills the global board texture _StarTrailMap and its globals (15.1-15.3, 15.5) for the shared tile shader (art):
    ///   the final look, zero extra draws;
    /// - draws the same marks with an interim combined quad mesh (one draw, 15.5 interim render), v2 states per brother:
    ///   Active (walking): brother-coloured beads on the cells walked, joined by straight lines centre to centre, breathing
    ///   gently (0.8 Hz, ±8 %); Fixed (KEEP): the path hardens into crystal stars (the planet's <see cref="StarTrailPreset.shapeMask"/>),
    ///   steady, and the statue carries a star; Passed (SWITCH): the same crystals fainter, and a two-colour knot where
    ///   the next brother sets off; the 7th step awakens as a large star; a fall's last star dims; a bridge's 7th sinks.
    ///   Every bead, line and star sits on a dark soft shadow so it reads on the near-white planets (frost, altar). A cell
    ///   two brothers share gets their rims inside each other (colour is not the only cue). Undo fades out only what the
    ///   state no longer has (undoSeconds); Restart fades all (restartSeconds). Texture made in code, built-in
    ///   Sprites/Default shader, alpha blended. Turned off once the art shader draws the map (<see cref="UseQuad"/>).
    /// Sizes, opacities, seconds, crystal shape and stone tint come from the chapter's <see cref="StarTrailPreset"/>.
    /// </summary>
    public sealed class StarTrailView : MonoBehaviour
    {
        // Interim quad look (cells); the preset gives the numbers it shares with the shader.
        const float Lift = 0.012f;                 // above the tile top, in cells
        const float StarSlot = 0.3f;               // a second step on a cell (mud, a revisit) sits off centre
        const float RimInset = 0.07f;              // each further body's rim sits this much further in
        const float RimAlpha = 0.6f;
        /// Bead quad per preset dot size (the dot size is the shader's core; the bead texture fills ~3/4 of its quad).
        const float BeadPerDot = 5.5f;
        const float CoreShare = 0.42f;             // white core of a bead / star, of its quad
        const float LineScale = 2.2f;              // interim line width per preset lineWidth
        const float ShadowScale = 1.7f;            // shadow quad per mark quad (lines: width)
        const float SeventhSize = 0.5f, KnotSize = 0.34f, KnotOffset = 0.1f;
        const float AppearSeconds = 0.18f, FellDim = 0.35f;
        /// v2 Active breathing: 0.8 Hz, ±8 %.
        const float BreathHz = 0.8f, BreathAmount = 0.08f;
        static readonly Color StarColor = new Color(1f, 0.93f, 0.74f);
        static readonly Color CoreColor = new Color(1f, 0.98f, 0.9f);
        static readonly Color ShadowColor = new Color(0.02f, 0.04f, 0.06f, 0.62f);

        /// Further stars on one cell (mud, a revisit): the first is at the centre, then corners and edge middles.
        static readonly Vector2[] Slots =
        {
            Vector2.zero,
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
            new Vector2(0, 1.1f), new Vector2(1.1f, 0), new Vector2(0, -1.1f), new Vector2(-1.1f, 0),
        };

        /// Parts of one mark, drawn in this order over the whole mesh: every shadow first, then the marks, then the cores.
        enum Part { Shadow, Main, Second, Core }

        sealed class Item
        {
            public TrailMark Mark;
            public float From, To, Start, Duration; // alpha factor tween
            public float Pulse;          // extra brightness for the 7th step's awakening / hardening / the finale, over Duration
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
        readonly List<(Item It, Part Part)> quads = new List<(Item, Part)>();
        readonly HashSet<int> sharedCells = new HashSet<int>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        // StepResults the session presented, so a body that ended inside a move keeps the cells it really took.
        readonly System.Runtime.CompilerServices.ConditionalWeakTable<State, StepResult> results =
            new System.Runtime.CompilerServices.ConditionalWeakTable<State, StepResult>();
        bool animating, breathing;
        // Shader globals animated by the view (15.1): newest-cell pulse, seventh awaken, Restart fade, finale.
        float newestStart = -1f, awakenStart = -1f, fadeStart = -1f, finale;
        StarTrail pendingRefill; // Restart: the old map fades out first, then this is written
        int currentBody, phase, used;
        bool lastStepPulse; // one step left: _StarTrailPulse.z

        /// Interim quad renderer on. Turn off when the art tile shader draws _StarTrailMap, or the trail is drawn twice
        /// (architect N2 on 26f01e0); the map and globals are written either way.
        public static bool UseQuad = true;
        /// Finale speed-up for dev runs (replay, smoke, flow check), set by GameController; 1 in play.
        public static float FinaleSpeed = 1f;
        /// Accessibility "reduce motion" (BoardView sets it before each Show): no breathing, no pulses; steady marks.
        public bool ReduceMotion { get; set; }

        static Material material;
        static Texture2D atlas;

        public int MarkCount => items.Count;
        /// Quads in the interim mesh (shadows, marks, cores; rims only on shared cells).
        public int QuadCount => quads.Count;
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

        /// The mark without its body's end state: the same footprint before and after KEEP / SWITCH (and Undo).
        static TrailMark Bare(TrailMark m) => new TrailMark(m.Kind, m.Body, m.Cell, m.Aux, m.Step, m.Dim);

        /// Draws the trail of s. animate (after Present): new marks light up, the 7th awakens, a fall's star dims, the
        /// newest cell pulses, a KEEP / SWITCH restyles the brother's path at once (with a short glint). Otherwise (Rebuild,
        /// Snap: Undo, Restart, replay) the picture snaps to s and marks s no longer has fade out (Undo: undoSeconds;
        /// Restart: restartSeconds, the shader map fades the same way first).
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

            // Marks that only changed state (Walking -> Kept / Switched, or back on Undo): restyled in place.
            var restyled = new HashSet<TrailMark>();
            var bare = new Dictionary<TrailMark, TrailMark>();
            foreach (var m in marks) if (!byMark.ContainsKey(m)) bare[Bare(m)] = m;
            foreach (var it in items)
            {
                if (it.Leaving || keep.Contains(it.Mark)) continue;
                if (bare.TryGetValue(Bare(it.Mark), out var now2) && !restart)
                {
                    restyled.Add(now2);
                    it.Leaving = true; it.Duration = 0; // replaced at once by its restyled mark
                    continue;
                }
                it.Leaving = true;
                Tween(it, Alpha(it), 0f, now, animate ? AppearSeconds : restart ? preset.restartSeconds : preset.undoSeconds);
            }

            var next = new List<Item>(marks.Count);
            foreach (var m in marks)
            {
                if (byMark.TryGetValue(m, out var it) && !it.Leaving) { next.Add(it); continue; }
                it = new Item { Mark = m };
                byMark[m] = it;
                if (restyled.Contains(m))
                {
                    Tween(it, 1f, 1f, now, 0f);
                    if (animate && !ReduceMotion) { it.Pulse = 0.6f; it.Start = now; it.Duration = 2f * preset.awakenSeconds; animating = true; }
                }
                else if (!animate) Tween(it, 1f, 1f, now, 0f);
                else if (m.Dim) Tween(it, 1f, 1f, now, m.Kind == TrailMarkKind.Seventh ? preset.awakenSeconds : preset.failSeconds); // lit, then lets go
                else
                {
                    Tween(it, 0f, 1f, now, m.Kind == TrailMarkKind.Seventh ? preset.awakenSeconds : AppearSeconds);
                    if (m.Kind == TrailMarkKind.Seventh && !ReduceMotion) { it.Pulse = 1f; awakenStart = now; }
                }
                next.Add(it);
            }
            // Leaving marks stay (drawn first) until they have faded.
            foreach (var it in items) if (it.Leaving && it.Duration > 0 && !next.Contains(it)) next.Insert(0, it);
            items.Clear();
            items.AddRange(next);
            byMark.Clear();
            foreach (var it in items) if (!it.Leaving) byMark[it.Mark] = it;

            // Rims only where brothers share a cell: two or three rims inside each other.
            sharedCells.Clear();
            var rimSeen = new HashSet<int>();
            foreach (var m in marks) if (m.Kind == TrailMarkKind.Rim && !rimSeen.Add(m.Cell)) sharedCells.Add(m.Cell);

            breathing = !ReduceMotion && marks.Exists(m => m.End == TrailEnd.Walking && (m.Kind == TrailMarkKind.Line || m.Kind == TrailMarkKind.Star));
            if (newestStart >= 0 || awakenStart >= 0 || lastStepPulse || breathing) animating = true;
            Rebuild();
        }

        /// Win finale (finaleSeconds): each body's path brightens in turn and _StarTrailState.z rises 0 -> 1, so the three
        /// colours join into one constellation before the result. Enter, Space, a click or a tap skips to the end (N1).
        /// Enter, Space, a click or a tap this frame: skips the win finale (and the finale line's hold, BoardView).
        public static bool SkipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
        }

        public IEnumerator Finale(float speed)
        {
            bool Skip() => SkipPressed();
            int bodies = 0;
            foreach (var it in items) bodies = Mathf.Max(bodies, it.Mark.Body + 1);
            float total = preset.finaleSeconds / Mathf.Max(speed * FinaleSpeed, 0.01f);
            float per = total / Mathf.Max(bodies + 1, 1);
            float start = Time.time;
            for (int b = 0; b < bodies; b++)
            {
                float now = Time.time;
                if (!ReduceMotion)
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
            if (it.Mark.Dim) a *= Mathf.Lerp(1f, it.Mark.Kind == TrailMarkKind.Seventh ? 1f - 0.8f * preset.bridgeSink : FellDim, k);
            return a;
        }

        /// 0..1, about one beat a second: the last step's pulse (_StarTrailPulse.z).
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
            bool still = breathing, dropped = false;
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
            float ring = lastStepPulse && !ReduceMotion ? RingPulse(now) : 0f;
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
            if (dropped) Rebuild();
            else WriteColors();
        }

        static float StepT(int step) => (Mathf.Clamp(step, 1, 6) - 1) / 5f;
        /// Bead quad size (cells) for step 1..6: grows with the preset dot size.
        float BeadSize(int step) => Mathf.Lerp(preset.dotSizeMin, preset.dotSizeMax, StepT(step)) * BeadPerDot;
        /// Bead alpha for step 1..6: the preset opacity, lifted so the interim beads read at 720p (lead feedback (d)).
        float BeadAlpha(int step) => Mathf.Clamp01(0.5f + Mathf.Lerp(preset.opacityMin, preset.opacityMax, StepT(step)));
        float LineAlpha => Mathf.Clamp01(0.45f + preset.lineOpacity);

        /// v2 state strength: active and kept full, passed fainter, a fall / vanish faint.
        static float EndStrength(TrailEnd e) => e switch
        {
            TrailEnd.Switched => 0.62f,
            TrailEnd.Fell => 0.55f,
            TrailEnd.Vanished => 0.55f,
            TrailEnd.Bridged => 0.75f,
            TrailEnd.Sealed => 0.75f,
            TrailEnd.Kept => 0.92f,
            _ => 1f,
        };

        /// Hardened (crystal) look: a finished path that stays on the board.
        static bool Crystal(TrailEnd e) => e == TrailEnd.Kept || e == TrailEnd.Switched || e == TrailEnd.Bridged || e == TrailEnd.Sealed;

        // ---------- Mesh ----------

        void Rebuild()
        {
            quads.Clear();
            foreach (var it in items) if (Has(it, Part.Shadow)) quads.Add((it, Part.Shadow));
            foreach (var it in items)
            {
                if (Has(it, Part.Main)) quads.Add((it, Part.Main));
                if (Has(it, Part.Second)) quads.Add((it, Part.Second));
            }
            foreach (var it in items) if (Has(it, Part.Core)) quads.Add((it, Part.Core));

            verts.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
            foreach (var (it, part) in quads) AddQuad(it, part);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            rend.enabled = UseQuad && quads.Count > 0;
        }

        bool Has(Item it, Part part)
        {
            var m = it.Mark;
            switch (m.Kind)
            {
                case TrailMarkKind.Rim: return part == Part.Main && sharedCells.Contains(m.Cell);
                case TrailMarkKind.Line: return (part == Part.Shadow || part == Part.Main) && m.Cell != m.Aux;
                case TrailMarkKind.Knot: return part != Part.Core;
                case TrailMarkKind.Star: return part != Part.Second && !(part == Part.Core && Crystal(m.End));
                default: return part != Part.Second; // Seventh, StoneStar
            }
        }

        void WriteColors()
        {
            colors.Clear();
            foreach (var (it, part) in quads)
            {
                var c = ColorNow(it, part);
                colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
            }
            if (colors.Count == mesh.vertexCount) mesh.SetColors(colors);
        }

        Color ColorNow(Item it, Part part)
        {
            var c = ColorOf(it.Mark, part);
            float gain = 1f + 0.4f * PulseNow(it);
            if (breathing && it.Mark.End == TrailEnd.Walking && part != Part.Shadow)
                gain *= 1f + BreathAmount * Mathf.Sin(Time.time * 2f * Mathf.PI * BreathHz);
            c.a = Mathf.Clamp01(c.a * Alpha(it) * (part == Part.Shadow ? 1f : gain));
            // Vertex colours are not converted by Sprites/Default: in a linear project they must be given in linear, or the
            // brother colours wash out to grey on the stone.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) { float a = c.a; c = c.linear; c.a = a; }
            return c;
        }

        /// The brother's colour, tinted toward the planet's stone (stoneTint, alpha = amount).
        Color Body(int body)
        {
            Color c = set != null ? set.BodyColor(body) : Color.white;
            var t = preset.stoneTint;
            return new Color(Mathf.Lerp(c.r, t.r, t.a), Mathf.Lerp(c.g, t.g, t.a), Mathf.Lerp(c.b, t.b, t.a), 1f);
        }

        Color ColorOf(TrailMark m, Part part)
        {
            float k = EndStrength(m.End);
            if (part == Part.Shadow) { var s = ShadowColor; s.a *= Mathf.Lerp(0.6f, 1f, k); return s; }
            if (part == Part.Core) { var w = CoreColor; w.a = 0.9f * k; return w; }
            Color body = Body(m.Body);
            switch (m.Kind)
            {
                case TrailMarkKind.Rim: body.a = RimAlpha * k; return body;
                case TrailMarkKind.Line: { var c = Color.Lerp(body, Color.white, 0.1f); c.a = LineAlpha * k; return c; }
                case TrailMarkKind.Seventh: { var c = Color.Lerp(body, StarColor, 0.35f); c.a = 0.95f * k; return c; }
                case TrailMarkKind.Star: { var c = Color.Lerp(body, Color.white, Crystal(m.End) ? 0.2f : 0.1f); c.a = BeadAlpha(m.Step) * k; return c; }
                case TrailMarkKind.Knot:
                {
                    // Two rings: this brother's and the next one's colours.
                    var c = Color.Lerp(Body(part == Part.Second ? m.Aux : m.Body), Color.white, 0.2f);
                    c.a = 0.95f;
                    return c;
                }
                default: { var c = StarColor; c.a = 0.95f; return c; } // StoneStar
            }
        }

        void AddQuad(Item it, Part part)
        {
            var m = it.Mark;
            float cs = grid.CellSize;
            Vector3 center = grid.CellToWorld(m.Cell) + Vector3.up * (Lift * cs);
            Vector3 right = Vector3.right, fwd = Vector3.forward; // flat on the board
            float w, h;
            Rect uv;
            bool shadow = part == Part.Shadow, core = part == Part.Core;
            switch (m.Kind)
            {
                case TrailMarkKind.Rim:
                    w = h = 1f - 2f * RimInset * m.Aux;
                    uv = Region(m.Body % 3, 1);
                    break;
                case TrailMarkKind.Line:
                {
                    // Centre to centre along the move (v2: straight lines, turns at the centres).
                    Vector3 a = grid.CellToWorld(m.Cell), b = grid.CellToWorld(m.Aux);
                    Vector3 d = b - a;
                    float len = d.magnitude / cs;
                    right = d.normalized;
                    fwd = Vector3.Cross(right, Vector3.up);
                    center = (a + b) * 0.5f + Vector3.up * (Lift * 1.5f * cs);
                    float lw = preset.lineWidth * LineScale;
                    w = len + (shadow ? lw * ShadowScale : lw);
                    h = shadow ? lw * ShadowScale * 1.6f : lw;
                    uv = Region(2, 0);
                    break;
                }
                case TrailMarkKind.Seventh:
                    center += Vector3.up * (Lift * 2f * cs);
                    w = h = SeventhSize * (shadow ? 1.25f : core ? CoreShare : 1f);
                    uv = shadow ? Region(3, 0) : Region(0, 0);
                    break;
                case TrailMarkKind.Star:
                {
                    var slot = Slots[m.Aux % Slots.Length] * StarSlot;
                    center += new Vector3(slot.x, Lift * 2f, slot.y) * cs;
                    float size = BeadSize(m.Step) * (Crystal(m.End) ? 1.15f : 1f);
                    w = h = size * (shadow ? ShadowScale : core ? CoreShare : 1f);
                    uv = shadow || core ? Region(3, 0) : Crystal(m.End) ? Region(Mathf.Clamp(preset.shapeMask, 0, 3), 2) : Region(3, 0);
                    break;
                }
                case TrailMarkKind.Knot:
                {
                    // Two interlocked rings across the cell (the baton seam), this brother's on the left.
                    center += Vector3.up * (Lift * 2.5f * cs);
                    if (part != Part.Shadow) center += Vector3.right * ((part == Part.Second ? KnotOffset : -KnotOffset) * cs);
                    w = h = shadow ? KnotSize * 1.9f : KnotSize;
                    uv = shadow ? Region(3, 0) : Region(3, 1);
                    if (shadow) w *= 1.2f;
                    break;
                }
                default: // StoneStar: faces the camera on top of the statue
                {
                    var v = set != null ? set.StatueFor(m.Body) : default;
                    float top = v.LabelOffset(false).y > 0.1f ? v.LabelOffset(false).y : 1f;
                    center = grid.CellToWorld(m.Cell) + v.offset * cs + Vector3.up * ((top + 0.08f) * cs);
                    if (cam != null) { right = cam.transform.right; fwd = cam.transform.up; }
                    else fwd = Vector3.up;
                    float size = Mathf.Max(preset.statueStarSize * 3f, 0.3f);
                    w = h = size * (shadow ? 1.2f : core ? CoreShare : 1f);
                    uv = shadow || core ? Region(3, 0) : Region(0, 0);
                    if (cam != null && !shadow) center -= cam.transform.forward * (0.01f * cs * (core ? 2 : 1)); // in front of its shadow
                    break;
                }
            }
            Vector3 rx = right * (w * 0.5f * cs), fz = fwd * (h * 0.5f * cs);
            int i0 = verts.Count;
            verts.Add(center - rx - fz); uvs.Add(new Vector2(uv.xMin, uv.yMin));
            verts.Add(center - rx + fz); uvs.Add(new Vector2(uv.xMin, uv.yMax));
            verts.Add(center + rx + fz); uvs.Add(new Vector2(uv.xMax, uv.yMax));
            verts.Add(center + rx - fz); uvs.Add(new Vector2(uv.xMax, uv.yMin));
            var c = ColorNow(it, part);
            colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        // ---------- Shared material and atlas (made in code, no image asset) ----------

        const int Cell = 128, Cols = 4, Rows = 3;

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

        /// Row 0: star, Big Dipper glyph (spare), line, bead (round). Row 1: rims with 1-3 breaks, knot ring. Row 2: the
        /// four crystal shapes of a hardened path (StarTrailPreset.shapeMask 0 diamond, 1 four-point, 2 hexagon, 3 shard).
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
                Paint(px, 3, 0, (x, y) => Shape(Mathf.Sqrt(x * x + y * y)));
                for (int k = 0; k < 3; k++) { int breaks = k + 1; Paint(px, k, 1, (x, y) => RimAt(x, y, breaks)); }
                Paint(px, 3, 1, (x, y) => { float r = Mathf.Sqrt(x * x + y * y) - 0.68f; return Mathf.Exp(-r * r / (2f * 0.085f * 0.085f)); });
                Paint(px, 0, 2, (x, y) => Shape(Mathf.Abs(x) + Mathf.Abs(y)));
                Paint(px, 1, 2, (x, y) => { float d = Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)); return Shape(d * d * 0.85f); });
                Paint(px, 2, 2, (x, y) => Shape(Mathf.Max(Mathf.Abs(x) * 0.866f + Mathf.Abs(y) * 0.5f, Mathf.Abs(y))));
                Paint(px, 3, 2, (x, y) => Shape(Mathf.Abs(x) / 0.62f + Mathf.Abs(y)));
                atlas.SetPixels32(px);
                atlas.Apply(true, true);
                return atlas;
            }
        }

        /// A solid shape whose edge is at distance 0.72 (in its own metric), with a soft glow outside.
        static float Shape(float d)
        {
            float t = Mathf.Clamp01((0.8f - d) / 0.12f);
            float solid = t * t * (3f - 2f * t);
            return Mathf.Max(solid, 0.35f * Mathf.Exp(-Mathf.Max(0, d - 0.7f) * Mathf.Max(0, d - 0.7f) / (2f * 0.12f * 0.12f)) * (d < 1f ? 1f : 0f));
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

        /// Four-point star: a solid core and thin rays.
        static float StarAt(float x, float y)
        {
            float r2 = x * x + y * y;
            float core = Mathf.Exp(-r2 / (2f * 0.14f * 0.14f)) + 0.4f * Mathf.Exp(-r2 / (2f * 0.3f * 0.3f));
            float rays = Mathf.Exp(-Mathf.Abs(x) / 0.04f) * Mathf.Exp(-Mathf.Abs(y) / 0.4f) + Mathf.Exp(-Mathf.Abs(y) / 0.04f) * Mathf.Exp(-Mathf.Abs(x) / 0.4f);
            float edge = Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 4f);
            return Mathf.Min(1f, core + 0.8f * rays) * edge;
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
            const float half = 0.9f, radius = 0.18f, width = 0.035f;
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
