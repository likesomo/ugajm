using System;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Makes the UI assets of docs/scene-flow.md S6 when they are missing: Data/UiTheme_Default.asset (v0 colors, the font
    /// from GameAssets) linked from GameAssets.uiTheme. Existing assets are left alone: after the first build they are
    /// edited by hand (or by the art theme). Menu: Seven Steps > Scene Flow > Build UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.UiBuilder.BatchBuild
    /// </summary>
    public static class UiBuilder
    {
        public const string ThemePath = "Assets/_Project/Data/UiTheme_Default.asset";
        const string GameAssetsPath = "Assets/_Project/Data/GameAssets.asset";

        [MenuItem("Seven Steps/Scene Flow/Build UI (theme, panel prefabs)", priority = 51)]
        public static void Build()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath)
                         ?? throw new InvalidOperationException($"{GameAssetsPath} missing: run unity.sh setup first");
            var theme = EnsureTheme(assets);
            if (assets.uiTheme == null)
            {
                assets.uiTheme = theme;
                EditorUtility.SetDirty(assets);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SevenSteps] UI built: theme {AssetDatabase.GetAssetPath(assets.uiTheme)}");
        }

        public static void BatchBuild()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static UiTheme EnsureTheme(GameAssets assets)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme != null) return theme;
            if (assets.font == null) throw new InvalidOperationException("GameAssets.font is empty (Assets/Shared junction? run tools/link-shared-assets.ps1)");
            theme = ScriptableObject.CreateInstance<UiTheme>(); // field defaults = v0 colors
            theme.font = assets.font;
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }
    }
}
