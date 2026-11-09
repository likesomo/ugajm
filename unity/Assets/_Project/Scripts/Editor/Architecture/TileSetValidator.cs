using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Checks every TileSet under Assets/_Project (Data/TileSets placeholder, Art/<Set>/ art sets) against docs/architecture.md 7 and 12
    /// (prefab spec: 1 cell = cellSize, floor top at y = 0, wall height, hooks that exist, meshes/materials resolved). Errors = the board cannot draw something; warnings = likely wrong placement.
    /// Menu: Seven Steps > Architecture > Validate TileSets. Batch: -executeMethod SevenSteps.EditorTools.TileSetValidator.BatchValidate
    /// (exit 1 on errors). Report: Logs/batch-tileset-validate.log via unity.sh, or the Console.
    /// </summary>
    public static class TileSetValidator
    {
        const float Tolerance = 0.06f;
        /// Search roots (docs 12.7). A TileSet anywhere else is reported as an error.
        public static readonly string[] Roots = { "Assets/_Project" };
        static readonly string[] AllowedPrefixes = { TileSetBuilder.Dir + "/", "Assets/_Project/Art/" };
        /// Walls, doors, raised flip walls: max height in cells (docs 12.3, front 3/4 view).
        const float MaxWallHeight = 0.6f;

        [MenuItem("Seven Steps/Architecture/Validate TileSets", priority = 41)]
        public static void ValidateMenu()
        {
            int errors = ValidateAll(out string report);
            if (errors > 0) Debug.LogError(report); else Debug.Log(report);
        }

        public static void BatchValidate()
        {
            int errors;
            try { errors = ValidateAll(out string report); Debug.Log(report); }
            catch (System.Exception e) { Debug.LogException(e); errors = 1; }
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }

        public static int ValidateAll(out string report)
        {
            var sb = new StringBuilder();
            int errors = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TileSet", Roots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ts = AssetDatabase.LoadAssetAtPath<TileSet>(path);
                var issues = new List<string>();
                int e = 0;
                if (System.Array.Find(AllowedPrefixes, pre => path.StartsWith(pre, System.StringComparison.Ordinal)) == null)
                { issues.Add("ERROR location: TileSets live in Data/TileSets (placeholder) or Art/<Set>/ (docs 12.7)"); e++; }
                e += Validate(ts, issues);
                errors += e;
                sb.AppendLine($"[SevenSteps] TileSet {path}: {(e > 0 ? "FAIL" : "OK")} ({e} errors, {issues.Count - e} warnings)");
                foreach (var i in issues) sb.AppendLine("  " + i);
            }
            errors += ValidateChapters(sb);
            report = sb.ToString();
            return errors;
        }

        public static int Validate(TileSet ts, List<string> issues)
        {
            int errors = 0;
            float cs = ts.cellSize;
            void Check(string name, TileVisual v, bool required, bool flatTop, bool fitsCell, bool wallHeight = false)
            {
                if (!v.HasPrefab)
                {
                    if (required) { issues.Add($"ERROR {name}: no prefab"); errors++; }
                    return;
                }
                foreach (var child in new[] { v.activeChild, v.inactiveChild, v.materialTarget })
                    if (!string.IsNullOrEmpty(child) && v.prefab.transform.Find(child) == null)
                    { issues.Add($"ERROR {name}: child \"{child}\" not found in {v.prefab.name}"); errors++; }

                var animator = v.prefab.GetComponentInChildren<Animator>(true);
                foreach (var trig in new[] { v.appearTrigger, v.vanishTrigger, v.activateTrigger, v.deactivateTrigger })
                {
                    if (string.IsNullOrEmpty(trig)) continue;
                    if (animator == null) { issues.Add($"WARN {name}: trigger \"{trig}\" but no Animator (code tween is used)"); continue; }
                    var ctrl = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    bool found = false;
                    if (ctrl != null) foreach (var p in ctrl.parameters) if (p.name == trig && p.type == AnimatorControllerParameterType.Trigger) found = true;
                    if (!found) { issues.Add($"ERROR {name}: Animator has no trigger \"{trig}\""); errors++; }
                }

                foreach (var missing in MissingParts(v.prefab))
                { issues.Add($"ERROR {name}: {missing} (shared folder not linked, or a GUID changed?)"); errors++; }

                if (!Bounds(v, cs, out var b)) { issues.Add($"WARN {name}: no renderers"); return; }
                if (wallHeight && b.max.y > cs * (MaxWallHeight + Tolerance))
                    issues.Add($"WARN {name}: height {b.max.y / cs:0.00} cells, walls must be <= {MaxWallHeight} so they do not hide the cell behind (docs 12.3)");
                if (fitsCell && (b.size.x > cs * (1 + Tolerance) || b.size.z > cs * (1 + Tolerance)))
                    issues.Add($"WARN {name}: footprint {b.size.x:0.00}x{b.size.z:0.00} is bigger than one cell ({cs:0.00})");
                if (fitsCell && (Mathf.Abs(b.center.x) > cs * Tolerance || Mathf.Abs(b.center.z) > cs * Tolerance))
                    issues.Add($"WARN {name}: not centered on the cell (center {b.center.x:0.00}, {b.center.z:0.00}); fix with offset");
                if (flatTop && Mathf.Abs(b.max.y) > cs * Tolerance)
                    issues.Add($"WARN {name}: top at y={b.max.y:0.00}, floor tiles must have their top at y=0; fix with offset.y");
            }

            Check("floor", ts.floor, true, true, true);
            Check("floorAlt", ts.floorAlt, false, true, true);
            Check("wall", ts.wall, true, false, true, wallHeight: true);
            Check("pit", ts.pit, true, false, true);
            Check("ice", ts.ice, true, true, true);
            Check("cracked", ts.cracked, true, true, true);
            Check("bridge", ts.bridge, true, false, true);
            Check("exit", ts.exit, true, false, true);
            Check("gate", ts.gate, true, false, true);
            for (int k = 0; k < 3; k++) Check($"plates[{k}]", At(ts.plates, k), true, false, true);
            for (int k = 0; k < 3; k++) Check($"doors[{k}]", At(ts.doors, k), true, false, true, wallHeight: true);
            Check("countDoor", ts.countDoor, true, false, true, wallHeight: true);
            Check("lever", ts.lever, true, false, true);
            Check("flipWalls[0]", At(ts.flipWalls, 0), true, false, true, wallHeight: true);
            Check("flipWalls[1]", At(ts.flipWalls, 1), true, false, true);
            if (!ts.mud.HasPrefab) issues.Add("WARN mud: no prefab, mud cells are drawn as floor (docs 6.3)");
            Check("mud", ts.mud, false, true, true);
            Check("player", ts.player, true, false, true);
            Check("statue", ts.statue, true, false, true);
            Check("startMarker", ts.startMarker, true, false, false);
            Check("oldStartMarker", ts.oldStartMarker, false, false, false);
            Check("footprint", ts.footprint, false, false, false);
            Check("luckyRing", ts.luckyRing, false, false, false);
            // Per body (12.6): optional; when filled, every entry needs a prefab. 3 = triplets.
            foreach (var (label, arr) in new[] { ("playerBodies", ts.playerBodies), ("statueBodies", ts.statueBodies) })
            {
                if (arr == null || arr.Length == 0) continue;
                if (arr.Length != 3) issues.Add($"WARN {label}: {arr.Length} entries, triplets use 3 (index cycles)");
                for (int k = 0; k < arr.Length; k++) Check($"{label}[{k}]", arr[k], true, false, true);
            }
            if (ts.bodyColors == null || ts.bodyColors.Length != 3)
                issues.Add($"WARN bodyColors: {(ts.bodyColors == null ? 0 : ts.bodyColors.Length)} entries, triplets use 3 (HUD body icons)");

            if (ts.projection == BoardProjection.Quarter && Mathf.Abs(ts.cameraRig.yaw) > 0.01f)
                issues.Add($"WARN camera yaw {ts.cameraRig.yaw}: arrow keys are map directions; GameController must rotate input (docs 12.2)");
            return errors;
        }

        /// ChapterPreset assets (docs 12.8): unique non-empty id, a TileSet, lighting and sky. Their TileSets are checked above.
        static int ValidateChapters(StringBuilder sb)
        {
            int errors = 0;
            var ids = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ChapterPreset", Roots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<ChapterPreset>(path);
                var issues = new List<string>();
                int e = 0;
                if (string.IsNullOrEmpty(c.id)) { issues.Add("ERROR id is empty"); e++; }
                else if (!ids.Add(c.id)) { issues.Add($"ERROR id \"{c.id}\" used by another chapter"); e++; }
                if (c.tileSet == null) { issues.Add("ERROR no tileSet"); e++; }
                if (c.lighting == null) issues.Add("WARN no lighting (scene keeps its current lights)");
                if (c.sky == null) issues.Add("WARN no sky");
                if (c.materialOverrides != null)
                    for (int k = 0; k < c.materialOverrides.Length; k++)
                        if (c.materialOverrides[k].from == null || c.materialOverrides[k].to == null)
                        { issues.Add($"ERROR materialOverrides[{k}] has an empty material"); e++; }
                if (c.sky != null && c.sky.planetPrefab != null && c.sky.planetPrefab.GetComponent<IStageEnvironment>() == null)
                    issues.Add("WARN sky.planetPrefab has no IStageEnvironment on its root (not fitted to the stage size)");
                errors += e;
                sb.AppendLine($"[SevenSteps] Chapter {path}: {(e > 0 ? "FAIL" : "OK")} ({e} errors, {issues.Count - e} warnings)");
                foreach (var i in issues) sb.AppendLine("  " + i);
            }
            return errors;
        }

        /// Meshes or material slots that did not resolve (missing shared-folder asset or broken GUID).
        static IEnumerable<string> MissingParts(GameObject prefab)
        {
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null) yield return $"mesh missing on \"{mf.name}\"";
            foreach (var sk in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sk.sharedMesh == null) yield return $"mesh missing on \"{sk.name}\"";
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                    if (mats[k] == null) yield return $"material slot {k} empty on \"{r.name}\"";
                    else if (mats[k].shader == null || mats[k].shader.name == "Hidden/InternalErrorShader") yield return $"shader missing on \"{r.name}\" ({mats[k].name})";
            }
        }

        static TileVisual At(TileVisual[] arr, int k) => arr != null && k < arr.Length ? arr[k] : default;

        /// Renderer bounds of the prefab placed at a cell anchor at the origin, with the TileVisual transform applied.
        static bool Bounds(TileVisual v, float cs, out Bounds b)
        {
            b = default;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(v.prefab);
            try
            {
                go.transform.SetPositionAndRotation(v.offset * cs, v.RotationFor(0));
                go.transform.localScale = v.Scale * cs;
                bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                return any;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
