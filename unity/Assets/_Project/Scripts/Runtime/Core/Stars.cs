using System;

namespace SevenSteps.Core
{
    /// <summary>
    /// Stars for one cleared attempt (design/levels/stars.md 0; user-confirmed 2026-11-12). Port of core.js stars().
    /// An attempt runs from level_start to the clear; restarts inside it belong to the same attempt.
    /// Only the best stars per stage name are kept (progress v2); stars lock nothing.
    /// </summary>
    public static class Stars
    {
        public const int Max = 3;

        /// 1 = cleared; 2 = also no hint (H) in the attempt; 3 = also every brother used: <paramref name="bodiesUsed"/> ==
        /// <paramref name="bodies"/> (the level's body count, Level.Bodies). Undo and restart do not matter.
        /// Throws ArgumentOutOfRangeException when bodies &lt; 1, bodiesUsed is not 1..bodies or hints &lt; 0 (caller bug).
        /// core.js returns null for an unknown body count (server rows); the game always knows it.
        public static int Of(int bodiesUsed, int bodies, int hints)
        {
            if (bodies < 1) throw new ArgumentOutOfRangeException(nameof(bodies), bodies, "a level has at least one body");
            if (bodiesUsed < 1 || bodiesUsed > bodies) throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, $"1..{bodies}");
            if (hints < 0) throw new ArgumentOutOfRangeException(nameof(hints), hints, "count");
            if (hints > 0) return 1;
            return bodiesUsed == bodies ? 3 : 2;
        }

        /// Bodies spent so far in this run, counting the current one (1 = first body). On a win: the bodies it took.
        /// Same as core.js bodiesUsed(). Assumes the state started with Level.Bodies (no NewState override).
        public static int BodiesUsed(State s) => s.Level.Bodies - s.Bodies + 1;
    }
}
