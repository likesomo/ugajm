using System;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// U5 UI toggle (docs/ui-u5-kit.md 2). Every U5 screen asks this once when it is built (scene root or flow controller) and
    /// builds either the U5 view or the current one; views never read it themselves. Off until the U5 screens are stable
    /// (lead plan 2026-11-12: the 11/13 playtest keeps the current UI unless U5 is ready). Playtest builds flip only DefaultOn.
    /// </summary>
    public static class UiStyleToggle
    {
        public const bool DefaultOn = false;

        /// -ui5 / -ui4, ?ui5=1|0 win; otherwise <see cref="DefaultOn"/>.
        public static bool U5(LaunchOptions launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return launch.Ui5 ?? DefaultOn;
        }
    }
}
