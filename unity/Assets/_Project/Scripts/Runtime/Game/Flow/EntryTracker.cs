using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// How the next stage entry was reached: telemetry level_start.via (docs/telemetry.md). Pure C#, one per Play,
    /// created by Bootstrap and registered in Services so it survives the Title → Game scene change.
    /// An announced via wins, also on the app run's first entry: a stage picked on the planet map / stage list or the title's
    /// Begin is "select" even right after launch (lead decision 2026-11-10, playtest rotation). Otherwise the first entry is
    /// "url" (stage from ?level= / -level) or "boot" (title Continue, the editor's start stage), later ones "select".
    /// Replaces GameController.FirstEntryPending + nextVia (docs/scene-flow.md S2).
    /// </summary>
    public sealed class EntryTracker
    {
        readonly string firstVia;
        string announced;

        /// <param name="firstVia">TelemetryNames.Via.Boot or .Url.</param>
        public EntryTracker(string firstVia)
        {
            if (firstVia != TelemetryNames.Via.Boot && firstVia != TelemetryNames.Via.Url)
                throw new ArgumentException($"first entry is boot or url, not \"{firstVia}\"", nameof(firstVia));
            this.firstVia = firstVia;
        }

        /// No stage has been entered yet in this app run.
        public bool FirstPending { get; private set; } = true;

        /// For a launch: url when the stage came from the command line or URL, boot otherwise (Title, or the editor's startLevel).
        public static EntryTracker ForLaunch(bool levelFromLaunch) =>
            new EntryTracker(levelFromLaunch ? TelemetryNames.Via.Url : TelemetryNames.Via.Boot);

        /// The next entry comes from the Result/HUD "next" or "again" action, or a stage pick (select).
        public void Announce(string via)
        {
            if (via != TelemetryNames.Via.Next && via != TelemetryNames.Via.Again && via != TelemetryNames.Via.Select)
                throw new ArgumentException($"announce next, again or select, not \"{via}\"", nameof(via));
            announced = via;
        }

        /// The via of the entry happening now. Call once per stage entry.
        public string TakeVia()
        {
            string via = announced ?? (FirstPending ? firstVia : TelemetryNames.Via.Select);
            FirstPending = false;
            announced = null;
            return via;
        }
    }
}
