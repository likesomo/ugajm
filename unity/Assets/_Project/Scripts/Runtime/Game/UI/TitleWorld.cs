using System;
using SevenSteps.Contracts;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The world behind the U5 title (concept U5-title.png, U1 grammar; UiStyleToggle on): the first planet's body, its
    /// summit cut flat, with the three brothers standing on it, and its own key light. View only. It takes the Title
    /// camera while shown (orthographic, looking down at 27 degrees) and stands far from the stage select's planet space,
    /// which keeps and gives back the camera pose on its own (PlanetSpace.Enter / Exit).
    /// </summary>
    public sealed class TitleWorld : IDisposable
    {
        /// Far from the map bodies (PlanetSpace, near the origin) so neither shows in the other's frame.
        public static readonly Vector3 Origin = new Vector3(-500f, 0f, 0f);
        public const float CameraSize = 6.5f, CameraPitch = 27f, CameraDistance = 30f, PlanetScale = 0.85f, PlanetDrop = -1.8f;
        public const int Brothers = 3;

        readonly Camera cam;
        readonly GameObject root;

        public PlanetBody Planet { get; }
        public bool Visible => root != null && root.activeSelf;

        /// Where the brothers stand on a V7 sprite planet: this share of its radius above the centre (the sprites' flat top).
        public const float SpriteTop = 0.5f;
        /// Stars behind the planet (seeded, the same sky every time), as the star map's backdrop.
        public const int StarCount = 150;
        const float BackdropDistance = 90f;

        /// <param name="brothers">Tile set whose player visuals stand on the planet (the first chapter's); null = no brothers.</param>
        /// <param name="sprites">Map planet sprites (V7): the title planet uses its sprite as the map does; null = the body mesh.</param>
        public TitleWorld(Camera cam, PlanetBodySet bodies, string planetKey, TileSet brothers, IPlanetMapSprites sprites = null)
        {
            this.cam = cam != null ? cam : throw new ArgumentNullException(nameof(cam), "the Title scene has no Main Camera");
            if (bodies == null) throw new ArgumentNullException(nameof(bodies), "GameAssets.planetBodies is not set (Data/PlanetBodies.asset)");
            if (string.IsNullOrEmpty(planetKey)) throw new ArgumentException("no planet key", nameof(planetKey));
            root = new GameObject("TitleWorld");
            root.transform.position = Origin;
            Planet = PlanetBody.Create(bodies, planetKey, 0, root.transform, true);
            Planet.transform.localPosition = new Vector3(0f, PlanetDrop, 0f);
            Planet.transform.localScale = Vector3.one * PlanetScale;
            Sprite art = null;
            bool sprite = sprites != null && sprites.TryGetSprite(planetKey, out art);
            if (sprite) Planet.UseSprite(art, cam);
            // On the sprite the brothers stand on its flat top, a little toward the camera so they draw in front of it.
            var toCamera = -(Quaternion.Euler(CameraPitch, 0, 0) * Vector3.forward);
            Vector3 stand = sprite
                ? new Vector3(0f, PlanetDrop + Planet.Radius * SpriteTop, 0f) + toCamera * (Planet.Radius * 0.6f)
                : new Vector3(0f, PlanetDrop + Planet.SeatLocal.y * PlanetScale, -0.15f);
            if (brothers != null)
                for (int i = 0; i < Brothers; i++)
                {
                    var visual = brothers.PlayerFor(i);
                    if (visual.prefab == null) continue;
                    var b = Object.Instantiate(visual.prefab, root.transform);
                    b.name = "Brother " + (i + 1);
                    b.transform.localPosition = stand + new Vector3((i - 1) * 0.72f, 0f, 0f);
                    b.transform.localScale = visual.Scale * 0.9f;
                    b.transform.localRotation = visual.RotationFor(i);
                    foreach (var behaviour in b.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false; // scenery: no board logic
                }
            var lightGo = new GameObject("Title Key Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            Stars(root.transform, cam);
            root.SetActive(false);
        }

        /// Shows the planet and frames it with the camera.
        public void Show()
        {
            root.SetActive(true);
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.transform.rotation = Quaternion.Euler(CameraPitch, 0, 0);
            cam.transform.position = Origin + new Vector3(0, 0.2f, 0) - cam.transform.forward * CameraDistance;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = CameraDistance * 2f + 50f;
        }

        /// A camera-space canvas far behind the planet with small seeded stars (a code backdrop, not the chapter sky files).
        static void Stars(Transform parent, Camera cam)
        {
            var go = new GameObject("TitleStars", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = BackdropDistance;
            canvas.sortingOrder = -100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var rng = new System.Random(11);
            for (int i = 0; i < StarCount; i++)
            {
                float size = 1.5f + (float)rng.NextDouble() * (i % 9 == 0 ? 2.5f : 1.2f);
                var star = Ui.MakeImage(go.transform, new Color(1f, 0.97f, 0.9f, 0.25f + (float)rng.NextDouble() * 0.6f), Ui.Circle, "Star");
                star.raycastTarget = false;
                var r = star.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = Vector2.zero;
            }
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
