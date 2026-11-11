using System;
using SevenSteps.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// A background camera in Scenes/Boot.unity (docs/scene-flow.md 8.2, architect N6). Boot draws nothing, and a scene
    /// without a camera shows the platform's default colour for the frame or two before Title. This camera clears to the
    /// WebGL loader page colour #101C32 (WebGLTemplates/SevenSteps/index.html) and renders no layers, so the
    /// loader page → Splash Screen → Boot → Title sequence keeps one background. Safe to re-run (finds the camera by name).
    /// Menu: Seven Steps > Scene Flow > Build Boot Camera. Batch: -executeMethod SevenSteps.EditorTools.BootSceneCamera.BatchBuild
    /// </summary>
    public static class BootSceneCamera
    {
        const string BootPath = SceneFlowValidator.SceneDir + "/Boot.unity";
        public const string CameraName = "Boot Background Camera";
        /// The WebGL loader page background (#101C32).
        public static readonly Color Background = new Color32(0x10, 0x1C, 0x32, 0xFF);

        [MenuItem("Seven Steps/Scene Flow/Build Boot Camera", priority = 54)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<BootScreen>() == null) throw new InvalidOperationException("Boot.unity has no BootScreen");
            var go = GameObject.Find(CameraName);
            if (go == null) go = new GameObject(CameraName); // Unity null: no ?? on UnityEngine.Object
            var cam = go.GetComponent<Camera>();
            if (cam == null) cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.cullingMask = 0; // nothing to draw: the clear colour only
            cam.orthographic = true;
            cam.depth = -100;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SevenSteps] Boot camera: clears to #101C32, renders nothing");
        }

        public static void BatchBuild()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
