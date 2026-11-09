using System;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Buttons added to a shared UI/Prefabs panel after the art variant was built (e.g. the settings language row) come
    /// into the variant without its skin: the variant's sprite overrides are per object. This copies the skin of a button
    /// the art builder did skin (image sprite, type, colour, sprite-swap states) onto every button that still has the
    /// shared prefab's look. Only those buttons change. Owner: gameplay/UI; the art skin itself stays the art builder's.
    /// Batch: -executeMethod SevenSteps.EditorTools.VariantButtonSkin.BatchSettings
    /// </summary>
    public static class VariantButtonSkin
    {
        const string SettingsVariant = "Assets/_Project/Art/UI/SettingsPanel.prefab";

        public static void BatchSettings()
        {
            try { Settings(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Seven Steps/Gameplay/Skin New Buttons In Art Settings Panel", priority = 35)]
        public static void Settings()
        {
            var root = PrefabUtility.LoadPrefabContents(SettingsVariant);
            try
            {
                var view = root.GetComponent<SettingsPanel>() ?? throw new InvalidOperationException($"{SettingsVariant}: no SettingsPanel");
                var reference = view.telemetryToggle; // skinned by RuinsHengeUiBuilder
                var refImage = reference.GetComponent<Image>();
                if (reference.transition != Selectable.Transition.SpriteSwap || refImage == null)
                    throw new InvalidOperationException($"{SettingsVariant}: telemetryToggle is not skinned (run Seven Steps > Art > Build Ruins UI Resources)");
                int changed = 0;
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                {
                    var image = b.GetComponent<Image>();
                    if (b.transition == Selectable.Transition.SpriteSwap || image == null) continue; // already skinned
                    image.sprite = refImage.sprite;
                    image.type = refImage.type;
                    image.color = refImage.color;
                    b.transition = Selectable.Transition.SpriteSwap;
                    b.spriteState = reference.spriteState;
                    changed++;
                    Debug.Log($"[SevenSteps] Skinned {b.name} in {SettingsVariant}");
                }
                PrefabUtility.SaveAsPrefabAsset(root, SettingsVariant);
                Debug.Log($"[SevenSteps] Art settings panel: {changed} button(s) skinned");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
