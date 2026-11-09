using System;
using System.Linq;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Lays out UI/Prefabs/ResultPanel.prefab after the art UI concept (concepts/ui/en-ko.html, result / result-1 /
    /// result-3, 1280 x 720): a 716 x 556 panel, the title on top, three 80 px stars (docs/architecture.md 6.5), the
    /// criterion under each star (result.star1..3), the body line, then one row of three buttons at the bottom.
    /// Positions and sizes only: words come from the string table at runtime, fonts from UiTheme.FontFor, the look of
    /// panel and buttons from the theme variant (RuinsHengeUiBuilder). Re-running rebuilds the star and label rows.
    /// After this the prefab is the source. Owner: gameplay/UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.ResultLayoutBuilder.BatchBuild
    /// </summary>
    public static class ResultLayoutBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/ResultPanel.prefab";
        const string AtlasPath = "Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png";

        static readonly Vector2 PanelSize = new Vector2(716, 556);
        const float StarSize = 80, Column = 210; // star and label columns share centres
        static readonly Vector2 ButtonSize = new Vector2(198, 50);

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Seven Steps/Gameplay/Lay Out Result Panel", priority = 33)]
        public static void Build()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
            Sprite Get(string n) => sprites.TryGetValue(n, out var s) ? s : throw new InvalidOperationException($"{AtlasPath}: no slice \"{n}\"");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<ResultView>() ?? throw new InvalidOperationException($"{PrefabPath}: no ResultView");
                var panel = root.transform.Find("Overlay/Panel") as RectTransform
                    ?? throw new InvalidOperationException($"{PrefabPath}: no Overlay/Panel");
                panel.sizeDelta = PanelSize;

                Top(view.title.rectTransform, -36, new Vector2(660, 76));
                view.title.fontSize = 46;
                view.title.alignment = TextAnchor.MiddleCenter;
                view.title.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.title.resizeTextForBestFit = true; // long planet-finale titles shrink instead of overflowing
                view.title.resizeTextMinSize = 28;
                view.title.resizeTextMaxSize = 46;

                // Stars
                var stars = Row(panel, "Stars", -146, new Vector2(Column * 3, StarSize), Column - StarSize);
                view.stars = new Image[Core.Stars.Max];
                for (int k = 0; k < view.stars.Length; k++)
                {
                    var star = Child($"Star {k + 1}", stars);
                    star.sizeDelta = new Vector2(StarSize, StarSize);
                    var img = star.gameObject.AddComponent<Image>();
                    img.sprite = Get("empty");
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    view.stars[k] = img;
                }
                view.starEmpty = Get("empty");
                view.starFilled = Get("star");

                // What each star asks, one column under each star
                var labels = Row(panel, "StarLabels", -246, new Vector2(Column * 3, 64), 0);
                view.starLabels = new Text[Core.Stars.Max];
                for (int k = 0; k < view.starLabels.Length; k++)
                {
                    var cell = Child($"Label {k + 1}", labels);
                    cell.sizeDelta = new Vector2(Column, 64);
                    var t = cell.gameObject.AddComponent<Text>();
                    t.font = view.body.font;
                    t.fontSize = 20;
                    t.alignment = TextAnchor.UpperCenter;
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.verticalOverflow = VerticalWrapMode.Truncate;
                    t.color = view.body.color;
                    t.raycastTarget = false;
                    view.starLabels[k] = t;
                }

                // Body: bodies used, records, planet finale or the journey's end (two lines at most)
                Top(view.body.rectTransform, -326, new Vector2(640, 90));
                view.body.fontSize = 20;
                view.body.alignment = TextAnchor.MiddleCenter;

                // Buttons: one row at the bottom (three show at a time: next / again / select, or undo / restart / select)
                var buttons = panel.Find("Buttons") as RectTransform ?? throw new InvalidOperationException($"{PrefabPath}: no Overlay/Panel/Buttons");
                var vertical = buttons.GetComponent<VerticalLayoutGroup>();
                if (vertical != null) Object.DestroyImmediate(vertical);
                var h = buttons.GetComponent<HorizontalLayoutGroup>() ?? buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 20;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
                buttons.anchorMin = buttons.anchorMax = new Vector2(.5f, 0);
                buttons.pivot = new Vector2(.5f, .5f);
                buttons.anchoredPosition = new Vector2(0, 62);
                buttons.sizeDelta = new Vector2(PanelSize.x - 40, ButtonSize.y);
                foreach (var b in new[] { view.next, view.again, view.select, view.undo, view.restart })
                {
                    ((RectTransform)b.transform).sizeDelta = ButtonSize;
                    var label = b.GetComponentInChildren<Text>(true);
                    if (label != null) { label.fontSize = 20; label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 20; }
                }

                var issues = new System.Collections.Generic.List<string>();
                view.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] Result panel laid out: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Top(RectTransform r, float y, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = size;
        }

        /// A fresh horizontal row under the panel top (an older row of the same name is replaced).
        static RectTransform Row(RectTransform panel, string name, float y, Vector2 size, float spacing)
        {
            var old = panel.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var rt = Child(name, panel);
            Top(rt, y, size);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return rt;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
