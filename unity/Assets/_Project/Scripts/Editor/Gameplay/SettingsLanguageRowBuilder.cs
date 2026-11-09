using System;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Adds the language row to UI/Prefabs/SettingsPanel.prefab (scene-flow S7c: the button opens the language picker):
    /// row label (settings.language), a button worded settings.language_change, and the current language's own name
    /// small beside it. The pieces are copies of the telemetry row (same fonts, sprites, colours), so the art variant
    /// skins them like the rest. The panel grows to 520 x 480; the consent note moves under the new row and the
    /// telemetry status line (added at runtime under the note) clears the close button. Re-running replaces the row.
    /// After this the prefab is the source. Owner: gameplay/UI.
    /// Batch: -executeMethod SevenSteps.EditorTools.SettingsLanguageRowBuilder.BatchBuild
    /// </summary>
    public static class SettingsLanguageRowBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/SettingsPanel.prefab";
        const float RowY = -270;

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Seven Steps/Gameplay/Add Settings Language Row", priority = 34)]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<SettingsPanel>() ?? throw new InvalidOperationException($"{PrefabPath}: no SettingsPanel");
                var panel = (RectTransform)view.telemetryLabel.transform.parent;
                foreach (var name in new[] { "LanguageLabel", "LanguageToggle", "LanguageText" })
                {
                    var old = panel.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 480);

                T Copy<T>(Component source, string name, Vector2 pos, Vector2 size) where T : Component
                {
                    var go = Object.Instantiate(source.gameObject, panel, false);
                    go.name = name;
                    var r = (RectTransform)go.transform;
                    r.anchoredPosition = pos;
                    r.sizeDelta = size;
                    return go.GetComponent<T>();
                }

                view.languageLabel = Copy<Text>(view.telemetryLabel, "LanguageLabel", new Vector2(-110, RowY), new Vector2(240, 40));
                view.languageToggle = Copy<Button>(view.telemetryToggle, "LanguageToggle", new Vector2(80, RowY), new Vector2(160, 40));
                view.languageText = Copy<Text>(view.telemetryNote, "LanguageText", new Vector2(215, RowY), new Vector2(90, 40));
                view.languageText.fontSize = Mathf.Max(12, view.telemetryNote.fontSize - 2);
                view.languageText.alignment = TextAnchor.MiddleLeft;
                var label = view.languageToggle.GetComponentInChildren<Text>(true);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = label.fontSize;

                var note = view.telemetryNote.rectTransform;
                note.anchoredPosition = new Vector2(note.anchoredPosition.x, RowY - 50);

                var issues = new System.Collections.Generic.List<string>();
                view.Validate(issues);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SevenSteps] Settings language row added: {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
