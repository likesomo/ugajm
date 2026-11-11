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
            // 12.8: an empty tileSet is the normal case once chapters carry their sets (TileSetValidator checks each chapter's).
            if (sharedConfig != null && sharedConfig.tileSet == null && (sharedConfig.chapters == null || sharedConfig.chapters.Length == 0))
                issues.Add($"ERROR {AssetDatabase.GetAssetPath(sharedConfig)}: neither tileSet nor chapters is set (the board has nothing to draw)");

            // Every character the UI can show must be in the UI font (missing glyphs draw nothing in WebGL).
            var gameAssets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            if (gameAssets == null) issues.Add("ERROR Assets/_Project/Data/GameAssets.asset missing");
            else
            {
                ValidateThemes(gameAssets, issues);
                ValidateStory(gameAssets, issues); // 16.2 / 16.3: story.json load check, picture keys
                LocalizationCheck.Check(gameAssets, issues); // 13.6: tables, keys in code, glyphs per language
                UiLayoutCheck.Check(gameAssets, sharedConfig, issues); // 14: text size, plates, overlaps on screens built in Edit Mode
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
            ValidateBuildOrder(enabled, names, AppScenes.BuildOrder, issues);
        }

        /// Build index order = AppScenes.BuildOrder (architect review N3 on the scene structure doc): one source for the order,
        /// holding every AppScenes name once. The enabled list must follow it exactly.
        public static void ValidateBuildOrder(IReadOnlyList<string> enabledPaths, IReadOnlyList<string> names, IReadOnlyList<string> order, List<string> issues)
        {
            if (enabledPaths == null) throw new ArgumentNullException(nameof(enabledPaths));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (order.Count != names.Count || names.Any(n => order.Count(o => o == n) != 1))
            {
                issues.Add($"ERROR AppScenes.BuildOrder ({string.Join(", ", order)}) must hold every AppScenes name ({string.Join(", ", names)}) exactly once");
                return;
            }
            var want = order.Select(PathFor).ToList();
            if (!enabledPaths.SequenceEqual(want))
                issues.Add($"ERROR build list order: {string.Join(", ", enabledPaths.Select(System.IO.Path.GetFileNameWithoutExtension))}; AppScenes.BuildOrder is {string.Join(", ", order)}");
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
                    if (title.assets != null && title.assets.uiTheme != null) RequireFontLicenses(name, title.assets.uiTheme, title.licenses, issues);
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

        /// Every font the theme uses ships its OFL text in the credits (TitleScreen.licenses; itch.io release, architect a4cadfb L1).
        /// Font "Gaegu-Regular" needs a license TextAsset named "Gaegu-OFL" (Shared/Fonts/<Family>-OFL.txt).
        static void RequireFontLicenses(string scene, UiTheme theme, TextAsset[] licenses, List<string> issues)
        {
            var have = new HashSet<string>();
            foreach (var l in licenses ?? new TextAsset[0]) if (l != null) have.Add(l.name);
            var fonts = new HashSet<Font> { theme.font, theme.number };
            foreach (var l in theme.languages ?? new LanguageFonts[0]) { fonts.Add(l.title); fonts.Add(l.body); }
            foreach (var font in fonts)
            {
                if (font == null) continue;
                int dash = font.name.IndexOf('-');
                string license = (dash > 0 ? font.name.Substring(0, dash) : font.name) + "-OFL";
                if (!have.Contains(license)) issues.Add($"ERROR {scene}: TitleScreen.licenses has no {license} for font {font.name} (UiTheme {theme.name})");
            }
        }

        static void Require(string scene, string field, Object value, List<string> issues)
        {
            if (value == null) issues.Add($"ERROR {scene}: {field} is not set");
        }

        /// Every UiTheme under Assets/_Project, not only the one GameAssets uses: an art theme (Art/UI/RuinsHengeTheme)
        /// or a chapter's theme can be switched in later, so its prefabs and font must already be complete. Glyphs are
        /// checked once per distinct theme font.
        /// story.json must pass StoryCatalog's load check (a failure would turn the story off in the player, A8), and every
        /// "images" key needs a sprite in GameAssets.storyImages: a warning while StoryImageSet.required is false (ending
        /// pictures land after 11/14), an error once art turns it on (16.2).
        public static void ValidateStory(GameAssets gameAssets, List<string> issues)
        {
            if (gameAssets.storyJson == null) { issues.Add("ERROR GameAssets.storyJson is not set (Data/story.json, unity.sh levels then setup)"); return; }
            if (gameAssets.chaptersJson == null) { issues.Add("ERROR GameAssets.chaptersJson is not set: story.json needs the chapter ids"); return; }
            var chapters = JsonUtility.FromJson<ChapterFileDto>(gameAssets.chaptersJson.text);
            StoryCatalog story;
            try { story = StoryCatalog.FromJson(gameAssets.storyJson.text, (chapters.chapters ?? new ChapterInfo[0]).Select(c => c.id).ToArray()); }
            catch (Exception e) { issues.Add($"ERROR {AssetDatabase.GetAssetPath(gameAssets.storyJson)}: {e.Message}"); return; }
            // The star map draws only UiTheme.DipperEdges between planet cards (PlanetMapView): a story "dipper" line with
            // no such edge never lights (review 6e8f3cd M2). "circle" is the last stage, not a planet card: no edge by design.
            var ids = story.ChapterIds.ToList();
            foreach (var d in story.Dipper)
            {
                if (d.to == StoryOns.Circle) continue;
                int a = ids.IndexOf(d.from), b = ids.IndexOf(d.to);
                if (!UiTheme.DipperEdges.Any(e => (e.a == a && e.b == b) || (e.a == b && e.b == a)))
                    issues.Add($"WARN {AssetDatabase.GetAssetPath(gameAssets.storyJson)}: dipper line {d.from} -> {d.to} is not a star map edge (UiTheme.DipperEdges): it never shows");
            }
            var images = gameAssets.storyImages;
            if (images == null) { issues.Add("WARN GameAssets.storyImages is not set: story cards show text only"); return; }
            var missing = story.ImageKeys.Where(k => images.Get(k) == null).ToArray();
            if (missing.Length > 0)
                issues.Add($"{(images.required ? "ERROR" : "WARN")} {AssetDatabase.GetAssetPath(images)}: no sprite for {missing.Length} story image key(s): {string.Join(", ", missing)}");
        }

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
