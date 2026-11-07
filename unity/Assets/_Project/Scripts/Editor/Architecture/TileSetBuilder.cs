using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Builds Data/TileSets/TopDown_Placeholder.asset from the v0 placeholder prefabs referenced by GameAssets,
    /// with the same offsets, child hooks and materials BoardView uses in v0. Run after ProjectSetup.
    /// Real art gets its own TileSet asset (made by the art agent); this one stays as the fallback.
    /// Batch: -executeMethod SevenSteps.EditorTools.TileSetBuilder.BatchBuild
    /// </summary>
    public static class TileSetBuilder
    {
        public const string Dir = "Assets/_Project/Data/TileSets";
        public const string PlaceholderPath = Dir + "/TopDown_Placeholder.asset";

        [MenuItem("Seven Steps/Architecture/Build Placeholder TileSet", priority = 40)]
        public static void Build()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(ProjectSetup.AssetsPath);
            if (a == null) throw new System.Exception($"[SevenSteps] {ProjectSetup.AssetsPath} missing. Run setup first.");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Data", "TileSets");

            var ts = AssetDatabase.LoadAssetAtPath<TileSet>(PlaceholderPath);
            bool isNew = ts == null;
            if (isNew) ts = ScriptableObject.CreateInstance<TileSet>();

            ts.projection = BoardProjection.TopDown;
            ts.cellSize = 1f;
            ts.cameraRig = CameraRig.TopDown;

            ts.floor = V(a.floorA);
            ts.floorAlt = V(a.floorB);
            ts.wall = V(a.wall);
            ts.pit = V(a.pit);
            ts.ice = V(a.ice);
            ts.cracked = V(a.cracked);
            ts.bridge = V(a.bridge);
            ts.exit = V(a.exit, label: 0.05f);
            ts.gate = V(a.gate, label: 0.95f);
            ts.gate.materialTarget = "Arch";
            ts.gate.activeMaterial = a.gateGo;
            ts.gate.inactiveMaterial = a.gateIdle;
            ts.plates = new[] { V(a.plateA, label: 0.05f), V(a.plateB, label: 0.05f), V(a.plateC, label: 0.05f) };
            ts.doors = new[] { Door(a.doorA), Door(a.doorB), Door(a.doorC) };
            ts.countDoor = Door(a.doorA); // placeholder until art makes a statue-count door

            ts.player = V(a.player, label: 1.4f);
            ts.statue = V(a.statue);
            ts.startMarker = V(a.startMarker);
            ts.oldStartMarker = V(a.oldStartMarker);
            ts.footprint = V(a.footprint, label: 0.02f);
            ts.luckyRing = V(a.luckyRing);

            if (isNew) AssetDatabase.CreateAsset(ts, PlaceholderPath);
            EditorUtility.SetDirty(ts);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SevenSteps] TileSet written: {PlaceholderPath}");
        }

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static TileVisual V(GameObject prefab, float label = 0f) =>
            new TileVisual { prefab = prefab, scale = Vector3.one, labelOffset = new Vector3(0, label, 0) };

        static TileVisual Door(GameObject prefab)
        {
            var v = V(prefab, label: 0.65f);
            v.inactiveChild = "Bars";
            v.activeLabelOffset = new Vector3(0, 0.05f, 0);
            return v;
        }
    }
}
