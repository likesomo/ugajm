using System;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// <summary>
    /// Text for the H key: the shortest solution from the stage start (same as the web prototype), solved once per stage
    /// and cached. Plain C#: the solving is Core's (<see cref="Solver"/>), this only words the route.
    /// </summary>
    public sealed class HintProvider
    {
        public const string NotFound = "풀이를 찾지 못했다.";
        const string Prefix = "처음부터의 최단 풀이: ";

        readonly Level[] levels;
        readonly Dictionary<int, string> cache = new Dictionary<int, string>();

        public HintProvider(Level[] levels)
        {
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        }

        /// Solution text for 0-based <paramref name="stage"/>. The first call per stage runs the solver.
        public string For(int stage)
        {
            if (stage < 0 || stage >= levels.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{levels.Length - 1}");
            if (cache.TryGetValue(stage, out var text)) return text;
            var sol = Solver.Solve(levels[stage]);
            text = sol == null ? NotFound : Describe(sol.Path);
            cache[stage] = text;
            return text;
        }

        static string Describe(List<Act> path)
        {
            // Words, not arrow symbols: Jua (the only UI font, no fallback in WebGL) has no U+2190-2193, so arrows drew nothing.
            var sb = new StringBuilder(Prefix);
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(path[i] switch { Act.Up => "상", Act.Down => "하", Act.Left => "좌", Act.Right => "우", Act.Keep => "KEEP", _ => "SWITCH" });
            }
            return sb.ToString();
        }
    }
}
