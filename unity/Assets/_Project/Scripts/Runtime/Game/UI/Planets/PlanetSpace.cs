using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The 3D space of the v4 stage select in the Title scene (view only): a world root for planet bodies, one directional
    /// light, and a quiet starfield drawn behind the bodies (a camera-space canvas far behind them, so the bodies are
    /// not covered by the screen-space UI). Takes the Title camera while entered and gives back its pose on exit.
    /// </summary>
    public sealed class PlanetSpace
    {
        /// Camera-space backdrop distance: behind every body (map bodies sit 30 units away, radius under 4).
        const float BackdropDistance = 90f;

        public Camera Camera { get; }
        /// Parent of the bodies (inactive while not entered).
        public Transform World { get; }
        public bool Entered { get; private set; }

        readonly GameObject backdrop;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedSize;
        bool savedOrtho;
        CameraClearFlags savedClear;
        Color savedColor;

        public PlanetSpace(Camera camera, Color background)
        {
            Camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera), "the Title scene has no Main Camera");
            var world = new GameObject("StageSelect3D");
            World = world.transform;
            var lightGo = new GameObject("Key Light");
            lightGo.transform.SetParent(World, false);
            lightGo.transform.rotation = Quaternion.Euler(42, -32, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            world.SetActive(false);

            backdrop = new GameObject("StageSelectBackdrop", typeof(RectTransform));
            backdrop.layer = 5;
            var canvas = backdrop.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = BackdropDistance;
            canvas.sortingOrder = -100;
            var scaler = backdrop.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var solid = Ui.MakeImage(backdrop.transform, background, null, "Solid");
            solid.rectTransform.Stretch();
            solid.raycastTarget = false;
            Stars(backdrop.transform);
            backdrop.SetActive(false);
        }

        /// A fixed scatter of small stars (seeded: the same sky every time) over the backdrop. The approved frames use the
        /// chapter sky; a sky asset from art can replace this backdrop later.
        static void Stars(Transform parent)
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 150; i++)
            {
                float size = 1.5f + (float)rng.NextDouble() * (i % 9 == 0 ? 2.5f : 1.2f);
                var star = Ui.MakeImage(parent, new Color(1f, 0.97f, 0.9f, 0.25f + (float)rng.NextDouble() * 0.6f), Ui.Circle, "Star");
                star.raycastTarget = false;
                var r = star.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = Vector2.zero;
            }
        }

        /// Shows the space and takes the camera (its pose is kept for <see cref="Exit"/>).
        public void Enter()
        {
            if (Entered) return;
            savedPos = Camera.transform.position;
            savedRot = Camera.transform.rotation;
            savedSize = Camera.orthographicSize;
            savedOrtho = Camera.orthographic;
            savedClear = Camera.clearFlags;
            savedColor = Camera.backgroundColor;
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            World.gameObject.SetActive(true);
            backdrop.SetActive(true);
            Entered = true;
        }

        /// Hides the space and gives the camera its pose back.
        public void Exit()
        {
            if (!Entered) return;
            World.gameObject.SetActive(false);
            backdrop.SetActive(false);
            Camera.transform.SetPositionAndRotation(savedPos, savedRot);
            Camera.orthographicSize = savedSize;
            Camera.orthographic = savedOrtho;
            Camera.clearFlags = savedClear;
            Camera.backgroundColor = savedColor;
            Entered = false;
        }

        /// Puts the camera at the stage-select framing: orthographic, pitch 50, yaw 0, looking at <paramref name="target"/>.
        public void Pose(Vector3 target, float orthoSize)
        {
            if (!Entered) throw new InvalidOperationException("PlanetSpace.Pose before Enter");
            Camera.transform.SetPositionAndRotation(PlanetMapLayout.CameraPosition(target), PlanetMapLayout.Rotation);
            Camera.orthographicSize = orthoSize;
        }

        /// A world point in canvas-local units of <paramref name="rect"/> (a screen-space overlay canvas child).
        public Vector2 ToCanvas(RectTransform rect, Vector3 world)
        {
            Vector2 screen = Camera.WorldToScreenPoint(world);
            var canvas = rect.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, uiCamera, out var local);
            return local;
        }

        /// A body's centre and radius in canvas-local units of <paramref name="rect"/>.
        public (Vector2 center, float radius) ToCanvas(RectTransform rect, PlanetBody body)
        {
            var c = ToCanvas(rect, body.transform.position);
            var edge = ToCanvas(rect, body.transform.position + Camera.transform.up * body.Radius);
            return (c, Vector2.Distance(c, edge));
        }
    }
}
