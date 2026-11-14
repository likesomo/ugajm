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
        /// Optional exact placement (altar A, 2026-11-14: props-layout.json from the concept): when set, the modules are
        /// placed from it instead of the scatter. Crust-local metres of the 5x5 reference board, fitted per board (FitAxis).
        public TextAsset layout;
        /// Models the layout names besides <see cref="modules"/> (matched by name = the FBX file name), e.g. Menhir_Count1..7.
        public GameObject[] layoutModels;
        /// Material for the layout's standing stones (null = <see cref="material"/>).
        public Material stoneMaterial;
    }
}
