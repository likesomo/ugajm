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

        public static Level[] Load(GameAssets assets)
        {
            if (cached != null && source == assets.levelsJson) return cached;
            var file = JsonUtility.FromJson<LevelFileDto>(assets.levelsJson.text);
            cached = file.levels.Select(d => d.ToLevel()).ToArray();
            source = assets.levelsJson;
            return cached;
        }
    }
}
