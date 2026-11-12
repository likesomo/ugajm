using System;

namespace SevenSteps.Core
{
    /// <summary>
    /// What the step constellation shows for one state (user decision 2026-11-12 "C": seven stars in the sky above the
    /// board, joined as the steps add up; replaces the stars at the brother's feet). Pure model, read by Game.StepSkyView.
    /// </summary>
    public readonly struct StepSky : IEquatable<StepSky>
    {
        /// Shown while a body walks (Play) or chooses KEEP / SWITCH after its 7th step (Choose); hidden on Win / Lose.
        public readonly bool Shown;
        /// Stars lit = steps walked by the current body, 0..Rules.Steps. Lit stars k-1 and k are joined by a line.
        public readonly int Lit;
        /// One step left (Play, 6 walked): the 7th star calls.
        public readonly bool Calling;

        public StepSky(bool shown, int lit, bool calling)
        {
            if (lit < 0 || lit > Rules.Steps) throw new ArgumentOutOfRangeException(nameof(lit), lit, $"0..{Rules.Steps}");
            if (calling && (!shown || lit != Rules.Steps - 1)) throw new ArgumentException("only the last empty star calls", nameof(calling));
            Shown = shown;
            Lit = lit;
            Calling = calling;
        }

        public static StepSky Of(State s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            bool shown = s.Status == Status.Play || s.Status == Status.Choose;
            return new StepSky(shown, s.Used, s.Status == Status.Play && s.Used == Rules.Steps - 1);
        }

        /// Lines drawn: one between each pair of neighbouring lit stars.
        public int Links => Lit > 1 ? Lit - 1 : 0;

        public bool Equals(StepSky o) => Shown == o.Shown && Lit == o.Lit && Calling == o.Calling;
        public override bool Equals(object obj) => obj is StepSky o && Equals(o);
        public override int GetHashCode() => (Shown ? 1 : 0) | Lit << 1 | (Calling ? 1 << 5 : 0);
        public override string ToString() => $"StepSky(shown {Shown}, lit {Lit}, calling {Calling})";
    }
}
