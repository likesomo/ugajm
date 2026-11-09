using System;
using SevenSteps.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// The three directional lights of docs/architecture.md 12.8 in Scenes/Game.unity: the existing directional light
    /// becomes the key (shadows), and a fill and a rim light without shadows are added. GameController.keyLight /
    /// fillLight / rimLight point at them; ChapterStage sets their color, intensity and angle per chapter. Values here are
    /// only the no-chapter defaults. Safe to re-run (finds lights by name).
    /// Menu: Seven Steps > Scene Flow > Build Game Lights. Batch: -executeMethod SevenSteps.EditorTools.GameSceneLights.BatchBuild
    /// </summary>
    public static class GameSceneLights
    {
        const string GamePath = SceneFlowValidator.SceneDir + "/Game.unity";

        [MenuItem("Seven Steps/Scene Flow/Build Game Lights (key, fill, rim)", priority = 53)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<GameController>() ?? throw new InvalidOperationException("Game.unity has no GameController");

            var key = Find("Key Light") ?? FindAnyDirectional() ?? throw new InvalidOperationException("Game.unity has no directional light");
            key.gameObject.name = "Key Light";
            key.shadows = LightShadows.Soft;

            var fill = Find("Fill Light") ?? Make("Fill Light", new Color(0.62f, 0.7f, 0.86f), 0.35f, Quaternion.Euler(35, 150, 0));
            var rim = Find("Rim Light") ?? Make("Rim Light", new Color(1f, 0.86f, 0.66f), 0.5f, Quaternion.Euler(20, -150, 0));

            root.keyLight = key;
            root.fillLight = fill;
            root.rimLight = rim;
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SevenSteps] Game lights: key, fill, rim set on GameController");
        }

        public static void BatchBuild()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static Light Find(string name)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.name == name) return l;
            return null;
        }

        static Light FindAnyDirectional()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        static Light Make(string name, Color color, float intensity, Quaternion rotation)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None; // only the key casts shadows (WebGL Mobile budget)
            l.transform.rotation = rotation;
            return l;
        }
    }
}
