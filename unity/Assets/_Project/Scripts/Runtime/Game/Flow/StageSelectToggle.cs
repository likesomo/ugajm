using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Stage select v4 (planet bodies, docs/stage-select-transitions.md 4) on or off for this run: -newmap / ?newmap=1 turn it
    /// on, -oldmap / ?newmap=0 / ?oldmap=1 off, otherwise <see cref="DefaultOn"/>. One place for the Title map (gameplay-ui) and
    /// the journey transitions (scene-flow). The lead flips only <see cref="DefaultOn"/> for a playtest build.
    /// </summary>
    public static class StageSelectToggle
    {
        /// Off until the new map is merged and stable (lead, 2026-11-11).
        public const bool DefaultOn = false;

        public static bool On(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return launch.NewMap ?? DefaultOn;
        }
    }
}
