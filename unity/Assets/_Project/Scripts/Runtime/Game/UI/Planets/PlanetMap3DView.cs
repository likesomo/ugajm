using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>Calls <see cref="Tick"/> every frame while its object is active (code-built views without a MonoBehaviour).</summary>
    public sealed class ViewTicker : MonoBehaviour
    {
        public Action Tick;
        void Update() => Tick?.Invoke();
    }

    /// <summary>
    /// Stage select v4 star map (view only; IMPLEMENT.md "Planet map", approved v4 images): seven round planet bodies on the
    /// Big Dipper, star-trail lines between them lit as planets are completed, a row of small stars under each body (one
    /// per stage, lit = cleared; no numbers), the brothers' marker (three scarf-colour dots) on the last-played planet,
    /// unvisited planets dimmed, and the name + one-line tagline only for the hovered / focused planet, placed clear of the
    /// trail lines. The planet is the button (invisible target at least 96 units, the body's size). Back is the ‹ only.
    /// Input: mouse hover / click, arrows (left / right = story order, up / down = nearest that way; explicit uGUI
    /// navigation), Enter, touch tap; a held press (0.5 s) previews the name without entering. Esc is the root's.
    /// </summary>
    public sealed class PlanetMap3DView
    {
        const float MinTarget = 96f, StarSize = 14f, StarGap = 18f, HoldPreview = 0.5f, LightSeconds = 0.8f;
        static readonly Vector2 LabelSize = new Vector2(360, 64);
        static readonly Color DimTint = new Color(0.52f, 0.54f, 0.6f, 1f);
        /// Scarf colours of the three brothers (v3 current-place dots).
        public static readonly Color[] Scarves = { new Color(0.95f, 0.76f, 0.31f), new Color(0.6f, 0.82f, 0.85f), new Color(0.85f, 0.59f, 0.46f) };

        readonly PlanetSpace space;
        readonly IReadOnlyList<PlanetEntry> planets;
        readonly GameObject panel;
        readonly RectTransform rect;
        readonly CanvasGroup group;
        readonly PlanetBody[] bodies;
        readonly Button[] targets;
        readonly RectTransform[] starRows;
        readonly List<Image>[] stars;
        readonly RectTransform marker, label, trailLayer;
        readonly Text nameText, taglineText, backText;
        readonly List<(Image dim, Image lit)> lines = new List<(Image, Image)>();
        readonly Vector2[] centers;
        readonly float[] radii;
        readonly List<(Vector2 a, Vector2 b)> segments = new List<(Vector2, Vector2)>();

        Localizer loc;
        StageSelectModel model;
        int held = -1, focused = -1, hovered = -1, lighting = -1, pressPlanet = -1;
        bool labelArmed, pending, suppressClick;
        float lightStart, pressAt, fittedAspect;

        /// Map click / Enter / tap on a planet (index into the planets list).
        public event Action<int> PlanetChosen;
        public event Action BackRequested;
        /// The newly lit trail finished lighting (after the journey return).
        public event Action TrailLit;

        public bool Visible => panel.activeSelf;
        public Camera MapCamera => space.Camera;
        public PlanetBody Body(int planet) => bodies[planet];
        /// A planet's target (-titlecheck presses it; read only).
        public Button Target(int planet) => targets[planet];
        /// The planet whose label shows, -1 none (UiLayoutCheck, tests).
        public int LabelShown => label.gameObject.activeSelf ? LabelPlanet() : -1;

        public PlanetMap3DView(Transform uiRoot, PlanetSpace space, PlanetBodySet set, IReadOnlyList<PlanetEntry> planets)
        {
            if (uiRoot == null) throw new ArgumentNullException(nameof(uiRoot));
            this.space = space ?? throw new ArgumentNullException(nameof(space));
            if (set == null) throw new ArgumentNullException(nameof(set), "GameAssets.planetBodies is not set");
            this.planets = planets ?? throw new ArgumentNullException(nameof(planets));
            if (planets.Count == 0) throw new ArgumentException("no planets", nameof(planets));
            int n = planets.Count;

            bodies = new PlanetBody[n];
            for (int i = 0; i < n; i++)
            {
                bodies[i] = PlanetBody.Create(set, planets[i].Key, i, space.World, seated: false);
                bodies[i].transform.position = PlanetMapLayout.World(PlanetMapLayout.Reference(i, n));
                bodies[i].transform.localScale = Vector3.one * PlanetMapLayout.BodyScale;
            }

            panel = Ui.Rect("PlanetMap3D", uiRoot).Stretch().gameObject;
            rect = (RectTransform)panel.transform;
            group = panel.AddComponent<CanvasGroup>();
            trailLayer = Ui.Rect("Trails", rect).Stretch();
            centers = new Vector2[n];
            radii = new float[n];
            targets = new Button[n];
            starRows = new RectTransform[n];
            stars = new List<Image>[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var hit = Ui.MakeImage(rect, new Color(1, 1, 1, 0), Ui.Circle, "Planet " + (i + 1));
                hit.raycastTarget = true;
                var b = hit.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Click(idx));
                targets[i] = b;
                var trigger = hit.gameObject.AddComponent<EventTrigger>();
                On(trigger, EventTriggerType.Select, () => { focused = idx; Refresh(); });
                On(trigger, EventTriggerType.Move, () => { labelArmed = true; Refresh(); });
                On(trigger, EventTriggerType.PointerEnter, () => { hovered = idx; Refresh(); });
                On(trigger, EventTriggerType.PointerExit, () => { if (hovered == idx) hovered = -1; Refresh(); });
                On(trigger, EventTriggerType.PointerDown, () => { pressPlanet = idx; pressAt = Time.unscaledTime; hovered = idx; Refresh(); });
                On(trigger, EventTriggerType.PointerUp, () =>
                {
                    if (pressPlanet == idx && Time.unscaledTime - pressAt >= HoldPreview) suppressClick = true; // held: preview only
                    pressPlanet = -1;
                });
                starRows[i] = Ui.Rect("Stars " + (i + 1), rect);
                starRows[i].anchorMin = starRows[i].anchorMax = starRows[i].pivot = new Vector2(0.5f, 0.5f);
                stars[i] = new List<Image>();
            }
            marker = Ui.Rect("Brothers", rect);
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(40, 12);
            for (int k = 0; k < 3; k++)
            {
                var dot = Ui.MakeImage(marker, Scarves[k], Ui.Circle, "Scarf " + (k + 1));
                dot.raycastTarget = false;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.anchoredPosition = new Vector2((k - 1) * 13, 0);
                dot.rectTransform.sizeDelta = new Vector2(10, 10);
            }
            label = Ui.Rect("Label", rect);
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0.5f, 0.5f);
            label.sizeDelta = LabelSize;
            nameText = Ui.Label(label, "", 24, Ui.Gold, TextAnchor.MiddleCenter, "Name");
            nameText.rectTransform.TopLeft(0, 0, LabelSize.x, 34);
            nameText.raycastTarget = false;
            taglineText = Ui.Label(label, "", 16, new Color(0.78f, 0.84f, 0.85f), TextAnchor.MiddleCenter, "Tagline");
            taglineText.rectTransform.TopLeft(0, 36, LabelSize.x, 26);
            taglineText.raycastTarget = false;
            label.gameObject.SetActive(false);

            var back = Ui.MakeButton(rect, "‹", 44, () => { if (!pending && held < 0) BackRequested?.Invoke(); }, out backText, "Back");
            ((Image)back.targetGraphic).color = new Color(0, 0, 0, 0);
            ((RectTransform)back.transform).TopLeft(20, 16, 80, 80); // 80 units: IMPLEMENT.md minimum target
            backText.color = Ui.Gold;

            panel.AddComponent<ResizeHook>().Resized = () => { if (Visible) Layout(); };
            panel.AddComponent<ViewTicker>().Tick = Tick;
            panel.SetActive(false);
        }

        static void On(EventTrigger trigger, EventTriggerType type, Action act)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => act());
            trigger.triggers.Add(e);
        }

        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            nameText.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            taglineText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            backText.font = Ui.Theme.FontFor(Language.Ko, FontRole.Body); // the ‹ glyph: the Korean body font has it in every language
            if (Visible) Refresh();
        }

        /// Shows the map. <paramref name="heldPlanet"/> (JourneyReturn pending): its new trail stays unlit, the camera is
        /// scene-flow's and the overlay waits hidden until <see cref="LightTrail"/>.
        public void Show(StageSelectModel m, int focus, int heldPlanet)
        {
            model = m ?? throw new ArgumentNullException(nameof(m));
            if (loc == null) throw new InvalidOperationException("PlanetMap3DView.Show before Apply");
            if (model.PlanetCount != bodies.Length) throw new ArgumentException($"model has {model.PlanetCount} planets, the map {bodies.Length}", nameof(m));
            held = heldPlanet;
            lighting = -1;
            hovered = -1;
            labelArmed = false;
            space.Enter();
            SetBodiesVisible(true);
            if (held < 0) Frame();
            panel.SetActive(true);
            BuildStars();
            for (int i = 0; i < bodies.Length; i++) bodies[i].SetTint(model.Visited(i) ? Color.white : DimTint);
            BuildLines();
            ApplyOverlay();
            Layout();
            Select(focus);
        }

        /// The journey return settled: light <paramref name="planet"/>'s outgoing trail(s) over 0.8 s and focus <paramref name="focus"/>.
        public void LightTrail(StageSelectModel m, int planet, int focus)
        {
            model = m ?? throw new ArgumentNullException(nameof(m));
            held = -1;
            lighting = planet;
            lightStart = Time.unscaledTime;
            Frame();
            ApplyOverlay();
            Layout();
            Select(focus);
        }

        /// A stage choice is pending (dive): everything but the bodies hides and input stops; camera and bodies stay as they are.
        public void SetPending(bool on)
        {
            pending = on;
            ApplyOverlay();
        }

        /// The surface path is open over the map: the map bodies hide (the surface body takes the space).
        public void SetBodiesVisible(bool on)
        {
            foreach (var b in bodies) b.gameObject.SetActive(on);
        }

        /// Frames the camera on the map (not while scene-flow drives it).
        public void Frame()
        {
            if (held >= 0 || pending) return;
            fittedAspect = space.Camera.aspect;
            space.Pose(PlanetMapLayout.Target, PlanetMapLayout.OrthoFor(fittedAspect));
        }

        /// The settled map camera pose for this screen (scene-flow's pullback ends here).
        public void MapPose(out Vector3 position, out Quaternion rotation, out float orthoSize)
        {
            position = PlanetMapLayout.CameraPosition(PlanetMapLayout.Target);
            rotation = PlanetMapLayout.Rotation;
            orthoSize = PlanetMapLayout.OrthoFor(space.Camera.aspect);
        }

        public void Focus(int planet) => Select(planet);

        public void Hide()
        {
            panel.SetActive(false);
            space.Exit();
        }

        void Select(int planet)
        {
            focused = Mathf.Clamp(planet, 0, targets.Length - 1);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(targets[focused].gameObject);
            Refresh();
        }

        void Click(int planet)
        {
            if (suppressClick) { suppressClick = false; return; }
            if (pending || held >= 0 || lighting >= 0) return;
            PlanetChosen?.Invoke(planet);
        }

        void ApplyOverlay()
        {
            bool on = !pending && held < 0;
            group.alpha = on ? 1 : 0;
            group.interactable = on;
            group.blocksRaycasts = on;
            if (!on) hovered = -1;
        }

        void BuildStars()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                int count = model.StageCount(i);
                while (stars[i].Count < count)
                {
                    var s = Ui.Star(starRows[i], false, "Star " + (stars[i].Count + 1));
                    s.raycastTarget = false;
                    s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    s.rectTransform.sizeDelta = new Vector2(StarSize, StarSize);
                    stars[i].Add(s);
                }
                for (int k = 0; k < stars[i].Count; k++)
                {
                    bool used = k < count;
                    stars[i][k].gameObject.SetActive(used);
                    if (!used) continue;
                    stars[i][k].rectTransform.anchoredPosition = new Vector2((k - (count - 1) / 2f) * StarGap, 0);
                    bool lit = model.IsCleared(i, k);
                    Ui.SetStar(stars[i][k], lit);
                    if (!lit) stars[i][k].color = new Color(stars[i][k].color.r, stars[i][k].color.g, stars[i][k].color.b, 0.45f);
                }
            }
            marker.gameObject.SetActive(model.MarkerPlanet >= 0);
        }

        void BuildLines()
        {
            while (lines.Count < model.Trails.Count)
            {
                var dim = Ui.MakeImage(trailLayer, new Color(Ui.Dim.r, Ui.Dim.g, Ui.Dim.b, 0.55f), null, "Trail");
                var lit = Ui.MakeImage(dim.transform, Ui.Gold, null, "Lit");
                dim.raycastTarget = lit.raycastTarget = false;
                foreach (var r in new[] { dim.rectTransform, lit.rectTransform })
                {
                    r.pivot = new Vector2(0, 0.5f);
                }
                lit.rectTransform.anchorMin = lit.rectTransform.anchorMax = new Vector2(0, 0.5f);
                lit.rectTransform.anchoredPosition = Vector2.zero;
                lines.Add((dim, lit));
            }
            for (int k = 0; k < lines.Count; k++) lines[k].dim.gameObject.SetActive(k < model.Trails.Count);
        }

        /// Places the overlay over the bodies as the camera sees them now.
        void Layout()
        {
            if (model == null) return;
            for (int i = 0; i < bodies.Length; i++)
            {
                var (c, r) = space.ToCanvas(rect, bodies[i]);
                centers[i] = c;
                radii[i] = r;
                var t = (RectTransform)targets[i].transform;
                t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
                t.anchoredPosition = c;
                t.sizeDelta = Vector2.one * Mathf.Max(MinTarget, 2 * r);
                starRows[i].anchoredPosition = c + new Vector2(0, -(r + 14));
            }
            if (model.MarkerPlanet >= 0) marker.anchoredPosition = centers[model.MarkerPlanet] + new Vector2(0, radii[model.MarkerPlanet] + 12);
            segments.Clear();
            for (int k = 0; k < model.Trails.Count; k++)
            {
                var (a, b) = model.Trails[k];
                var (dim, lit) = lines[k];
                bool room = PlanetMapLayout.TrailSegment(centers[a], radii[a], centers[b], radii[b], 8, out var from, out var to);
                dim.gameObject.SetActive(room);
                if (!room) continue;
                segments.Add((from, to));
                var r = dim.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = from;
                float len = Vector2.Distance(from, to);
                r.sizeDelta = new Vector2(len, 3);
                r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                lit.rectTransform.sizeDelta = new Vector2(LitLength(k, len), 3);
            }
            // Arrows: left / right = story order, up / down = nearest that way on screen (y down for Neighbor).
            var points = new Vector2[centers.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(centers[i].x, -centers[i].y);
            for (int i = 0; i < targets.Length; i++)
            {
                Selectable Pick(int dx, int dy)
                {
                    int j = StageSelectModel.Neighbor(points, i, dx, dy);
                    return j == i ? null : targets[j];
                }
                targets[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = Pick(-1, 0), selectOnRight = Pick(1, 0), selectOnUp = Pick(0, -1), selectOnDown = Pick(0, 1),
                };
            }
            Refresh();
        }

        float LitLength(int edge, float len)
        {
            if (!model.TrailLit(edge, held)) return 0;
            if (lighting < 0 || model.Trails[edge].from != lighting) return len;
            float t = Mathf.Clamp01((Time.unscaledTime - lightStart) / LightSeconds);
            return len * t * t * (3 - 2 * t);
        }

        int LabelPlanet() => hovered >= 0 ? hovered : labelArmed ? focused : -1;

        /// Label for the hovered / focused planet.
        void Refresh()
        {
            if (model == null || loc == null) return;
            int p = LabelPlanet();
            bool show = p >= 0 && !pending && held < 0;
            label.gameObject.SetActive(show);
            if (!show) return;
            nameText.text = planets[p].Name(loc);
            string tag = planets[p].Tagline(loc);
            taglineText.text = tag;
            taglineText.gameObject.SetActive(!string.IsNullOrEmpty(tag));
            var bounds = new Rect(rect.rect.xMin + 24, rect.rect.yMin + 24, rect.rect.width - 48, rect.rect.height - 48);
            label.anchoredPosition = PlanetMapLayout.LabelSpot(centers[p], radii[p], LabelSize, 26, segments, bounds);
        }

        void Tick()
        {
            if (model == null) return;
            if (held < 0 && !pending && !Mathf.Approximately(space.Camera.aspect, fittedAspect)) { Frame(); Layout(); }
            if (lighting >= 0)
            {
                for (int k = 0; k < model.Trails.Count; k++)
                    if (model.Trails[k].from == lighting && lines[k].dim.gameObject.activeSelf)
                        lines[k].lit.rectTransform.sizeDelta = new Vector2(LitLength(k, lines[k].dim.rectTransform.sizeDelta.x), 3);
                if (Time.unscaledTime - lightStart >= LightSeconds)
                {
                    lighting = -1;
                    TrailLit?.Invoke();
                }
            }
            if (pressPlanet >= 0 && Time.unscaledTime - pressAt >= HoldPreview && !labelArmed) { labelArmed = true; Refresh(); }
        }
    }
}
