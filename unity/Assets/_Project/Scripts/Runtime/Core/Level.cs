using System;
using System.Collections.Generic;

namespace SevenSteps.Core
{
    /// <summary>
    /// A parsed stage. Same legend as prototypes/seven-steps/src/core.js parseLevel():
    /// # wall, . floor, S start, E exit, o pit, ~ ice, x cracked floor,
    /// a-c plate, A-C door (open while a statue stands on the matching plate), 1-7 gate (pass only on that step),
    /// Q statue-count door (open while exactly <see cref="StatueCount"/> statues stand; bridges and seals are not statues),
    /// L lever (a 7th step ending on it flips every flip wall), R raised flip wall (blocks), r lowered flip wall (open),
    /// m mud (stepping into it costs 2 steps, blocks with one step left; an ice slide stops on it at no extra cost).
    /// </summary>
    public sealed class Level
    {
        static readonly int[] NoCells = new int[0];

        public readonly string Name;
        public readonly string Hint;
        public readonly int W;
        public readonly int H;
        public readonly int Start;
        public readonly int Bodies;
        /// N of the statue-count doors (Q): they are open while exactly this many statues stand. One N per stage.
        public readonly int StatueCount;
        readonly char[] tiles;
        readonly Dictionary<char, int[]> plates;

        Level(string name, string hint, int w, int h, char[] tiles, Dictionary<char, int[]> plates, int start, int bodies, int statueCount)
        {
            Name = name; Hint = hint; W = w; H = h; this.tiles = tiles; this.plates = plates; Start = start; Bodies = bodies;
            StatueCount = statueCount;
        }

        public char Tile(int i) => tiles[i];
        public int Count => tiles.Length;
        public int X(int i) => i % W;
        public int Y(int i) => i / W;

        /// Plate cells for a lowercase plate letter (a-c).
        public int[] PlatesFor(char plate) => plates.TryGetValue(plate, out var cells) ? cells : NoCells;

        public static Level Parse(string name, string hint, int bodies, string[] map, int statueCount = 0)
        {
            if (map == null || map.Length == 0) throw new ArgumentException($"level \"{name}\" has no map");
            int h = map.Length;
            int w = 0;
            foreach (var row in map) w = Math.Max(w, row.Length);
            var tiles = new char[w * h];
            var plateLists = new Dictionary<char, List<int>>();
            int start = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char t = x < map[y].Length ? map[y][x] : '#';
                    int i = y * w + x;
                    if (t == 'S') start = i;
                    if (t >= 'a' && t <= 'c')
                    {
                        if (!plateLists.TryGetValue(t, out var list)) plateLists[t] = list = new List<int>();
                        list.Add(i);
                    }
                    tiles[i] = t == 'S' ? '.' : t;
                }
            }
            if (start < 0) throw new ArgumentException($"level \"{name}\" has no S");
            var plates = new Dictionary<char, int[]>();
            foreach (var kv in plateLists) plates[kv.Key] = kv.Value.ToArray();
            return new Level(name, hint ?? "", w, h, tiles, plates, start, bodies > 0 ? bodies : Rules.Steps, statueCount);
        }
    }

    /// JSON shapes of unity/Assets/_Project/Data/levels.json (written by unity/Tools/export-levels.mjs from levels.js).
    [Serializable]
    public sealed class LevelFileDto
    {
        public int steps;
        public LevelDto[] levels;
    }

    [Serializable]
    public sealed class LevelDto
    {
        public string name;
        public string hint;
        public int bodies;
        /// N of the statue-count doors (Q). 0 when the stage has none (missing in older files reads as 0).
        public int statueCount;
        /// Chapter id from Data/chapters.json (docs/architecture.md 6.4), e.g. "moss". Empty or null = none yet
        /// (runtime treats it as the first chapter). Not a rule: Core Level does not carry it.
        public string chapter;
        /// One-line story (the triplets' view), separate from the hint. Optional; empty or null = none.
        public string story;
        public string[] map;
        public ExpectedDto expected;

        public Level ToLevel() => Level.Parse(name, hint, bodies, map, statueCount);
    }

    /// What the JS solver found; LevelVerifier checks the C# port gives the same answer.
    [Serializable]
    public sealed class ExpectedDto
    {
        public int minBodies;
        public int moves;
        public bool needsSwitch;
        public string path;
    }
}
