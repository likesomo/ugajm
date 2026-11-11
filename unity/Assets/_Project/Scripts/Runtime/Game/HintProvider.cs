using System;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// <summary>
    /// Text for the H key: the shortest solution from the stage start (same as the web prototype). The route is solved
    /// once per stage and cached; the words come from the string table (hint.*, docs/architecture.md 13.2) on each call,
    /// so a language change rewords it. Plain C#: the solving is Core's (<see cref="Solver"/>), this only words the route.
    /// </summary>
    public sealed class HintProvider
    {
        readonly Level[] levels;
        readonly Localizer loc;
        readonly Dictionary<int, List<Act>> cache = new Dictionary<int, List<Act>>(); // null value = no solution
        readonly Func<int, string> stored;

        /// <param name="stored">The stage's stored route (replay letters, LevelCatalog.PathOf; "" = none, solve it). Null =
        /// always solve. The stored one is the route the staged hint's goals come from, so the last tier agrees with them.</param>
        public HintProvider(Level[] levels, Localizer loc, Func<int, string> stored = null)
        {
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            this.stored = stored;
        }

        static List<Act> Parse(string letters)
        {
            var acts = new List<Act>(letters.Length);
            foreach (char c in letters)
                if (Rules.TryParseLetter(c, out var a)) acts.Add(a);
                else throw new ArgumentException($"'{c}' in \"{letters}\" is not a command", nameof(letters));
            return acts;
        }

        /// Solution text for 0-based <paramref name="stage"/> in the current language. The first call per stage runs the solver.
        public string For(int stage)
        {
            if (stage < 0 || stage >= levels.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{levels.Length - 1}");
            if (!cache.TryGetValue(stage, out var path))
            {
                string letters = stored?.Invoke(stage);
                path = !string.IsNullOrEmpty(letters) ? Parse(letters) : Solver.Solve(levels[stage])?.Path;
                cache[stage] = path;
            }
            return path == null ? loc.Get("hint.not_found") : loc.Format("hint.route", Describe(path));
        }

        /// The route in words, repeated moves grouped ("Right x6, Down, SWITCH, Left x6"; hint.repeat), so the longest
        /// solution still fits the HUD side column next to KEEP / SWITCH (docs/architecture.md 14, UiLayoutCheck B3).
        string Describe(List<Act> path)
        {
            // Words, not arrow symbols: the UI fonts (no fallback in WebGL) have no U+2190-2193, so arrows drew nothing.
            string up = loc.Get("hint.up"), down = loc.Get("hint.down"), left = loc.Get("hint.left"), right = loc.Get("hint.right");
            var sb = new StringBuilder();
            for (int i = 0; i < path.Count;)
            {
                int run = 1;
                while (i + run < path.Count && path[i + run] == path[i]) run++;
                if (sb.Length > 0) sb.Append(", ");
                string word = path[i] switch { Act.Up => up, Act.Down => down, Act.Left => left, Act.Right => right, Act.Keep => "KEEP", _ => "SWITCH" };
                sb.Append(run == 1 ? word : loc.Format("hint.repeat", word, run));
                i += run;
            }
            return sb.ToString();
        }
    }
}
