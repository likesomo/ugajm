using UnityEngine;

namespace SevenSteps.Art
{
    /// <summary>Blender modules and a single shared surface material for one planet.</summary>
    [CreateAssetMenu(menuName="Seven Steps/Planet Dressing")]
    public sealed class PlanetDressingSet : ScriptableObject
    {
        public string planetId;
        public GameObject[] modules;
        public Material material;
    }
}
