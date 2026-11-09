namespace SevenSteps.Core
{
    /// <summary>
    /// Stars for one cleared attempt (design/levels/stars.md 0, plan B). Port of core.js stars().
    /// An attempt runs from level_start to the clear; restarts inside it belong to the same attempt.
    /// Only the best stars per stage name are kept (progress v2); stars lock nothing.
    /// </summary>
    public static class Stars
    {
        public const int Max = 3;

        /// 1 = cleared; 2 = also no hint and <paramref name="bodiesUsed"/> == <paramref name="minBodies"/>;
        /// 3 = also no undo and no restart in the attempt.
        /// <paramref name="minBodies"/> is levels.json expected.minBodies (the solver's fewest bodies).
        /// Returns 0 when minBodies is unknown (&lt;= 0), where core.js returns null.
        public static int Of(int bodiesUsed, int minBodies, int hints, int undos, int restarts)
        {
            if (minBodies <= 0) return 0;
            if (hints > 0 || bodiesUsed > minBodies) return 1;
            return undos == 0 && restarts == 0 ? 3 : 2;
        }

        /// Bodies spent so far in this run, counting the current one (1 = first body). On a win: the bodies it took.
        /// Same as core.js bodiesUsed(). Assumes the state started with Level.Bodies (no NewState override).
        public static int BodiesUsed(State s) => s.Level.Bodies - s.Bodies + 1;
    }
}
