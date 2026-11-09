using System;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Makes the UI assets of docs/scene-flow.md S6 when they are missing: Data/UiTheme_Default.asset (v0 colors, the font
    /// from GameAssets) linked from GameAssets.uiTheme, and the panel prefabs UI/Prefabs/SettingsPanel.prefab and
    /// ResultPanel.prefab (the v0 code layout) linked from the theme. Existing assets are left alone: after the first build
    /// the prefabs are edited by hand (or replaced by the art theme); delete one to rebuild it.
    /// Menu: Seven Steps > Scene Flow > Build UI. Batch: -executeMethod SevenSteps.EditorTools.UiBuilder.BatchBuild
    /// </summary>
    public static class UiBuilder
    {
        public const string ThemePath = "Assets/_Project/Data/UiTheme_Default.asset";
        public const string PrefabDir = "Assets/_Project/UI/Prefabs";
        public const string SettingsPrefabPath = PrefabDir + "/SettingsPanel.prefab";
        public const string ResultPrefabPath = PrefabDir + "/ResultPanel.prefab";
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

            Ui.Theme = theme; // the layout code below reads font and colors from it
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/_Project/UI", "Prefabs");
            if (theme.settingsPanel == null)
            {
                theme.settingsPanel = LoadOrBuild(SettingsPrefabPath, BuildSettings).GetComponent<SettingsPanel>();
                EditorUtility.SetDirty(theme);
            }
            if (theme.resultPanel == null)
            {
                theme.resultPanel = LoadOrBuild(ResultPrefabPath, BuildResult).GetComponent<ResultView>();
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SevenSteps] UI built: theme {AssetDatabase.GetAssetPath(assets.uiTheme)}, " +
                      $"{AssetDatabase.GetAssetPath(theme.settingsPanel)}, {AssetDatabase.GetAssetPath(theme.resultPanel)}");
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

        static GameObject LoadOrBuild(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = build();
            try { return PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { Object.DestroyImmediate(go); }
        }

        static void Nothing() { }

        // ---------- v0 layouts (moved from SettingsPanel / ResultView, S6) ----------

        /// Root = the dim overlay (stretched under the scene's overlay canvas); the panel is a child.
        static GameObject BuildSettings()
        {
            var holder = new GameObject("holder", typeof(RectTransform));
            var dim = Ui.MakeImage(holder.transform, new Color(0, 0, 0, 0.6f), null, "SettingsPanel");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            dim.transform.SetParent(null, false);
            Object.DestroyImmediate(holder);
            var view = dim.gameObject.AddComponent<SettingsPanel>();

            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 380));
            var title = Ui.Label(panel.transform, "설정", 34, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(480, 44));

            view.masterText = VolumeRow(panel.transform, "전체 음량", -90, out view.masterDown, out view.masterUp);
            view.sfxText = VolumeRow(panel.transform, "효과음", -150, out view.sfxDown, out view.sfxUp);

            var tLabel = Ui.Label(panel.transform, "플레이 기록 보내기", 20, Ui.Ink, TextAnchor.MiddleLeft, "TelemetryLabel");
            tLabel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, -210), new Vector2(240, 40));
            view.telemetryToggle = Ui.MakeButton(panel.transform, "", 18, Nothing, out view.telemetryText, "TelemetryToggle");
            ((RectTransform)view.telemetryToggle.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, -210), new Vector2(140, 40));
            view.telemetryNote = Ui.Label(panel.transform, "익명 플레이 기록(이동·클리어)만 보냅니다. 이름이나 계정은 보내지 않습니다.", 14, Ui.Dim, TextAnchor.MiddleCenter, "TelemetryNote");
            view.telemetryNote.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -256), new Vector2(480, 40));

            view.close = Ui.MakeButton(panel.transform, "닫기 (Esc)", 20, Nothing, out _, "Close");
            ((RectTransform)view.close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(200, 44));
            ClearRuntimeListeners(dim.gameObject);
            dim.gameObject.SetActive(false); // closed until the OverlayStack shows it
            return dim.gameObject;
        }

        static Text VolumeRow(Transform parent, string label, float y, out Button down, out Button up)
        {
            var l = Ui.Label(parent, label, 20, Ui.Ink, TextAnchor.MiddleLeft, label);
            l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, y), new Vector2(240, 40));
            down = Ui.MakeButton(parent, "−", 22, Nothing, out _, label + " -");
            ((RectTransform)down.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, y), new Vector2(44, 40));
            var value = Ui.Label(parent, "", 20, Ui.Gold, TextAnchor.MiddleCenter, label + " value");
            value.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(140, y), new Vector2(80, 40));
            up = Ui.MakeButton(parent, "+", 22, Nothing, out _, label + " +");
            ((RectTransform)up.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(210, y), new Vector2(44, 40));
            return value;
        }

        /// Root = its own screen-space canvas (sorting 5: above the HUD, below the overlay canvas at 10).
        static GameObject BuildResult()
        {
            var canvasGo = new GameObject("ResultPanel", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<ResultView>();

            var dim = Ui.MakeImage(canvasGo.transform, new Color(0, 0, 0, 0.55f), null, "Overlay");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            view.overlay = dim.gameObject;
            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 330));
            view.title = Ui.Label(panel.transform, "클리어", 40, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            view.title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(420, 50));
            view.body = Ui.Label(panel.transform, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Body");
            view.body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(420, 50));
            var col = Ui.Rect("Buttons", panel.transform);
            col.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(300, 150));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.LowerCenter;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            view.next = ResultButton(col, "다음 스테이지 (Enter)");
            view.again = ResultButton(col, "다시 하기 (R)");
            view.select = ResultButton(col, "스테이지 선택 (Esc)");
            view.undo = ResultButton(col, "한 걸음 되돌리기 (Z)");
            view.restart = ResultButton(col, "다시 시작 (R)");
            ClearRuntimeListeners(canvasGo);
            dim.gameObject.SetActive(false); // hidden until Show
            return canvasGo;
        }

        static Button ResultButton(Transform parent, string label)
        {
            var b = Ui.MakeButton(parent, label, 18, Nothing, out _, label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(300, 40);
            return b;
        }

        /// Ui.MakeButton adds a runtime listener; prefabs carry none (the views add theirs in Awake).
        static void ClearRuntimeListeners(GameObject root)
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true)) b.onClick.RemoveAllListeners();
        }
    }
}
