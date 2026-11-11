using System;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// One planet body in the world (map, surface path, journey transitions; docs/stage-select-transitions.md). Display only.
    /// Read by scene-flow: <see cref="Radius"/> (world units, for the dive zoom) and <see cref="SeatLocal"/> (the summit point
    /// a board stands on, local space; <c>transform.TransformPoint(SeatLocal)</c> is the world point).
    /// Built by <see cref="Create"/>; the mesh is shared, never destroy it.
    /// </summary>
    public sealed class PlanetBody : MonoBehaviour
    {
        /// Chapter key (chapters.json id).
        public string Key { get; private set; } = "";
        /// Look index of the run-time stone mesh (PlanetStoneMesh.Looks).
        public int Look { get; private set; }
        /// Summit cut flat for a board.
        public bool Seated { get; private set; }
        /// Body radius in world units (mesh radius x the largest lossy scale).
        public float Radius => radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        /// Where a board stands, local space (on the flat summit).
        public Vector3 SeatLocal => new Vector3(0, seatHeight, 0);
        /// Board scale on the summit (local units per cell).
        public float BoardScale { get; private set; }
        /// An authored mesh is used (not the run-time stone mesh).
        public bool Authored { get; private set; }

        public MeshRenderer Renderer { get; private set; }

        float radius, seatHeight;
        MaterialPropertyBlock block;

        /// <param name="index">The planet's position in chapters.json order (look for keys outside PlanetStoneMesh.Looks).</param>
        /// <param name="seated">Summit cut flat for a board (surface path, dive, advance); false = round (map).</param>
        public static PlanetBody Create(PlanetBodySet set, string key, int index, Transform parent, bool seated)
        {
            if (set == null) throw new ArgumentNullException(nameof(set), "GameAssets.planetBodies is not set (Data/PlanetBodies.asset)");
            if (key == null) throw new ArgumentNullException(nameof(key));
            var entry = set.Find(key);
            var material = entry != null && entry.material != null ? entry.material : set.material;
            if (material == null) throw new MissingReferenceException($"[SevenSteps] {set.name}: no material for planet {key}");
            int look = PlanetStoneMesh.LookOf(key, index);
            Mesh mesh = entry == null ? null : seated ? entry.seatedMesh : entry.mesh;
            bool authored = mesh != null;
            if (mesh == null) mesh = PlanetStoneMesh.Get(look, seated);

            var go = new GameObject($"Planet {key}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var body = go.AddComponent<PlanetBody>();
            body.Key = key;
            body.Look = look;
            body.Seated = seated;
            body.Authored = authored;
            body.Renderer = r;
            body.radius = entry != null && entry.radius > 0 ? entry.radius : PlanetStoneMesh.Radius;
            body.seatHeight = entry != null ? entry.seatHeight : PlanetStoneMesh.SeatTop;
            body.BoardScale = entry != null && entry.boardScale > 0 ? entry.boardScale : 0.22f;
            return body;
        }

        /// The crust point (local space) in a local direction: exact for the run-time mesh; for an authored mesh the
        /// direction at the entry's radius with the v4 squash (good enough for marks lifted a little off the ground).
        public Vector3 Crust(Vector3 localDirection)
        {
            if (localDirection.sqrMagnitude < 1e-8f) throw new ArgumentException("zero direction", nameof(localDirection));
            if (!Authored) return PlanetStoneMesh.SurfacePoint(Look, localDirection, Seated);
            var q = Vector3.Scale(localDirection.normalized * radius, new Vector3(1, PlanetStoneMesh.Squash, 1));
            if (Seated) q.y = Mathf.Min(q.y, seatHeight - 0.05f);
            return q;
        }

        /// Dims or restores the body (unvisited planets on the map, research point 2): a tint over the vertex colour.
        public void SetTint(Color tint)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            Renderer.SetPropertyBlock(block);
        }
    }
}
