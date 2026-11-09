using System;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Which body (0 = first, 1 = second, ...) is playing now, and which body left each statue.
    /// Derived from the State undo chain (<see cref="State.Prev"/>) only, so Undo/Restart redraws stay correct
    /// (docs/architecture.md 5, 12.6). No rule logic: Core keeps statues as a sorted cell list without owners.
    /// Cost: one walk of the undo chain per call. Call when the state changes, not every frame.
    /// </summary>
    public static class BodyIndex
    {
        /// Body playing in <paramref name="s"/>. After the last body is spent (Lose) this is the last body.
        public static int Current(State s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            int first = Root(s).Bodies;
            return Math.Max(0, Math.Min(first - 1, first - s.Bodies));
        }

        /// Body that turned into the statue standing on <paramref name="cell"/>, or -1 if no statue stands there.
        public static int OfStatue(State s, int cell)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (!Rules.Contains(s.Statues, cell)) return -1;
            // Walk back to the first state that has this statue; the state before it is the body that made it.
            var t = s;
            while (t.Prev != null && Rules.Contains(t.Prev.Statues, cell)) t = t.Prev;
            if (t.Prev == null) throw new InvalidOperationException($"BodyIndex: statue at {cell} has no history (state chain starts with it)");
            return Current(t.Prev);
        }

        static State Root(State s)
        {
            while (s.Prev != null) s = s.Prev;
            return s;
        }
    }
}
