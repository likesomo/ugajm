using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Journey transitions (dive, advance, planet-complete return; docs/stage-select-transitions.md) on or off for this run:
    /// only with the new map (StageSelectToggle), and not when -journey off / ?journey=0. -flowcheck runs keep them off: they
    /// check the result panel path (Result, Next, chapter entry). The v4 map that the U5 UI brings by itself does not bring
    /// the journey: its advance skips the result, and the U5 clear (star stones, Next by the exit) is the result. Open
    /// question to the lead (2026-11-12); until then the journey needs the map from -newmap or StageSelectToggle.DefaultOn.
    /// </summary>
    public static class JourneyToggle
    {
        public static bool On(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            bool mapAsked = launch.NewMap ?? StageSelectToggle.DefaultOn; // not the U5 default (UiStyleToggle)
            return StageSelectToggle.On(launch) && mapAsked && launch.Journey != false && !launch.FlowCheck;
        }
    }
}
