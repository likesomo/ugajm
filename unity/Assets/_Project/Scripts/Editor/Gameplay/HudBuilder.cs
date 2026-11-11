using System;
using System.Collections.Generic;
using System.Linq;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Builds UI/Prefabs/Hud.prefab, the minimal HUD (production/ui-minimal-plan.md "승인된 결정" 2026-11-11,
    /// design/art/ui-minimal.md "배치 좌표"), and puts it in UiTheme.hud of every theme (Data/UiTheme_Default and the art's
    /// RuinsHengeTheme). Buttons use the art UI kit (Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png slices); plates are flat
    /// panel colour (docs/architecture.md 14.4). An existing prefab is left alone unless forced; after a build the prefab is
    /// the source for art tweaks. Owner: gameplay/UI.
    /// Areas, 1280 x 720 reference, top-left origin (HudView.BoardFrame 232,88 816x500 stays clear):
    ///   menu 24,24 48x48 | title plate 84,24 h48 | brother dots plate top right 24 in | statues plate under it (x >= 1064)
    ///   guide / story plate 24,104 w192 (left margin) | KEEP / SWITCH cards right margin w192 | undo 24 / restart 86 at
    ///   the bottom left 48x48 | message plate bottom centre w760
    /// Menu: Seven Steps > Gameplay > Build HUD. Batch: -executeMethod SevenSteps.EditorTools.HudBuilder.BatchBuild [-force]
    /// </summary>
    public static class HudBuilder
    {
        public const string PrefabPath = "Assets/_Project/UI/Prefabs/Hud.prefab";
        const string AtlasPath = "Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png";
        const string FontPath = "Assets/Shared/Fonts/Jua-Regular.ttf";
        static readonly string[] ThemePaths = { "Assets/_Project/Data/UiTheme_Default.asset", "Assets/_Project/Art/UI/RuinsHengeTheme.asset" };

        const float Safe = 24, Pad = 12, Icon = 48, Margin = 192;
        /// Title plate: 22 px hand-font glyphs (about 29 tall) + 8 above and below with room for their offset; centred on the menu button.
        const float TitleH = 56;

        static Dictionary<string, Sprite> sprites;
        static Font font;

        [MenuItem("Seven Steps/Gameplay/Build HUD", priority = 32)]
        public static void BuildMenu()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            if (exists && !EditorUtility.DisplayDialog("Build HUD", "Hud.prefab exists. Rebuild it from code (prefab edits are lost)?", "Rebuild", "Cancel")) return;
            Build(force: exists);
        }

        public static void BatchBuild()
        {
            try { Build(force: Environment.GetCommandLineArgs().Contains("-force")); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Build(bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<HudView>(PrefabPath);
            HudView prefab = existing;
            if (existing == null || force)
            {
                sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
                if (sprites.Count == 0) throw new InvalidOperationException($"{AtlasPath}: no sprites (link the shared folder: tools/link-shared-assets.ps1)");
                font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? throw new InvalidOperationException($"{FontPath} missing");
                var assets = AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
                Ui.Theme = assets != null && assets.uiTheme != null ? assets.uiTheme
                    : throw new InvalidOperationException("Assets/_Project/Data/GameAssets.asset: uiTheme is not set");
                var go = BuildHud();
                prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath).GetComponent<HudView>();
                Object.DestroyImmediate(go);
                Debug.Log($"[SevenSteps] HUD built: {PrefabPath}");
            }
            else Debug.Log($"[SevenSteps] {PrefabPath} exists, kept (edit the prefab; pass -force to rebuild)");

            foreach (var path in ThemePaths)
            {
                var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(path) ?? throw new InvalidOperationException($"{path} missing");
                theme.hud = prefab;
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();
            var issues = new List<string>();
            prefab.Validate(issues);
            if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
        }

        static GameObject BuildHud()
        {
            var go = new GameObject("Hud", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var hud = go.AddComponent<HudView>();
            var root = go.transform;

            // While choosing: the board dimmed 30% (behind everything, docs 14.11 UI variant with its exemption).
            hud.chooseDim = Image("ChooseDim", root, null, new Color(0, 0, 0, 0.3f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            hud.chooseDim.rectTransform.Stretch();
            Ui.Exempt(hud.chooseDim.gameObject, UiLayoutRule.Decoration | UiLayoutRule.Board, "choose dim 30% (ui-minimal v3)");
            hud.chooseDim.gameObject.SetActive(false);

            // World-anchored labels (gate digits, step numbers when switched on, the stars at the feet) live behind the HUD.
            hud.labelLayer = Rect("WorldLabels", root).Stretch();

            // Top left: menu (three bars) and the stage title on a plate that fits the name.
            hud.menu = IconButton(root, "Menu", null, new Vector2(0, 1), new Vector2(Safe, -Safe));
            for (int i = 0; i < 3; i++)
            {
                var bar = Image($"Bar {i + 1}", hud.menu.transform, null, Ui.Ink, new Vector2(.5f, .5f), new Vector2(0, (1 - i) * 7), new Vector2(22, 3));
                bar.rectTransform.pivot = new Vector2(.5f, .5f);
            }
            var titlePlate = Plate("TitlePlate", root, new Vector2(0, 1), new Vector2(Safe + Icon + Pad, -Safe + 4), new Vector2(200, TitleH));
            var titleRow = titlePlate.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleRow.padding = new RectOffset((int)Pad, (int)Pad, 0, 0);
            titleRow.childAlignment = TextAnchor.MiddleLeft;
            titleRow.childControlWidth = titleRow.childControlHeight = true;
            titleRow.childForceExpandWidth = titleRow.childForceExpandHeight = false;
            titlePlate.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.title = Text(titlePlate, "Title", "", 22, Ui.Ink, TextAnchor.MiddleLeft);
            hud.title.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Top right: the brothers as three muffler-colour dots (12 px, 14 apart) on a small plate.
            var dotsPlate = Plate("BodyDots", root, new Vector2(1, 1), new Vector2(-Safe, -Safe), new Vector2(3 * 12 + 2 * 14 + 2 * 16, 36));
            hud.bodyDots = new HudView.BodyDot[3];
            for (int k = 0; k < 3; k++)
            {
                var r = Rect($"Body {k + 1}", dotsPlate);
                r.Place(new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(16 + 6 + k * 26, 0), new Vector2(12, 12));
                var ring = Image("Ring", r, null, Color.white, new Vector2(.5f, .5f), Vector2.zero, new Vector2(12, 12));
                var core = Image("Core", r, null, Color.white, new Vector2(.5f, .5f), Vector2.zero, new Vector2(7, 7));
                ring.rectTransform.pivot = core.rectTransform.pivot = new Vector2(.5f, .5f);
                hud.bodyDots[k] = new HudView.BodyDot { root = r, ring = ring, core = core };
            }

            // Under the dots, right margin: statues on statue-door stages (secondary 18, up to 2 lines).
            var statuesPlate = Plate("StatuesPlate", root, new Vector2(1, 1), new Vector2(-Safe, -(Safe + 36 + 12)), new Vector2(Margin, 60));
            hud.statues = Text(statuesPlate, "Statues", "", 18, Ui.Ink, TextAnchor.MiddleLeft);
            Inside(hud.statues.rectTransform, Pad);
            hud.statues.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Secondary(hud.statues);
            hud.statues.supportRichText = true;

            // Left margin: the stage story (on entering a stage) and the controls guide (first play), stacked.
            var guide = Plate("Guide", root, new Vector2(0, 1), new Vector2(Safe, -(Safe + Icon + 32)), new Vector2(Margin, 100));
            var col = guide.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            col.spacing = 14;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            guide.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.guidePanel = guide.gameObject;
            hud.story = Text(guide, "Story", "", 22, Ui.Ink, TextAnchor.UpperLeft);
            hud.guide = Text(guide, "Keys", "", 18, Ui.Ink, TextAnchor.UpperLeft);
            Ui.Secondary(hud.guide);
            foreach (var t in new[] { hud.story, hud.guide }) { t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; }
            guide.gameObject.SetActive(false);

            // KEEP / SWITCH: guide plate + two cards (picture, name, key badge, result line, border in the next brother's
            // colour), placed beside the board near the brother at runtime (HudView.PlaceChoice), only while choosing.
            var choice = Rect("Choice", root);
            choice.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Safe, -100), new Vector2(HudView.CardW, 2 * HudView.CardH + HudView.CardGap));
            hud.choice = choice;
            var guidePlate = Plate("GuidePlate", choice, new Vector2(0, 1), Vector2.zero, new Vector2(HudView.CardW, HudView.GuideH));
            hud.choiceGuide = Text(guidePlate, "Guide", "", 22, Ui.Ink, TextAnchor.MiddleLeft);
            Inside(hud.choiceGuide.rectTransform, Pad);
            hud.choiceGuide.horizontalOverflow = HorizontalWrapMode.Wrap;
            (hud.keep, hud.keepDesc, hud.keepPicture, hud.keepEdges) = Card(choice, "Keep", "K", 0);
            (hud.switchBody, hud.switchDesc, hud.switchPicture, hud.switchEdges) = Card(choice, "Switch", "X", 1);
            hud.bodySprites = new[] { Sprite("body_a"), Sprite("body_b"), Sprite("body_c") };
            hud.stoneSprite = Sprite("body_stone");
            choice.gameObject.SetActive(false);

            // Bottom left: undo, restart (icons, 48 touch targets).
            hud.undo = IconButton(root, "Undo", "undo", new Vector2(0, 0), new Vector2(Safe, Safe));
            hud.restart = IconButton(root, "Restart", "restart", new Vector2(0, 0), new Vector2(Safe + Icon + 14, Safe));

            // Bottom centre: the message line (bumps, the H solution), as tall as its lines.
            var msgPlate = Plate("MessagePlate", root, new Vector2(.5f, 0), new Vector2(0, Safe), new Vector2(720, 48));
            msgPlate.pivot = new Vector2(.5f, 0);
            var msgCol = msgPlate.gameObject.AddComponent<VerticalLayoutGroup>();
            msgCol.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            msgCol.childControlWidth = msgCol.childControlHeight = true;
            msgCol.childForceExpandWidth = true;
            msgPlate.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.message = Text(msgPlate, "Message", "", 22, Ui.Ink, TextAnchor.MiddleCenter);
            hud.message.horizontalOverflow = HorizontalWrapMode.Wrap;
            msgPlate.gameObject.SetActive(false);
            statuesPlate.gameObject.SetActive(false);
            return go;
        }

        /// A KEEP / SWITCH card: the button (art frame), picture and name top left, the key badge top right, the result
        /// line below (22, up to 3 lines), and four border lines (coloured at runtime).
        static (Button, Text, Image, Image[]) Card(RectTransform parent, string name, string key, int index)
        {
            var size = new Vector2(HudView.CardW, HudView.CardH);
            var b = Button(parent, name, size);
            ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -index * (size.y + HudView.CardGap)), size);
            var edges = new Image[4];
            for (int k = 0; k < 4; k++)
            {
                var e = Image(new[] { "Edge Top", "Edge Bottom", "Edge Left", "Edge Right" }[k], b.transform, null, Color.white, Vector2.zero, Vector2.zero, Vector2.zero);
                var r = e.rectTransform;
                const float In = 3, T = 2;
                switch (k)
                {
                    case 0: r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(-2 * In, T); r.anchoredPosition = new Vector2(0, -In); break;
                    case 1: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(-2 * In, T); r.anchoredPosition = new Vector2(0, In); break;
                    case 2: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, .5f); r.sizeDelta = new Vector2(T, -2 * In); r.anchoredPosition = new Vector2(In, 0); break;
                    default: r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, .5f); r.sizeDelta = new Vector2(T, -2 * In); r.anchoredPosition = new Vector2(-In, 0); break;
                }
                edges[k] = e;
            }
            // + 2: hand-font glyphs reach a little past the pen position (UiLayoutCheck glyph boxes need the full 8 inside).
            var inset = Ui.FrameBorder((Image)b.targetGraphic) + Vector4.one * (Ui.LabelPadding + 2);
            const float Row = 36;
            var picture = Image("Picture", b.transform, null, Color.white, new Vector2(0, 1), new Vector2(inset.x, -inset.w), new Vector2(Row, Row));
            picture.preserveAspect = true;
            var label = b.GetComponentInChildren<Text>();
            label.transform.SetAsLastSibling();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 22;
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(.5f, 1);
            lr.offsetMin = new Vector2(inset.x + Row + 8, -(inset.w + Row));
            lr.offsetMax = new Vector2(-(inset.z + 34), -inset.w);
            var badge = Text(b.transform, "Key", key, 22, Ui.Gold, TextAnchor.MiddleCenter);
            Ui.Secondary(badge);
            badge.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-inset.z, -inset.w), new Vector2(30, Row));
            var desc = Text(b.transform, "Desc", "", 22, Ui.Ink, TextAnchor.UpperLeft);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            desc.lineSpacing = 0.95f;
            var d = desc.rectTransform;
            d.anchorMin = Vector2.zero; d.anchorMax = Vector2.one;
            d.offsetMin = new Vector2(inset.x, inset.y);
            d.offsetMax = new Vector2(-inset.z, -(inset.w + Row + 6));
            return (b, desc, picture, edges);
        }

        static Button IconButton(Transform parent, string name, string icon, Vector2 corner, Vector2 pos)
        {
            var b = Button(parent, name, new Vector2(Icon, Icon));
            ((RectTransform)b.transform).Place(corner, corner, pos, new Vector2(Icon, Icon));
            Object.DestroyImmediate(b.GetComponentInChildren<Text>().gameObject); // icon only
            if (icon != null)
            {
                var i = Image("Icon", b.transform, Sprite(icon), Ui.Ink, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 28));
                i.rectTransform.pivot = new Vector2(.5f, .5f);
                i.preserveAspect = true;
            }
            return b;
        }

        static Button Button(Transform parent, string name, Vector2 size)
        {
            var r = Rect(name, parent);
            r.sizeDelta = size;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Sprite("button_default");
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Ui.ButtonFrameScale;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = Sprite("button_hover"), pressedSprite = Sprite("button_pressed"),
                selectedSprite = Sprite("button_hover"), disabledSprite = Sprite("button_disabled"),
            };
            var t = Text(r, "Label", "", 22, Ui.Ink, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch();
            return b;
        }

        /// A flat panel-colour plate (14.4), placed with pivot = anchor.
        static RectTransform Plate(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = Rect(name, parent);
            r.Place(anchor, anchor, pos, size);
            var img = r.gameObject.AddComponent<Image>();
            var c = Ui.PanelBg;
            img.color = new Color(c.r, c.g, c.b, Ui.PlateAlpha);
            img.raycastTarget = false;
            return r;
        }

        static void Inside(RectTransform r, float pad)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(pad, pad); r.offsetMax = new Vector2(-pad, -pad);
        }

        /// Image placed with pivot = anchor (corner-relative), raycasts off.
        static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = Rect(name, parent);
            r.Place(anchor, anchor, pos, size);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor align)
        {
            var t = Ui.Label(parent, text, size, color, align, name);
            t.font = font;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        static Sprite Sprite(string name) =>
            sprites.TryGetValue(name, out var s) ? s : throw new InvalidOperationException($"{AtlasPath}: no slice \"{name}\"");
    }
}
