using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Checks the scene flow wiring before a build (docs/scene-flow.md 1.3 rule 5, 5 S0):
    /// the build list holds every AppScenes name exactly once, Boot first, at Assets/_Project/Scenes/&lt;name&gt;.unity;
    /// each scene has exactly one root (Boot: BootScreen, Title: TitleScreen, Game: GameController) and no other scene's root;
    /// each root's required references are set and every scene points at the same AppConfig; AppConfig.tileSet and the
    /// GameAssets data (levels.json, font, uiTheme) are set; every UI character is in the UI font (GlyphCheck).
    /// Scene loading is by name, so a missing or duplicated name only fails here.
    /// Menu: Seven Steps > Scene Flow > Validate Scenes. Batch: -executeMethod SevenSteps.EditorTools.SceneFlowValidator.BatchValidate
    /// (exit 1 on errors). Report: Logs/batch-scene-validate.log via unity.sh scene-validate, or the Console.
    /// </summary>
    public static class SceneFlowValidator
    {
        public const string SceneDir = "Assets/_Project/Scenes";

        /// Root component each scene must hold exactly once. A new AppScenes name without an entry here is an error.
        static readonly Dictionary<string, Type> RootTypes = new Dictionary<string, Type>
        {
            { AppScenes.Boot, typeof(BootScreen) },
            { AppScenes.Title, typeof(TitleScreen) },
            { AppScenes.Game, typeof(GameController) },
        };

        public static string PathFor(string sceneName) => $"{SceneDir}/{sceneName}.unity";

        /// Every scene name in AppScenes (its public string constants).
        public static string[] SceneNames() =>
            typeof(AppScenes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

        [MenuItem("Seven Steps/Scene Flow/Validate Scenes", priority = 50)]
        public static void ValidateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                int errors = ValidateAll(out string report);
                if (errors > 0) Debug.LogError(report); else Debug.Log(report);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void BatchValidate()
        {
            int errors;
            try { errors = ValidateAll(out string report); Debug.Log(report); }
            catch (Exception e) { Debug.LogException(e); errors = 1; }
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }

        /// Opens each build scene (replacing the open scenes) and returns the error count. Warnings do not count.
        public static int ValidateAll(out string report)
        {
            var issues = new List<string>();
            var names = SceneNames();
            ValidateBuildList(EditorBuildSettings.scenes, names, issues);

            AppConfig sharedConfig = null;
            foreach (var name in names)
            {
                string path = PathFor(name);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue; // reported by ValidateBuildList
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var config = ValidateScene(scene, name, issues);
                if (config == null) continue;
                if (sharedConfig == null) sharedConfig = config;
                else if (config != sharedConfig)
                    issues.Add($"ERROR {name}: AppConfig {AssetDatabase.GetAssetPath(config)} differs from {AssetDatabase.GetAssetPath(sharedConfig)}; direct Play would run with other settings");
            }
            if (sharedConfig != null && sharedConfig.tileSet == null)
                issues.Add($"ERROR {AssetDatabase.GetAssetPath(sharedConfig)}: tileSet is empty (the board has nothing to draw)");

            // Every character the UI can show must be in the UI font (missing glyphs draw nothing in WebGL).
            var gameAssets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            if (gameAssets == null) issues.Add("ERROR Assets/_Project/Data/GameAssets.asset missing");
            else
            {
                ValidateThemes(gameAssets, issues);
                LocalizationCheck.Check(gameAssets, issues); // 13.6: tables, keys in code, glyphs per language
            }

            int errors = issues.Count(i => i.StartsWith("ERROR", StringComparison.Ordinal));
            var sb = new StringBuilder();
            sb.AppendLine($"[SevenSteps] Scene flow: {(errors > 0 ? "FAIL" : "OK")} ({errors} errors, {issues.Count - errors} warnings, scenes {string.Join(", ", names)})");
            foreach (var i in issues) sb.AppendLine("  " + i);
            report = sb.ToString();
            return errors;
        }

        /// Build list = AppScenes: each name once, enabled, at its path, Boot at index 0. Extra enabled scenes are errors.
        public static void ValidateBuildList(IReadOnlyList<EditorBuildSettingsScene> buildScenes, IReadOnlyList<string> names, List<string> issues)
        {
            if (buildScenes == null) throw new ArgumentNullException(nameof(buildScenes));
            if (names == null || names.Count == 0) throw new ArgumentException("AppScenes has no scene names", nameof(names));

            var enabled = buildScenes.Where(s => s.enabled).Select(s => s.path).ToList();
            if (enabled.Count == 0 || enabled[0] != PathFor(AppScenes.Boot))
                issues.Add($"ERROR build list: index 0 must be {PathFor(AppScenes.Boot)} (is {(enabled.Count > 0 ? enabled[0] : "empty")})");
            foreach (var name in names)
            {
                string path = PathFor(name);
                int count = enabled.Count(p => p == path);
                if (count != 1) issues.Add($"ERROR build list: {path} appears {count} times (must be once, enabled)");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) issues.Add($"ERROR {path}: scene file missing");
                var sameName = enabled.Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == name && p != path).ToList();
                foreach (var p in sameName) issues.Add($"ERROR build list: {p} has the scene name \"{name}\" but is not at {path}; loading by name is ambiguous");
            }
            foreach (var p in enabled.Where(p => !names.Any(n => PathFor(n) == p)))
                issues.Add($"ERROR build list: {p} is not an AppScenes name; add it to AppScenes or remove it from the build");
        }

        /// One root of the scene's type and none of the others; its references set. Returns the root's AppConfig (or null).
        static AppConfig ValidateScene(Scene scene, string name, List<string> issues)
        {
            if (!RootTypes.TryGetValue(name, out var rootType))
            {
                issues.Add($"ERROR {name}: no root type registered in SceneFlowValidator.RootTypes");
                return null;
            }
            foreach (var other in RootTypes.Where(kv => kv.Key != name))
            {
                int n = Object.FindObjectsByType(other.Value, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                if (n > 0) issues.Add($"ERROR {name}: contains {n} {other.Value.Name} (belongs to the {other.Key} scene)");
            }
            var roots = Object.FindObjectsByType(rootType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (roots.Length != 1)
            {
                issues.Add($"ERROR {name}: {roots.Length} {rootType.Name} (must be exactly one)");
                if (roots.Length == 0) return null;
            }

            switch (roots[0])
            {
                case BootScreen boot:
                    Require(name, "BootScreen.config", boot.config, issues);
                    RequireAssets(name, "BootScreen.assets", boot.assets, issues);
                    return boot.config;
                case TitleScreen title:
                    Require(name, "TitleScreen.config", title.config, issues);
                    RequireAssets(name, "TitleScreen.assets", title.assets, issues);
                    return title.config;
                case GameController game:
                    Require(name, "GameController.config", game.config, issues);
                    RequireAssets(name, "GameController.assets", game.assets, issues);
                    Require(name, "GameController.keyLight", game.keyLight, issues); // Seven Steps > Scene Flow > Build Game Lights
                    Require(name, "GameController.fillLight", game.fillLight, issues);
                    Require(name, "GameController.rimLight", game.rimLight, issues);
                    if (game.fillLight != null && game.fillLight.shadows != LightShadows.None) issues.Add($"WARN {name}: fill light casts shadows (only the key should, WebGL budget)");
                    if (game.rimLight != null && game.rimLight.shadows != LightShadows.None) issues.Add($"WARN {name}: rim light casts shadows (only the key should, WebGL budget)");
                    if (Camera.main == null) issues.Add($"ERROR {name}: no MainCamera-tagged camera (GameController uses Camera.main)");
                    return game.config;
                default:
                    throw new InvalidOperationException($"Root type {rootType.Name} has no reference checks");
            }
        }

        static void Require(string scene, string field, Object value, List<string> issues)
        {
            if (value == null) issues.Add($"ERROR {scene}: {field} is not set");
        }

        /// Every UiTheme under Assets/_Project, not only the one GameAssets uses: an art theme (Art/UI/RuinsHengeTheme)
        /// or a chapter's theme can be switched in later, so its prefabs and font must already be complete. Glyphs are
        /// checked once per distinct theme font.
        static void ValidateThemes(GameAssets gameAssets, List<string> issues)
        {
            var fontsChecked = new HashSet<Font>();
            foreach (var guid in AssetDatabase.FindAssets("t:UiTheme", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(path);
                var themeIssues = new List<string>();
                theme.Validate(themeIssues);
                foreach (var i in themeIssues) issues.Add($"ERROR {path}: {i}");
                if (theme.font != null && fontsChecked.Add(theme.font)) GlyphCheck.Check(theme, gameAssets, issues);
            }
            if (gameAssets.uiTheme == null) issues.Add("ERROR GameAssets.uiTheme is not set");
        }

        static void RequireAssets(string scene, string field, GameAssets assets, List<string> issues)
        {
            Require(scene, field, assets, issues);
            if (assets == null) return;
            Require(scene, $"{field}.levelsJson", assets.levelsJson, issues);
            Require(scene, $"{field}.font", assets.font, issues); // in Assets/Shared: run tools/link-shared-assets.ps1 if missing
            Require(scene, $"{field}.uiTheme", assets.uiTheme, issues); // Seven Steps > Scene Flow > Build UI
            if (assets.uiTheme != null)
            {
                var themeIssues = new List<string>();
                assets.uiTheme.Validate(themeIssues);
                foreach (var i in themeIssues) issues.Add($"ERROR {scene}: {i}");
            }
        }
    }
}
