using System;
using System.Collections.Generic;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Music and the newer effects by id (user request 2026-11-13: BGM + SFX): Data/Sounds.asset gets one entry per
    /// <see cref="SoundIds"/> id, its clip Assets/Shared/Audio/&lt;id&gt;.ogg, else &lt;id&gt;.wav. Import settings stay
    /// as the audio's own .meta files have them (art's folder; never edited here). A missing file is a warning and an
    /// empty clip: the game plays nothing for it. Run by setup and by unity.sh sound-wire.
    /// </summary>
    public static class SoundWire
    {
        public const string AssetPath = "Assets/_Project/Data/Sounds.asset";
        public const string AudioDir = "Assets/Shared/Audio";
        static readonly string[] Extensions = { ".ogg", ".wav" };

        /// The clip file for <paramref name="id"/>, or null.
        public static AudioClip Find(string id)
        {
            foreach (var ext in Extensions)
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{id}{ext}");
                if (c != null) return c;
            }
            return null;
        }

        /// Data/Sounds.asset (made when missing) with every id filled from the audio folder. Returns the ids still missing.
        public static SoundCatalog Asset(out List<string> missing)
        {
            var set = AssetDatabase.LoadAssetAtPath<SoundCatalog>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SoundCatalog>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            missing = new List<string>();
            var entries = new List<SoundCatalog.Entry>();
            foreach (var id in SoundIds.All)
            {
                var clip = Find(id);
                if (clip == null) missing.Add(id);
                entries.Add(new SoundCatalog.Entry { id = id, clip = clip });
            }
            bool changed = set.entries == null || set.entries.Length != entries.Count;
            for (int i = 0; !changed && i < entries.Count; i++)
                changed = set.entries[i] == null || set.entries[i].id != entries[i].id || set.entries[i].clip != entries[i].clip;
            if (changed)
            {
                set.entries = entries.ToArray();
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }
            if (missing.Count > 0)
                Debug.LogWarning($"[SevenSteps] Sounds: no file yet for {string.Join(", ", missing)} (looked for {AudioDir}/<id>.ogg|.wav; those hooks stay silent)");
            return set;
        }

        /// unity.sh sound-wire: fill Data/Sounds.asset and point GameAssets.sounds at it. Exit 0 even with missing files.
        public static void Batch()
        {
            int code = 0;
            try
            {
                AssetDatabase.Refresh();
                var set = Asset(out var missing);
                var ga = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
                if (ga == null) throw new InvalidOperationException("Data/GameAssets.asset is missing (unity.sh setup)");
                if (ga.sounds != set)
                {
                    ga.sounds = set;
                    EditorUtility.SetDirty(ga);
                    AssetDatabase.SaveAssets();
                }
                int ids = 0;
                foreach (var _ in SoundIds.All) ids++;
                Debug.Log($"[SevenSteps] Sounds: {ids - missing.Count}/{ids} ids have a clip");
            }
            catch (Exception e)
            {
                Debug.LogError("[SevenSteps] sound-wire FAILED: " + e.Message);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
