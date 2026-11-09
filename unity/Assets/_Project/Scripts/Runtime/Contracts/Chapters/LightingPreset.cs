using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Lights of a chapter as data (docs/architecture.md 12.8): warm key with shadows, cool fill, faint rim, flat ambient.
    /// The game scene owns three directional lights and copies these values on chapter change; the scene has no other lights.
    /// WebGL runs Mobile_RPAsset: only the key light casts shadows; fill and rim are cheap (vertex/additional per the RP asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Lighting Preset", fileName = "Lighting")]
    public sealed class LightingPreset : ScriptableObject
    {
        public LightRig key = new LightRig { enabled = true, color = Color.white, intensity = 1f, euler = new Vector3(50f, -30f, 0f), shadows = true };
        public LightRig fill = new LightRig { enabled = false, color = Color.white, intensity = 0.5f, euler = new Vector3(30f, 150f, 0f) };
        public LightRig rim = new LightRig { enabled = false, color = Color.white, intensity = 0.3f, euler = new Vector3(20f, 90f, 0f) };
        [Tooltip("RenderSettings.ambientLight (flat ambient).")]
        public Color ambient = new Color(0.5f, 0.55f, 0.6f);
    }

    [Serializable]
    public struct LightRig
    {
        public bool enabled;
        public Color color;
        [Min(0f)] public float intensity;
        [Tooltip("Directional light rotation (x = down from horizon, y = around Y).")]
        public Vector3 euler;
        [Tooltip("Only the key light should cast shadows (WebGL budget).")]
        public bool shadows;
    }
}
