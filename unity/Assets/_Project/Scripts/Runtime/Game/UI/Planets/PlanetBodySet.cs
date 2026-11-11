using System;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The planet bodies of the stage select (v4) and the journey transitions, one swappable reference (IMPLEMENT.md):
    /// art replaces a body (v4b: altar, carved, crumble, map lighting) by setting <see cref="Entry.mesh"/> /
    /// <see cref="Entry.material"/> here, without code changes. An entry with no mesh uses the run-time v4 stone mesh
    /// (<see cref="PlanetStoneMesh"/>); no material uses <see cref="material"/>. Data/PlanetBodies.asset, GameAssets.planetBodies.
    /// Mesh space: centre at the origin, about <see cref="Entry.radius"/> across the middle, summit up (+y), front -z.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Planet Body Set", fileName = "PlanetBodies")]
    public sealed class PlanetBodySet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Chapter key (chapters.json id): moss, hollow, carved, frost, joinery, crumble, altar")]
            public string key = "";
            [Tooltip("Round body for the map. Empty = the run-time v4 stone mesh")]
            public Mesh mesh;
            [Tooltip("Body with its summit cut flat for a board (surface path, dive, advance). Empty = the run-time seated mesh")]
            public Mesh seatedMesh;
            [Tooltip("Empty = the set's material")]
            public Material material;
            [Tooltip("Mesh radius in its own units (v4: 3.65)")]
            public float radius = PlanetStoneMesh.Radius;
            [Tooltip("Height of the flat summit a board stands on, mesh units (v4: 2.30)")]
            public float seatHeight = PlanetStoneMesh.SeatTop;
            [Tooltip("Scale of a board on the summit, mesh units per cell (v4: 0.22)")]
            public float boardScale = 0.22f;
        }

        [Tooltip("Shared vertex-colour stone material (RuinsSurface, _UseVertexColor 1)")]
        public Material material;
        [Tooltip("Ground marks on the surface path (stars, scarf dots, footprints): the same shader, unlit")]
        public Material marksMaterial;
        public Entry[] planets = new Entry[0];

        /// The entry for a chapter key, or null.
        public Entry Find(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            foreach (var e in planets) if (e != null && e.key == key) return e;
            return null;
        }
    }
}
