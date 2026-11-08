using System.Linq;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Stages from levels.json (docs/architecture.md 6). Parsed once per app run and shared by the Title and Game scenes.</summary>
    public static class LevelCatalog
    {
        static Level[] cached;
        static TextAsset source;
        static int[] minBodies;

        public static Level[] Load(GameAssets assets)
        {
            if (cached != null && source == assets.levelsJson) return cached;
            var file = JsonUtility.FromJson<LevelFileDto>(assets.levelsJson.text);
            cached = file.levels.Select(d => d.ToLevel()).ToArray();
            minBodies = file.levels.Select(d => d.expected != null ? d.expected.minBodies : 0).ToArray();
            source = assets.levelsJson;
            return cached;
        }

        /// Fewest bodies the JS solver needs for a stage (levels.json expected.minBodies), 0 if unknown. Call after Load.
        public static int MinBodies(int stage)
        {
            return minBodies != null && stage >= 0 && stage < minBodies.Length ? minBodies[stage] : 0;
        }
    }
}
