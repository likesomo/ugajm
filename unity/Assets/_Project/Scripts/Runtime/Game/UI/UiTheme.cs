using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The UI look in one asset (docs/scene-flow.md 2.5, S6): font, colors, and the panel prefabs. Changing the art theme
    /// (and later a chapter's theme, docs/architecture.md 12.8) means pointing GameAssets.uiTheme at another UiTheme, not
    /// editing view code. Code-built UI (HUD, Title, Credits until they become prefabs) reads it through <see cref="Ui"/>.
    /// Default: Data/UiTheme_Default.asset (made by Seven Steps > Scene Flow > Build UI, same values as v0).
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Text")]
        [Tooltip("Legacy uGUI Text font (dynamic). v0: Shared/Fonts/Jua-Regular.ttf")]
        public Font font;

        [Header("Colors")]
        public Color ink = new Color32(0xec, 0xe6, 0xd6, 0xff);
        public Color dim = new Color32(0x7d, 0x76, 0x90, 0xff);
        public Color gold = new Color32(0xf2, 0xc1, 0x4e, 0xff);
        public Color stone = new Color32(0x9a, 0x94, 0x8a, 0xff);
        public Color buttonBg = new Color32(0x2a, 0x26, 0x35, 0xff);
        public Color panelBg = new Color32(0x1f, 0x1c, 0x2a, 0xf2);
        [Tooltip("Pressure plate / door colors A, B, C (board labels and HUD)")]
        public Color plateA = new Color32(0xe7, 0x6f, 0x51, 0xff);
        public Color plateB = new Color32(0x2a, 0x9d, 0x8f, 0xff);
        public Color plateC = new Color32(0x8e, 0x7d, 0xbe, 0xff);

        [Header("Panel prefabs (UI/Prefabs, first built by UiBuilder; edit the prefab, not code)")]
        public SettingsPanel settingsPanel;
        [Tooltip("Clear / loss panel with its own canvas (sorting 5)")]
        public ResultView resultPanel;
        [Tooltip("Game HUD with its own canvas (UI/Prefabs/Hud.prefab, first built by Seven Steps > Gameplay > Build HUD)")]
        public HudView hud;
        [Tooltip("Optional: chapter card shown when a new chapter starts (gameplay/UI prefab). Empty = no card.")]
        public ChapterCardView chapterCard;

        /// Missing required parts, one line each (SceneFlowValidator, and Ui.Theme at runtime).
        public void Validate(List<string> issues)
        {
            if (font == null) issues.Add($"UiTheme {name}: font is not set");
            if (settingsPanel == null) issues.Add($"UiTheme {name}: settingsPanel prefab is not set");
            else settingsPanel.Validate(issues);
            if (resultPanel == null) issues.Add($"UiTheme {name}: resultPanel prefab is not set");
            else resultPanel.Validate(issues);
            if (hud == null) issues.Add($"UiTheme {name}: hud prefab is not set");
            else hud.Validate(issues);
            if (chapterCard != null) chapterCard.Validate(issues);
        }
    }

    public static partial class Ui
    {
        static UiTheme theme;

        /// The theme every code-built view reads. Set by the scene roots from GameAssets.uiTheme before they build views.
        public static UiTheme Theme
        {
            get => theme != null ? theme : throw new System.InvalidOperationException("[SevenSteps] Ui.Theme is not set (scene root sets it from GameAssets.uiTheme)");
            set
            {
                if (value == null) throw new System.ArgumentNullException(nameof(value), "[SevenSteps] GameAssets.uiTheme is not set (unity.sh scene-validate)");
                // Text needs the font at once; panel prefabs are checked when a view is created (and by SceneFlowValidator).
                if (value.font == null) throw new System.InvalidOperationException($"[SevenSteps] UiTheme {value.name}: font is not set");
                theme = value;
            }
        }

        public static bool HasTheme => theme != null;

        public static Font Font => Theme.font;
        public static Color Ink => Theme.ink;
        public static Color Dim => Theme.dim;
        public static Color Gold => Theme.gold;
        public static Color Stone => Theme.stone;
        public static Color ButtonBg => Theme.buttonBg;
        public static Color PanelBg => Theme.panelBg;
        public static Color PlateA => Theme.plateA;
        public static Color PlateB => Theme.plateB;
        public static Color PlateC => Theme.plateC;
    }
}
