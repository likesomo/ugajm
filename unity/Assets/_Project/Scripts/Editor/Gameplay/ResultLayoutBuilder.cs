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
    /// Lays out UI/Prefabs/ResultPanel.prefab. Loss: the modal panel (title, body line, one row of buttons). Clear: the
    /// minimal result over the board (docs 14.7 "보드 위 결과", 9eda0b0; design/art/ui-minimal.md): Overlay/Clear holds
    /// the three stars on a plate in the band above the board (BoardFrame top 80), a tip plate beside them for what the
    /// hovered / tapped star asks (result.star1..3), the Next / [끝까지 보기] slot bottom right and the planet / journey
    /// line plate bottom centre (ResultView docks body there for a clear).
    /// Positions and sizes only: words come from the string table at runtime, fonts from UiTheme.FontFor, the look of
    /// panel and buttons from the theme variant (RuinsHengeUiBuilder). Re-running rebuilds the star and label rows.
    /// After this the prefab is the source. Owner: gameplay/UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.ResultLayoutBuilder.BatchBuild
    /// </summary>
    public static class ResultLayoutBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/ResultPanel.prefab";
        const string AtlasPath = "Assets/Shared/UI/RuinsHenge/RH_UI_Atlas.png";

        static readonly Vector2 PanelSize = new Vector2(860, 420);
        static readonly Vector2 ButtonSize = new Vector2(252, 64);
        // Clear over the board (1280 x 720 units, safe edge 24, board frame from y 80).
        const float Safe = 24, StarSize = 44, StarGap = 34, StarsPad = 22, BandH = 54;
        static readonly Vector2 NextSize = new Vector2(252, 64); // the loss buttons' size (labels 8 inside the frame)
        const float FinaleH = 72;
        /// Modal scrim over the HUD (docs/architecture.md 14.7: alpha 0.6 or more over the whole screen).
        const float ScrimAlpha = 0.72f;

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

            var assets = AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = assets != null && assets.uiTheme != null ? assets.uiTheme
                : throw new InvalidOperationException("Assets/_Project/Data/GameAssets.asset: uiTheme is not set");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<ResultView>() ?? throw new InvalidOperationException($"{PrefabPath}: no ResultView");
                var panel = root.transform.Find("Overlay/Panel") as RectTransform
                    ?? throw new InvalidOperationException($"{PrefabPath}: no Overlay/Panel");
                panel.sizeDelta = PanelSize;
                var scrim = root.transform.Find("Overlay")?.GetComponent<Image>() ?? throw new InvalidOperationException($"{PrefabPath}: no Overlay image");
                scrim.color = new Color(0.02f, 0.04f, 0.08f, ScrimAlpha);
                scrim.raycastTarget = true;
                // A clear lays it at 0.25 over the board (ResultView.ClearScrimAlpha, docs 14.7); a loss at 0.72 (the modal rule
                // cannot be skipped, so the loss is checked as before).
                var mark = scrim.GetComponent<UiLayout>();
                if (mark != null) Object.DestroyImmediate(mark, true);
                Ui.Exempt(scrim.gameObject, UiLayoutRule.Decoration | UiLayoutRule.Board, "clear result scrim 0.25 over the board (14.7)");

                view.scrim = scrim;
                view.panel = panel;
                Top(view.title.rectTransform, -68, new Vector2(660, 64)); // glyphs 8+ inside the panel frame (border 32)
                view.title.fontSize = 46;
                view.title.alignment = TextAnchor.MiddleCenter;
                view.title.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.title.resizeTextForBestFit = true; // long planet-finale titles shrink instead of overflowing
                view.title.resizeTextMinSize = 28;
                view.title.resizeTextMaxSize = 46;

                // The old in-panel rows (stars, labels) are replaced by the clear group.
                foreach (var old in new[] { "Stars", "StarLabels" }) { var o = panel.Find(old); if (o != null) Object.DestroyImmediate(o.gameObject); }

                var overlay = (RectTransform)scrim.transform;
                var oldClear = overlay.Find("Clear");
                // Next lives in the clear group: take it out before the old group goes.
                ((RectTransform)view.next.transform).SetParent(panel, false);
                if (oldClear != null) Object.DestroyImmediate(oldClear.gameObject);
                var clear = Child("Clear", overlay);
                clear.anchorMin = Vector2.zero; clear.anchorMax = Vector2.one; clear.offsetMin = clear.offsetMax = Vector2.zero;
                view.clearGroup = clear;

                // Stars on a plate in the band above the board.
                float plateW = 3 * StarSize + 2 * StarGap + 2 * StarsPad;
                var starsPlate = Plate("StarsPlate", clear, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -Safe), new Vector2(plateW, BandH));
                var stars = starsPlate.gameObject.AddComponent<HorizontalLayoutGroup>();
                stars.spacing = StarGap;
                stars.childAlignment = TextAnchor.MiddleCenter;
                stars.childControlWidth = stars.childControlHeight = false;
                stars.childForceExpandWidth = stars.childForceExpandHeight = false;
                view.stars = new Image[Core.Stars.Max];
                for (int k = 0; k < view.stars.Length; k++)
                {
                    var star = Child($"Star {k + 1}", starsPlate);
                    star.sizeDelta = new Vector2(StarSize, StarSize);
                    var img = star.gameObject.AddComponent<Image>();
                    img.sprite = Get("empty");
                    img.preserveAspect = true;
                    img.raycastTarget = true; // hover / tap shows what it asks
                    view.stars[k] = img;
                }
                view.starEmpty = Get("empty");
                view.starFilled = Get("star");

                // What a star asks: one tip plate per star, all in the band right of the stars (clear of the board).
                view.starLabels = new Text[Core.Stars.Max];
                for (int k = 0; k < view.starLabels.Length; k++)
                {
                    var tip = Plate($"Tip {k + 1}", clear, new Vector2(.5f, 1), new Vector2(0, 1), new Vector2(plateW / 2 + 12, -Safe), new Vector2(200, BandH));
                    var row = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
                    row.padding = new RectOffset(16, 16, 8, 8);
                    row.childAlignment = TextAnchor.MiddleLeft;
                    row.childControlWidth = row.childControlHeight = true;
                    row.childForceExpandWidth = row.childForceExpandHeight = false;
                    tip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var cell = Child("Label", tip);
                    var t = cell.gameObject.AddComponent<Text>();
                    t.font = view.body.font;
                    t.fontSize = 22;
                    t.alignment = TextAnchor.MiddleLeft;
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                    t.color = view.body.color;
                    t.raycastTarget = false;
                    view.starLabels[k] = t;
                    tip.gameObject.SetActive(false);
                }

                // Next / [끝까지 보기], bottom right.
                var slot = Child("NextSlot", clear);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(1, 0);
                slot.anchoredPosition = new Vector2(-Safe - 8, Safe + 12);
                slot.sizeDelta = NextSize;
                view.nextSlot = slot;
                Fill((RectTransform)view.next.transform, slot);
                var oldEnd = slot.Find("ToEnding") ?? overlay.Find("ToEnding");
                if (oldEnd != null) Object.DestroyImmediate(oldEnd.gameObject);

                // The planet / journey line, bottom centre (body docks here for a clear). Stretched between equal insets
                // that clear the next slot (its width + 16 gap), so it stays centred and off the button at every aspect
                // (680 wide at 1280, narrower at 4:3, docs 14 1024x768).
                var finale = Plate("FinalePlate", clear, new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, Vector2.zero);
                float inset = Safe + 8 + NextSize.x + 16;
                finale.anchorMin = new Vector2(0, 0); finale.anchorMax = new Vector2(1, 0);
                finale.offsetMin = new Vector2(inset, Safe + 12);
                finale.offsetMax = new Vector2(-inset, Safe + 12 + FinaleH);
                view.finalePlate = finale;
                finale.gameObject.SetActive(false);
                clear.gameObject.SetActive(false);

                // Loss body line (two lines at most)
                Top(view.body.rectTransform, -170, new Vector2(PanelSize.x - 120, 90));
                view.body.fontSize = 22;
                view.body.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.body.verticalOverflow = VerticalWrapMode.Overflow;
                view.body.alignment = TextAnchor.MiddleCenter;

                // Buttons: one row at the bottom (three show at a time: next / again / select, or undo / restart / select)
                var buttons = panel.Find("Buttons") as RectTransform ?? throw new InvalidOperationException($"{PrefabPath}: no Overlay/Panel/Buttons");
                var vertical = buttons.GetComponent<VerticalLayoutGroup>();
                if (vertical != null) Object.DestroyImmediate(vertical);
                var h = buttons.GetComponent<HorizontalLayoutGroup>() ?? buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 16;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
                buttons.anchorMin = buttons.anchorMax = new Vector2(.5f, 0);
                buttons.pivot = new Vector2(.5f, .5f);
                buttons.anchoredPosition = new Vector2(0, 76);
                buttons.sizeDelta = new Vector2(PanelSize.x - 40, ButtonSize.y);
                foreach (var b in new[] { view.next, view.again, view.select, view.undo, view.restart })
                {
                    if (b != view.next) ((RectTransform)b.transform).sizeDelta = ButtonSize;
                    var label = b.GetComponentInChildren<Text>(true);
                    if (label != null) { label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; label.fontSize = 22; label.resizeTextForBestFit = false; }
                    Ui.FitButtonSkin(b); // frame at 1/2.5, label inside the lines
                }

                var issues = new System.Collections.Generic.List<string>();
                view.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] Result panel laid out: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static RectTransform Plate(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Child(name, parent);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = r.gameObject.AddComponent<Image>();
            var c = Ui.PanelBg;
            img.color = new Color(c.r, c.g, c.b, Ui.PlateAlpha);
            img.raycastTarget = false;
            return r;
        }

        static void Fill(RectTransform r, RectTransform slot)
        {
            r.SetParent(slot, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = r.offsetMax = Vector2.zero;
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
