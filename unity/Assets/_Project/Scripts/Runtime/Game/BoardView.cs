using System.Collections;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Draws a State on the grid. Reads state only; never changes it.
    /// Cell (x, y) sits at world (x, 0, -y) around the board center, so screen-up is map-up.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float Pitch = 58f;
        const float StepTime = 0.1f;

        sealed class WorldLabel
        {
            public Text Text;
            public Vector3 World;
            public Transform Follow;
            public float Size; // font size in cells
        }

        GameAssets a;
        RectTransform labelLayer;
        Camera cam;
        float fittedAspect;

        Transform root;
        Level level;
        GameObject[] tiles;
        char[] shown;
        GameObject[] feet;
        WorldLabel[] footLabels;
        WorldLabel[] cellLabels;
        readonly List<WorldLabel> labels = new List<WorldLabel>();
        readonly Dictionary<int, GameObject> statues = new Dictionary<int, GameObject>();
        GameObject player, startMarker, oldStartMarker;
        WorldLabel playerLabel;

        Coroutine anim;
        State pending;

        public void Init(GameAssets assets, RectTransform labelLayer, Camera cam)
        {
            a = assets;
            this.labelLayer = labelLayer;
            this.cam = cam;
        }

        public Vector3 CellPos(int i) =>
            new Vector3(level.X(i) - (level.W - 1) / 2f, 0, (level.H - 1) / 2f - level.Y(i));

        public int CellAt(Vector3 world)
        {
            int x = Mathf.RoundToInt(world.x + (level.W - 1) / 2f);
            int y = Mathf.RoundToInt((level.H - 1) / 2f - world.z);
            return x < 0 || y < 0 || x >= level.W || y >= level.H ? -1 : y * level.W + x;
        }

        /// Rebuild everything for a new stage.
        public void Build(State s)
        {
            StopAnim();
            if (root != null) Destroy(root.gameObject);
            foreach (var l in labels) if (l.Text != null) Destroy(l.Text.gameObject);
            labels.Clear();
            statues.Clear();

            level = s.Level;
            root = new GameObject("Board").transform;
            root.SetParent(transform, false);
            int n = level.Count;
            tiles = new GameObject[n];
            shown = new char[n];
            feet = new GameObject[n];
            footLabels = new WorldLabel[n];
            cellLabels = new WorldLabel[n];

            for (int i = 0; i < n; i++)
            {
                char t = level.Tile(i);
                if (t >= '1' && t <= '7') cellLabels[i] = AddLabel(t.ToString(), CellPos(i) + Vector3.up * 0.95f, 0.5f, Ui.Ink);
                else if (t >= 'a' && t <= 'c') cellLabels[i] = AddLabel(char.ToUpperInvariant(t).ToString(), CellPos(i) + Vector3.up * 0.05f, 0.4f, Ui.Ink);
                else if (t >= 'A' && t <= 'C') cellLabels[i] = AddLabel(t.ToString(), CellPos(i) + Vector3.up * 0.65f, 0.4f, Ui.Ink);
                else if (t == 'E') cellLabels[i] = AddLabel("출구", CellPos(i) + Vector3.up * 0.05f, 0.26f, Ui.Ink);
            }

            startMarker = Instantiate(a.startMarker, root);
            oldStartMarker = Instantiate(a.oldStartMarker, root);
            player = Instantiate(a.player, root);
            player.name = "Player";
            playerLabel = AddLabel("7", Vector3.zero, 0.42f, Ui.Ink);
            playerLabel.Follow = player.transform;
            playerLabel.World = Vector3.up * 1.4f;

            FitCamera();
            ApplyState(s, false);
        }

        /// Show a new state. With animate, the player walks the command's path first, then the board catches up.
        public void Show(State s, StepResult r, bool animate)
        {
            StopAnim();
            if (!animate || r == null) { ApplyState(s, false); return; }
            pending = s;
            anim = StartCoroutine(Animate(s, r));
        }

        void StopAnim()
        {
            if (anim == null) return;
            StopCoroutine(anim);
            anim = null;
            if (pending != null) ApplyState(pending, false);
            pending = null;
        }

        IEnumerator Animate(State s, StepResult r)
        {
            var pt = player.transform;
            foreach (int cell in r.Path)
            {
                Vector3 from = pt.position, to = CellPos(cell);
                for (float t = 0; t < StepTime; t += Time.deltaTime)
                {
                    float k = t / StepTime;
                    pt.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.12f);
                    yield return null;
                }
                pt.position = to;
            }

            foreach (var e in r.Events)
            {
                if (e.StepIdx == Rules.Steps && e.At >= 0) StartCoroutine(Ring(CellPos(e.At)));
                if (e.Type == GameEventType.Fall || e.Type == GameEventType.Bridge)
                {
                    Vector3 from = pt.position;
                    for (float t = 0; t < 0.3f; t += Time.deltaTime)
                    {
                        float k = t / 0.3f;
                        pt.position = from + Vector3.down * (k * k * 1.2f);
                        pt.localScale = Vector3.one * (1 - k * 0.6f);
                        yield return null;
                    }
                }
            }

            ApplyState(s, true);
            if (r.Has(GameEventType.Respawn)) StartCoroutine(Pop(player.transform, 0f));
            anim = null;
            pending = null;
        }

        void ApplyState(State s, bool pop)
        {
            int n = level.Count;
            for (int i = 0; i < n; i++)
            {
                char t = Rules.TileAt(s, i);
                if (shown[i] != t)
                {
                    if (tiles[i] != null) Destroy(tiles[i]);
                    var prefab = TilePrefab(t, i);
                    tiles[i] = prefab != null ? Instantiate(prefab, CellPos(i), Quaternion.identity, root) : null;
                    shown[i] = t;
                    if (pop && tiles[i] != null && t == '=') StartCoroutine(Pop(tiles[i].transform, 0f));
                }

                if (t >= 'A' && t <= 'C')
                {
                    bool open = Rules.DoorOpen(s, t);
                    var bars = tiles[i].transform.Find("Bars");
                    if (bars != null) bars.gameObject.SetActive(!open);
                    if (cellLabels[i] != null)
                    {
                        cellLabels[i].World = CellPos(i) + Vector3.up * (open ? 0.05f : 0.65f);
                        cellLabels[i].Text.color = open ? Ui.Dim : Ui.Ink;
                    }
                }
                else if (t >= '1' && t <= '7')
                {
                    bool go = s.Status == Status.Play && t - '0' == s.Used + 1;
                    var arch = tiles[i].transform.Find("Arch");
                    if (arch != null)
                        foreach (var rend in arch.GetComponentsInChildren<Renderer>()) rend.sharedMaterial = go ? a.gateGo : a.gateIdle;
                    if (cellLabels[i] != null) cellLabels[i].Text.color = go ? Ui.Gold : Ui.Ink;
                }
                else if (cellLabels[i] != null)
                {
                    // Plate/exit letter; hidden while a statue or the player stands on it.
                    cellLabels[i].Text.enabled = !Rules.Contains(s.Statues, i) && !(i == s.Pos && s.Status != Status.Lose);
                    if (t == 'E') cellLabels[i].Text.color = s.Status == Status.Play && s.Used == Rules.Steps - 1 ? Ui.Gold : Ui.Ink;
                }
            }

            // Statues
            var gone = new List<int>();
            foreach (var kv in statues) if (!Rules.Contains(s.Statues, kv.Key)) gone.Add(kv.Key);
            foreach (int i in gone) { Destroy(statues[i]); statues.Remove(i); }
            foreach (int i in s.Statues)
            {
                if (statues.ContainsKey(i)) continue;
                var st = Instantiate(a.statue, CellPos(i), Quaternion.identity, root);
                st.name = $"Statue {i}";
                statues[i] = st;
                if (pop) StartCoroutine(Pop(st.transform, 0f));
            }

            // Start markers
            startMarker.transform.position = CellPos(s.Start);
            oldStartMarker.SetActive(s.Start != level.Start);
            oldStartMarker.transform.position = CellPos(level.Start);

            // Footprints: step numbers on every cell this body walked through (not under the player).
            var marks = new Dictionary<int, StringBuilder>();
            foreach (var f in s.Trail)
            {
                if (f.Step <= 0 || f.Cell == s.Pos) continue;
                if (!marks.TryGetValue(f.Cell, out var sb)) marks[f.Cell] = sb = new StringBuilder();
                else sb.Append('·');
                sb.Append(f.Step);
            }
            for (int i = 0; i < n; i++)
            {
                bool has = marks.TryGetValue(i, out var sb);
                if (has && feet[i] == null)
                {
                    feet[i] = Instantiate(a.footprint, CellPos(i), Quaternion.identity, root);
                    footLabels[i] = AddLabel("", CellPos(i) + Vector3.up * 0.02f, 0.3f, Ui.Dim);
                }
                if (feet[i] == null) continue;
                feet[i].SetActive(has);
                footLabels[i].Text.enabled = has;
                if (has) footLabels[i].Text.text = sb.ToString();
            }

            // Player
            bool alive = s.Status != Status.Lose;
            player.SetActive(alive);
            player.transform.position = CellPos(s.Pos);
            player.transform.localScale = Vector3.one;
            playerLabel.Text.enabled = alive;
            int left = Rules.Steps - s.Used;
            playerLabel.Text.text = left.ToString();
            playerLabel.Text.color = s.Status == Status.Choose || left == 1 ? Ui.Gold : Ui.Ink;
        }

        GameObject TilePrefab(char t, int i)
        {
            if (t >= '1' && t <= '7') return a.gate;
            if (t >= 'a' && t <= 'c') return a.Plate(t);
            if (t >= 'A' && t <= 'C') return a.Door(t);
            switch (t)
            {
                case '#': return a.wall;
                case 'o': return a.pit;
                case '~': return a.ice;
                case 'x': return a.cracked;
                case '=': return a.bridge;
                case 'E': return a.exit;
                default: return (level.X(i) + level.Y(i)) % 2 == 0 ? a.floorA : a.floorB;
            }
        }

        WorldLabel AddLabel(string text, Vector3 world, float size, Color color)
        {
            var t = Ui.Label(labelLayer, text, 20, color);
            t.fontStyle = FontStyle.Bold;
            Ui.AddOutline(t, new Color(0, 0, 0, 0.75f));
            var l = new WorldLabel { Text = t, World = world, Size = size };
            labels.Add(l);
            return l;
        }

        IEnumerator Pop(Transform t, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.22f)
            {
                if (t == null) yield break;
                float u = k - 1; // ease-out-back: overshoots a little, then settles at 1
                t.localScale = Vector3.one * (1 + 2.70158f * u * u * u + 1.70158f * u * u);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        IEnumerator Ring(Vector3 at)
        {
            var ring = Instantiate(a.luckyRing, at, Quaternion.identity, root);
            var rends = ring.GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            Color baseColor = rends.Length > 0 && rends[0].sharedMaterial.HasProperty("_BaseColor")
                ? rends[0].sharedMaterial.GetColor("_BaseColor") : Ui.Gold;
            const float dur = 0.7f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (ring == null) yield break;
                float k = t / dur;
                ring.transform.localScale = new Vector3(1, 1, 1) * Mathf.Lerp(0.3f, 2.2f, 1 - (1 - k) * (1 - k));
                var c = baseColor;
                c.a = baseColor.a * (1 - k);
                block.SetColor("_BaseColor", c);
                foreach (var rend in rends) rend.SetPropertyBlock(block);
                yield return null;
            }
            if (ring != null) Destroy(ring);
        }

        void FitCamera()
        {
            if (cam == null || level == null) return;
            fittedAspect = cam.aspect;
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(Pitch, 0, 0);
            float rad = Pitch * Mathf.Deg2Rad;
            float projH = level.H * Mathf.Sin(rad) + 1.2f * Mathf.Cos(rad);
            // HUD takes ~130px on top and ~200px (KEEP/SWITCH panel) at the bottom of 720: the board gets the band
            // between, about 52% of the height, centered ~5% above the screen middle.
            float halfH = projH / 2f / 0.52f;
            float halfW = level.W / 2f / 0.9f / cam.aspect;
            cam.orthographicSize = Mathf.Max(3.2f, halfH, halfW);
            float lift = 0.05f * 2f * cam.orthographicSize;
            cam.transform.position = new Vector3(0, 0.3f, 0) - cam.transform.forward * 30f - cam.transform.up * lift;
        }

        void LateUpdate()
        {
            if (level == null || cam == null) return;
            if (!Mathf.Approximately(cam.aspect, fittedAspect)) FitCamera();
            float h = labelLayer.rect.height;
            float cellPx = h / (2f * cam.orthographicSize);
            foreach (var l in labels)
            {
                if (l.Text == null || !l.Text.enabled) continue;
                Vector3 world = l.Follow != null ? l.Follow.position + l.World : l.World;
                Vector3 sp = cam.WorldToScreenPoint(world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelLayer, sp, null, out var local);
                l.Text.rectTransform.anchoredPosition = local;
                l.Text.fontSize = Mathf.Max(10, Mathf.RoundToInt(cellPx * l.Size));
            }
            if (playerLabel != null) playerLabel.Text.enabled = player.activeSelf;
        }
    }
}
