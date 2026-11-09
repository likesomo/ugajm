using System;
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
        [Tooltip("Debug/transition override: when set, every chapter uses this TileSet (docs/architecture.md 12.8).")]
        public TileSet tileSet;

        [Header("Chapters")]
        [Tooltip("First = default (stages without a \"chapter\" id). docs/architecture.md 12.8")]
        public ChapterPreset[] chapters = new ChapterPreset[0];

        /// Chapter for a levels.json "chapter" id. Empty id = first chapter; null when no chapters are configured.
        /// Unknown id throws: levels.json and AppConfig disagree (caught by TileSetValidator before a build).
        public ChapterPreset Chapter(string id)
        {
            if (chapters == null || chapters.Length == 0) return null;
            if (string.IsNullOrEmpty(id)) return chapters[0];
            foreach (var c in chapters)
                if (c != null && c.id == id) return c;
            throw new ArgumentException($"AppConfig: no chapter \"{id}\" in chapters", nameof(id));
        }
    }
}
