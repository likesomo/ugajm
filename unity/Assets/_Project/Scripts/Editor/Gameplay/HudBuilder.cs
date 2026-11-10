using System;
using System.Collections.Generic;
using System.Linq;
using SevenSteps.Core;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Builds UI/Prefabs/Hud.prefab once from the art UI kit (Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png slices,
    /// design/art/ui-theme.md, game-hud-v2) and puts it in UiTheme.hud of every theme (Data/UiTheme_Default and the art's
    /// RuinsHengeTheme). After the first build the prefab is the source: edit it, do not rebuild (an existing prefab is
    /// left alone unless forced). Owner: gameplay/UI.
    /// Menu: Seven Steps > Gameplay > Build HUD. Batch: -executeMethod SevenSteps.EditorTools.HudBuilder.BatchBuild
    /// </summary>
    public static class HudBuilder
    {
        public const string PrefabPath = "Assets/_Project/UI/Prefabs/Hud.prefab";
        const string AtlasPath = "Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png";
        const string FontPath = "Assets/Shared/Fonts/Jua-Regular.ttf";
        static readonly string[] ThemePaths = { "Assets/_Project/Data/UiTheme_Default.asset", "Assets/_Project/Art/UI/RuinsHengeTheme.asset" };

        // design/art/ui-theme.md palette
        static readonly Color Ink = Hex("EEE6D4"), Dim = Hex("A0B0BA"), Brass = Hex("C9AB6B");

        // Big Dipper, normalized (y down), ui-theme.md: Dubhe .. Alkaid. Lines 1-2, 2-3, 3-4, 4-1, 4-5, 5-6, 6-7.
        internal static readonly Vector2[] Dipper =
        {
            new Vector2(.12f, .12f), new Vector2(.10f, .36f), new Vector2(.35f, .40f), new Vector2(.42f, .18f),
            new Vector2(.62f, .12f), new Vector2(.79f, .20f), new Vector2(.98f, .28f),
        };
        internal static readonly (int, int)[] Edges = { (0, 1), (1, 2), (2, 3), (3, 0), (3, 4), (4, 5), (5, 6) };

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
                Ui.Theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePaths[1]); // Ui.Label reads the theme font
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
            var go = new GameObject("Hud", typeof(RectTransform));
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var hud = go.AddComponent<HudView>();
            var root = go.transform;

            // World-anchored labels (step numbers, gate digits) live behind the HUD.
            hud.labelLayer = Stretch(Rect("WorldLabels", root));

            // Stage box (wood frame) and the hint with a brass bar, top left
            var box = Panel("StageBox", root, "panel_wood", new Vector2(0, 1), new Vector2(24, -20), new Vector2(430, 54));
            hud.stageNumber = Label(box, "스테이지 01", 18, Dim, TextAnchor.MiddleLeft, "Number", new Vector2(0, .5f), new Vector2(18, 0), new Vector2(120, 40));
            Image("Divider", box, null, Brass * new Color(1, 1, 1, .6f), new Vector2(0, .5f), new Vector2(140, 0), new Vector2(2, 28));
            hud.stageName = Label(box, "", 24, Ink, TextAnchor.MiddleLeft, "Name", new Vector2(0, .5f), new Vector2(156, 0), new Vector2(262, 40));
            Image("HintBar", root, null, Brass, new Vector2(0, 1), new Vector2(28, -86), new Vector2(3, 56));
            hud.stageHint = Label(root, "", 17, Ink, TextAnchor.UpperLeft, "Hint", new Vector2(0, 1), new Vector2(42, -84), new Vector2(400, 66));
            hud.stageHint.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Big Dipper counter, top centre (360 x 132, the art's DipperCounter layout)
            var dipper = Rect("Dipper", root);
            dipper.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(360, 130));
            // y stretched about the middle row (art y 0.12..0.40 spans 95 px) so the bowl reads at HUD size; numbers sit above the stars.
            Vector2 P(int i) => new Vector2((Dipper[i].x - .5f) * 330, -(Dipper[i].y - .26f) * 340);
            hud.lines = new Image[Rules.Steps];
            for (int e = 0; e < Edges.Length; e++)
            {
                var (a, b) = Edges[e];
                var l = Image($"Line_{a + 1}_{b + 1}", dipper, null, Brass * new Color(1, 1, 1, .35f), new Vector2(.5f, .5f), (P(a) + P(b)) * .5f,
                    new Vector2(Vector2.Distance(P(a), P(b)), 2.5f));
                l.rectTransform.pivot = new Vector2(.5f, .5f);
                l.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(P(b).y - P(a).y, P(b).x - P(a).x) * Mathf.Rad2Deg);
                hud.lines[e] = l;
            }
            hud.stars = new Image[Rules.Steps];
            hud.starNumbers = new Text[Rules.Steps];
            for (int i = 0; i < Rules.Steps; i++)
            {
                var star = Image($"Star_{i + 1}", dipper, Sprite("empty"), Color.white, new Vector2(.5f, .5f), P(i), new Vector2(30, 30));
                star.rectTransform.pivot = new Vector2(.5f, .5f);
                star.preserveAspect = true;
                hud.stars[i] = star;
                var n = Label(star.transform, (i + 1).ToString(), 16, Dim, TextAnchor.MiddleCenter, $"Number_{i + 1}", new Vector2(.5f, .5f), new Vector2(0, 22), new Vector2(32, 22));
                n.rectTransform.pivot = new Vector2(.5f, .5f);
                hud.starNumbers[i] = n;
            }
            hud.starEmpty = Sprite("empty");
            hud.starFilled = Sprite("star");
            var next = Panel("NextStep", root, "panel_stone", new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(240, 40));
            next.pivot = new Vector2(.5f, 1);
            next.anchoredPosition = new Vector2(0, -150);
            hud.nextStep = Label(next, "다음 걸음 1", 20, Brass, TextAnchor.MiddleCenter, "Text", new Vector2(.5f, .5f), Vector2.zero, new Vector2(230, 36));
            hud.nextStep.rectTransform.pivot = new Vector2(.5f, .5f);
            hud.nextStepBox = next.gameObject;

            // Travellers, top right: one slot per body (A, B, C), state written out
            var bodies = Panel("Travellers", root, "panel_stone", new Vector2(1, 1), new Vector2(-20, -16), new Vector2(380, 150));
            bodies.pivot = new Vector2(1, 1);
            bodies.anchoredPosition = new Vector2(-20, -16);
            var row = bodies.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 10, 10);
            row.spacing = 6;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = bodies.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.bodySlots = new HudView.BodySlot[3];
            hud.bodySprites = new[] { Sprite("body_a"), Sprite("body_b"), Sprite("body_c") };
            hud.bodyStone = Sprite("body_stone");
            string[] names = { "청록 여행자", "황토 여행자", "적갈 여행자" };
            for (int k = 0; k < 3; k++) hud.bodySlots[k] = BodySlot(bodies, k, names[k], hud.bodySprites[k], k == 0 ? "사용 중" : "남음");

            hud.statues = Label(root, "", 18, Ink, TextAnchor.MiddleRight, "Statues", new Vector2(1, 1), new Vector2(-24, -176), new Vector2(320, 28));
            hud.statues.rectTransform.pivot = new Vector2(1, 1);
            hud.statues.rectTransform.anchoredPosition = new Vector2(-24, -176);

            // Message, KEEP / SWITCH, footer and key help, bottom
            hud.message = Label(root, "", 20, Ink, TextAnchor.MiddleCenter, "Message", new Vector2(.5f, 0), new Vector2(0, 100), new Vector2(1200, 30));
            hud.message.rectTransform.pivot = new Vector2(.5f, 0);
            Ui.AddOutline(hud.message, new Color(0, 0, 0, .6f));
            hud.choice = Row("Choice", root, 24, new Vector2(.5f, 0), new Vector2(0, 140), new Vector2(640, 88));
            hud.keep = ChoiceButton(hud.choice, "KEEP (K)", "여기에 선돌을 남긴다\n다음 형제는 지금 출발점에서", Hex("B9BFC0"));
            hud.switchBody = ChoiceButton(hud.choice, "SWITCH (X)", "선돌을 남기지 않는다\n여기가 새 출발점이 된다", Brass);
            hud.keepDesc = hud.keep.transform.Find("Desc").GetComponent<Text>();
            hud.switchDesc = hud.switchBody.transform.Find("Desc").GetComponent<Text>();
            hud.choice.gameObject.SetActive(false);

            var footer = Row("Footer", root, 12, new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(940, 52));
            hud.undo = FooterButton(footer, "되돌리기 Z", "undo");
            hud.restart = FooterButton(footer, "재시작 R", "restart");
            hud.hint = FooterButton(footer, "풀이 보기 H", "hint");
            hud.stageSelect = FooterButton(footer, "스테이지 Esc", null);
            hud.settings = FooterButton(footer, "설정", "settings");
            var keys = Label(root, "이동: 방향키 / WASD / 옆 칸 클릭, 7걸음 후: K / X, 스테이지: [ ]", 13, Dim, TextAnchor.MiddleCenter, "Keys",
                new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(700, 22));
            keys.rectTransform.pivot = new Vector2(.5f, 0);
            hud.keys = keys;
            return go;
        }

        static HudView.BodySlot BodySlot(Transform parent, int k, string name, Sprite portrait, string state)
        {
            var slot = Rect($"Body {k + 1}", parent);
            slot.sizeDelta = new Vector2(112, 128);
            var img = Image("Portrait", slot, portrait, Color.white, new Vector2(.5f, 1), new Vector2(0, -2), new Vector2(60, 60));
            img.rectTransform.pivot = new Vector2(.5f, 1);
            img.preserveAspect = true;
            var n = Label(slot, name, 15, Ink, TextAnchor.MiddleCenter, "Name", new Vector2(.5f, 1), new Vector2(0, -64), new Vector2(112, 22));
            n.rectTransform.pivot = new Vector2(.5f, 1);
            var bg = Image("StateBg", slot, Sprite("button_default"), Hex("568C88"), new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(92, 28));
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.rectTransform.pivot = new Vector2(.5f, 0);
            var s = Label(bg.transform, state, 15, Ink, TextAnchor.MiddleCenter, "State", new Vector2(.5f, .5f), Vector2.zero, new Vector2(92, 28));
            s.rectTransform.pivot = new Vector2(.5f, .5f);
            return new HudView.BodySlot { root = slot.gameObject, portrait = img, name = n, state = s, stateBg = bg };
        }

        static Button FooterButton(Transform parent, string label, string icon)
        {
            var b = Button(parent, label, 18, new Vector2(176, 52));
            var text = b.GetComponentInChildren<Text>();
            if (icon != null)
            {
                var i = Image("Icon", b.transform, Sprite(icon), Ink, new Vector2(0, .5f), new Vector2(16, 0), new Vector2(26, 26));
                i.preserveAspect = true;
                text.rectTransform.offsetMin = new Vector2(30, 0);
            }
            return b;
        }

        static Button ChoiceButton(Transform parent, string title, string desc, Color accent)
        {
            var b = Button(parent, title, 24, new Vector2(300, 88));
            var t = b.GetComponentInChildren<Text>();
            t.color = accent;
            t.alignment = TextAnchor.UpperCenter;
            t.rectTransform.offsetMax = new Vector2(0, -8);
            var d = Ui.Label(b.transform, desc, 15, Dim, TextAnchor.LowerCenter, "Desc");
            d.font = font;
            Stretch(d.rectTransform).offsetMin = new Vector2(0, 8);
            d.lineSpacing = 1.1f;
            return b;
        }

        static Button Button(Transform parent, string label, int size, Vector2 sizeDelta)
        {
            var r = Rect(label, parent);
            r.sizeDelta = sizeDelta;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Sprite("button_default");
            img.type = UnityEngine.UI.Image.Type.Sliced;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = Sprite("button_hover"), pressedSprite = Sprite("button_pressed"),
                selectedSprite = Sprite("button_hover"), disabledSprite = Sprite("button_disabled"),
            };
            var t = Ui.Label(r, label, size, Ink, TextAnchor.MiddleCenter, "Label");
            t.font = font;
            Stretch(t.rectTransform);
            return b;
        }

        static RectTransform Panel(string name, Transform parent, string sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Image(name, parent, Sprite(sprite), Color.white, anchor, pos, size);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.raycastTarget = false;
            return img.rectTransform;
        }

        static RectTransform Row(string name, Transform parent, float spacing, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = Rect(name, parent);
            r.Place(anchor, new Vector2(.5f, 0), pos, size);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return r;
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

        static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, string name, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var t = Ui.Label(parent, text, size, color, align, name);
            t.font = font;
            t.rectTransform.Place(anchor, anchor, pos, sizeDelta);
            return t;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        static RectTransform Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        static Sprite Sprite(string name) =>
            sprites.TryGetValue(name, out var s) ? s : throw new InvalidOperationException($"{AtlasPath}: no slice \"{name}\"");

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : throw new ArgumentException(h);
    }
}
