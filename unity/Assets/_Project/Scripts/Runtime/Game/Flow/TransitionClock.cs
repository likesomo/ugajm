using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Time of one journey transition (docs/stage-select-transitions.md). Pure C#: the runner ticks it with unscaled time,
    /// a skip jumps it to the end. <see cref="Eased"/> is the SmoothStep curve the v3/v4 storyboards were sampled from.
    /// </summary>
    public sealed class TransitionClock
    {
        public TransitionClock(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "TransitionClock: seconds must be 0 or more");
            Seconds = seconds;
        }

        public float Seconds { get; }
        public float Elapsed { get; private set; }
        public bool Done => Elapsed >= Seconds;
        /// Linear progress 0..1 (1 for a zero-length transition).
        public float T => Seconds <= 0f ? 1f : Clamp01(Elapsed / Seconds);
        /// SmoothStep(T).
        public float Eased => Ease(T);

        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "TransitionClock: time runs forward");
            Elapsed = Math.Min(Seconds, Elapsed + deltaSeconds);
        }

        /// Jump to the settled end (skip input, reduced motion).
        public void Skip() => Elapsed = Seconds;

        public static float Ease(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// The part of a 0..1 progress that falls in [from, to], as 0..1 (0 before, 1 after). Staggers pieces of one transition.
        public static float Window(float t, float from, float to)
        {
            if (!(to > from)) throw new ArgumentException($"TransitionClock.Window: [{from}, {to}] is empty");
            return Clamp01((t - from) / (to - from));
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
