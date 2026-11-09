using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The UI look in one asset (docs/scene-flow.md 2.5, S6): font, colors, and the panel prefabs. Changing the art theme
    /// (and later a chapter's theme, docs/architecture.md 12.8) means pointing GameAssets.uiTheme at another UiTheme, not
    /// editing view code. Code-built UI (HUD, Title, Credits until they become prefabs) reads it through <see cref="Ui"/>.
    /// Default: Data/UiTheme_Default.asset (made by Seven Steps > Scene Flow > Build UI, same values as v0).
    /// </summary>
    /// <summary>What a Text is for, which picks its font (docs/architecture.md 13.4). Numbers use one font in every language.</summary>
    public enum FontRole { Title, Body, Number }

    /// <summary>Title and body fonts of one language (13.4). Scales and spacing: 0 = 1 (script fonts have a small x-height).</summary>
    [Serializable]
    public struct LanguageFonts
    {
        public Language language;
        public Font title, body;
        public float titleScale, bodyScale;
        [Tooltip("Body line spacing; 0 = 1. Art preview: en 0.90, ko 1.10")]
        public float bodyLineSpacing;

        public float TitleScale => titleScale > 0 ? titleScale : 1f;
        public float BodyScale => bodyScale > 0 ? bodyScale : 1f;
        public float BodyLineSpacing => bodyLineSpacing > 0 ? bodyLineSpacing : 1f;
    }

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

        [Header("Fonts per language and role (docs/architecture.md 13.4, design/art/fonts.md)")]
        [Tooltip("One entry per language in Languages.All: title and body fonts")]
        public LanguageFonts[] languages = new LanguageFonts[0];
        [Tooltip("Number role, the same in every language (board labels, steps, bodies, statue count, stars). v0: Jua")]
        public Font number;

        [Header("Menu skin (Title, planet select, loading; optional: empty = flat colors)")]
        [Tooltip("Full-screen UI background prefab (a stretched Image), behind the menu screens. Art/UI/MenhirJoineryBackground")]
        public GameObject menuBackground;
        [Tooltip("9-sliced button states. All four or none")]
        public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
        [Tooltip("9-sliced frame of large panels (title menu, stage list)")]
        public Sprite panel;
        [Tooltip("9-sliced frame of small cards (planets, stages)")]
        public Sprite card;
        [Tooltip("Star icons (planet and stage stars). Both or none")]
        public Sprite starFilled, starEmpty;
        [Tooltip("Big Dipper star positions, 7 points normalized (0..1, y down): planet map and loading. Empty = DefaultDipper")]
        public Vector2[] dipper = new Vector2[0];

        /// The art Dipper (RuinsHengeUiResources.dipper, design/art/ui-theme.md): bowl 1-2-3-4-1, handle 4-5-6-7.
        public static readonly Vector2[] DefaultDipper =
        {
            new Vector2(.12f, .12f), new Vector2(.10f, .36f), new Vector2(.35f, .40f), new Vector2(.42f, .18f),
            new Vector2(.62f, .12f), new Vector2(.79f, .20f), new Vector2(.98f, .28f),
        };

        /// Lines of the Dipper as point index pairs.
        public static readonly (int a, int b)[] DipperEdges = { (0, 1), (1, 2), (2, 3), (3, 0), (3, 4), (4, 5), (5, 6) };

        public Vector2[] Dipper => dipper != null && dipper.Length == 7 ? dipper : DefaultDipper;
        public bool HasButtonSkin => buttonNormal != null;

        /// The fonts of a language. A language without an entry throws (SceneFlowValidator catches it before a build).
        public LanguageFonts FontsFor(Language lang)
        {
            foreach (var l in languages ?? new LanguageFonts[0])
                if (l.language == lang) return l;
            throw new KeyNotFoundException($"UiTheme {name}: no fonts for {Languages.Code(lang)}");
        }

        /// The font for a role in a language. Number ignores the language.
        public Font FontFor(Language lang, FontRole role)
        {
            if (role == FontRole.Number) return number != null ? number : throw new InvalidOperationException($"UiTheme {name}: number font is not set");
            var fonts = FontsFor(lang);
            var font = role == FontRole.Title ? fonts.title : fonts.body;
            return font != null ? font : throw new InvalidOperationException($"UiTheme {name}: {Languages.Code(lang)} {role} font is not set");
        }

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
            if (number == null) issues.Add($"UiTheme {name}: number font is not set");
            int buttons = (buttonNormal != null ? 1 : 0) + (buttonHover != null ? 1 : 0) + (buttonPressed != null ? 1 : 0) + (buttonDisabled != null ? 1 : 0);
            if (buttons != 0 && buttons != 4) issues.Add($"UiTheme {name}: set all four button sprites or none ({buttons} set)");
            if ((starFilled == null) != (starEmpty == null)) issues.Add($"UiTheme {name}: set both star sprites or none");
            if (dipper != null && dipper.Length != 0 && dipper.Length != 7) issues.Add($"UiTheme {name}: dipper needs 7 points or none ({dipper.Length})");
            if (menuBackground != null && menuBackground.GetComponent<RectTransform>() == null) issues.Add($"UiTheme {name}: menuBackground must be a UI prefab (RectTransform)");
            var seen = new HashSet<Language>();
            foreach (var l in languages ?? new LanguageFonts[0])
            {
                if (!seen.Add(l.language)) issues.Add($"UiTheme {name}: fonts for {Languages.Code(l.language)} appear twice");
                if (l.title == null) issues.Add($"UiTheme {name}: {Languages.Code(l.language)} title font is not set");
                if (l.body == null) issues.Add($"UiTheme {name}: {Languages.Code(l.language)} body font is not set");
                if (l.titleScale < 0 || l.bodyScale < 0 || l.bodyLineSpacing < 0) issues.Add($"UiTheme {name}: {Languages.Code(l.language)} scales must be 0 (= 1) or more");
            }
            foreach (var lang in Languages.All)
                if (!seen.Contains(lang)) issues.Add($"UiTheme {name}: no fonts for {Languages.Code(lang)}");
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
