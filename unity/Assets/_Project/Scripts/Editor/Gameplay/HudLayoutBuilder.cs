using System;
using SevenSteps.Core;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Lays out UI/Prefabs/Hud.prefab after the art UI concept (concepts/ui/en-ko.html hud, 1280 x 720): the stage number
    /// and name on one line top left without a box, the stage story in the left column and the hint at its foot, the
    /// next step as plain text under the dipper, the travellers as portrait + state with no panel, and one row of plain
    /// buttons at the bottom. Positions, sizes and which art pieces show only: words come from the string table, fonts
    /// from UiTheme.FontFor at runtime, sprites stay as the art kit made them. Re-running is safe (the story text is
    /// reused). After this the prefab is the source. Owner: gameplay/UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.HudLayoutBuilder.BatchBuild
    /// </summary>
    public static class HudLayoutBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/Hud.prefab";
        /// Safe area (14.6) and the plates' inside padding: 8 required + 4 so the 22 px glyph boxes clear it comfortably.
        const float Safe = 24, Pad = 12, TitleW = 412;
        static readonly Vector2 ButtonSize = new Vector2(220, 60);
        static readonly Vector2 ChoiceSize = new Vector2(252, 120);

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Seven Steps/Gameplay/Lay Out HUD", priority = 32)]
        public static void Build()
        {
            // Colours (Ui.Gold, Ui.Ink) come from the game's theme, as at runtime.
            var assets = AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = assets != null && assets.uiTheme != null ? assets.uiTheme
                : throw new InvalidOperationException("Assets/_Project/Data/GameAssets.asset: uiTheme is not set");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var hud = root.GetComponent<HudView>() ?? throw new InvalidOperationException($"{PrefabPath}: no HudView");
                Transform Find(string path) => root.transform.Find(path) ?? throw new InvalidOperationException($"{PrefabPath}: no {path}");

                // docs/architecture.md 14 (ui-layout-fix, 2026-11-10): every word over the 3D scene sits on a plate (flat panel
                // colour, alpha 0.9, 8+ clear inside), body text 22+, secondary 18+, nothing in the 24 px edge or in
                // HudView.BoardFrame (where BoardView fits the board). Areas, top-left reference pixels:
                //   title plate 24,24 412x72 | Dipper plate (Dipper + next step) centre top | travellers plate top right
                //   left column 24,112 276x508: story, hint, controls | right column 980,112 276 wide: statues, message, KEEP/SWITCH
                //   footer 5 buttons 220x60 at the bottom (y 636..696)
                foreach (var t in root.GetComponentsInChildren<Outline>(true)) Object.DestroyImmediate(t); // plates instead
                var bar = root.transform.Find("HintBar");
                if (bar != null) bar.gameObject.SetActive(false);

                // Title plate: "08  name" (number in the number font, name in the title font at runtime, up to 2 lines)
                var box = (RectTransform)Find("StageBox");
                TopLeft(box, Safe, Safe, TitleW, 72);
                foreach (var img in box.GetComponentsInChildren<Image>(true)) img.enabled = false;
                MakePlate(box.gameObject);
                Place(hud.stageNumber.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(Pad, 0), new Vector2(60, 50));
                Style(hud.stageNumber, 34, Ui.Gold, TextAnchor.MiddleLeft);
                Place(hud.stageName.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(Pad + 68, 0), new Vector2(TitleW - 2 * Pad - 68, 72 - 2 * Pad));
                Style(hud.stageName, 34, Ui.Gold, TextAnchor.MiddleLeft);
                hud.stageName.horizontalOverflow = HorizontalWrapMode.Wrap;
                hud.stageName.verticalOverflow = VerticalWrapMode.Truncate;
                hud.stageName.lineSpacing = 0.9f;
                BestFit(hud.stageName, 22, 34);

                // Left column: story, hint, then the controls line at the foot (one plate, stacked by a layout group)
                var left = Column(root.transform, "LeftColumn", Safe, 112, 276, 508);
                var story = hud.stageStory;
                if (story == null)
                {
                    var go = new GameObject("Story", typeof(RectTransform)) { layer = 5 };
                    story = go.AddComponent<Text>();
                    story.raycastTarget = false;
                    story.font = hud.stageHint.font;
                    hud.stageStory = story;
                }
                story.transform.SetParent(left, false);
                Style(story, 24, Ui.Ink, TextAnchor.UpperLeft);
                story.lineSpacing = 1.1f;
                hud.stageHint.transform.SetParent(left, false);
                Style(hud.stageHint, 22, Ui.Gold, TextAnchor.UpperLeft);
                var spacer = left.Find("Spacer") ?? new GameObject("Spacer", typeof(RectTransform)) { layer = 5 }.transform;
                spacer.SetParent(left, false);
                (spacer.GetComponent<LayoutElement>() ?? spacer.gameObject.AddComponent<LayoutElement>()).flexibleHeight = 1;
                hud.keys.transform.SetParent(left, false);
                Style(hud.keys, 18, Ui.Dim, TextAnchor.LowerLeft);
                Ui.Secondary(hud.keys);
                story.transform.SetSiblingIndex(0);
                hud.stageHint.transform.SetSiblingIndex(1);
                spacer.SetSiblingIndex(2);
                hud.keys.transform.SetSiblingIndex(3);
                foreach (var t in new[] { story, hud.stageHint, hud.keys }) Flow(t);

                // Right column: statues count, the message line (also the H solution), KEEP / SWITCH; hidden while empty
                var right = Column(root.transform, "SideColumn", 1280 - Safe - 276, 112, 276, 508);
                var fit = right.GetComponent<ContentSizeFitter>() ?? right.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                hud.statues.transform.SetParent(right, false);
                Style(hud.statues, 22, Ui.Ink, TextAnchor.UpperLeft);
                hud.message.transform.SetParent(right, false);
                Style(hud.message, 22, Ui.Ink, TextAnchor.UpperLeft);
                foreach (var t in new[] { hud.statues, hud.message }) Flow(t);
                hud.choice.SetParent(right, false);
                var h = hud.choice.GetComponent<HorizontalLayoutGroup>();
                if (h != null) Object.DestroyImmediate(h);
                var v = hud.choice.GetComponent<VerticalLayoutGroup>() ?? hud.choice.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 12;
                v.childControlWidth = v.childControlHeight = false;
                v.childForceExpandWidth = v.childForceExpandHeight = false;
                v.childAlignment = TextAnchor.UpperCenter;
                var choiceSize = new Vector2(276 - 2 * Pad, 2 * ChoiceSize.y + v.spacing);
                (hud.choice.GetComponent<LayoutElement>() ?? hud.choice.gameObject.AddComponent<LayoutElement>()).preferredHeight = choiceSize.y;
                hud.choice.sizeDelta = choiceSize;
                foreach (var (b, desc) in new[] { (hud.keep, hud.keepDesc), (hud.switchBody, hud.switchDesc) })
                {
                    ((RectTransform)b.transform).sizeDelta = new Vector2(choiceSize.x, ChoiceSize.y);
                    Ui.FitButtonSkin(b);
                    var title = b.GetComponentInChildren<Text>(true);
                    title.fontSize = 22;
                    title.alignment = TextAnchor.UpperCenter;
                    var inset = Ui.FrameBorder((Image)b.targetGraphic) + Vector4.one * Ui.LabelPadding;
                    var d = desc.rectTransform;
                    d.anchorMin = Vector2.zero; d.anchorMax = Vector2.one;
                    d.offsetMin = new Vector2(inset.x, inset.y);
                    d.offsetMax = new Vector2(-inset.z, -(inset.w + 30));
                    Style(desc, 22, Ui.Ink, TextAnchor.LowerCenter);
                    desc.lineSpacing = 1f;
                    desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                    desc.verticalOverflow = VerticalWrapMode.Overflow;
                }
                hud.choice.gameObject.SetActive(false);
                hud.sidePanel = right.gameObject;

                // Dipper plate: the counter and the next step under it. Star positions from HudBuilder.Dipper, the bowl a
                // little flatter (0.8) so the plate stays above the board frame; numbers 18 (secondary) beside their stars on
                // the side no line leaves; dim lines in the dim ink (3:1 on the plate), 2.5 thick.
                var dipper = (RectTransform)Find("Dipper");
                MakePlate(dipper.gameObject);
                hud.lineDim = Ui.Dim;
                Vector2 P(int i) => new Vector2((HudBuilder.Dipper[i].x - .5f) * 330, -(HudBuilder.Dipper[i].y - .26f) * 272);
                // 1 left, 2 left, 3 right-down, 4 right-down, 5 down, 6 left-down, 7 down (between the lines at each star)
                var numberAt = new[] { new Vector2(-1, 0), new Vector2(-1, 0), new Vector2(1, -.5f), new Vector2(.7f, -.7f), new Vector2(0, -1),
                    new Vector2(-.5f, -.87f), new Vector2(0, -1) };
                const float starHalf = 15, numHalf = 12, numDist = 32, grow = 1.25f; // the next star (and its number) grows 1.25x
                var bounds = new Rect();
                for (int k = 0; k < Rules.Steps; k++)
                {
                    var s = P(k);
                    var n = s + numberAt[k].normalized * numDist * grow;
                    var rr = Rect.MinMaxRect(Mathf.Min(s.x - starHalf * grow, n.x - numHalf * grow), Mathf.Min(s.y - starHalf * grow, n.y - numHalf * grow),
                        Mathf.Max(s.x + starHalf * grow, n.x + numHalf * grow), Mathf.Max(s.y + starHalf * grow, n.y + numHalf * grow));
                    bounds = k == 0 ? rr : Rect.MinMaxRect(Mathf.Min(bounds.xMin, rr.xMin), Mathf.Min(bounds.yMin, rr.yMin),
                        Mathf.Max(bounds.xMax, rr.xMax), Mathf.Max(bounds.yMax, rr.yMax));
                }
                const float nextH = 34;
                float plateW = bounds.width + 2 * Pad, plateH = bounds.height + 2 * Pad + nextH + 4;
                var offset = new Vector2(-bounds.center.x, plateH * .5f - Pad - bounds.yMax);
                Vector2 Q(int i) => P(i) + offset;
                for (int k = 0; k < Rules.Steps; k++)
                {
                    var star = hud.stars[k].rectTransform;
                    Place(star, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Q(k), new Vector2(2 * starHalf, 2 * starHalf));
                    var n = hud.starNumbers[k];
                    Style(n, 18, n.color, TextAnchor.MiddleCenter);
                    Ui.Secondary(n);
                    Place(n.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), numberAt[k].normalized * numDist, new Vector2(2 * numHalf, 2 * numHalf));
                }
                for (int e = 0; e < HudBuilder.Edges.Length; e++)
                {
                    var (a, b) = HudBuilder.Edges[e];
                    var l = hud.lines[e].rectTransform;
                    Place(l, new Vector2(.5f, .5f), new Vector2(.5f, .5f), (Q(a) + Q(b)) * .5f, new Vector2(Vector2.Distance(Q(a), Q(b)), 2.5f));
                    l.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Q(b).y - Q(a).y, Q(b).x - Q(a).x) * Mathf.Rad2Deg);
                }
                Place(dipper, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -Safe), new Vector2(plateW, plateH));
                var next = (RectTransform)hud.nextStepBox.transform;
                next.SetParent(dipper, false);
                foreach (var img in hud.nextStepBox.GetComponents<Image>()) img.enabled = false;
                Place(next, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, Pad), new Vector2(plateW - 2 * Pad, nextH));
                Place(hud.nextStep.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(plateW - 2 * Pad, nextH));
                Style(hud.nextStep, 24, Ui.Gold, TextAnchor.MiddleCenter);
                if (Safe + plateH > HudView.BoardFrame.yMin - 8)
                    throw new InvalidOperationException($"Dipper plate bottom {Safe + plateH} reaches the board frame top {HudView.BoardFrame.yMin}");
                Debug.Log($"[SevenSteps] HUD Dipper plate {plateW:0} x {plateH:0}");

                // Travellers plate, top right: portrait with its state word beside it (docs 12.6; names stay hidden)
                var travellers = (RectTransform)Find("Travellers");
                foreach (var img in travellers.GetComponents<Image>()) img.enabled = false;
                MakePlate(travellers.gameObject);
                var row = travellers.GetComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
                row.spacing = 8;
                var slotSize = new Vector2(112, 44);
                Place(travellers, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Safe, -Safe),
                    new Vector2(3 * slotSize.x + 2 * row.spacing + 2 * Pad, slotSize.y + 2 * Pad));
                foreach (var slot in hud.bodySlots)
                {
                    ((RectTransform)slot.root.transform).sizeDelta = slotSize;
                    slot.name.gameObject.SetActive(false);
                    Place(slot.portrait.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(40, 40));
                    slot.stateBg.enabled = false;
                    Place(slot.stateBg.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(46, 0), new Vector2(slotSize.x - 46, slotSize.y));
                    Place(slot.state.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(slotSize.x - 46, slotSize.y));
                    Style(slot.state, 18, Ui.Ink, TextAnchor.MiddleLeft);
                    Ui.Secondary(slot.state);
                }

                // Footer: one row of art buttons, labels inside the frame lines (Ui.FitButtonSkin)
                var footer = (RectTransform)Find("Footer");
                var frow = footer.GetComponent<HorizontalLayoutGroup>();
                frow.spacing = 16;
                Place(footer, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, Safe), new Vector2(5 * ButtonSize.x + 4 * frow.spacing, ButtonSize.y));
                foreach (var b in new[] { hud.undo, hud.restart, hud.hint, hud.stageSelect, hud.settings })
                {
                    ((RectTransform)b.transform).sizeDelta = ButtonSize;
                    var icon = b.transform.Find("Icon");
                    if (icon != null) icon.gameObject.SetActive(false);
                    var label = b.GetComponentInChildren<Text>(true);
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    Ui.FitButtonSkin(b);
                    Style(label, 22, Ui.Ink, TextAnchor.MiddleCenter);
                    label.resizeTextForBestFit = false;
                }

                var issues = new System.Collections.Generic.List<string>();
                hud.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] HUD laid out: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// The plate image on <paramref name="go"/> (its own Image, drawn before its children): flat panel colour, no sprite.
        static Image MakePlate(GameObject go)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            var c = Ui.PanelBg;
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = new Color(c.r, c.g, c.b, Ui.PlateAlpha);
            img.raycastTarget = false;
            img.enabled = true;
            return img;
        }

        /// A plated column (top-left placement) stacking its texts top to bottom with Pad inside.
        static RectTransform Column(Transform root, string name, float x, float y, float w, float hgt)
        {
            var t = root.Find(name) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
                t = (RectTransform)go.transform;
                t.SetParent(root, false);
            }
            TopLeft(t, x, y, w, hgt);
            MakePlate(t.gameObject);
            var v = t.GetComponent<VerticalLayoutGroup>() ?? t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            v.spacing = 14;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperLeft;
            return t;
        }

        /// A text in a column: wraps to the column width, as tall as its lines (never cut).
        static void Flow(Text t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
        }

        static void TopLeft(RectTransform r, float x, float y, float w, float hgt) =>
            Place(r, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, hgt));

        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void BestFit(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
        }

        static void Style(Text t, int size, Color color, TextAnchor align)
        {
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
        }
    }
}
