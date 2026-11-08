using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Build-time settings (not user settings). One asset: Assets/_Project/Data/AppConfig.asset, referenced by the Boot scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/App Config", fileName = "AppConfig")]
    public sealed class AppConfig : ScriptableObject
    {
        [Header("Telemetry")]
        [Tooltip("Collector base URL (docs/telemetry.md). Empty = telemetry off (NullTelemetry).")]
        public string telemetryEndpoint = "";
        [Tooltip("Default for new players. The player can still opt out in Settings.")]
        public bool telemetryOnByDefault = true;
        [Tooltip("Sent with every event, e.g. jam, playtest, dev.")]
        public string buildChannel = "dev";

        [Header("Board")]
        public TileSet tileSet;
    }
}
