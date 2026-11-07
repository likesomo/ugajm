using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Checks every TileSet in Data/TileSets against docs/architecture.md 7 and 12 (prefab spec: 1 cell = cellSize,
    /// floor top at y = 0, hooks that exist). Errors = the board cannot draw something; warnings = likely wrong placement.
    /// Menu: Seven Steps > Architecture > Validate TileSets. Batch: -executeMethod SevenSteps.EditorTools.TileSetValidator.BatchValidate
    /// (exit 1 on errors). Report: Logs/batch-tileset-validate.log via unity.sh, or the Console.
    /// </summary>
    public static class TileSetValidator
    {
        const float Tolerance = 0.06f;

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
            foreach (var guid in AssetDatabase.FindAssets("t:TileSet", new[] { TileSetBuilder.Dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ts = AssetDatabase.LoadAssetAtPath<TileSet>(path);
                var issues = new List<string>();
                int e = Validate(ts, issues);
                errors += e;
                sb.AppendLine($"[SevenSteps] TileSet {path}: {(e > 0 ? "FAIL" : "OK")} ({e} errors, {issues.Count - e} warnings)");
                foreach (var i in issues) sb.AppendLine("  " + i);
            }
            report = sb.ToString();
            return errors;
        }

        public static int Validate(TileSet ts, List<string> issues)
        {
            int errors = 0;
            float cs = ts.cellSize;
            void Check(string name, TileVisual v, bool required, bool flatTop, bool fitsCell)
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

                if (!Bounds(v, cs, out var b)) { issues.Add($"WARN {name}: no renderers"); return; }
                if (fitsCell && (b.size.x > cs * (1 + Tolerance) || b.size.z > cs * (1 + Tolerance)))
                    issues.Add($"WARN {name}: footprint {b.size.x:0.00}x{b.size.z:0.00} is bigger than one cell ({cs:0.00})");
                if (fitsCell && (Mathf.Abs(b.center.x) > cs * Tolerance || Mathf.Abs(b.center.z) > cs * Tolerance))
                    issues.Add($"WARN {name}: not centered on the cell (center {b.center.x:0.00}, {b.center.z:0.00}); fix with offset");
                if (flatTop && Mathf.Abs(b.max.y) > cs * Tolerance)
                    issues.Add($"WARN {name}: top at y={b.max.y:0.00}, floor tiles must have their top at y=0; fix with offset.y");
            }

            Check("floor", ts.floor, true, true, true);
            Check("floorAlt", ts.floorAlt, false, true, true);
            Check("wall", ts.wall, true, false, true);
            Check("pit", ts.pit, true, false, true);
            Check("ice", ts.ice, true, true, true);
            Check("cracked", ts.cracked, true, true, true);
            Check("bridge", ts.bridge, true, false, true);
            Check("exit", ts.exit, true, false, true);
            Check("gate", ts.gate, true, false, true);
            for (int k = 0; k < 3; k++) Check($"plates[{k}]", At(ts.plates, k), true, false, true);
            for (int k = 0; k < 3; k++) Check($"doors[{k}]", At(ts.doors, k), true, false, true);
            Check("countDoor", ts.countDoor, true, false, true);
            Check("lever", ts.lever, true, false, true);
            for (int k = 0; k < 2; k++) Check($"flipWalls[{k}]", At(ts.flipWalls, k), true, false, true);
            Check("player", ts.player, true, false, true);
            Check("statue", ts.statue, true, false, true);
            Check("startMarker", ts.startMarker, true, false, false);
            Check("oldStartMarker", ts.oldStartMarker, false, false, false);
            Check("footprint", ts.footprint, false, false, false);
            Check("luckyRing", ts.luckyRing, false, false, false);

            if (ts.projection == BoardProjection.Quarter && Mathf.Abs(ts.cameraRig.yaw) > 0.01f)
                issues.Add($"WARN camera yaw {ts.cameraRig.yaw}: arrow keys are map directions; GameController must rotate input (docs 12.2)");
            return errors;
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
