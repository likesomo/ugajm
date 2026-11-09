using System;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Builds the scene split of docs/architecture.md 8 (owner: gameplay/UI): Scenes/Boot.unity (BootScreen),
    /// Scenes/Title.unity (camera + TitleScreen), and wires Scenes/Game.unity's GameController to Data/AppConfig.asset
    /// (created here if missing, tileSet = TopDown_Placeholder, telemetry endpoint empty). Scenes hold only a camera and
    /// one root object; the UI is built in code by those roots. Safe to re-run: Boot/Title are rewritten, Game is only patched.
    /// The build list is ordered Boot, Title, Game by ProjectSetup (unity.sh setup).
    /// Menu: Seven Steps > Gameplay > Build Scenes. Batch: -executeMethod SevenSteps.EditorTools.GameplayScenes.BatchBuild
    /// </summary>
    public static class GameplayScenes
    {
        const string SceneDir = "Assets/_Project/Scenes";
        public const string BootPath = SceneDir + "/Boot.unity";
        public const string TitlePath = SceneDir + "/Title.unity";
        public const string GamePath = SceneDir + "/Game.unity";
        public const string ConfigPath = "Assets/_Project/Data/AppConfig.asset";
        const string GameAssetsPath = "Assets/_Project/Data/GameAssets.asset";
        const string JuaLicensePath = "Assets/Shared/Fonts/Jua-OFL.txt"; // shared asset folder, read-only
        const string TileSetPath = "Assets/_Project/Data/TileSets/TopDown_Placeholder.asset";

        [MenuItem("Seven Steps/Gameplay/Build Scenes (Boot, Title, Game wiring)", priority = 30)]
        public static void Build()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath)
                         ?? throw new Exception($"{GameAssetsPath} missing: run unity.sh setup first");
            var config = EnsureConfig();

            // Boot: no screen, just the bootstrap.
            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = new GameObject("Boot").AddComponent<BootScreen>();
            b.config = config;
            b.assets = assets;
            EditorSceneManager.SaveScene(boot, BootPath);

            // Title: camera (for the background color and the AudioListener) + TitleScreen.
            var title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera(assets);
            var t = new GameObject("Title").AddComponent<TitleScreen>();
            t.assets = assets;
            t.config = config;
            CreditsExport.Export();
            t.credits = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsExport.OutPath);
            t.licenses = new[] { AssetDatabase.LoadAssetAtPath<TextAsset>(JuaLicensePath) };
            EditorSceneManager.SaveScene(title, TitlePath);

            // Game: patch the existing scene (made by ProjectSetup) so GameController knows the config.
            var game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            var gc = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (gc == null) throw new Exception("Game.unity has no GameController");
            gc.config = config;
            if (gc.assets == null) gc.assets = assets;
            EditorUtility.SetDirty(gc);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SevenSteps] Scenes written: {BootPath}, {TitlePath}; {GamePath} wired to {ConfigPath}");
        }

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static AppConfig EnsureConfig()
        {
            var c = AssetDatabase.LoadAssetAtPath<AppConfig>(ConfigPath);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<AppConfig>();
                c.telemetryEndpoint = "";
                c.telemetryOnByDefault = true;
                c.buildChannel = "dev";
                AssetDatabase.CreateAsset(c, ConfigPath);
            }
            if (c.tileSet == null)
            {
                c.tileSet = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
                EditorUtility.SetDirty(c);
            }
            return c;
        }

        static void Camera(GameAssets assets)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = assets.background;
            go.AddComponent<AudioListener>();
        }
    }
}
