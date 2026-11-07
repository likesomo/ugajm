using System;
using SevenSteps.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Generates the placeholder art (URP materials + primitive-built prefabs), the GameAssets bundle, the Game scene,
    /// and the build/player settings. Safe to re-run: existing assets are updated in place (GUIDs kept).
    /// Menu: Seven Steps > Build Assets and Scene. Batch: -executeMethod SevenSteps.EditorTools.ProjectSetup.BatchSetup
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string MatDir = Root + "/Art/Materials";
        const string PrefabDir = Root + "/Prefabs/Generated";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/Game.unity";
        public const string AssetsPath = DataDir + "/GameAssets.asset";
        public const string LevelsPath = DataDir + "/levels.json";
        const string FontPath = "Assets/Shared/Fonts/Jua-Regular.ttf";   // binary assets: git 밖 공유 폴더 (tools/link-shared-assets.ps1)
        const string AudioDir = "Assets/Shared/Audio";

        [MenuItem("Seven Steps/Build Assets and Scene", priority = 1)]
        public static void BuildAll() => Build(forceScene: false);

        [MenuItem("Seven Steps/Rebuild Game Scene (overwrite)", priority = 2)]
        public static void RebuildScene() => Build(forceScene: true);

        [MenuItem("Seven Steps/Open Game Scene", priority = 20)]
        public static void OpenScene() => EditorSceneManager.OpenScene(ScenePath);

        public static void BatchSetup()
        {
            try
            {
                Build(forceScene: false);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Build(bool forceScene)
        {
            foreach (var dir in new[] { MatDir, PrefabDir, DataDir, SceneDir }) EnsureFolder(dir);
            var assets = BuildGameAssets();
            bool sceneMissing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null;
            if (forceScene || sceneMissing) BuildScene(assets);
            ApplyProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SevenSteps] Setup done. Assets: {AssetsPath}, scene: {ScenePath} ({(forceScene || sceneMissing ? "written" : "kept")})");
        }

        // ---------- Materials ----------

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Material Mat(string name, string hex, float smooth = 0.15f, float emission = 0f, float alpha = 1f, bool unlit = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP shaders not found. Is the Universal Render Pipeline package installed?");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(shader);
            else m.shader = shader;

            var color = Hex(hex);
            color.a = alpha;
            m.SetColor("_BaseColor", color);
            if (!unlit)
            {
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0f);
            }
            if (emission > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Hex(hex) * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            if (alpha < 1f)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Prefabs ----------

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, string name, float yaw = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Transform NewRoot(string name) => new GameObject(name).transform;

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void Floor(Transform root, Material mat) =>
            Part(root, PrimitiveType.Cube, new Vector3(0, -0.1f, 0), new Vector3(0.96f, 0.2f, 0.96f), mat, "Floor");

        static GameObject Save(Transform root)
        {
            string path = $"{PrefabDir}/{root.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        static void Frame(Transform root, Material mat, float size, float thick)
        {
            float o = (size - thick) / 2f;
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.006f, o), new Vector3(size, 0.012f, thick), mat, "N");
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.006f, -o), new Vector3(size, 0.012f, thick), mat, "S");
            Part(root, PrimitiveType.Cube, new Vector3(o, 0.006f, 0), new Vector3(thick, 0.012f, size), mat, "E");
            Part(root, PrimitiveType.Cube, new Vector3(-o, 0.006f, 0), new Vector3(thick, 0.012f, size), mat, "W");
        }

        static void Figure(Transform root, Material body, Material head, Material eyes)
        {
            Part(root, PrimitiveType.Capsule, new Vector3(0, 0.42f, 0), new Vector3(0.5f, 0.42f, 0.5f), body, "Body");
            Part(root, PrimitiveType.Sphere, new Vector3(0, 0.95f, 0), Vector3.one * 0.36f, head, "Head");
            if (eyes != null)
            {
                Part(root, PrimitiveType.Cube, new Vector3(-0.07f, 0.97f, -0.165f), new Vector3(0.05f, 0.07f, 0.03f), eyes, "EyeL");
                Part(root, PrimitiveType.Cube, new Vector3(0.07f, 0.97f, -0.165f), new Vector3(0.05f, 0.07f, 0.03f), eyes, "EyeR");
            }
        }

        static GameAssets BuildGameAssets()
        {
            var floorA = Mat("FloorA", "#5f577a");
            var floorB = Mat("FloorB", "#575070");
            var wall = Mat("Wall", "#1c1925");
            var wallTop = Mat("WallTop", "#25212f");
            var pit = Mat("Pit", "#060509", 0f);
            var ice = Mat("Ice", "#9fd8ef", 0.95f);
            var iceShine = Mat("IceShine", "#e8f8ff", 0.95f);
            var cracked = Mat("Cracked", "#6b5f63");
            var crack = Mat("Crack", "#141119");
            var stone = Mat("Stone", "#9a948a", 0.05f);
            var stoneDark = Mat("StoneDark", "#6e695f", 0.05f);
            var plateA = Mat("PlateA", "#e76f51", 0.3f);
            var plateB = Mat("PlateB", "#2a9d8f", 0.3f);
            var plateC = Mat("PlateC", "#8e7dbe", 0.3f);
            var doorA = Mat("DoorA", "#a8503b", 0.2f);
            var doorB = Mat("DoorB", "#1f7268", 0.2f);
            var doorC = Mat("DoorC", "#665a8a", 0.2f);
            var gateIdle = Mat("GateIdle", "#6d6680");
            var gateGo = Mat("GateGo", "#f2c14e", 0.4f, 0.8f);
            var exit = Mat("Exit", "#f2c14e", 0.5f, 1.4f);
            var exitRim = Mat("ExitRim", "#8a6d2a");
            var startMat = Mat("StartMarker", "#f2c14e", 0.3f, 0.5f);
            var oldStartMat = Mat("OldStartMarker", "#7d7690");
            var foot = Mat("Footprint", "#3f3956");
            var cloak = Mat("Cloak", "#d9774a", 0.25f);
            var skin = Mat("Skin", "#f3d2a8", 0.25f);
            var eyes = Mat("Eyes", "#1b1822");
            var lucky = Mat("LuckyRing", "#ffd76a", 0f, 0f, 0.85f, unlit: true);

            Transform r;
            r = NewRoot("FloorA"); Floor(r, floorA); var pFloorA = Save(r);
            r = NewRoot("FloorB"); Floor(r, floorB); var pFloorB = Save(r);

            r = NewRoot("Wall");
            Part(r, PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(1f, 0.8f, 1f), wall, "Block");
            Part(r, PrimitiveType.Cube, new Vector3(0, 0.605f, 0), new Vector3(0.92f, 0.012f, 0.92f), wallTop, "Top");
            var pWall = Save(r);

            r = NewRoot("Pit");
            Part(r, PrimitiveType.Cube, new Vector3(0, -0.95f, 0), new Vector3(0.96f, 0.1f, 0.96f), pit, "Bottom");
            var pPit = Save(r);

            r = NewRoot("Ice");
            Floor(r, ice);
            Part(r, PrimitiveType.Cube, new Vector3(-0.1f, 0.004f, 0.1f), new Vector3(0.5f, 0.008f, 0.06f), iceShine, "Shine", 35);
            var pIce = Save(r);

            r = NewRoot("Cracked");
            Floor(r, cracked);
            Part(r, PrimitiveType.Cube, new Vector3(-0.05f, 0.004f, 0.08f), new Vector3(0.7f, 0.008f, 0.04f), crack, "CrackA", 28);
            Part(r, PrimitiveType.Cube, new Vector3(0.12f, 0.004f, -0.12f), new Vector3(0.45f, 0.008f, 0.035f), crack, "CrackB", -50);
            var pCracked = Save(r);

            r = NewRoot("Bridge");
            Floor(r, stone);
            Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.02f, 0.5f), stoneDark, "Mark");
            var pBridge = Save(r);

            r = NewRoot("Exit");
            Floor(r, floorA);
            Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(0.86f, 0.01f, 0.86f), exitRim, "Rim");
            Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0), new Vector3(0.68f, 0.015f, 0.68f), exit, "Portal");
            var pExit = Save(r);

            r = NewRoot("Gate");
            Floor(r, floorA);
            var arch = Group(r, "Arch");
            Part(arch, PrimitiveType.Cube, new Vector3(-0.39f, 0.4f, 0), new Vector3(0.14f, 0.8f, 0.2f), gateIdle, "PillarL");
            Part(arch, PrimitiveType.Cube, new Vector3(0.39f, 0.4f, 0), new Vector3(0.14f, 0.8f, 0.2f), gateIdle, "PillarR");
            Part(arch, PrimitiveType.Cube, new Vector3(0, 0.84f, 0), new Vector3(0.92f, 0.14f, 0.24f), gateIdle, "Lintel");
            var pGate = Save(r);

            GameObject PlatePrefab(string name, Material m)
            {
                var t = NewRoot(name);
                Floor(t, floorA);
                Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0), new Vector3(0.68f, 0.025f, 0.68f), m, "Plate");
                return Save(t);
            }
            GameObject DoorPrefab(string name, Material m)
            {
                var t = NewRoot(name);
                Floor(t, floorA);
                var bars = Group(t, "Bars");
                Part(bars, PrimitiveType.Cube, new Vector3(0, 0.3f, 0), new Vector3(0.86f, 0.6f, 0.86f), m, "Block");
                for (int k = -1; k <= 1; k++)
                    Part(bars, PrimitiveType.Cube, new Vector3(k * 0.25f, 0.3f, -0.435f), new Vector3(0.06f, 0.56f, 0.02f), wall, $"Bar{k + 1}");
                return Save(t);
            }
            var pPlateA = PlatePrefab("PlateA", plateA);
            var pPlateB = PlatePrefab("PlateB", plateB);
            var pPlateC = PlatePrefab("PlateC", plateC);
            var pDoorA = DoorPrefab("DoorA", doorA);
            var pDoorB = DoorPrefab("DoorB", doorB);
            var pDoorC = DoorPrefab("DoorC", doorC);

            r = NewRoot("Player"); Figure(r, cloak, skin, eyes); var pPlayer = Save(r);

            r = NewRoot("Statue");
            Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(0.7f, 0.03f, 0.7f), stoneDark, "Base");
            Figure(r, stone, stone, null);
            var pStatue = Save(r);

            r = NewRoot("StartMarker"); Frame(r, startMat, 0.86f, 0.07f); var pStart = Save(r);
            r = NewRoot("OldStartMarker"); Frame(r, oldStartMat, 0.86f, 0.05f); var pOldStart = Save(r);

            r = NewRoot("Footprint");
            Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.004f, 0), new Vector3(0.36f, 0.004f, 0.36f), foot, "Disc");
            var pFoot = Save(r);

            r = NewRoot("LuckyRing");
            var ringPart = Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(1f, 0.004f, 1f), lucky, "Ring");
            ringPart.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var pRing = Save(r);

            var ga = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
            if (ga == null)
            {
                ga = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(ga, AssetsPath);
            }
            ga.levelsJson = Load<TextAsset>(LevelsPath);
            ga.font = Load<Font>(FontPath);
            ga.floorA = pFloorA; ga.floorB = pFloorB; ga.wall = pWall; ga.pit = pPit; ga.ice = pIce;
            ga.cracked = pCracked; ga.bridge = pBridge; ga.exit = pExit; ga.gate = pGate;
            ga.plateA = pPlateA; ga.plateB = pPlateB; ga.plateC = pPlateC;
            ga.doorA = pDoorA; ga.doorB = pDoorB; ga.doorC = pDoorC;
            ga.player = pPlayer; ga.statue = pStatue; ga.startMarker = pStart; ga.oldStartMarker = pOldStart;
            ga.footprint = pFoot; ga.luckyRing = pRing;
            ga.gateIdle = gateIdle; ga.gateGo = gateGo;
            ga.sfxStep = Clip("step"); ga.sfxLucky = Clip("lucky"); ga.sfxStone = Clip("stone"); ga.sfxSwitch = Clip("switch");
            ga.sfxFall = Clip("fall"); ga.sfxBump = Clip("bump"); ga.sfxBridge = Clip("bridge"); ga.sfxCrumble = Clip("crumble");
            ga.sfxUndo = Clip("undo"); ga.sfxWin = Clip("win"); ga.sfxLose = Clip("lose");
            ga.background = Hex("#15131c");
            EditorUtility.SetDirty(ga);
            AssetDatabase.SaveAssets();
            return ga;
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new Exception($"[SevenSteps] Missing asset {path}. Link the shared asset folder (tools/link-shared-assets.ps1), or run unity/Tools/unity.sh levels / sfx.");
            return a;
        }

        static AudioClip Clip(string name) => Load<AudioClip>($"{AudioDir}/{name}.wav");

        // ---------- Scene ----------

        static void BuildScene(GameAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = assets.background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.SetPositionAndRotation(new Vector3(0, 25, -16), Quaternion.Euler(58, 0, 0));
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52, -35, 0);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.52f);
            RenderSettings.skybox = null;

            var game = new GameObject("Game").AddComponent<GameController>();
            game.assets = assets;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ---------- Settings ----------

        static void ApplyProjectSettings()
        {
            // Make sure the Game scene is the (only) build scene.
            if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.runInBackground = true;
            // Desktop: 1280x720 window.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            // WebGL: 1280x720 canvas; gzip with decompression fallback so it runs on itch.io and on a plain local server.
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
