using System;
using System.Collections.Generic;
using System.IO;
using SevenSteps.Game;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// U5 icon set tools (docs/ui-u5-kit.md 1.1): fills Data/UiIconSet.asset from the Codex manifest
    /// (Art/UI/U5/u5-icons.json, id -> GUID / path) and checks the set for scene-validate (an icon still on its line
    /// placeholder is a warning; 0 before a release build). The runtime never reads the manifest.
    /// </summary>
    public static class UiIconSetTools
    {
        public const string AssetPath = "Assets/_Project/Data/UiIconSet.asset";
        public const string ManifestPath = "Assets/_Project/Art/UI/U5/u5-icons.json";

        [Serializable] public sealed class ManifestDto { public int version; public EntryDto[] icons = Array.Empty<EntryDto>(); }
        [Serializable] public sealed class EntryDto { public string id = ""; public string path = ""; public string guid = ""; public int size; }

        /// scene-validate: GameAssets.uiIcons set (error), every UiIcon with art (warning per missing id).
        public static void Check(GameAssets assets, List<string> issues)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            if (assets.uiIcons == null) { issues.Add($"ERROR GameAssets.uiIcons is not set ({AssetPath})"); return; }
            var missing = assets.uiIcons.Missing();
            if (missing.Count > 0)
                issues.Add($"WARN {AssetPath}: {missing.Count} U5 icons on line placeholders ({string.Join(", ", missing.ConvertAll(UiIconSet.ManifestId))}); " +
                           "0 before a release build (Seven Steps > Gameplay > Import U5 Icons)");
        }

        [MenuItem("Seven Steps/Gameplay/Import U5 Icons")]
        public static void ImportMenu()
        {
            var log = new List<string>();
            int set = Import(ManifestPath, AssetPath, log);
            foreach (var l in log) Debug.Log("[SevenSteps] " + l);
            Debug.Log($"[SevenSteps] U5 icons: {set} sprites set from {ManifestPath}");
        }

        /// Batch: -executeMethod SevenSteps.EditorTools.UiIconSetTools.BatchImport (exit 1 when the manifest is missing).
        public static void BatchImport()
        {
            int code = 0;
            try { ImportMenu(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// Reads the manifest and sets each sprite it names (GUID first, path as fallback). Unknown ids and missing
        /// sprites are logged; ids the manifest leaves out keep their sprite (or placeholder). Returns the sprites set.
        public static int Import(string manifestPath, string assetPath, List<string> log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("U5 icon manifest not found (feat/art-ui-u5)", manifestPath);
            var set = AssetDatabase.LoadAssetAtPath<UiIconSet>(assetPath)
                      ?? throw new FileNotFoundException("UiIconSet asset not found", assetPath);
            var dto = JsonUtility.FromJson<ManifestDto>(File.ReadAllText(manifestPath));
            int count = Apply(set, dto, ResolveSprite, log);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return count;
        }

        /// <see cref="Import"/> without files (tests): <paramref name="resolve"/> maps (guid, path) to a sprite or null.
        public static int Apply(UiIconSet set, ManifestDto dto, Func<string, string, Sprite> resolve, List<string> log)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (dto == null || dto.icons == null) throw new ArgumentException("manifest has no icons", nameof(dto));
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            int count = 0;
            foreach (var e in dto.icons)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) { log.Add("WARN manifest entry without id"); continue; }
                bool isIcon = UiIconSet.TryIcon(e.id, out var icon);
                if (!isIcon && Array.IndexOf(UiIconSet.SpriteIds, e.id) < 0) { log.Add($"WARN unknown manifest id \"{e.id}\""); continue; }
                var sprite = resolve(e.guid, e.path);
                if (sprite == null) { log.Add($"WARN {e.id}: no sprite at guid {e.guid} / {e.path}"); continue; }
                if (isIcon) set.SetIcon(icon, sprite);
                else set.SetSprite(e.id, sprite);
                count++;
            }
            return count;
        }

        /// Batch: -executeMethod SevenSteps.EditorTools.UiIconSetTools.ExportPreview (no graphics needed): every UiIcon of
        /// Data/UiIconSet.asset (art or line placeholder) at 48 and 96 on the HUD background colour, with its manifest id
        /// order, to reports/u5-kit/icons.png (review sheet; art sprites must be readable to appear).
        public static void ExportPreview()
        {
            int code = 0;
            try
            {
                var set = AssetDatabase.LoadAssetAtPath<UiIconSet>(AssetPath);
                var ids = (UiIcon[])Enum.GetValues(typeof(UiIcon));
                const int cell = 120, cols = 10;
                int rows = (ids.Length + cols - 1) / cols;
                int w = cols * cell, h = rows * cell * 2;
                var px = new Color[w * h];
                var bg = new Color(0.06f, 0.09f, 0.14f, 1);
                for (int i = 0; i < px.Length; i++) px[i] = bg;
                var ink = new Color(0.93f, 0.9f, 0.84f, 1);
                for (int k = 0; k < ids.Length; k++)
                {
                    var sprite = UiIconSet.Of(set, ids[k]);
                    var tex = sprite.texture;
                    var sr = sprite.textureRect;
                    for (int band = 0; band < 2; band++)
                    {
                        int size = band == 0 ? 48 : 96;
                        int ox = (k % cols) * cell + (cell - size) / 2, oy = h - 1 - ((k / cols) + band * rows) * cell - (cell - size) / 2 - size;
                        for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float a = tex.GetPixelBilinear((sr.x + (x + 0.5f) / size * sr.width) / tex.width, (sr.y + (y + 0.5f) / size * sr.height) / tex.height).a;
                            int i = (oy + y) * w + ox + x;
                            px[i] = Color.Lerp(px[i], ink, a);
                        }
                    }
                }
                var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                outTex.SetPixels(px);
                outTex.Apply();
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/u5-kit"));
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "icons.png"), outTex.EncodeToPNG());
                Debug.Log($"[SevenSteps] U5 icon preview: {dir}/icons.png ({string.Join(" ", Array.ConvertAll(ids, UiIconSet.ManifestId))})");
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static Sprite ResolveSprite(string guid, string path)
        {
            string p = string.IsNullOrEmpty(guid) ? "" : AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p)) p = path;
            return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
    }
}
