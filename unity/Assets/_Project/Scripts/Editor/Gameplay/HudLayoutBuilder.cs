using System;
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
        const float Left = 44;
        static readonly Vector2 ButtonSize = new Vector2(220, 50);

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

                // Stage line, top left: "08  name", no box (number in the number font, name in the title font at runtime)
                var box = (RectTransform)Find("StageBox");
                Place(box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Left, -26), new Vector2(640, 56));
                foreach (var img in box.GetComponentsInChildren<Image>(true)) img.enabled = false;
                Place(hud.stageNumber.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(64, 50));
                Style(hud.stageNumber, 34, Ui.Gold, TextAnchor.MiddleLeft);
                Place(hud.stageName.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(76, 0), new Vector2(560, 50));
                Style(hud.stageName, 34, Ui.Gold, TextAnchor.MiddleLeft);
                hud.stageName.horizontalOverflow = HorizontalWrapMode.Wrap;
                hud.stageName.resizeTextForBestFit = true;
                hud.stageName.resizeTextMinSize = 22;
                hud.stageName.resizeTextMaxSize = 34;

                // Left column: the story in the middle, the hint at its foot (clear of the KEEP/SWITCH row at x >= 320)
                var bar = root.transform.Find("HintBar");
                if (bar != null) bar.gameObject.SetActive(false);
                var story = hud.stageStory;
                if (story == null)
                {
                    var go = new GameObject("Story", typeof(RectTransform)) { layer = 5 };
                    go.transform.SetParent(root.transform, false);
                    story = go.AddComponent<Text>();
                    story.raycastTarget = false;
                    story.font = hud.stageHint.font;
                    hud.stageStory = story;
                }
                Place(story.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(Left, 90), new Vector2(270, 130));
                Style(story, 24, Ui.Ink, TextAnchor.UpperLeft);
                story.horizontalOverflow = HorizontalWrapMode.Wrap;
                story.verticalOverflow = VerticalWrapMode.Truncate;
                story.lineSpacing = 1.2f;
                Place(hud.stageHint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(Left, 148), new Vector2(270, 96));
                Style(hud.stageHint, 18, Ui.Ink, TextAnchor.LowerLeft);
                hud.stageHint.horizontalOverflow = HorizontalWrapMode.Wrap;
                hud.stageHint.verticalOverflow = VerticalWrapMode.Truncate;
                // E-4: longer text shrinks to fit instead of being cut (hint 23: ko 56 / en 97 characters fit at full size today)
                BestFit(story, 16, 24);
                BestFit(hud.stageHint, 13, 18);
                // The message line also carries the H solution (long routes in en: "Up Down ..."): wrap and shrink.
                hud.message.horizontalOverflow = HorizontalWrapMode.Wrap;
                hud.message.verticalOverflow = VerticalWrapMode.Truncate;
                hud.message.rectTransform.sizeDelta = new Vector2(1200, 48);
                BestFit(hud.message, 12, 20);
                // The 2026-11-10 planets reach under the left column: a soft outline keeps the text readable on them.
                foreach (var t in new[] { story, hud.stageHint, hud.stageName, hud.stageNumber, hud.nextStep })
                    (t.GetComponent<Outline>() ?? t.gameObject.AddComponent<Outline>()).effectColor = new Color(0, 0, 0, .55f);

                // Next step: plain text under the dipper
                foreach (var img in hud.nextStepBox.GetComponents<Image>()) img.enabled = false;
                Style(hud.nextStep, 24, Ui.Gold, TextAnchor.MiddleCenter);

                // Travellers: portrait and state only (state in words, docs 12.6), no panel, no names
                var travellers = Find("Travellers");
                foreach (var img in travellers.GetComponents<Image>()) img.enabled = false;
                foreach (var slot in hud.bodySlots)
                {
                    slot.name.gameObject.SetActive(false);
                    slot.stateBg.enabled = false;
                    Style(slot.state, 18, Ui.Ink, TextAnchor.MiddleCenter);
                }

                // Footer: one row of plain buttons (text only, the art button sprite stays)
                var footer = (RectTransform)Find("Footer");
                var row = footer.GetComponent<HorizontalLayoutGroup>();
                row.spacing = 16;
                footer.sizeDelta = new Vector2(5 * ButtonSize.x + 4 * row.spacing, ButtonSize.y);
                foreach (var b in new[] { hud.undo, hud.restart, hud.hint, hud.stageSelect, hud.settings })
                {
                    ((RectTransform)b.transform).sizeDelta = ButtonSize;
                    var icon = b.transform.Find("Icon");
                    if (icon != null) icon.gameObject.SetActive(false);
                    var label = b.GetComponentInChildren<Text>(true);
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    Style(label, 20, Ui.Ink, TextAnchor.MiddleCenter);
                }

                var issues = new System.Collections.Generic.List<string>();
                hud.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] HUD laid out: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

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
