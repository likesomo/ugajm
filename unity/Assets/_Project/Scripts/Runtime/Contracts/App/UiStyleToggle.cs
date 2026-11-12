using System;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// U5 UI toggle (docs/ui-u5-kit.md 2). Every U5 screen asks this once when it is built (scene root or flow controller) and
    /// builds either the U5 view or the current one; views never read it themselves. On since the 11/13 playtest build
    /// (user decision 2026-11-12-b, after the title-to-ending capture check); -ui4 / ?ui5=0 keep the old UI.
    /// </summary>
    public static class UiStyleToggle
    {
        public const bool DefaultOn = true;

        /// -ui5 / -ui4, ?ui5=1|0 win; otherwise <see cref="DefaultOn"/>.
        public static bool U5(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return launch.Ui5 ?? DefaultOn;
        }
    }
}
