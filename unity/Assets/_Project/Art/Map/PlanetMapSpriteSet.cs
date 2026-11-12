using System;
using UnityEngine;

namespace SevenSteps.Art.Map
{
    /// <summary>Data-only mapping for V7 map planet sprites. Stage-select code may consume this separately.</summary>
    [CreateAssetMenu(menuName = "Seven Steps/Map/Planet Sprite Set", fileName = "PlanetMapSpriteSet")]
    public sealed class PlanetMapSpriteSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string planetId;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGetSprite(string planetId, out Sprite sprite)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    Entry entry = entries[i];
                    if (entry != null && string.Equals(entry.planetId, planetId, StringComparison.Ordinal))
                    {
                        sprite = entry.sprite;
                        return sprite != null;
                    }
                }
            }

            sprite = null;
            return false;
        }
    }
}
