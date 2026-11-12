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
            if (Impostor != null) Impostor.color = tint;
        }

        /// The flat sprite drawn instead of the mesh (map V7 cutouts, <see cref="UseSprite"/>); null when the mesh draws.
        public SpriteRenderer Impostor { get; private set; }

        /// Fraction of the sprite square the V7 planet fills edge to edge (512 px cutouts, a few px of margin).
        public const float SpriteFill = 0.94f;

        /// Draws the body as <paramref name="sprite"/> facing <paramref name="facing"/> (the map's V7 cutouts) instead of its
        /// mesh: the same position, scale and <see cref="Radius"/>, so the map layout, its targets and the journey dive are
        /// unchanged; the planet's width matches the body's diameter. Once per body.
        public void UseSprite(Sprite sprite, Camera facing)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            if (facing == null) throw new ArgumentNullException(nameof(facing));
            if (Impostor != null) throw new InvalidOperationException($"PlanetBody {Key}: already drawn with a sprite");
            var go = new GameObject("Sprite " + Key);
            go.transform.SetParent(transform, false);
            Impostor = go.AddComponent<SpriteRenderer>();
            Impostor.sprite = sprite;
            Impostor.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Impostor.receiveShadows = false;
            float width = sprite.bounds.size.x * SpriteFill;
            go.transform.localScale = Vector3.one * (2f * radius / Mathf.Max(width, 1e-4f));
            go.AddComponent<FaceCamera>().Target = facing;
            Renderer.enabled = false;
        }

        /// The body is drawn with a front-projected painting (<see cref="UseProjection"/>).
        public bool Projected => projectedMesh != null;

        Mesh projectedMesh;
        Material ownMaterial;

        /// Paints the body with <paramref name="texture"/> projected from the front (S2, v7-surface README): the painting is
        /// laid on the crust as the camera sees it at <paramref name="restRotation"/> (the body's local rotation at rest) under
        /// <paramref name="viewRotation"/> (the camera's world rotation), its width the body's diameter x <see cref="SpriteFill"/>
        /// (the V7 cutout's fill), so the arrival matches the map sprite and the painting turns with the body. The run-time
        /// stone keeps its crust only (the v4 details are trimmed). With no <paramref name="material"/> the default projected
        /// look is built (URP Lit, transparent, depth writes on so the back cannot show through). Once per body.
        public void UseProjection(Texture texture, Material material, Quaternion viewRotation, Quaternion restRotation)
        {
            if (texture == null && material == null) throw new ArgumentException($"PlanetBody {Key}: a projection needs a texture or a material");
            if (Impostor != null || Projected) throw new InvalidOperationException($"PlanetBody {Key}: already drawn with a sprite or a projection");
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            var mesh = Instantiate(source);
            mesh.name = $"Planet {Key} (projected)";
            if (!Authored)
            {
                var triangles = source.triangles;
                if (triangles.Length < PlanetStoneMesh.CrustTriangles * 3) throw new InvalidOperationException($"PlanetBody {Key}: the stone mesh has fewer than {PlanetStoneMesh.CrustTriangles} triangles");
                var crust = new int[PlanetStoneMesh.CrustTriangles * 3];
                Array.Copy(triangles, crust, crust.Length);
                mesh.triangles = crust;
            }
            var vertices = mesh.vertices;
            var uv = new Vector2[vertices.Length];
            var right = viewRotation * Vector3.right;
            var up = viewRotation * Vector3.up;
            var scale = transform.localScale;
            float diameter = 2f * radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * SpriteFill;
            for (int i = 0; i < vertices.Length; i++)
            {
                var rel = restRotation * Vector3.Scale(vertices[i], scale);
                uv[i] = new Vector2(0.5f + Vector3.Dot(rel, right) / diameter, 0.5f + Vector3.Dot(rel, up) / diameter);
            }
            mesh.uv = uv;
            mesh.RecalculateBounds();
            filter.sharedMesh = projectedMesh = mesh;
            Renderer.sharedMaterial = material != null ? material : ownMaterial = DefaultProjected(texture, Key);
        }

        static Material DefaultProjected(Texture texture, string key)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new MissingReferenceException("[SevenSteps] URP Lit shader missing (the default projected planet material)");
            var m = new Material(shader) { name = $"Planet {key} (projected)", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        void OnDestroy()
        {
            if (projectedMesh != null) Destroy(projectedMesh);
            if (ownMaterial != null) Destroy(ownMaterial);
        }
    }

    /// <summary>Keeps a flat sprite turned to the camera (its rotation, so an orthographic map shows it face on).</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        public Camera Target;

        void LateUpdate()
        {
            if (Target != null) transform.rotation = Target.transform.rotation;
        }
    }
}
