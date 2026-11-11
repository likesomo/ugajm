using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Journey transitions (dive, advance, planet-complete return; docs/stage-select-transitions.md) on or off for this run:
    /// only with the new map (StageSelectToggle), and not when -journey off / ?journey=0. -flowcheck runs keep them off: they
    /// check the result panel path (Result, Next, chapter entry).
    /// </summary>
    public static class JourneyToggle
    {
        public static bool On(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return StageSelectToggle.On(launch) && launch.Journey != false && !launch.FlowCheck;
        }
    }
}
