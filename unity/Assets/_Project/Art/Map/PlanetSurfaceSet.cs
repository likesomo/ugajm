using System;
using UnityEngine;

namespace SevenSteps.Art.Map
{
    /// <summary>Maps stable planet ids to the V7 front projection used by the surface body.</summary>
    [CreateAssetMenu(menuName = "Seven Steps/Map/Planet Surface Set", fileName = "PlanetSurfaceSet")]
    public sealed class PlanetSurfaceSet : ScriptableObject, SevenSteps.Game.UI.Planets.IPlanetSurfaceArt
    {
        [Serializable]
        public sealed class Entry
        {
            public string planetId;
            public Texture projection;
            public Material material;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public bool TryGetSurface(string planetId, out Texture projection, out Material material)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry != null && string.Equals(entry.planetId, planetId, StringComparison.Ordinal))
                    {
                        projection = entry.projection;
                        material = entry.material;
                        return projection != null && material != null;
                    }
                }
            }

            projection = null;
            material = null;
            return false;
        }
    }
}
