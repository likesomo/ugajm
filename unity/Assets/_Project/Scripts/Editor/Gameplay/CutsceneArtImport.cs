using System;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Storybook cutscene pictures (design/story/cutscenes.md 3; Codex art in shared-assets, never in git):
    /// Assets/Shared/Art/Story/Cutscenes/&lt;key&gt;.png, 1920 x 1080. Import rule (every import, so a fresh workspace or new
    /// art gets the same settings): Sprite (2D and UI), single, no mipmaps, clamp, bilinear, opaque (alpha ignored: DXT1,
    /// 4 bits per pixel). WebGL: 1024 wide (1024 x 576), crunched (download size); Standalone: full size, 2048 cap.
    /// <see cref="Wire"/> puts each picture into GameAssets.cutsceneImages under its key (empty entries only; art's own
    /// choices stay). Batch: unity.sh cutscene-art. ProjectSetup calls Wire too.
    /// </summary>
    public sealed class CutsceneArtImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Shared/Art/Story/Cutscenes";
        public const int WebGLMax = 1024, StandaloneMax = 2048, CrunchQuality = 60;

        public static bool IsCutscenePicture(string path) =>
            path.StartsWith(Folder + "/", StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!IsCutscenePicture(assetPath)) return;
            Apply((TextureImporter)assetImporter);
        }

        /// The import settings above on <paramref name="ti"/>.
        public static void Apply(TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None; // opaque paintings: DXT1, not DXT5
            ti.alphaIsTransparency = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.isReadable = false;
            ti.maxTextureSize = StandaloneMax;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL", overridden = true, maxTextureSize = WebGLMax, format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed, crunchedCompression = true, compressionQuality = CrunchQuality,
            });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = StandaloneMax, format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed, crunchedCompression = false,
            });
        }

        /// Fills empty entries of GameAssets.cutsceneImages from the folder (key = file name). Returns how many it set.
        public static int Wire()
        {
            var ga = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            if (ga == null) throw new InvalidOperationException("[SevenSteps] Assets/_Project/Data/GameAssets.asset is missing (unity.sh setup)");
            var set = ga.cutsceneImages;
            if (set == null) throw new InvalidOperationException("[SevenSteps] GameAssets.cutsceneImages is not set (unity.sh setup)");
            if (ga.cutscenesJson == null) return Fill(set);
            var catalog = CutsceneCatalog.FromJson(ga.cutscenesJson.text);
            var reuse = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var s in catalog.Scenes) foreach (var p in s.pages) if (!string.IsNullOrEmpty(p.reuse)) reuse[p.image] = p.reuse;
            return Fill(set, catalog.ImageKeys, reuse);
        }

        /// Fills empty entries of <paramref name="set"/> from the folder (key = file name) and saves it when it changed.
        /// With <paramref name="keys"/> (the catalog's picture keys): a key the set lacks gets an entry first (a new page,
        /// e.g. prologue 5b on 2026-11-13), and <see cref="StoryImageSet.required"/> follows the set: on once every
        /// entry has its picture, off while one is still out (a plain card, a scene-validate warning).
        /// <param name="reuse">Page key -> the key of the picture it shows instead of its own file (CutscenePageDto.reuse).</param>
        public static int Fill(StoryImageSet set, System.Collections.Generic.IReadOnlyList<string> keys = null, System.Collections.Generic.IReadOnlyDictionary<string, string> reuse = null)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            int n = 0;
            bool changed = false;
            if (keys != null)
            {
                var entries = new System.Collections.Generic.List<StoryImageSet.Entry>(set.entries ?? new StoryImageSet.Entry[0]);
                foreach (var k in keys)
                    if (!entries.Exists(e => e != null && e.key == k)) { entries.Add(new StoryImageSet.Entry { key = k }); changed = true; }
                set.entries = entries.ToArray();
            }
            foreach (var e in set.entries)
            {
                if (e == null || e.sprite != null) continue;
                string file = reuse != null && reuse.TryGetValue(e.key, out var other) ? other : e.key; // arrivals reuse a picture
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{file}.png");
                if (sprite == null) continue;
                e.sprite = sprite;
                n++;
            }
            if (keys != null)
            {
                bool all = Array.TrueForAll(set.entries, e => e != null && e.sprite != null);
                if (set.required != all) { set.required = all; changed = true; }
            }
            if (n > 0 || changed)
            {
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }
            return n;
        }

        [MenuItem("Seven Steps/Gameplay/Import Cutscene Pictures")]
        public static void Menu()
        {
            Reimport();
            Debug.Log($"[SevenSteps] Cutscene pictures: {Wire()} key(s) wired");
        }

        /// Re-applies the import rule to every picture already in the folder (art imported before the rule existed).
        static int Reimport()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsCutscenePicture(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }
            return n;
        }

        /// Batch: -executeMethod SevenSteps.EditorTools.CutsceneArtImport.Batch (unity.sh cutscene-art). Exit 1 when the
        /// folder is missing (junction: tools/link-shared-assets.ps1).
        public static void Batch()
        {
            int code = 0;
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException($"{Folder} is missing (shared-assets junction)");
                int pictures = Reimport();
                int wired = Wire();
                var set = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").cutsceneImages;
                int missing = 0;
                foreach (var e in set.entries) if (e.sprite == null) missing++;
                Debug.Log($"[SevenSteps] Cutscene pictures: {pictures} imported, {wired} newly wired, {set.entries.Length - missing}/{set.entries.Length} keys have a sprite");
            }
            catch (Exception e)
            {
                Debug.LogError("[SevenSteps] cutscene-art FAILED: " + e.Message);
                code = 1;
            }
            EditorApplication.Exit(code);
        }
    }
}
