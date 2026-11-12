using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// A planet's surface stage path (view only; IMPLEMENT.md "Planet surface stage path", v3 surface captures): the body
    /// seated large in the middle, one place per stage on the crust joined by alternating footprints. Cleared = a lit star
    /// in the ground, current = three scarf-colour dots, untouched = a dark ground mark. Invisible targets of at least 80
    /// units follow the projected places (hidden when a place turns away). The body turns slowly while idle (about 6°/s,
    /// swinging so the path stays in front) and pauses while a place is focused or touched; dragging turns it.
    /// Input: hover / click, left / right along the path, up / down nearest place, Enter plays, touch tap; Esc and the back chevron close
    /// (the root's OverlayStack).
    /// </summary>
    public sealed class PlanetSurfaceView : IOverlay
    {
        /// The U5 constellation frames the body larger (d2-refine: orthographic 3.9, centred).
        public const float ConstellationOrthoSize = 3.9f;
        const float Target = 80f, OrthoSize = 5.5f, IdleAmplitude = 14f, IdleSpeed = 0.43f, BaseYaw = -8f, DragDegreesPerUnit = 0.25f;
        static readonly Color Gold = new Color(0.95f, 0.76f, 0.31f);

        readonly PlanetSpace space;
        readonly PlanetBodySet set;
        readonly IReadOnlyList<PlanetEntry> planets;
        readonly Action<bool> mapBodies;
        readonly GameObject panel;
        readonly RectTransform rect;
        readonly CanvasGroup group;
        readonly Text title, step;
        readonly Image ring;
        readonly List<Button> targets = new List<Button>();
        readonly Transform root;
        readonly IPlanetSurfaceArt art;
        /// The U5 stage constellation (D2b); null = the v4 crust places and footprints.
        readonly ConstellationView stars;
        readonly GameObject titlePlate;
        int pressed = -1, wired;
        bool dragCancelled;
        Vector2 fittedLayer;

        Localizer loc;
        StageSelectModel model;
        int planet = -1, focused = -1, hovered = -1;
        bool armed, pending, dragging, dragged;
        PlanetBody body;
        GameObject marks;
        Vector3[] places = new Vector3[0], normals = new Vector3[0];
        float idleT, dragYaw, fittedAspect, navAt;

        /// Planet, slot.
        public event Action<int, int> PlaceChosen;
        public event Action CloseRequested;

        public bool Visible => panel.activeSelf;
        /// The surface's own back chevron (scene-flow's U5 map controls hide it and press it, so its guard stays one place).
        public Button Back => back;
        readonly Button back;
        public int Planet => planet;
        /// The seated body on show (null while closed).
        public PlanetBody Body => body;
        /// A place's target (-titlecheck, tests; read only).
        public Button Place(int slot) => targets[slot];
        /// The U5 constellation (null without it).
        public ConstellationView Constellation => stars;
        /// Steady glows and immediate state changes on the constellation (settings reduce motion).
        public bool ReduceMotion { set { if (stars != null) stars.ReduceMotion = value; } }

        /// <param name="mapBodies">Shows / hides the map's bodies (the surface body takes their space while open).</param>
        /// <param name="art">GameAssets.PlanetSurfaceArt: the V7 paintings projected on the body (S2); null, or a planet
        /// without one, = the current body.</param>
        /// <param name="constellation">U5 (D2b Orbit sparks, user choice 2026-11-12): the stages as a constellation of star
        /// nodes around a larger body with one caption line, instead of the crust places, footprints and the planet name plate.</param>
        public PlanetSurfaceView(Transform uiRoot, PlanetSpace space, PlanetBodySet set, IReadOnlyList<PlanetEntry> planets, Action<bool> mapBodies,
            IPlanetSurfaceArt art = null, bool constellation = false)
        {
            this.art = art;
            if (uiRoot == null) throw new ArgumentNullException(nameof(uiRoot));
            this.space = space ?? throw new ArgumentNullException(nameof(space));
            this.set = set != null ? set : throw new ArgumentNullException(nameof(set));
            this.planets = planets ?? throw new ArgumentNullException(nameof(planets));
            this.mapBodies = mapBodies ?? throw new ArgumentNullException(nameof(mapBodies));
            if (set.marksMaterial == null) throw new MissingReferenceException($"[SevenSteps] {set.name}: marksMaterial is not set");

            root = new GameObject("Surface").transform;
            root.SetParent(space.World, false);
            root.gameObject.SetActive(false);

            panel = Ui.Rect("PlanetSurface", uiRoot).Stretch().gameObject;
            rect = (RectTransform)panel.transform;
            group = panel.AddComponent<CanvasGroup>();
            // Drag anywhere off a place turns the body (also a touch target so a tap off a place does nothing).
            var drag = Ui.MakeImage(rect, new Color(0, 0, 0, 0), null, "Drag");
            drag.rectTransform.Stretch();
            drag.raycastTarget = true;
            var dragTrigger = drag.gameObject.AddComponent<EventTrigger>();
            On(dragTrigger, EventTriggerType.BeginDrag, _ => dragging = true);
            On(dragTrigger, EventTriggerType.Drag, e => { if (e is PointerEventData p) { dragYaw -= p.delta.x * DragDegreesPerUnit * 1280f / Mathf.Max(1, Screen.width); dragged = true; } });
            On(dragTrigger, EventTriggerType.EndDrag, _ => dragging = false);

            if (constellation) stars = new ConstellationView(rect);

            ring = Ui.MakeImage(rect, Gold, Ui.RingShape, "FocusRing");
            ring.raycastTarget = false;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(72, 72);
            ring.gameObject.SetActive(false);

            // The planet name and the place's stage on plates (14.4: text over the starry sky, architect note 2026-11-12),
            // each sized to its words (PlateFit).
            var titleImage = Ui.Plate(rect, "Planet plate");
            titlePlate = titleImage.gameObject;
            titleImage.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(200, 44));
            title = Ui.Label(titleImage.transform, "", 26, Gold, TextAnchor.MiddleCenter, "Planet");
            title.rectTransform.Stretch();
            title.raycastTarget = false;
            var stepPlate = Ui.Plate(rect, "Step plate");
            // U5: the one caption line centred at y 670 (d2-refine), the sole stage number and best score.
            stepPlate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, constellation ? 29 : 40), new Vector2(160, 40));
            step = Ui.Label(stepPlate.transform, "", 23, Gold, TextAnchor.MiddleCenter, "Step");
            step.rectTransform.Stretch();
            step.raycastTarget = false;
            stepPlate.gameObject.SetActive(false);
            if (constellation) titlePlate.SetActive(false); // back + settings only over the sky (D2b)
            if (constellation) step.color = new Color32(238, 231, 208, 255); // the caption in cream (d2-refine)
            back = PlanetMap3DView.BackChevron(rect, Gold, () => { if (!pending) CloseRequested?.Invoke(); });

            panel.AddComponent<ViewTicker>().Tick = Tick;
            panel.SetActive(false);
        }

        static void On(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> act)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(d => act(d));
            trigger.triggers.Add(e);
        }

        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            step.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            if (model != null && planet >= 0) { title.text = planets[planet].Name(loc); PlateFit(title); Refresh(); }
        }

        /// Fills the view for a planet (call before Show; the root pushes it on the OverlayStack).
        /// <param name="news">The planet's clears not shown yet (ClearNews, taken by the controller): the constellation replays
        /// them from their before state (first-clear trace). Null = none (UiLayoutCheck, tests).</param>
        public void Bind(StageSelectModel m, int p, IReadOnlyList<ClearNote> news = null)
        {
            model = m ?? throw new ArgumentNullException(nameof(m));
            if (loc == null) throw new InvalidOperationException("PlanetSurfaceView.Bind before Apply");
            if (p < 0 || p >= planets.Count || p >= m.PlanetCount) throw new ArgumentOutOfRangeException(nameof(p), p, "planet");
            planet = p;
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            UnityEngine.Texture painting = null;
            UnityEngine.Material material = null;
            var hasProjection = art != null && art.TryGetSurface(planets[p].Key, out painting, out material);
            // The seated mesh has a flat summit cut for a board. That cap shears the tops of the painted
            // menhirs/crystals/spires at one horizontal height, so projected artwork needs the full round body.
            body = PlanetBody.Create(set, planets[p].Key, p, root, seated: !hasProjection);
            if (hasProjection)
                body.UseProjection(painting, material, PlanetMapLayout.Rotation, Quaternion.Euler(0, BaseYaw, 0));
            title.text = planets[p].Name(loc);
            PlateFit(title);
            int count = model.StageCount(p);
            if (stars != null)
            {
                places = new Vector3[0];
                stars.Bind(model, p, ConstellationFigure.For(planets[p].Key, count), planets[p].Stages, news);
                targets.Clear();
                for (int s = 0; s < stars.Count; s++)
                {
                    targets.Add(stars.Button(s));
                    if (s >= wired) { Wire(stars.Button(s), s); wired++; }
                }
            }
            else BuildPlaces();
            while (stars == null && targets.Count < count)
            {
                int slot = targets.Count;
                var hit = Ui.MakeImage(rect, new Color(1, 1, 1, 0), Ui.Circle, "Place " + (slot + 1));
                hit.raycastTarget = true;
                hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = hit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                hit.rectTransform.sizeDelta = new Vector2(Target, Target);
                var b = hit.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                Wire(b, slot);
                targets.Add(b);
            }
            if (stars == null) for (int k = 0; k < targets.Count; k++) targets[k].gameObject.SetActive(k < count);
            int current = model.CurrentSlot(p);
            focused = current >= 0 ? current : 0;
            hovered = -1;
            armed = false;
            idleT = 0;
            dragYaw = 0;
            dragged = false;
            pressed = -1;
            fittedLayer = new Vector2(-1, -1);
        }

        /// A place's input: click / Enter plays (on release; a drag off the press cancels it, D2b), select / hover / press
        /// feed focus and the constellation's states.
        void Wire(Button b, int slot)
        {
            b.onClick.AddListener(() =>
            {
                bool cancelled = dragCancelled;
                dragCancelled = false;
                if (!pending && planet >= 0 && !cancelled) PlaceChosen?.Invoke(planet, slot);
            });
            var trigger = b.gameObject.AddComponent<EventTrigger>();
            On(trigger, EventTriggerType.Select, _ => { focused = slot; Refresh(); });
            On(trigger, EventTriggerType.Move, _ => { armed = true; Refresh(); });
            On(trigger, EventTriggerType.PointerEnter, _ => { hovered = slot; Refresh(); });
            On(trigger, EventTriggerType.PointerExit, _ => { if (hovered == slot) hovered = -1; if (pressed == slot) pressed = -1; Refresh(); });
            On(trigger, EventTriggerType.PointerDown, _ => { hovered = slot; pressed = slot; dragCancelled = false; Refresh(); });
            On(trigger, EventTriggerType.PointerUp, _ => { if (pressed == slot) pressed = -1; Refresh(); });
            if (stars != null) On(trigger, EventTriggerType.BeginDrag, _ => { dragCancelled = true; pressed = -1; Refresh(); });
            // A drag ends with no click from the EventSystem (on release it sends up, a click only when eligible, then end drag),
            // so the flag drops here: the next Enter / Submit plays (architect R1, 2ed1665).
            if (stars != null) On(trigger, EventTriggerType.EndDrag, _ => dragCancelled = false);
        }

        public void Show()
        {
            if (model == null) throw new InvalidOperationException("PlanetSurfaceView.Show before Bind");
            space.Enter();
            mapBodies(false);
            root.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            pending = false;
            ApplyOverlay();
            Frame();
            Turn();
            Tick();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(targets[focused].gameObject);
            Refresh();
        }

        public void Hide()
        {
            panel.SetActive(false);
            root.gameObject.SetActive(false);
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            body = null;
            marks = null;
            planet = -1;
            mapBodies(true);
        }

        /// Focuses a place as if chosen with the arrows (-1 = none) and runs one frame of placement (editor captures, tests).
        public void Preview(int slot)
        {
            armed = slot >= 0;
            if (slot >= 0) focused = slot;
            Tick();
            Refresh();
        }

        /// A stage choice from this view is pending (dive): overlay hidden, input off, the body stops turning.
        public void SetPending(bool on)
        {
            pending = on;
            ApplyOverlay();
        }

        void ApplyOverlay()
        {
            group.alpha = pending ? 0 : 1;
            group.interactable = !pending;
            group.blocksRaycasts = !pending;
        }

        void Frame()
        {
            fittedAspect = space.Camera.aspect;
            float wide = PlanetMapLayout.RefW / PlanetMapLayout.RefH;
            float size = stars != null ? ConstellationOrthoSize : OrthoSize;
            space.Pose(root.position + (stars != null ? Vector3.zero : new Vector3(0, 0.5f, 0)), fittedAspect >= wide ? size : size * wide / fittedAspect);
        }

        /// v3 place positions on the front of the body (StageSelectJourneyV3.Place), on the crust and lifted a little.
        public static Vector3 PlaceDirection(int slot, int count)
        {
            if (count < 1 || slot < 0 || slot >= count) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"slot of {count}");
            float u = count == 1 ? 0.5f : slot / (float)(count - 1);
            float x = -2.7f + u * 5.4f, z = -1.9f + Mathf.Sin(u * Mathf.PI * 2) * .55f;
            float r = PlanetStoneMesh.Radius;
            return new Vector3(x, Mathf.Sqrt(Mathf.Max(.1f, r * r - x * x - z * z)) * PlanetStoneMesh.Squash + .10f, z).normalized;
        }

        void BuildPlaces()
        {
            int count = model.StageCount(planet);
            places = new Vector3[count];
            normals = new Vector3[count];
            for (int s = 0; s < count; s++)
            {
                var ground = body.Crust(PlaceDirection(s, count));
                var n = new Vector3(ground.x, ground.y / (PlanetStoneMesh.Squash * PlanetStoneMesh.Squash), ground.z).normalized;
                normals[s] = n;
                places[s] = ground + n * 0.07f;
            }
            var b = new MarkBuilder();
            int current = model.CurrentSlot(planet);
            var dark = new Color(.22f, .27f, .29f);
            for (int s = 0; s < count; s++)
            {
                var state = model.Place(planet, s);
                var pos = places[s];
                var n = normals[s];
                bool cleared = model.IsCleared(planet, s);
                b.Disc(pos, n, .34f, cleared ? new Color(.50f, .60f, .44f) : dark, 16);
                if (cleared) b.Star(pos + n * .02f, n, .27f, Gold);
                if (state == PlaceState.Current)
                {
                    b.Disc(pos + n * .012f, n, .42f, new Color(.43f, .48f, .34f), 16);
                    if (cleared) b.Star(pos + n * .03f, n, .27f, Gold);
                    var side = Vector3.Cross(n, Vector3.forward).normalized;
                    if (side.sqrMagnitude < 0.5f) side = Vector3.right;
                    for (int dot = 0; dot < 3; dot++) b.Disc(pos + n * .045f - side * (dot - 1) * .16f, n, .065f, PlanetMap3DView.Scarves[dot], 10);
                }
                if (s == 0) continue;
                for (int k = 1; k < 8; k++) // alternating footprints from the previous place, on the crust
                {
                    var g = body.Crust(Vector3.Lerp(places[s - 1], pos, k / 8f));
                    var gn = new Vector3(g.x, g.y / (PlanetStoneMesh.Squash * PlanetStoneMesh.Squash), g.z).normalized;
                    var across = Vector3.Cross(gn, (pos - places[s - 1]).normalized).normalized;
                    b.Disc(g + gn * .05f + across * ((k % 2 == 0 ? 1 : -1) * .045f), gn, .027f, s <= current || cleared ? Gold : new Color(.47f, .51f, .45f), 6);
                }
            }
            marks = b.Draw("Places and footprints", set.marksMaterial, body.transform);
        }

        void Turn()
        {
            if (body == null) return;
            float idle = dragged ? 0 : Mathf.Sin(idleT * IdleSpeed) * IdleAmplitude;
            body.transform.localRotation = Quaternion.Euler(0, BaseYaw + dragYaw + idle, 0);
        }

        void Refresh()
        {
            if (model == null || planet < 0 || loc == null) return;
            if (stars != null) { RefreshConstellation(); return; }
            int slot = hovered >= 0 ? hovered : armed ? focused : -1;
            bool show = slot >= 0 && !pending;
            step.transform.parent.gameObject.SetActive(show);
            ring.gameObject.SetActive(show);
            if (show) { step.text = $"{loc.Get("planets.stage")} {(planets[planet].Stages[slot] + 1):00}"; PlateFit(step); }
        }

        /// U5: the focused (or hovered) stage's one caption line, always shown; the nodes' focus and press states.
        void RefreshConstellation()
        {
            int slot = hovered >= 0 ? hovered : focused;
            bool show = slot >= 0 && slot < stars.Count && !pending;
            step.transform.parent.gameObject.SetActive(show);
            ring.gameObject.SetActive(false);
            stars.SetInput(focused, hovered, pressed);
            if (show) { step.text = Caption(slot); PlateFit(step); }
        }

        /// "03 · title · 최고 2" (d2-refine caption): the stage's number on this planet, its localized title, its best stars
        /// ("도전 전" / "Not cleared" when it is not cleared).
        public string Caption(int slot)
        {
            if (model == null || planet < 0) throw new InvalidOperationException("PlanetSurfaceView.Caption before Bind");
            int stage = planets[planet].Stages[slot];
            var text = LevelCatalog.TextOf(stage, loc.Current);
            string name = text != null && !string.IsNullOrEmpty(text.name) ? text.name : $"{loc.Get("planets.stage")} {stage + 1:00}";
            return model.IsCleared(planet, slot)
                ? loc.Format("ui5.surface.caption", slot + 1, name, model.Best(planet, slot))
                : loc.Format("ui5.surface.caption_new", slot + 1, name);
        }

        /// The text's plate (its parent) sized to the words: 16 on each side, 8 clear above and below the drawn glyphs (14.4).
        static void PlateFit(Text t)
        {
            var plate = (RectTransform)t.transform.parent;
            float w = t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, t.GetGenerationSettings(Vector2.zero)) / Mathf.Max(0.01f, t.pixelsPerUnit);
            plate.sizeDelta = new Vector2(Mathf.Ceil(w) + 32, U5.FocusPlate.PlateHeight(t, t.text, w, 8));
        }

        /// The planet name and the place's stage line (UiLayoutCheck, tests).
        public Text Title => title;
        public Text Step => step;

        void Tick()
        {
            if (body == null || model == null) return;
            if (!pending)
            {
                bool hold = dragging || (armed && focused >= 0) || hovered >= 0;
                if (!hold) idleT += Time.unscaledDeltaTime;
                if (!Mathf.Approximately(space.Camera.aspect, fittedAspect)) Frame();
                Turn();
            }
            if (stars != null) { TickConstellation(); return; }
            var toCamera = -space.Camera.transform.forward;
            var points = new Vector2[places.Length];
            for (int s = 0; s < places.Length; s++)
            {
                var world = body.transform.TransformPoint(places[s]);
                var c = space.ToCanvas(rect, world);
                points[s] = new Vector2(c.x, -c.y);
                var t = (RectTransform)targets[s].transform;
                t.anchoredPosition = c;
                bool facing = Vector3.Dot(body.transform.TransformDirection(normals[s]), toCamera) > 0.2f;
                if (targets[s].gameObject.activeSelf != facing) targets[s].gameObject.SetActive(facing);
            }
            int ringSlot = hovered >= 0 ? hovered : focused;
            if (ringSlot >= 0 && ringSlot < places.Length) ring.rectTransform.anchoredPosition = ((RectTransform)targets[ringSlot].transform).anchoredPosition;
            if (Time.unscaledTime < navAt) return;
            navAt = Time.unscaledTime + 0.25f;
            for (int s = 0; s < places.Length; s++)
            {
                int i = s;
                Selectable Pick(int dx, int dy)
                {
                    int j = StageSelectModel.Neighbor(points, i, dx, dy);
                    return j == i || !targets[j].gameObject.activeSelf ? null : targets[j];
                }
                targets[s].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = Pick(-1, 0), selectOnRight = Pick(1, 0), selectOnUp = Pick(0, -1), selectOnDown = Pick(0, 1),
                };
            }
        }

        /// U5: the constellation's animation; keyboard navigation from the fixed nodes, rebuilt only when the canvas changes.
        void TickConstellation()
        {
            stars.Tick(Time.unscaledDeltaTime);
            var size = stars.Layer.rect.size;
            if (size == fittedLayer) return;
            fittedLayer = size;
            var points = new Vector2[stars.Count];
            for (int s = 0; s < points.Length; s++) { var c = stars.Centre(s); points[s] = new Vector2(c.x, -c.y); }
            for (int s = 0; s < points.Length; s++)
            {
                int i = s;
                Selectable Pick(int dx, int dy) { int j = StageSelectModel.Neighbor(points, i, dx, dy); return j == i ? null : targets[j]; }
                targets[s].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = Pick(-1, 0), selectOnRight = Pick(1, 0), selectOnUp = Pick(0, -1), selectOnDown = Pick(0, 1) };
            }
        }

        /// Flat ground marks (discs, five-point stars) as one vertex-colour mesh (port of the review's PlanetBodyStudy marks).
        sealed class MarkBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int> t = new List<int>();
            readonly List<Color> c = new List<Color>();

            void Tri(Vector3 a, Vector3 b, Vector3 d, Color color)
            {
                int n = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                t.Add(n); t.Add(n + 1); t.Add(n + 2);
                var col = PlanetStoneMesh.Lin(color);
                c.Add(col); c.Add(col); c.Add(col);
            }

            public void Disc(Vector3 p, Vector3 normal, float radius, Color color, int sides)
            {
                var r = Quaternion.FromToRotation(Vector3.up, normal);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                    Tri(p, p + r * new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius), p + r * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), color);
                }
            }

            public void Star(Vector3 p, Vector3 normal, float radius, Color color)
            {
                var r = Quaternion.FromToRotation(Vector3.up, normal);
                for (int k = 0; k < 10; k++)
                {
                    float a = k * Mathf.PI / 5, b = (k + 1) * Mathf.PI / 5;
                    float ra = radius * (k % 2 == 0 ? 1 : .4f), rb = radius * ((k + 1) % 2 == 0 ? 1 : .4f);
                    Tri(p, p + r * new Vector3(Mathf.Cos(b) * rb, 0, Mathf.Sin(b) * rb), p + r * new Vector3(Mathf.Cos(a) * ra, 0, Mathf.Sin(a) * ra), color);
                }
            }

            public GameObject Draw(string name, Material material, Transform parent)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.SetColors(c);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go;
            }
        }
    }
}
