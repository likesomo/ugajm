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
    /// One-time edit of UI/Prefabs/ResultPanel.prefab for the 3 stars (docs/architecture.md 6.5): a row of three star
    /// images under the title (art atlas "empty" / "star"), the panel grown to 460 x 400 and the body moved down.
    /// Re-running replaces the row. After this the prefab is the source. Owner: gameplay/UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.ResultStarsBuilder.BatchBuild
    /// </summary>
    public static class ResultStarsBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/ResultPanel.prefab";
        const string AtlasPath = "Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png";

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Seven Steps/Gameplay/Add Result Stars", priority = 33)]
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
                panel.sizeDelta = new Vector2(460, 400);
                view.body.rectTransform.anchoredPosition = new Vector2(0, -138);
                view.body.rectTransform.sizeDelta = new Vector2(420, 64);

                var old = panel.Find("Stars");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var row = new GameObject("Stars", typeof(RectTransform)) { layer = 5 };
                var rt = (RectTransform)row.transform;
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1);
                rt.pivot = new Vector2(.5f, 1);
                rt.anchoredPosition = new Vector2(0, -78);
                rt.sizeDelta = new Vector2(200, 54);
                var h = row.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 14;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;

                view.stars = new Image[Core.Stars.Max];
                for (int k = 0; k < view.stars.Length; k++)
                {
                    var star = new GameObject($"Star {k + 1}", typeof(RectTransform)) { layer = 5 };
                    var sr = (RectTransform)star.transform;
                    sr.SetParent(rt, false);
                    sr.sizeDelta = new Vector2(52, 52);
                    var img = star.AddComponent<Image>();
                    img.sprite = Get("empty");
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    view.stars[k] = img;
                }
                view.starEmpty = Get("empty");
                view.starFilled = Get("star");

                var issues = new System.Collections.Generic.List<string>();
                view.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] Result stars added: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
