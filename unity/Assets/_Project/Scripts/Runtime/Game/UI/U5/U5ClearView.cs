using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// U5 result (docs/ui-u5-kit.md 3, U5-result / U5-failure). A clear: the board stays as it is, both HUDs hide
    /// (<see cref="HudHidden"/>), three world star stones (UiIconSet.worldStarStone, 3D) stand just behind the exit (lit
    /// inlay = earned) and what each asks shows on hover / focus / hold through a transparent hit over it (exempt Board: the
    /// explanation of a world object; R1 on 1f90617); without the 3D stone, a 2D row above the board area. Next (56, on a
    /// round backing for 3:1) sits beside the board next to the exit, Map at the top left, Retry at the top right; the
    /// planet / journey line on a plate at the top centre when this clear ends one. A loss: no panel - one line on a plate
    /// and undo / restart (52, backed) at the bottom centre over the board as it ended. View only: events, the flow
    /// controller decides.
    /// </summary>
    public sealed class U5ClearView : MonoBehaviour, Flow.IResultPanel
    {
        public const float StoneSize = 40, StoneGap = 8, NextSize = 56, NextGap = 16, Safe = 24;
        /// Failure buttons (touch-targets.csv U5 failure: 52 at y 591, 80 apart).
        public const float FailSize = 52, FailTop = 591, FailPitch = 80;
        /// World stones: behind the exit by this many cells (0: over the exit cell itself), this far apart, about this tall,
        /// and floating this far above the brother's head (cells). Over the exit, not behind it: the cell behind often holds
        /// a wall, a statue or a waiting brother, and the stones would sink into or hide them.
        public const float WorldBehind = 0f, WorldSpacing = 0.6f, WorldHeight = 0.7f, WorldLift = 0.35f;
        public const int SortingOrder = 6;

        IconButton map, retry, next, undo, restart;
        readonly IconButton[] stones = new IconButton[Core.Stars.Max];
        readonly GameObject[] worldStones = new GameObject[Core.Stars.Max];
        readonly Renderer[] worldInlays = new Renderer[Core.Stars.Max];
        Image[] stoneBodies;
        RectTransform stoneRow, failGroup;
        Image finalePlate;
        Text finaleText, failText;
        FocusPlate plate;
        Localizer loc;
        UiIconSet icons;
        ResultModel shown;
        Coroutine reveal;

        public event Action NextRequested, RetryRequested, StageSelectRequested;
        /// The failure's undo / restart (the board's, as the current loss panel).
        public event Action UndoRequested, RestartRequested;
        /// true while a result is up (both HUDs hide), false when it goes.
        public event Action<bool> HudHidden;

        /// The brother on the exit (his head), canvas units, top-left origin (BoardView.BrotherOnCanvas after the win).
        public Func<Vector2> ExitPoint;
        /// The board area (BoardView.ProjectedArea): Next goes beside it.
        public Func<Rect> BoardArea;
        /// World stones (set by the scene root; any null: the 2D row): the brother on the exit (world), the board root they
        /// stand under, the board camera, the cell size.
        public Func<Vector3> ExitWorld;
        public Func<Transform> WorldParent;
        public Func<Camera> WorldCamera;
        public Func<float> CellSize;

        public IconButton Map => map;
        public IconButton Retry => retry;
        public IconButton Next => next;
        public IconButton Undo => undo;
        public IconButton Restart => restart;
        public IReadOnlyList<IconButton> Stones => stones;
        /// The 3D stones (null until a clear shows them).
        public IReadOnlyList<GameObject> WorldStoneObjects => worldStones;
        public bool IsShown => gameObject.activeSelf && shown != null;
        /// The result on screen (null when hidden; -flowcheck reads its planet and last-stage flags).
        public ResultModel Model => IsShown ? shown : null;
        /// A loss is shown (the failure line and its two buttons).
        public bool FailureShown => IsShown && !shown.Cleared;
        public FocusPlate Plate => plate;
        /// The third-star nudge line ("" = not shown).
        public string NudgeLine => nudgePlate.gameObject.activeSelf ? nudgeText.text : "";
        public RectTransform NudgePlate => nudgePlate.rectTransform;
        public const int NudgeSize = 22;
        Image nudgePlate;
        Text nudgeText;
        /// Earned stones drawn lit right now (the reveal fills them in order).
        public int LitStones { get; private set; }

        /// The 3D stones are used when the art and hooks are there (on since 7lucky 48e31d4: Codex's world star stone fix -
        /// outward shell normals, raised inlays, self-lit star materials). Off: the 2D row above the board area.
        public const bool WorldStonesReady = true;

        /// The 3D stones are used (ready, art and the scene root's world hooks are there).
        public bool WorldStones => WorldStonesReady && icons != null && icons.worldStarStone != null && ExitWorld != null && WorldParent != null
                                   && WorldCamera != null && WorldCamera() != null && WorldParent() != null;

        /// Builds the clear and failure on their own canvas (Edit Mode too). <paramref name="icons"/> may be null (placeholders).
        public static U5ClearView Build(UiIconSet icons, Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject("U5 Clear", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var v = go.AddComponent<U5ClearView>();
            v.icons = icons;
            v.loc = loc;
            v.Assemble();
            go.SetActive(false);
            return v;
        }

        /// Full-screen catch behind the result (R3 on 6a792b6, 14.7: 0.25 with the HUDs hidden): presses on the board do nothing.
        public Image Scrim { get; private set; }

        void Assemble()
        {
            var root = transform;
            Scrim = Ui.MakeImage(root, new Color(0.02f, 0.04f, 0.08f, U5RowOverlay.ScrimAlpha), null, "Scrim");
            Scrim.rectTransform.Stretch();
            Scrim.raycastTarget = true;
            Ui.Exempt(Scrim.gameObject, UiLayoutRule.Board | UiLayoutRule.Overlap, "U5 result modal scrim (14.7: 0.25 with the HUD hidden)");
            map = IconButton.Build(root, UiIcon.Map, icons, "Esc", () => StageSelectRequested?.Invoke(), name: "Map");
            map.Rect.TopLeft(Safe, Safe, IconButton.Size, IconButton.Size);
            retry = IconButton.Build(root, UiIcon.Retry, icons, "R", () => RetryRequested?.Invoke(), name: "Retry");
            retry.Rect.anchorMin = retry.Rect.anchorMax = retry.Rect.pivot = new Vector2(1, 1);
            retry.Rect.anchoredPosition = new Vector2(-Safe, -Safe);
            retry.Rect.sizeDelta = new Vector2(IconButton.Size, IconButton.Size);
            next = IconButton.Build(root, UiIcon.Next, icons, "Enter", () => NextRequested?.Invoke(), name: "Next", size: NextSize);
            next.Rect.anchorMin = next.Rect.anchorMax = next.Rect.pivot = new Vector2(0, 1);
            next.Backing();

            // Star stones: tip-only buttons. Over a 3D stone: a transparent hit; without one: a 2D stone (star-stone art).
            stoneRow = Ui.Rect("Star stones", root);
            stoneRow.anchorMin = stoneRow.anchorMax = stoneRow.pivot = new Vector2(0, 1);
            stoneRow.anchoredPosition = Vector2.zero;
            stoneRow.sizeDelta = Vector2.zero; // a container: each stone's hit is placed on its own
            stoneBodies = new Image[stones.Length];
            for (int k = 0; k < stones.Length; k++)
            {
                var s = IconButton.Build(stoneRow, UiIcon.Credits, icons, null, null, "result.star1", $"Stone {k + 1}");
                s.Rect.anchorMin = s.Rect.anchorMax = new Vector2(0, 1);
                s.Rect.pivot = new Vector2(0.5f, 0.5f);
                var body = Ui.MakeImage(s.transform, Color.white, null, "Stone");
                body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                body.rectTransform.sizeDelta = new Vector2(StoneSize, StoneSize);
                body.transform.SetAsFirstSibling();
                stoneBodies[k] = body;
                s.Glyph.rectTransform.offsetMin = new Vector2(12, 12);
                s.Glyph.rectTransform.offsetMax = new Vector2(-12, -12);
                stones[k] = s;
            }

            finalePlate = Ui.Plate(root, "Finale plate");
            // Top centre, between Map and Retry: the band the hidden HUD title leaves free (Next's words go by the exit).
            finalePlate.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -Safe), new Vector2(560, 48));
            finaleText = Ui.Label(finalePlate.transform, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Finale");
            finaleText.rectTransform.Stretch();
            // The third-star nudge (ResultModel.Star3Nudge): under the finale line, or in its place.
            nudgePlate = Ui.Plate(root, "Nudge plate");
            nudgePlate.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -Safe), new Vector2(480, 44));
            nudgeText = Ui.Label(nudgePlate.transform, "", NudgeSize, Ui.Gold, TextAnchor.MiddleCenter, "Nudge");
            nudgeText.rectTransform.Stretch();
            nudgePlate.gameObject.SetActive(false);
            BuildStrands(root);

            // Failure: the line and undo / restart at the bottom centre.
            failGroup = Ui.Rect("Failure", root);
            failGroup.Stretch();
            var failPlate = Ui.Plate(failGroup, "Failure plate");
            failPlate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 720 - FailTop + 12), new Vector2(320, 44));
            failText = Ui.Label(failPlate.transform, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Line");
            failText.rectTransform.Stretch();
            undo = IconButton.Build(failGroup, UiIcon.Undo, icons, "Z", () => UndoRequested?.Invoke(), name: "Undo", size: FailSize);
            restart = IconButton.Build(failGroup, UiIcon.Restart, icons, "R", () => RestartRequested?.Invoke(), name: "Restart", size: FailSize);
            for (int i = 0; i < 2; i++)
            {
                var b = i == 0 ? undo : restart;
                b.Backing();
                b.Rect.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 0.5f) * FailPitch, 720 - FailTop - FailSize), new Vector2(FailSize, FailSize));
            }
            IconNav.Row(new[] { undo, restart });
            failGroup.gameObject.SetActive(false);

            plate = FocusPlate.Build(root, loc, icons);
            plate.Avoid = () =>
            {
                avoid.Clear();
                if (BoardArea != null) avoid.Add(BoardArea());
                foreach (var s in stones) if (s.gameObject.activeInHierarchy) avoid.Add(StoneRect(s));
                if (finalePlate.gameObject.activeSelf) avoid.Add(CanvasRect(finalePlate.rectTransform));
                if (nudgePlate.gameObject.activeSelf) avoid.Add(CanvasRect(nudgePlate.rectTransform));
                if (failGroup.gameObject.activeSelf) avoid.Add(CanvasRect((RectTransform)failText.transform.parent));
                return avoid;
            };
            foreach (var b in new[] { map, retry, next, undo, restart }) plate.Track(b);
            foreach (var s in stones) plate.Track(s);
            IconNav.Row(new[] { map, stones[0], stones[1], stones[2], next, retry });
        }

        readonly List<Rect> avoid = new List<Rect>();

        static Rect StoneRect(IconButton s) =>
            new Rect(s.Rect.anchoredPosition.x - s.Rect.sizeDelta.x / 2, -s.Rect.anchoredPosition.y - s.Rect.sizeDelta.y / 2, s.Rect.sizeDelta.x, s.Rect.sizeDelta.y);

        /// A point-anchored child of this canvas as a canvas rect, top-left origin.
        Rect CanvasRect(RectTransform r)
        {
            var canvas = ((RectTransform)transform).rect.size;
            var size = r.sizeDelta;
            float x = r.anchorMin.x * canvas.x + r.anchoredPosition.x - r.pivot.x * size.x;
            float yUp = r.anchorMin.y * canvas.y + r.anchoredPosition.y - r.pivot.y * size.y;
            return new Rect(x, canvas.y - yUp - size.y, size.x, size.y);
        }

        /// Words and fonts in the current language; a shown result redraws.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            finaleText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            if (IsShown) Words(shown);
        }

        public void Show(ResultModel m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            bool redraw = ReferenceEquals(m, shown) && IsShown;
            shown = m;
            gameObject.SetActive(true);
            bool clear = m.Cleared;
            foreach (var g in new GameObject[] { map.gameObject, retry.gameObject, next.gameObject, stoneRow.gameObject }) g.SetActive(clear);
            failGroup.gameObject.SetActive(!clear);
            if (clear && WorldStones) EnsureWorldStones();
            Words(m);
            if (clear) Layout();
            if (redraw) return;
            HudHidden?.Invoke(true);
            plate.Model.Clear();
            if (clear) GameSound.Sfx(SoundIds.ClearJingle);
            if (clear) ShowStones(m.Stars);
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(clear ? next.gameObject : restart.gameObject); // Enter: Next / restart
        }

        void Words(ResultModel m)
        {
            failText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            if (!m.Cleared)
            {
                failText.text = loc.Get("result.lost_title");
                var fp = (RectTransform)failText.transform.parent;
                fp.sizeDelta = new Vector2(fp.sizeDelta.x, FocusPlate.PlateHeight(failText, failText.text, fp.sizeDelta.x, 8));
                finalePlate.gameObject.SetActive(false);
                nudgePlate.gameObject.SetActive(false);
                strands.gameObject.SetActive(false);
                return;
            }
            next.SetTip(m.IsLast ? "result.ending" : "ui5.icon.next");
            stones[0].SetTip("result.star1");
            stones[1].SetTip("result.star2");
            stones[2].SetTip(m.LevelBodies == 1 ? "result.star3_one" : "result.star3_many", new object[] { m.LevelBodies });
            string line = m.IsLast ? loc.Get("result.journey_done")
                : m.FinishedChapter != null ? loc.Format("result.chapter_done", m.FinishedChapter.Planet(loc.Current)) : "";
            finaleText.text = line.Replace("\n", " ");
            finaleText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            finalePlate.gameObject.SetActive(line.Length > 0);
            if (line.Length > 0)
            {
                float w = finaleText.cachedTextGeneratorForLayout.GetPreferredWidth(finaleText.text, finaleText.GetGenerationSettings(Vector2.zero))
                          / Mathf.Max(0.01f, finaleText.pixelsPerUnit);
                finalePlate.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 32, FocusPlate.PlateHeight(finaleText, finaleText.text, w, 8));
            }
            bool nudge = m.Star3Nudge || m.HintNudge;
            nudgePlate.gameObject.SetActive(nudge);
            if (nudge)
            {
                nudgeText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
                nudgeText.text = loc.Get(m.HintNudge ? "result.hint_again" : "result.star3_nudge");
                float w = nudgeText.cachedTextGeneratorForLayout.GetPreferredWidth(nudgeText.text, nudgeText.GetGenerationSettings(Vector2.zero))
                          / Mathf.Max(0.01f, nudgeText.pixelsPerUnit);
                float top = Safe + (line.Length > 0 ? finalePlate.rectTransform.sizeDelta.y + 8 : 0);
                nudgePlate.rectTransform.anchoredPosition = new Vector2(0, -top);
                nudgePlate.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 32, FocusPlate.PlateHeight(nudgeText, nudgeText.text, w, 8));
            }
            ShowStrands(m, Safe + (line.Length > 0 ? finalePlate.rectTransform.sizeDelta.y + 8 : 0) + (nudge ? nudgePlate.rectTransform.sizeDelta.y + 8 : 0));
        }

        /// World stones (or the 2D row) and their hits; Next beside the board at the exit's height (or width), nearest
        /// side; a shown tip follows its owner (R2 on 1f90617).
        public void Layout()
        {
            var canvas = ((RectTransform)transform).rect.size;
            if (canvas.x <= 0 || canvas.y <= 0) canvas = new Vector2(1280, 720);
            var exit = ExitPoint != null ? ExitPoint() : canvas / 2;
            var board = BoardArea != null ? BoardArea() : new Rect(canvas.x / 2, canvas.y / 2, 0, 0);
            bool world = WorldStones && worldStones[0] != null;
            var mark = stoneRow.GetComponent<UiLayout>();
            if (world)
            {
                var cam = WorldCamera();
                float cell = CellSize != null ? CellSize() : 1f;
                for (int k = 0; k < stones.Length; k++)
                {
                    var v = cam.WorldToViewportPoint(worldStones[k].transform.position + Vector3.up * WorldHeight * 0.5f * cell);
                    Hit(stones[k], new Vector2(v.x * canvas.x, (1 - v.y) * canvas.y), IconButton.Size);
                }
                if (mark == null || (mark.exempt & UiLayoutRule.Board) == 0)
                    Ui.Exempt(stoneRow.gameObject, UiLayoutRule.Board, "U5 clear: transparent hits over the world star stones (their explanation), R1 on 1f90617");
            }
            else
            {
                var row = StoneRow(board, exit, canvas);
                for (int k = 0; k < stones.Length; k++)
                    Hit(stones[k], new Vector2(row.x + StoneSize / 2 + k * (StoneSize + StoneGap), row.y + StoneSize / 2), StoneSize);
                if (mark != null) mark.exempt &= ~UiLayoutRule.Board;
            }
            var n = NextSpot(board, exit, canvas);
            next.Rect.anchoredPosition = new Vector2(n.x, -n.y);
            next.Rect.sizeDelta = n.size;
            plate.Refresh();
        }

        static void Hit(IconButton s, Vector2 centre, float size)
        {
            s.Rect.anchoredPosition = new Vector2(centre.x, -centre.y);
            s.Rect.sizeDelta = new Vector2(size, size);
        }

        /// The 2D stone row (no 3D stone): centred on the exit, just above the board area, under the top button band,
        /// inside the safe area (canvas, top-left origin; pure, tests).
        public static Rect StoneRow(Rect board, Vector2 exit, Vector2 canvas)
        {
            float w = 3 * StoneSize + 2 * StoneGap;
            float x = Mathf.Clamp(exit.x - w / 2, Safe, canvas.x - Safe - w);
            float y = Mathf.Clamp(board.yMin - 8 - StoneSize, Safe + IconButton.Size + 8, canvas.y - Safe - StoneSize);
            return new Rect(x, y, w, StoneSize);
        }

        /// Where the three world stones stand (world): a row <see cref="WorldBehind"/> behind the exit as the camera sees it (<paramref name="away"/> =
        /// the camera's forward along the ground), centred on it, <see cref="WorldSpacing"/> cells apart across the view, on
        /// the ground (EnsureWorldStones lifts them over the brother's head; pure, tests).
        public static Vector3[] WorldStoneSpots(Vector3 exit, float cell, Vector3 away)
        {
            away = new Vector3(away.x, 0f, away.z);
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            var across = Vector3.Cross(Vector3.up, away); // left to right as the camera sees it
            var spots = new Vector3[Core.Stars.Max];
            for (int k = 0; k < spots.Length; k++)
            {
                var p = exit + away * WorldBehind * cell + across * ((k - 1) * WorldSpacing * cell);
                spots[k] = new Vector3(p.x, 0f, p.z);
            }
            return spots;
        }

        /// Next (56) beside <paramref name="board"/> on the side nearest to <paramref name="exit"/>, level with it, inside
        /// the safe area and below the top buttons; no side fits: just right of the exit (pure; tests).
        public static Rect NextSpot(Rect board, Vector2 exit, Vector2 canvas)
        {
            float top = Safe + IconButton.Size + 8;
            var options = new List<Rect>
            {
                new Rect(board.xMax + NextGap, exit.y - NextSize / 2, NextSize, NextSize),
                new Rect(board.xMin - NextGap - NextSize, exit.y - NextSize / 2, NextSize, NextSize),
                new Rect(exit.x - NextSize / 2, board.yMax + NextGap, NextSize, NextSize),
                new Rect(exit.x - NextSize / 2, board.yMin - NextGap - NextSize, NextSize, NextSize),
            };
            Rect best = new Rect(exit.x + NextGap, exit.y - NextSize / 2, NextSize, NextSize);
            float bestD = float.MaxValue;
            foreach (var o in options)
            {
                var r = new Rect(o.x, Mathf.Clamp(o.y, top, canvas.y - Safe - NextSize), o.width, o.height);
                r.x = Mathf.Clamp(r.x, Safe, canvas.x - Safe - NextSize);
                if (r.Overlaps(board)) continue;
                float d = Vector2.Distance(r.center, exit);
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        void EnsureWorldStones()
        {
            var parent = WorldParent();
            float cell = CellSize != null ? CellSize() : 1f;
            var cam = WorldCamera();
            var spots = WorldStoneSpots(ExitWorld(), cell, cam.transform.forward);
            for (int k = 0; k < worldStones.Length; k++)
            {
                if (worldStones[k] == null || worldStones[k].transform.parent != parent)
                {
                    if (worldStones[k] != null) Destroy(worldStones[k]);
                    var go = Instantiate(icons.worldStarStone, parent, false);
                    go.name = $"Star stone {k + 1}";
                    foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                    worldInlays[k] = null;
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        if (r.name.Contains("StarInlay")) worldInlays[k] = r;
                        else if (icons.stoneShell != null) r.sharedMaterial = icons.stoneShell;
                    }
                    go.transform.rotation = Quaternion.identity;
                    // About WorldHeight cells tall (manifest: up +Y, front -Z, pivot at its centre), whatever the model's units.
                    var b = new Bounds(go.transform.position, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    if (b.size.y > 0.0001f) go.transform.localScale *= WorldHeight * cell / b.size.y;
                    worldStones[k] = go;
                    worldBaseScale[k] = go.transform.localScale;
                }
                worldStones[k].SetActive(false); // not counted as ground below
            }
            // One row on one level: the highest ground under the three (the wall cap behind the exit), so none sinks into a block.
            float ground = (BoardView.ActorHeight + WorldLift) * cell; // over the winning brother's head
            foreach (var p in spots) ground = Mathf.Max(ground, GroundAt(parent, p, cell));
            // The front (-Z, the star inlay) toward the board camera, leaned back with its pitch so it reads at full size.
            // (+Z of the stone = away from the camera, so its -Z front with the inlay faces it.)
            var facing = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            Place(spots, ground, facing, 1f);
            // Small boards on screen (a wide 17 x 3 stage): grown so each stone is at least MinStonePx tall on the 720-unit
            // canvas, the row spreading with it from the middle stone; it grows upward from over the brother's head.
            float px = StonePx(cam, 0);
            if (px > 0.01f && px < MinStonePx)
            {
                float grow = MinStonePx / px;
                var mid = spots[1];
                var wide = new Vector3[spots.Length];
                for (int k = 0; k < spots.Length; k++) wide[k] = mid + (spots[k] - mid) * grow;
                Place(wide, ground, facing, grow);
            }
        }

        /// The smallest on-screen height of a world stone's star face (canvas units of the 720-tall reference): the 48 icon size.
        public const float MinStonePx = 48f;
        readonly Vector3[] worldBaseScale = new Vector3[Core.Stars.Max];

        void Place(Vector3[] spots, float ground, Quaternion facing, float grow)
        {
            for (int k = 0; k < worldStones.Length; k++)
            {
                var t = worldStones[k].transform;
                t.localScale = worldBaseScale[k] * grow;
                t.rotation = facing;
                t.position = new Vector3(spots[k].x, ground, spots[k].z);
                worldStones[k].SetActive(true);
                // Stand on the ground: the pivot is the stone's centre, lift by what reaches below.
                float low = float.MaxValue;
                foreach (var r in worldStones[k].GetComponentsInChildren<Renderer>()) low = Mathf.Min(low, r.bounds.min.y);
                if (low < float.MaxValue) t.position += Vector3.up * (ground - low);
            }
        }

        /// Stone <paramref name="k"/>'s height on screen, canvas units of the 720-tall reference.
        float StonePx(Camera cam, int k)
        {
            // The star inlay's face (thin) as the visible size; all eight corners, since it leans back to face the camera.
            Bounds b;
            if (worldInlays[k] != null) b = worldInlays[k].bounds;
            else
            {
                b = new Bounds(worldStones[k].transform.position, Vector3.zero);
                foreach (var r in worldStones[k].GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            }
            float lo = float.MaxValue, hi = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                float y = cam.WorldToViewportPoint(p).y;
                lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
            }
            return (hi - lo) * 720f;
        }

        /// The top of the tile-sized geometry under <paramref name="spot"/> (a floor tile: 0; a wall block with its cap: the
        /// cap top), so a stone stands on it, not inside it. Pieces wider than 6 cells (the island, the board's rim) and
        /// taller than 2 cells are not ground.
        static float GroundAt(Transform root, Vector3 spot, float cell)
        {
            float top = 0f;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                if (b.size.x > 6f * cell || b.size.z > 6f * cell || b.max.y > 2f * cell) continue;
                if (spot.x < b.min.x || spot.x > b.max.x || spot.z < b.min.z || spot.z > b.max.z) continue;
                top = Mathf.Max(top, b.max.y);
            }
            return top;
        }

        void ShowStones(int earned)
        {
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            for (int k = 0; k < stones.Length; k++) DrawStone(k, false);
            LitStones = 0;
            if (Application.isPlaying && isActiveAndEnabled) reveal = StartCoroutine(Reveal(earned));
            else for (int k = 0; k < earned; k++) DrawStone(k, true);
        }

        void DrawStone(int k, bool lit)
        {
            if (lit) LitStones = Mathf.Max(LitStones, k + 1);
            if (worldStones[k] != null && worldStones[k].activeSelf)
            {
                if (worldInlays[k] != null) worldInlays[k].sharedMaterial = lit ? icons.starLit : icons.starUnlit;
                stoneBodies[k].enabled = false; // a transparent hit over the 3D stone
                stones[k].Glyph.enabled = false;
                return;
            }
            var art = icons != null ? (lit ? icons.starStoneLit : icons.starStoneUnlit) : null;
            var body = stoneBodies[k];
            body.enabled = true;
            body.sprite = art != null ? art : Ui.Circle;
            body.color = art != null ? Color.white : (lit ? new Color(0.36f, 0.33f, 0.27f, 1) : new Color(0.2f, 0.21f, 0.24f, 0.9f));
            stones[k].Glyph.enabled = art == null; // the art stone carries its own star
            stones[k].Glyph.color = lit ? Ui.Gold : Ui.Dim;
        }

        // ---------- The three strands (user request 2026-11-14): who walked, braided into one line on a 3-star clear ----------

        /// Scarf colours: eldest teal, middle ochre, youngest terracotta (cutscene art, world-bible 4).
        static readonly Color[] StrandColors = { new Color32(0x56, 0x8C, 0x88, 0xFF), new Color32(0xB9, 0x9A, 0x57, 0xFF), new Color32(0xB9, 0x6C, 0x59, 0xFF) };
        static readonly Color StrandMissing = new Color(0.42f, 0.43f, 0.47f, 0.45f);
        public const float StrandW = 300, StrandH = 5, StrandGap = 7;
        RectTransform strands;
        readonly Image[] strandBars = new Image[3];
        Image braidGlow;
        Coroutine strandRun;

        /// Strands drawn in colour (the brothers who walked) on the shown clear.
        public int StrandsLit { get; private set; }
        /// The strands braided into one bright line (a 3-star clear, after the third star).
        public bool Braided { get; private set; }
        public RectTransform Strands => strands;

        void BuildStrands(Transform root)
        {
            strands = Ui.Rect("Strands", root);
            strands.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -Safe), new Vector2(StrandW, StrandGap * 2 + StrandH));
            braidGlow = Ui.MakeImage(strands, new Color(1f, 0.93f, 0.6f, 0f), null, "Braid glow");
            braidGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StrandW + 16, 14));
            for (int k = 0; k < 3; k++)
            {
                strandBars[k] = Ui.MakeImage(strands, StrandColors[k], null, $"Strand {k + 1}");
                strandBars[k].rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -k * StrandGap), new Vector2(StrandW, StrandH));
            }
            strands.gameObject.SetActive(false);
        }

        void ShowStrands(ResultModel m, float top)
        {
            if (strandRun != null) { StopCoroutine(strandRun); strandRun = null; }
            strands.anchoredPosition = new Vector2(0, -top);
            strands.gameObject.SetActive(true);
            int walked = Mathf.Clamp(m.BodiesUsed, 0, 3);
            int bodies = Mathf.Clamp(m.LevelBodies, 1, 3);
            StrandsLit = walked;
            Braided = false;
            braidGlow.color = new Color(1f, 0.93f, 0.6f, 0f);
            bool still = !Application.isPlaying || !isActiveAndEnabled
                         || (Services.TryGet<SaveSystem>(out var save) && save.Settings.reduceMotion);
            for (int k = 0; k < 3; k++)
            {
                var r = strandBars[k].rectTransform;
                strandBars[k].gameObject.SetActive(k < bodies);
                strandBars[k].color = k < walked ? StrandColors[k] : StrandMissing;
                r.anchoredPosition = new Vector2(0, -k * StrandGap);
                r.sizeDelta = new Vector2(still || k >= walked ? StrandW : 0, StrandH);
            }
            bool braid = m.Stars == ResultModel3Stars && walked == bodies;
            if (still) { if (braid) SetBraid(1f); return; }
            strandRun = StartCoroutine(RunStrands(walked, braid));
        }

        const int ResultModel3Stars = 3;

        /// Each walked strand draws across, one after another; a 3-star clear then braids them into one bright line once the
        /// third star is lit (about 1.3 s in all; the clear's own input works throughout).
        IEnumerator RunStrands(int walked, bool braid)
        {
            for (int k = 0; k < walked; k++)
            {
                var r = strandBars[k].rectTransform;
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { r.sizeDelta = new Vector2(StrandW * Mathf.SmoothStep(0, 1, t / 0.25f), StrandH); yield return null; }
                r.sizeDelta = new Vector2(StrandW, StrandH);
            }
            if (!braid) { strandRun = null; yield break; }
            for (float t = 0; t < 2f && LitStones < 3; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime) { SetBraid(Mathf.SmoothStep(0, 1, t / 0.45f)); yield return null; }
            SetBraid(1f);
            strandRun = null;
        }

        /// 0 = three separate strands, 1 = woven into one bright line with a soft gold glow.
        void SetBraid(float k)
        {
            for (int i = 0; i < 3; i++)
            {
                var r = strandBars[i].rectTransform;
                r.anchoredPosition = new Vector2(0, Mathf.Lerp(-i * StrandGap, -StrandGap + (1 - i) * 3f, k)); // a tight woven band
                r.sizeDelta = new Vector2(StrandW, Mathf.Lerp(StrandH, StrandH + 1, k));
                strandBars[i].color = Color.Lerp(StrandColors[i], Color.Lerp(StrandColors[i], Color.white, 0.25f), k);
            }
            braidGlow.rectTransform.anchoredPosition = new Vector2(0, -StrandGap + (StrandGap + StrandH / 2f) - (StrandGap * 2 + StrandH) / 2f);
            braidGlow.color = new Color(1f, 0.93f, 0.6f, 0.22f * k);
            Braided = k >= 1f;
        }

        IEnumerator Reveal(int earned)
        {
            for (int k = 0; k < earned; k++)
            {
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) yield return null;
                DrawStone(k, true);
                GameSound.Sfx(SoundIds.StarChime(k + 1));
                var r = worldStones[k] != null && worldStones[k].activeSelf ? worldStones[k].transform : stones[k].transform;
                var home = r.localScale;
                for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
                {
                    // The third star of a 3-star clear lands harder: the woven line's star (2026-11-14).
                    r.localScale = home * (1f + (k == 2 ? 0.6f : 0.35f) * Mathf.Sin(t / 0.22f * Mathf.PI));
                    yield return null;
                }
                r.localScale = home;
            }
            reveal = null;
        }

        public void Hide()
        {
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            bool was = IsShown;
            shown = null;
            plate.Model.Clear();
            foreach (var w in worldStones) if (w != null) w.SetActive(false);
            gameObject.SetActive(false);
            if (was) HudHidden?.Invoke(false);
        }

        void OnDestroy()
        {
            foreach (var w in worldStones) if (w != null) Destroy(w);
        }
    }
}
