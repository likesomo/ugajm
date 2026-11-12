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

        /// Smoke check camera clear (LaunchOptions.SmokeView): a colour no palette uses, measured against by board-pixels.ps1.
        public static readonly Color SmokeClear = new Color(1f, 0f, 1f, 1f);
        readonly SmokeView smoke;

        /// <param name="key">Key light (the only one with shadows, WebGL Mobile budget).</param>
        /// <param name="parent">Where the planet instance goes (the Game scene root). The sky goes under the camera.</param>
        /// <param name="smoke">Board / Empty: no sky or planet (so no Fit) and a #FF00FF clear; lights as the chapter says.</param>
        public ChapterStage(Camera cam, Light key, Light fill, Light rim, Transform parent, SmokeView smoke = SmokeView.None)
        {
            this.smoke = smoke;
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
            if (smoke != SmokeView.None) cam.backgroundColor = SmokeClear;
            if (chapter == null) return true;

            if (chapter.lighting != null) ApplyLighting(chapter.lighting);
            if (chapter.sky != null && smoke == SmokeView.None)
            {
                cam.backgroundColor = chapter.sky.background;
                // The sky follows the camera (12.8 contract): a camera child, local pose from the prefab.
                if (chapter.sky.skyPrefab != null)
                {
                    sky = Object.Instantiate(chapter.sky.skyPrefab, cam.transform, false);
                    ChapterMaterials.Apply(sky, chapter);
                }
                if (chapter.sky.planetPrefab != null)
                {
                    planet = Object.Instantiate(chapter.sky.planetPrefab, parent);
                    ChapterMaterials.Apply(planet, chapter);
                    environment = planet.GetComponent<IStageEnvironment>(); // optional: TileSetValidator warns when missing
                }
            }
            return true;
        }

        /// After the board is built for a stage: fit the planet summit to W x H (same chapter or new one).
        public void Fit(BoardGrid grid)
        {
            if (environment == null) return;
            environment.Fit(grid);
            ChapterMaterials.Apply(planet, Current); // Fit rebuilds the summit meshes: same overrides on the new renderers
        }

        /// Cells around the board within which planet renderers count as its ground (the summit and its crust, not moons).
        public const float GroundMarginCells = 4f;

        /// A still copy of the ground the board stands on now, for the journey advance (the cleared board keeps its island
        /// while the planet refits to the next board): the planet's mesh renderers whose bounds come within
        /// <see cref="GroundMarginCells"/> of <paramref name="grid"/>, with their own copies of the meshes (the refit disposes
        /// the planet's) and no scripts. Null when there is no planet (no chapter, smoke runs). The copy owns its meshes and
        /// frees them when destroyed.
        public GameObject CopyGround(BoardGrid grid)
        {
            if (planet == null) return null;
            var copy = new GameObject("Ground (snapshot)");
            var owned = copy.AddComponent<OwnedMeshes>();
            foreach (var r in GroundRenderers(grid))
            {
                var f = r.GetComponent<MeshFilter>();
                var go = new GameObject(r.name);
                go.transform.SetParent(copy.transform, false);
                go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                go.transform.localScale = r.transform.lossyScale;
                var mesh = Object.Instantiate(f.sharedMesh);
                owned.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var cr = go.AddComponent<MeshRenderer>();
                cr.sharedMaterials = r.sharedMaterials;
                cr.shadowCastingMode = r.shadowCastingMode;
                cr.receiveShadows = r.receiveShadows;
                if (r.HasPropertyBlock())
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    cr.SetPropertyBlock(block);
                }
            }
            return copy;
        }

        /// World bounds of the ground under <paramref name="grid"/> (same renderers as <see cref="CopyGround"/>); null when
        /// there is no planet.
        public Bounds? GroundBounds(BoardGrid grid)
        {
            Bounds? b = null;
            foreach (var r in GroundRenderers(grid))
            {
                if (b is Bounds have) { have.Encapsulate(r.bounds); b = have; }
                else b = r.bounds;
            }
            return b;
        }

        System.Collections.Generic.List<MeshRenderer> GroundRenderers(BoardGrid grid)
        {
            if (grid.W <= 0 || grid.H <= 0 || grid.CellSize <= 0) throw new ArgumentOutOfRangeException(nameof(grid));
            var list = new System.Collections.Generic.List<MeshRenderer>();
            if (planet == null) return list;
            var near = grid.Bounds;
            near.Expand(new Vector3(2f, 0f, 2f) * GroundMarginCells * grid.CellSize);
            near.Expand(new Vector3(0f, 2000f, 0f)); // any height: the crust hangs far below
            foreach (var r in planet.GetComponentsInChildren<MeshRenderer>())
            {
                var f = r.GetComponent<MeshFilter>();
                if (r.enabled && f != null && f.sharedMesh != null && near.Intersects(r.bounds)) list.Add(r);
            }
            return list;
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
