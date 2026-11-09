using System;
using SevenSteps.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Game
{
    /// <summary>
    /// Chapter look around the board (docs/architecture.md 12.8): sky and planet instances, the three directional lights,
    /// ambient light and camera background, taken from a <see cref="ChapterPreset"/> when a stage is entered.
    /// A null chapter (no chapter assets yet) leaves the scene as it is. Nothing here runs per frame.
    /// The board's tile set and material overrides are <see cref="BoardView.UseChapter"/>'s; the Game scene owns the lights.
    /// </summary>
    public sealed class ChapterStage : IDisposable
    {
        readonly Camera cam;
        readonly Light key, fill, rim;
        readonly Transform parent;
        GameObject sky, planet;
        IStageEnvironment environment;
        bool entered;

        public ChapterPreset Current { get; private set; }

        /// <param name="key">Key light (the only one with shadows, WebGL Mobile budget).</param>
        /// <param name="parent">Where the planet instance goes (the Game scene root). The sky goes under the camera.</param>
        public ChapterStage(Camera cam, Light key, Light fill, Light rim, Transform parent)
        {
            this.cam = cam != null ? cam : throw new ArgumentNullException(nameof(cam));
            this.key = key != null ? key : throw new ArgumentNullException(nameof(key));
            this.fill = fill != null ? fill : throw new ArgumentNullException(nameof(fill));
            this.rim = rim != null ? rim : throw new ArgumentNullException(nameof(rim));
            this.parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
        }

        /// Called before a stage loads. Returns true when the chapter changed (the board then needs the chapter's tile set).
        public bool Enter(ChapterPreset chapter)
        {
            if (entered && chapter == Current) return false;
            entered = true;
            Current = chapter;
            DestroyInstances();
            if (chapter == null) return true;

            if (chapter.lighting != null) ApplyLighting(chapter.lighting);
            if (chapter.sky != null)
            {
                cam.backgroundColor = chapter.sky.background;
                // The sky follows the camera (12.8 contract): a camera child, local pose from the prefab.
                if (chapter.sky.skyPrefab != null) sky = Object.Instantiate(chapter.sky.skyPrefab, cam.transform, false);
                if (chapter.sky.planetPrefab != null)
                {
                    planet = Object.Instantiate(chapter.sky.planetPrefab, parent);
                    environment = planet.GetComponent<IStageEnvironment>(); // optional: TileSetValidator warns when missing
                }
            }
            return true;
        }

        /// After the board is built for a stage: fit the planet summit to W x H (same chapter or new one).
        public void Fit(BoardGrid grid)
        {
            environment?.Fit(grid);
        }

        public void Dispose() => DestroyInstances();

        void ApplyLighting(LightingPreset p)
        {
            Apply(key, p.key, true);
            Apply(fill, p.fill, false); // shadows only on the key light (12.8, WebGL Mobile budget)
            Apply(rim, p.rim, false);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = p.ambient;
        }

        static void Apply(Light l, LightRig r, bool shadowsAllowed)
        {
            l.enabled = r.enabled;
            l.color = r.color;
            l.intensity = r.intensity;
            l.transform.rotation = Quaternion.Euler(r.euler);
            l.shadows = shadowsAllowed && r.shadows ? LightShadows.Soft : LightShadows.None;
        }

        void DestroyInstances()
        {
            if (sky != null) Kill(sky);
            if (planet != null) Kill(planet);
            sky = planet = null;
            environment = null;
        }

        /// Destroy in play mode; outside it (edit-mode tests) Destroy is not allowed.
        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
