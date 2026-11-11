using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Board view (docs/architecture.md 4.3, 5, 7). Implements <see cref="IBoardPresenter"/> for <see cref="GameSession"/>:
    /// Rebuild for a new stage, Snap to draw a snapshot with no animation (Undo/Restart/replay), Present to animate one command.
    /// Prefabs come only from the <see cref="TileSet"/>, cell kinds only from <see cref="TileId.At"/>, positions from <see cref="BoardGrid"/>.
    /// Everything drawn is recomputed from a State, so Snap(state) twice draws the same picture.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardPresenter
    {
        const float StepTime = 0.1f;

        sealed class WorldLabel
        {
            public Text Text;
            public Vector3 World;
            public Transform Follow;
            public float Size; // font size in cells
        }

        sealed class Cell
        {
            public GameObject Go;
            public TileId Id;
            public bool HasId;
            public bool Active;
            public bool ActiveKnown;
            public WorldLabel Label;
        }

        TileSet set;
        ChapterPreset chapter; // material overrides for the stage being drawn; null = none (12.8)
        CameraRig? rigOverride;
        RectTransform labelLayer;
        Camera cam;
        float fittedAspect;

        Transform root;
        Level level;
        BoardGrid grid;
        Cell[] cells;
        GameObject[] feet;
        WorldLabel[] footLabels;
        readonly List<WorldLabel> labels = new List<WorldLabel>();
        readonly Dictionary<int, GameObject> statues = new Dictionary<int, GameObject>();
        // Body index that made each statue (BodyIndex.OfStatue), so a cell is rebuilt only when its owner changes (docs 12.6).
        readonly Dictionary<int, int> statueOwners = new Dictionary<int, int>();
        readonly List<GameObject> transients = new List<GameObject>();
        readonly StringBuilder sb = new StringBuilder(16);
        GameObject player, startMarker, oldStartMarker;
        StarTrailView trail;
        WorldLabel playerLabel;
        int playerBody = -1; // BodyIndex.Current of the player instance on the board; -1 before Rebuild
        bool choosing;

        TileVisual PlayerVisual => set.PlayerFor(playerBody);
        Vector3 PlayerScale => PlayerVisual.Scale * grid.CellSize;

        Coroutine anim;
        Coroutine shake;

        /// True while the session has queued input: animations play faster (docs/architecture.md 4.1).
        public Func<bool> Hurry;

        public BoardGrid Grid => grid;
        /// The TileSet given to Init (the HUD reads body colors from it, docs 12.6).
        public TileSet TileSet => set;
        public bool IsAnimating => anim != null;
        /// Word on the exit tile in the UI language (board.exit), set by BoardController; the next draw uses it.
        public string ExitLabel { get; set; } = "EXIT";
        /// Accessibility "show step numbers" (off by default, production/ui-minimal-plan.md): numeric footprints on the cells
        /// walked and the steps-left number over the player. Off, steps are read from the star trail and the stars at the feet.
        public bool StepNumbers { get; set; }
        /// Accessibility "reduce motion" (SettingsData.reduceMotion): no confirm burst, no pulsing; steady marks instead.
        public bool ReduceMotion { get; set; }

        /// Confirm presentation of a KEEP / SWITCH, seconds (inside that command's Present, docs 4.4): 0 with reduce motion.
        /// With a door the statue opens (0.12 s) the Present stays within ChoicePreview.ConfirmSecondsMax.
        public static float ConfirmSeconds(bool reduceMotion) => reduceMotion ? 0f : 0.3f;

        BoardMarksView marks;
        /// Screen-space marks on the board (foot stars, choice ring, KEEP dots, SWITCH knot); tests and captures read it.
        public BoardMarksView Marks => marks;

        /// <param name="cameraOverride">Camera preset to use instead of tileSet.cameraRig (Game scene setting), or null.</param>
        public void Init(TileSet tileSet, RectTransform labelLayer, Camera cam, CameraRig? cameraOverride = null)
        {
            set = tileSet;
            this.labelLayer = labelLayer;
            this.cam = cam;
            rigOverride = cameraOverride;
            if (marks == null && labelLayer != null)
            {
                var layer = Ui.Rect("Board Marks", labelLayer).Stretch();
                marks = layer.gameObject.AddComponent<BoardMarksView>();
                marks.Init(layer, ToCanvas);
            }
        }

        /// World point → anchored position on the label layer (canvas units, centre origin), as the world labels use.
        Vector2 ToCanvas(Vector3 world)
        {
            if (cam == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(labelLayer, cam.WorldToScreenPoint(world), null, out var local);
            return local;
        }

        /// The board area of docs 14.6 on the HUD canvas, top-left origin (cells and actors projected, grown by 8): where the
        /// choice cards must not go. Same corners as UiLayoutCheck.
        public Rect ProjectedArea()
        {
            if (cam == null || level == null) return default;
            var b = grid.Bounds;
            float top = ActorHeight * grid.CellSize, w = labelLayer.rect.width, h = labelLayer.rect.height;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                var p = b.center + new Vector3((k & 1) == 0 ? -b.extents.x : b.extents.x, (k & 2) == 0 ? 0 : top, (k & 4) == 0 ? -b.extents.z : b.extents.z);
                var v = cam.WorldToViewportPoint(p);
                float x = v.x * w, y = (1 - v.y) * h;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
            return Rect.MinMaxRect(x0 - 8, y0 - 8, x1 + 8, y1 + 8);
        }

        /// The brother's head on the HUD canvas, top-left origin (choice card placement).
        public Vector2 BrotherOnCanvas()
        {
            if (cam == null || player == null) return default;
            return BrotherPoint(cam, grid, 0, labelLayer.rect.size, player.transform.position);
        }

        /// The head of a brother standing on <paramref name="cell"/> (or at <paramref name="at"/>) through <paramref name="cam"/>
        /// on a canvas of <paramref name="canvas"/> units, top-left origin. Edit Mode checks (UiLayoutCheck 14.11) place the
        /// choice cards with it: HudView.ShowChoice, then HudView.PlaceChoice(board area, this).
        public static Vector2 BrotherPoint(Camera cam, BoardGrid grid, int cell, Vector2 canvas, Vector3? at = null)
        {
            var v = cam.WorldToViewportPoint((at ?? grid.CellToWorld(cell)) + Vector3.up * 0.6f * grid.CellSize);
            return new Vector2(v.x * canvas.x, (1 - v.y) * canvas.y);
        }

        // ---------- KEEP / SWITCH choice (docs 4.4): ghosts and marks, from a ChoicePreview ----------

        GameObject ghostBody, ghostStone;
        int ghostBodyIndex = -1;
        bool choiceShown;
        ChoicePreview choicePreview;
        BoardMarksView.ChoiceFocus choiceFocus;

        /// Shows (or with <paramref name="on"/> false hides) the choice on the board: the ring round the brother and, for the
        /// focused choice only, the next brother's ghost where he would wake (KEEP: the start, with the statue ghost here and a
        /// dotted line; SWITCH: here, with the knot). Pure view: the preview comes from ChoicePreview.TryOf.
        public void ShowChoice(bool on, ChoicePreview p, BoardMarksView.ChoiceFocus focus)
        {
            choiceShown = on;
            choicePreview = p;
            choiceFocus = on ? focus : BoardMarksView.ChoiceFocus.None;
            DrawChoice();
        }

        /// The next brother's ghost while a choice is focused (tests, captures).
        public GameObject ChoiceGhost => ghostBody != null && ghostBody.activeSelf ? ghostBody : null;
        public GameObject StatueGhost => ghostStone != null && ghostStone.activeSelf ? ghostStone : null;

        void DrawChoice()
        {
            if (level == null || marks == null) return;
            bool on = choiceShown && choicePreview.Keep != null;
            var p = choicePreview;
            int next = on ? p.NextBody : 0;
            Color nextColor = set.BodyColor(next), curColor = set.BodyColor(Mathf.Max(0, playerBody));
            marks.SetChoice(on, on ? grid.CellToWorld(p.Cell) : Vector3.zero, on ? grid.CellToWorld(p.KeepStart) : Vector3.zero,
                nextColor, curColor, choiceFocus);
            bool keep = on && choiceFocus == BoardMarksView.ChoiceFocus.Keep, sw = on && choiceFocus == BoardMarksView.ChoiceFocus.Switch;
            if (keep || sw)
            {
                if (ghostBody == null || ghostBodyIndex != next)
                {
                    if (ghostBody != null) Destroy(ghostBody);
                    ghostBody = Ghost(set.PlayerFor(next), "Ghost (next body)", nextColor);
                    ghostBodyIndex = next;
                }
                if (ghostBody != null) { ghostBody.SetActive(true); Place(ghostBody, set.PlayerFor(next), keep ? p.KeepStart : p.Cell); }
            }
            else if (ghostBody != null) ghostBody.SetActive(false);
            if (keep)
            {
                if (ghostStone == null) ghostStone = Ghost(set.Get(ActorKind.Statue), "Ghost (statue)", new Color(0.85f, 0.88f, 0.85f));
                if (ghostStone != null) { ghostStone.SetActive(true); Place(ghostStone, set.Get(ActorKind.Statue), p.Cell); }
            }
            else if (ghostStone != null) ghostStone.SetActive(false);
        }

        /// Resources/ChoiceGhost.mat: URP Unlit, transparent (a Resources asset, so its variant is always in the build).
        public const string GhostMaterial = "ChoiceGhost";

        /// A see-through copy of an actor prefab in one flat colour (URP Unlit ignores the art's vertex colours). One
        /// material per ghost (two at most), destroyed with it.
        GameObject Ghost(TileVisual v, string name, Color color)
        {
            if (!v.HasPrefab || root == null) return null;
            var src = Resources.Load<Material>(GhostMaterial);
            if (src == null) { Debug.LogError($"[SevenSteps] Resources/{GhostMaterial}.mat missing: no choice ghost"); return null; }
            var mat = new Material(src) { name = name };
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.45f));
            var go = Instantiate(v.prefab, root);
            go.name = name;
            go.transform.localScale = v.Scale * grid.CellSize;
            go.AddComponent<DestroyWith>().Target = mat;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            return go;
        }

        /// Destroys a runtime asset (the ghost's material) together with its object.
        sealed class DestroyWith : MonoBehaviour
        {
            public UnityEngine.Object Target;
            void OnDestroy() { if (Target != null) Destroy(Target); }
        }

        /// Chapter change (12.8): the next Rebuild draws with <paramref name="tileSet"/> and swaps materials through
        /// <paramref name="chapterPreset"/> (null = no overrides). Call before the stage loads, not mid-stage.
        public void UseChapter(TileSet tileSet, ChapterPreset chapterPreset)
        {
            set = tileSet != null ? tileSet : throw new ArgumentNullException(nameof(tileSet));
            chapter = chapterPreset;
        }

        // ---------- IBoardPresenter ----------

        public void Rebuild(State s)
        {
            StopAnim();
            if (root != null) Destroy(root.gameObject);
            ghostBody = ghostStone = null;
            ghostBodyIndex = -1;
            choiceShown = false;
            marks?.Clear();
            foreach (var l in labels) if (l.Text != null) Destroy(l.Text.gameObject);
            labels.Clear();
            statues.Clear();
            statueOwners.Clear();
            transients.Clear();

            level = s.Level;
            grid = set.GridFor(level);
            root = new GameObject("Board").transform;
            root.SetParent(transform, false);
            trail = new GameObject("Star Trail").AddComponent<StarTrailView>();
            trail.transform.SetParent(root, false);
            trail.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // marks are built in world space
            trail.Init(grid, set, cam, chapter != null ? chapter.starTrail : null); // planet look (docs 15.5)
            int n = level.Count;
            cells = new Cell[n];
            feet = new GameObject[n];
            footLabels = new WorldLabel[n];
            for (int i = 0; i < n; i++) cells[i] = new Cell();

            startMarker = Spawn(set.Get(ActorKind.StartMarker), s.Start, "Start");
            oldStartMarker = Spawn(set.Get(ActorKind.OldStartMarker), level.Start, "Old Start");
            playerLabel = AddLabel("7", Vector3.zero, 0.42f, Ui.Ink);
            // Steps-left number stands on the label anchor (bottom edge there) so it never covers the head:
            // TileVisual.labelOffset = top of the model. A centered label put half of it over the RuinsHenge player.
            playerLabel.Text.rectTransform.pivot = new Vector2(0.5f, 0f);
            playerLabel.Text.alignment = TextAnchor.LowerCenter;
            player = null; // destroyed with the old root
            SpawnPlayer(BodyIndex.Current(s), s.Pos);

            FitCamera();
            Draw(s, false);
        }

        public void Snap(State s)
        {
            StopAnim();
            Draw(s, false);
        }

        public void Present(State prev, StepResult r, Action done)
        {
            StopAnim();
            anim = StartCoroutine(Animate(prev, r, done));
        }

        /// Short shake for a rejected command (Feedback). Does not lock input and does not change the picture.
        public void Bump(int cell)
        {
            if (player == null) return;
            if (shake != null) StopCoroutine(shake);
            Vector3 dir = Vector3.right;
            if (cell >= 0 && level != null)
            {
                var d = grid.CellToWorld(cell) - player.transform.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
            }
            shake = StartCoroutine(Shake(player.transform, dir));
        }

        void StopAnim()
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
            if (shake != null) { StopCoroutine(shake); shake = null; }
            foreach (var t in transients) if (t != null) Destroy(t);
            transients.Clear();
            marks?.StopBursts();
            // Settle every scale tween so the picture is exactly the snapshot (Snap = State).
            foreach (var tw in tweens)
            {
                if (tw.Routine != null) StopCoroutine(tw.Routine);
                if (tw.Target != null) tw.Target.localScale = tw.Scale;
            }
            tweens.Clear();
        }

        struct ScaleTween { public Coroutine Routine; public Transform Target; public Vector3 Scale; }
        readonly List<ScaleTween> tweens = new List<ScaleTween>();

        void Tween(Transform t, Vector3 scale)
        {
            if (t == null) return;
            tweens.Add(new ScaleTween { Routine = StartCoroutine(Pop(t, scale)), Target = t, Scale = scale });
        }

        float Speed => Hurry != null && Hurry() ? 2f : 1f;

        IEnumerator Animate(State prev, StepResult r, Action done)
        {
            var pt = player.transform;
            var lift = PlayerVisual.offset * grid.CellSize;
            // KEEP / SWITCH confirmed (docs 4.4): the choice marks go, a short burst on the cell, then the result draws.
            if (prev != null && prev.Status == Status.Choose)
            {
                ShowChoice(false, default, BoardMarksView.ChoiceFocus.None);
                var color = set.BodyColor(BodyIndex.Current(r.State));
                if (marks != null) yield return marks.Burst(grid.CellToWorld(prev.Pos), color, ConfirmSeconds(ReduceMotion));
            }
            // Ice (Slide event): the first cell is a step, the rest of the path is a glide without hops.
            bool slide = r.Has(GameEventType.Slide);
            for (int p = 0; p < r.Path.Length; p++)
            {
                bool gliding = slide && p > 0;
                Vector3 from = pt.position, to = grid.CellToWorld(r.Path[p]) + lift;
                float dur = (gliding ? StepTime * 0.6f : StepTime) / Speed;
                float hop = gliding ? 0f : 0.12f * grid.CellSize;
                for (float t = 0; t < dur; t += Time.deltaTime)
                {
                    float k = t / dur;
                    pt.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * hop);
                    yield return null;
                }
                pt.position = to;
            }

            foreach (var e in r.Events)
            {
                if (e.StepIdx == Rules.Steps && e.At >= 0) StartCoroutine(Ring(grid.CellToWorld(e.At)));
                if (e.Type == GameEventType.Crumble && e.At >= 0) Flash(e.At);
                if (e.Type == GameEventType.Flip)
                {
                    // Lever pulled: every flip wall turns now (TileId.At already gives the new shape), then the rest plays.
                    FlipWalls(r.State, e.At);
                    yield return new WaitForSeconds(0.15f / Speed);
                }
                if ((e.Type == GameEventType.DoorOpen || e.Type == GameEventType.DoorClose) && e.At >= 0)
                {
                    // The door changes right after the statue appears; Draw at the end sees it already done.
                    var c = cells[e.At];
                    bool open = e.Type == GameEventType.DoorOpen;
                    if (c.HasId) { c.Active = open; c.ActiveKnown = true; ApplyActive(c.Go, set.Get(c.Id), open, true); }
                    yield return new WaitForSeconds(0.12f / Speed);
                }
                if (e.Type == GameEventType.Fall || e.Type == GameEventType.Bridge)
                {
                    Vector3 from = pt.position;
                    float dur = 0.3f / Speed;
                    for (float t = 0; t < dur; t += Time.deltaTime)
                    {
                        float k = t / dur;
                        pt.position = from + Vector3.down * (k * k * 1.2f * grid.CellSize);
                        pt.localScale = PlayerScale * (1 - k * 0.6f);
                        yield return null;
                    }
                }
            }

            trail.Remember(r);
            Draw(r.State, true, prev);
            if (r.Has(GameEventType.Respawn)) Tween(player.transform, PlayerScale);
            if (r.State.Status == Status.Win) yield return trail.Finale(Speed); // the paths join up before the result
            anim = null;
            done?.Invoke();
        }

        // ---------- Drawing from State ----------

        /// Draws state s. With pop, things that appeared since prev get a small tween (only after Present).
        void Draw(State s, bool pop, State prev = null)
        {
            int n = level.Count;
            for (int i = 0; i < n; i++)
            {
                var c = cells[i];
                var id = TileId.At(s, i);
                var v = set.Get(id);
                if (!c.HasId || !c.Id.Equals(id))
                {
                    if (c.Go != null) Destroy(c.Go);
                    c.Go = Spawn(v, i, $"{id} {i}");
                    c.Id = id;
                    c.HasId = true;
                    c.ActiveKnown = false;
                    SetCellLabel(c, id, i, v);
                    if (pop && c.Go != null && (id.Kind == TileKind.Bridge || id.Kind == TileKind.FlipWall)) Tween(c.Go.transform, v.Scale * grid.CellSize);
                }

                bool active = IsActive(s, id, i);
                if (!c.ActiveKnown || c.Active != active)
                {
                    bool changed = c.ActiveKnown;
                    c.Active = active;
                    c.ActiveKnown = true;
                    ApplyActive(c.Go, v, active, pop && changed);
                }

                if (c.Label != null)
                {
                    c.Label.World = grid.CellToWorld(i) + v.LabelOffset(active) * grid.CellSize;
                    bool covered = Rules.Contains(s.Statues, i) || (i == s.Pos && s.Status != Status.Lose);
                    switch (id.Kind)
                    {
                        case TileKind.Gate: c.Label.Text.color = active ? Ui.Gold : Ui.Ink; c.Label.Text.enabled = true; break;
                        case TileKind.Door: c.Label.Text.color = active ? Ui.Dim : Ui.Ink; c.Label.Text.enabled = !covered; break;
                        // The count matters even when a statue or the player stands in the doorway (the start can be a Q cell).
                        case TileKind.CountDoor: c.Label.Text.color = active ? Ui.Gold : Ui.Ink; c.Label.Text.enabled = true; break;
                        case TileKind.Exit: c.Label.Text.color = active ? Ui.Gold : Ui.Ink; c.Label.Text.enabled = !covered; break;
                        default: c.Label.Text.enabled = !covered; break;
                    }
                }
            }

            // Statues
            var gone = ListPool.Ints;
            foreach (var kv in statues) if (!Rules.Contains(s.Statues, kv.Key)) gone.Add(kv.Key);
            foreach (int i in gone) { Destroy(statues[i]); statues.Remove(i); statueOwners.Remove(i); }
            gone.Clear();
            foreach (int i in s.Statues)
            {
                // Owner walks the Undo chain: fine here, Draw runs once per state change (docs 12.6), never per frame.
                int owner = BodyIndex.OfStatue(s, i);
                if (statues.TryGetValue(i, out var old))
                {
                    if (statueOwners[i] == owner) continue;
                    Destroy(old); // Undo, then a different body kept here
                }
                var sv = set.StatueFor(owner);
                var st = Spawn(sv, i, $"Statue {i} (body {owner})");
                statues[i] = st;
                statueOwners[i] = owner;
                if (pop && st != null) Tween(st.transform, sv.Scale * grid.CellSize);
            }

            // Start markers
            Place(startMarker, set.Get(ActorKind.StartMarker), s.Start);
            if (oldStartMarker != null)
            {
                oldStartMarker.SetActive(s.Start != level.Start);
                Place(oldStartMarker, set.Get(ActorKind.OldStartMarker), level.Start);
            }

            // Footprints: step numbers on every cell this body walked through (not under the player).
            for (int i = 0; i < n; i++) if (footLabels[i] != null) footLabels[i].Text.text = "";
            var marked = ListPool.Ints;
            int prevStep = 0;
            foreach (var f in s.Trail)
            {
                // Entering mud costs 2 steps (docs 6.3): the footprint jumps by 2, so that cell shows both numbers ("2·3").
                bool mudEntry = f.Step - prevStep == 2;
                prevStep = f.Step;
                if (f.Step <= 0 || f.Cell == s.Pos) continue;
                EnsureFoot(f.Cell);
                var t = footLabels[f.Cell].Text;
                string steps = mudEntry ? (f.Step - 1) + "·" + f.Step : f.Step.ToString();
                t.text = t.text.Length == 0 ? steps : t.text + "·" + steps;
                if (!marked.Contains(f.Cell)) marked.Add(f.Cell);
            }
            for (int i = 0; i < n; i++)
            {
                if (footLabels[i] == null) continue;
                bool has = marked.Contains(i);
                if (feet[i] != null) feet[i].SetActive(has && StepNumbers);
                footLabels[i].Text.enabled = has && StepNumbers;
            }
            marked.Clear();

            // Player
            // A new body (Respawn, Undo across a KEEP/SWITCH, Restart) gets that body's prefab (docs 12.6).
            int body = BodyIndex.Current(s);
            if (body != playerBody) SpawnPlayer(body, s.Pos);
            bool alive = s.Status != Status.Lose;
            player.SetActive(alive);
            Place(player, PlayerVisual, s.Pos);
            player.transform.localScale = PlayerScale;
            playerLabel.Text.enabled = alive && StepNumbers;
            int left = Rules.Steps - s.Used;
            playerLabel.Text.text = left.ToString();
            choosing = s.Status == Status.Choose;
            playerLabel.Text.color = choosing || left == 1 ? Ui.Gold : Ui.Ink;

            // Stars at the feet (15.6, ui-minimal v3): the walking brother only; screen space, so never behind a tile.
            if (marks != null)
                marks.SetFeet(alive && s.Status == Status.Play, player.transform,
                    -PlayerVisual.offset * grid.CellSize + new Vector3(0, 0, -0.42f * grid.CellSize),
                    set.BodyColor(body), s.Used, ReduceMotion, cam != null ? CellPixels() * grid.CellSize : 77f);
            DrawChoice();

            // Star trail (design/art/star-trail.md): rebuilt from s, lights up new marks only after Present.
            trail.Show(s, pop);
        }

        /// Flip event: rebuild the flip-wall cells whose shape changed and show the lever as pulled.
        void FlipWalls(State s, int lever)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                var id = TileId.At(s, i);
                if (id.Kind != TileKind.FlipWall || (c.HasId && c.Id.Equals(id))) continue;
                var v = set.Get(id);
                if (c.Go != null) Destroy(c.Go);
                c.Go = Spawn(v, i, $"{id} {i}");
                c.Id = id;
                c.HasId = true;
                c.ActiveKnown = false;
                if (c.Go != null) Tween(c.Go.transform, v.Scale * grid.CellSize);
            }
            if (lever >= 0 && cells[lever].HasId)
            {
                var c = cells[lever];
                c.Active = s.Flipped;
                c.ActiveKnown = true;
                ApplyActive(c.Go, set.Get(c.Id), s.Flipped, true);
            }
        }

        /// TileVisual "active" (TileVisual.cs): door open, gate passable on the next step, plate pressed, exit on the 7th step.
        /// Answers come from Core queries and State fields; nothing is recomputed here.
        static bool IsActive(State s, TileId id, int i)
        {
            switch (id.Kind)
            {
                case TileKind.Door: return Rules.DoorOpen(s, (char)('A' + id.Variant));
                case TileKind.CountDoor: return Rules.DoorOpen(s, 'Q');
                case TileKind.Lever: return s.Flipped;
                case TileKind.Gate: return s.Status == Status.Play && id.Variant == s.Used + 1;
                case TileKind.Plate: return Rules.Contains(s.Statues, i);
                case TileKind.Exit: return s.Status == Status.Play && s.Used == Rules.Steps - 1;
                default: return false;
            }
        }

        void ApplyActive(GameObject go, TileVisual v, bool active, bool animate)
        {
            if (go == null) return;
            if (!string.IsNullOrEmpty(v.activeChild)) { var t = go.transform.Find(v.activeChild); if (t != null) t.gameObject.SetActive(active); }
            if (!string.IsNullOrEmpty(v.inactiveChild)) { var t = go.transform.Find(v.inactiveChild); if (t != null) t.gameObject.SetActive(!active); }
            var mat = Chapter(active ? v.activeMaterial : v.inactiveMaterial);
            if (mat != null)
            {
                var target = string.IsNullOrEmpty(v.materialTarget) ? go.transform : go.transform.Find(v.materialTarget);
                if (target != null) foreach (var rend in target.GetComponentsInChildren<Renderer>(true)) rend.sharedMaterial = mat;
            }
            string trigger = active ? v.activateTrigger : v.deactivateTrigger;
            var animator = string.IsNullOrEmpty(trigger) ? null : go.GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(trigger);
            else if (animate) Tween(go.transform, v.Scale * grid.CellSize);
        }

        void SetCellLabel(Cell c, TileId id, int i, TileVisual v)
        {
            string text = id.Kind switch
            {
                TileKind.Gate => id.Variant.ToString(),
                TileKind.Plate => ((char)('A' + id.Variant)).ToString(),
                TileKind.Door => ((char)('A' + id.Variant)).ToString(),
                TileKind.Exit => ExitLabel,
                TileKind.CountDoor => level.StatueCount.ToString(),
                _ => null,
            };
            if (text == null)
            {
                if (c.Label != null) c.Label.Text.enabled = false;
                return;
            }
            float size = id.Kind switch { TileKind.Gate => 0.5f, TileKind.Exit => 0.26f, _ => 0.4f };
            if(v.labelScale>0)size=v.labelScale;
            if (c.Label == null) c.Label = AddLabel(text, Vector3.zero, size, Ui.Ink);
            c.Label.Text.text = text;
            c.Label.Size = size;
            c.Label.Text.enabled = true;
        }

        void EnsureFoot(int i)
        {
            if (footLabels[i] != null) return;
            var v = set.Get(ActorKind.Footprint);
            feet[i] = Spawn(v, i, $"Foot {i}");
            footLabels[i] = AddLabel("", grid.CellToWorld(i) + v.LabelOffset(false) * grid.CellSize, 0.3f, Ui.Dim);
        }

        // ---------- TileVisual placement ----------

        GameObject Spawn(TileVisual v, int cell, string name)
        {
            if (!v.HasPrefab) return null;
            var go = Instantiate(v.prefab, root);
            go.name = name;
            ChapterMaterials.Apply(go, chapter); // 12.8, once per spawned prefab
            Place(go, v, cell);
            go.transform.localScale = v.Scale * grid.CellSize;
            return go;
        }

        Material Chapter(Material m) => chapter != null ? chapter.Override(m) : m;

        /// Replaces the player instance with body <paramref name="body"/>'s prefab (TileSet.PlayerFor) and moves the step label onto it.
        void SpawnPlayer(int body, int cell)
        {
            if (player != null) Destroy(player);
            playerBody = body;
            var pv = PlayerVisual;
            player = Spawn(pv, cell, $"Player (body {body})") ?? new GameObject("Player");
            player.transform.SetParent(root, true);
            playerLabel.Follow = player.transform;
            playerLabel.World = pv.LabelOffset(false) * grid.CellSize;
        }

        void Place(GameObject go, TileVisual v, int cell)
        {
            if (go == null) return;
            go.transform.SetPositionAndRotation(grid.CellToWorld(cell) + v.offset * grid.CellSize, v.RotationFor(cell));
        }

        Vector3 BaseScale(ActorKind k) => set.Get(k).Scale * grid.CellSize;

        WorldLabel AddLabel(string text, Vector3 world, float size, Color color)
        {
            var t = Ui.Label(labelLayer, text, 20, color);
            t.fontStyle = FontStyle.Bold;
            Ui.AddOutline(t, new Color(0, 0, 0, 0.75f));
            var l = new WorldLabel { Text = t, World = world, Size = size };
            labels.Add(l);
            return l;
        }

        // ---------- Tweens ----------

        IEnumerator Pop(Transform t, Vector3 target)
        {
            for (float k = 0; k < 1; k += Time.deltaTime / 0.22f)
            {
                if (t == null) yield break;
                float u = k - 1; // ease-out-back: overshoots a little, then settles at 1
                t.localScale = target * (1 + 2.70158f * u * u * u + 1.70158f * u * u);
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        IEnumerator Shake(Transform t, Vector3 dir)
        {
            Vector3 home = t.position;
            const float dur = 0.18f;
            for (float k = 0; k < dur; k += Time.deltaTime)
            {
                if (t == null) yield break;
                t.position = home + dir * (Mathf.Sin(k / dur * Mathf.PI) * 0.12f * grid.CellSize);
                yield return null;
            }
            if (t != null) t.position = home;
            shake = null;
        }

        /// Crumble cue: a quick dip of the cell that broke (the new pit is drawn by Draw afterwards).
        void Flash(int cell)
        {
            var c = cells[cell];
            if (c.Go != null) Tween(c.Go.transform, set.Get(c.Id).Scale * grid.CellSize);
        }

        IEnumerator Ring(Vector3 at)
        {
            var v = set.Get(ActorKind.LuckyRing);
            if (!v.HasPrefab) yield break;
            var ring = Instantiate(v.prefab, at + v.offset * grid.CellSize, v.RotationFor(0), root);
            transients.Add(ring);
            var rends = ring.GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            Color baseColor = rends.Length > 0 && rends[0].sharedMaterial != null && rends[0].sharedMaterial.HasProperty("_BaseColor")
                ? rends[0].sharedMaterial.GetColor("_BaseColor") : Ui.Gold;
            Vector3 scale = v.Scale * grid.CellSize;
            const float dur = 0.7f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (ring == null) yield break;
                float k = t / dur;
                ring.transform.localScale = scale * Mathf.Lerp(0.3f, 2.2f, 1 - (1 - k) * (1 - k));
                var c = baseColor;
                c.a = baseColor.a * (1 - k);
                block.SetColor("_BaseColor", c);
                foreach (var rend in rends) rend.SetPropertyBlock(block);
                yield return null;
            }
            transients.Remove(ring);
            if (ring != null) Destroy(ring);
        }

        // ---------- Camera ----------

        /// Frames the board with the TileSet's CameraRig inside the part of the screen the HUD leaves free (HudView.BoardFrame).
        void FitCamera()
        {
            if (cam == null || level == null) return;
            fittedAspect = cam.aspect;
            Fit(cam, grid, rigOverride ?? set.cameraRig);
        }

        /// Actor height above the tiles that the framing keeps in view (cells).
        public const float ActorHeight = 1.2f;

        /// The camera framing for a board (rotation, projection, position) at <paramref name="cam"/>'s aspect: the board
        /// (with the rig's padding and actor height) inside HudView.BoardViewport. Public so UiLayoutCheck (14.6) projects the
        /// board exactly as the game does.
        public static void Fit(Camera cam, BoardGrid grid, CameraRig rig)
        {
            if (cam == null) throw new ArgumentNullException(nameof(cam));
            var rot = rig.Rotation;
            cam.transform.rotation = rot;
            cam.orthographic = rig.orthographic;

            // The projected box of the padded board and its actors (tiles at y 0 to ActorHeight) in camera space: min / max,
            // not a symmetric half-size, since actor height makes it reach further up than down (the back row stands up).
            var b = grid.Bounds;
            float pad = rig.padding * grid.CellSize;
            Vector3 ext = new Vector3(b.extents.x + pad, 0, b.extents.z + pad);
            var inv = Quaternion.Inverse(rot);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int k = 0; k < 8; k++)
            {
                var p = new Vector3((k & 1) == 0 ? -ext.x : ext.x, (k & 2) == 0 ? 0 : ActorHeight * grid.CellSize, (k & 4) == 0 ? -ext.z : ext.z);
                var q = inv * p;
                min = Vector2.Min(min, q);
                max = Vector2.Max(max, q);
            }
            // Inside the HUD's board frame (docs/architecture.md 14.6): the box's half-sizes in the frame's share of the screen.
            var frame = HudView.BoardViewport(cam.aspect);
            Vector2 half = (max - min) * 0.5f, mid = (max + min) * 0.5f;
            float needH = Mathf.Max(3.2f, half.y / frame.height, half.x / frame.width / cam.aspect);
            // The camera axis goes through the box centre, then moves so that centre lands on the frame centre.
            Vector3 center = b.center + rot * new Vector3(mid.x, mid.y, 0);
            float shiftX = (frame.center.x - 0.5f) * 2f * needH * cam.aspect;
            float lift = (frame.center.y - 0.5f) * 2f * needH;
            float dist = Mathf.Max(rig.distance, 1f);
            if (rig.orthographic)
            {
                cam.orthographicSize = needH;
            }
            else
            {
                cam.fieldOfView = rig.fieldOfView > 1f ? rig.fieldOfView : 30f;
                dist = Mathf.Max(dist, needH / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            }
            cam.transform.position = center - (rot * Vector3.forward) * dist - (rot * Vector3.up) * lift - (rot * Vector3.right) * shiftX;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = dist * 2f + 50f;
        }

        float CellPixels()
        {
            float h = labelLayer.rect.height;
            if (cam.orthographic) return h / (2f * cam.orthographicSize);
            float d = Vector3.Distance(cam.transform.position, grid.Bounds.center);
            return h / (2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        void LateUpdate()
        {
            if (level == null || cam == null) return;
            if (!Mathf.Approximately(cam.aspect, fittedAspect)) FitCamera();
            float cellPx = CellPixels() * grid.CellSize;
            for (int k = 0; k < labels.Count; k++)
            {
                var l = labels[k];
                if (l.Text == null || !l.Text.enabled) continue;
                Vector3 world = l.Follow != null ? l.Follow.position + l.World : l.World;
                Vector3 sp = cam.WorldToScreenPoint(world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelLayer, sp, null, out var local);
                l.Text.rectTransform.anchoredPosition = local;
                l.Text.fontSize = Mathf.Max(10, Mathf.RoundToInt(cellPx * l.Size));
            }
            if (playerLabel != null) playerLabel.Text.enabled = player.activeSelf && StepNumbers;
            // KEEP/SWITCH choice: the player breathes until a choice is made.
            if (choosing && anim == null && player.activeSelf)
                player.transform.localScale = PlayerScale * (1f + 0.06f * Mathf.Sin(Time.time * 6f));
            if (Hidden) HideAll();
        }

        /// Smoke check negative control (LaunchOptions.SmokeView.Empty): the board still plays, nothing of it is drawn.
        /// Checked every frame because pieces are spawned and labels enabled while playing; smoke runs only.
        public bool Hidden { get; set; }

        void HideAll()
        {
            if (root != null) foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var l in labels) if (l.Text != null) l.Text.enabled = false;
            if (marks != null) marks.gameObject.SetActive(false);
        }

        static class ListPool
        {
            static readonly List<int> ints = new List<int>(32);
            public static List<int> Ints { get { ints.Clear(); return ints; } }
        }
    }
}
