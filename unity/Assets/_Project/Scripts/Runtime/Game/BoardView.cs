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
        readonly List<GameObject> transients = new List<GameObject>();
        readonly StringBuilder sb = new StringBuilder(16);
        GameObject player, startMarker, oldStartMarker;
        WorldLabel playerLabel;
        bool choosing;

        Coroutine anim;
        Coroutine shake;

        /// True while the session has queued input: animations play faster (docs/architecture.md 4.1).
        public Func<bool> Hurry;

        public BoardGrid Grid => grid;
        public bool IsAnimating => anim != null;

        public void Init(TileSet tileSet, RectTransform labelLayer, Camera cam)
        {
            set = tileSet;
            this.labelLayer = labelLayer;
            this.cam = cam;
        }

        // ---------- IBoardPresenter ----------

        public void Rebuild(State s)
        {
            StopAnim();
            if (root != null) Destroy(root.gameObject);
            foreach (var l in labels) if (l.Text != null) Destroy(l.Text.gameObject);
            labels.Clear();
            statues.Clear();
            transients.Clear();

            level = s.Level;
            grid = set.GridFor(level);
            root = new GameObject("Board").transform;
            root.SetParent(transform, false);
            int n = level.Count;
            cells = new Cell[n];
            feet = new GameObject[n];
            footLabels = new WorldLabel[n];
            for (int i = 0; i < n; i++) cells[i] = new Cell();

            startMarker = Spawn(set.Get(ActorKind.StartMarker), s.Start, "Start");
            oldStartMarker = Spawn(set.Get(ActorKind.OldStartMarker), level.Start, "Old Start");
            var pv = set.Get(ActorKind.Player);
            player = Spawn(pv, s.Pos, "Player") ?? new GameObject("Player");
            player.transform.SetParent(root, true);
            playerLabel = AddLabel("7", Vector3.zero, 0.42f, Ui.Ink);
            playerLabel.Follow = player.transform;
            playerLabel.World = pv.LabelOffset(false) * grid.CellSize;

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
        }

        float Speed => Hurry != null && Hurry() ? 2f : 1f;

        IEnumerator Animate(State prev, StepResult r, Action done)
        {
            var pt = player.transform;
            var lift = set.Get(ActorKind.Player).offset * grid.CellSize;
            foreach (int cell in r.Path)
            {
                Vector3 from = pt.position, to = grid.CellToWorld(cell) + lift;
                float dur = StepTime / Speed;
                for (float t = 0; t < dur; t += Time.deltaTime)
                {
                    float k = t / dur;
                    pt.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.12f * grid.CellSize);
                    yield return null;
                }
                pt.position = to;
            }

            foreach (var e in r.Events)
            {
                if (e.StepIdx == Rules.Steps && e.At >= 0) StartCoroutine(Ring(grid.CellToWorld(e.At)));
                if (e.Type == GameEventType.Crumble && e.At >= 0) Flash(e.At);
                if (e.Type == GameEventType.Fall || e.Type == GameEventType.Bridge)
                {
                    Vector3 from = pt.position;
                    float dur = 0.3f / Speed;
                    for (float t = 0; t < dur; t += Time.deltaTime)
                    {
                        float k = t / dur;
                        pt.position = from + Vector3.down * (k * k * 1.2f * grid.CellSize);
                        pt.localScale = BaseScale(ActorKind.Player) * (1 - k * 0.6f);
                        yield return null;
                    }
                }
            }

            Draw(r.State, true, prev);
            if (r.Has(GameEventType.Respawn)) StartCoroutine(Pop(player.transform, BaseScale(ActorKind.Player)));
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
                    if (pop && c.Go != null && id.Kind == TileKind.Bridge) StartCoroutine(Pop(c.Go.transform, c.Go.transform.localScale));
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
            foreach (int i in gone) { Destroy(statues[i]); statues.Remove(i); }
            gone.Clear();
            foreach (int i in s.Statues)
            {
                if (statues.ContainsKey(i)) continue;
                var st = Spawn(set.Get(ActorKind.Statue), i, $"Statue {i}");
                statues[i] = st;
                if (pop && st != null) StartCoroutine(Pop(st.transform, st.transform.localScale));
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
            foreach (var f in s.Trail)
            {
                if (f.Step <= 0 || f.Cell == s.Pos) continue;
                EnsureFoot(f.Cell);
                var t = footLabels[f.Cell].Text;
                t.text = t.text.Length == 0 ? f.Step.ToString() : t.text + "·" + f.Step;
                if (!marked.Contains(f.Cell)) marked.Add(f.Cell);
            }
            for (int i = 0; i < n; i++)
            {
                if (footLabels[i] == null) continue;
                bool has = marked.Contains(i);
                if (feet[i] != null) feet[i].SetActive(has);
                footLabels[i].Text.enabled = has;
            }
            marked.Clear();

            // Player
            bool alive = s.Status != Status.Lose;
            player.SetActive(alive);
            Place(player, set.Get(ActorKind.Player), s.Pos);
            player.transform.localScale = BaseScale(ActorKind.Player);
            playerLabel.Text.enabled = alive;
            int left = Rules.Steps - s.Used;
            playerLabel.Text.text = left.ToString();
            choosing = s.Status == Status.Choose;
            playerLabel.Text.color = choosing || left == 1 ? Ui.Gold : Ui.Ink;
        }

        /// TileVisual "active" (TileVisual.cs): door open, gate passable on the next step, plate pressed, exit on the 7th step.
        /// Answers come from Core queries and State fields; nothing is recomputed here.
        static bool IsActive(State s, TileId id, int i)
        {
            switch (id.Kind)
            {
                case TileKind.Door: return Rules.DoorOpen(s, (char)('A' + id.Variant));
                case TileKind.CountDoor: return Rules.DoorOpen(s, 'Q');
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
            var mat = active ? v.activeMaterial : v.inactiveMaterial;
            if (mat != null)
            {
                var target = string.IsNullOrEmpty(v.materialTarget) ? go.transform : go.transform.Find(v.materialTarget);
                if (target != null) foreach (var rend in target.GetComponentsInChildren<Renderer>(true)) rend.sharedMaterial = mat;
            }
            string trigger = active ? v.activateTrigger : v.deactivateTrigger;
            var animator = string.IsNullOrEmpty(trigger) ? null : go.GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(trigger);
            else if (animate) StartCoroutine(Pop(go.transform, go.transform.localScale));
        }

        void SetCellLabel(Cell c, TileId id, int i, TileVisual v)
        {
            string text = id.Kind switch
            {
                TileKind.Gate => id.Variant.ToString(),
                TileKind.Plate => ((char)('A' + id.Variant)).ToString(),
                TileKind.Door => ((char)('A' + id.Variant)).ToString(),
                TileKind.Exit => "출구",
                TileKind.CountDoor => level.StatueCount.ToString(),
                _ => null,
            };
            if (text == null)
            {
                if (c.Label != null) c.Label.Text.enabled = false;
                return;
            }
            float size = id.Kind switch { TileKind.Gate => 0.5f, TileKind.Exit => 0.26f, _ => 0.4f };
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
            Place(go, v, cell);
            go.transform.localScale = v.Scale * grid.CellSize;
            return go;
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
            if (c.Go != null) StartCoroutine(Pop(c.Go.transform, c.Go.transform.localScale));
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

        /// Frames the board with the TileSet's CameraRig inside the band the HUD leaves free (about 52% of the height).
        void FitCamera()
        {
            if (cam == null || level == null) return;
            fittedAspect = cam.aspect;
            var rig = set.cameraRig;
            var rot = rig.Rotation;
            cam.transform.rotation = rot;
            cam.orthographic = rig.orthographic;

            // Board extents (with a little height for actors) in camera space.
            var b = grid.Bounds;
            float pad = rig.padding * grid.CellSize;
            Vector3 ext = new Vector3(b.extents.x + pad, 0, b.extents.z + pad);
            var inv = Quaternion.Inverse(rot);
            float halfX = 0, halfY = 0;
            for (int k = 0; k < 8; k++)
            {
                var p = new Vector3((k & 1) == 0 ? -ext.x : ext.x, (k & 2) == 0 ? 0 : 1.2f * grid.CellSize, (k & 4) == 0 ? -ext.z : ext.z);
                var q = inv * p;
                halfX = Mathf.Max(halfX, Mathf.Abs(q.x));
                halfY = Mathf.Max(halfY, Mathf.Abs(q.y));
            }
            float needH = Mathf.Max(3.2f, halfY / 0.52f, halfX / 0.9f / cam.aspect);
            Vector3 center = b.center + Vector3.up * 0.3f;
            float lift = 0.05f * 2f * needH;
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
            cam.transform.position = center - (rot * Vector3.forward) * dist - (rot * Vector3.up) * lift;
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
            if (playerLabel != null) playerLabel.Text.enabled = player.activeSelf;
            // KEEP/SWITCH choice: the player breathes until a choice is made.
            if (choosing && anim == null && player.activeSelf)
                player.transform.localScale = BaseScale(ActorKind.Player) * (1f + 0.06f * Mathf.Sin(Time.time * 6f));
        }

        static class ListPool
        {
            static readonly List<int> ints = new List<int>(32);
            public static List<int> Ints { get { ints.Clear(); return ints; } }
        }
    }
}
