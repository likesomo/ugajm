using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Planet complete → star map hand-off (stage select v4, docs/stage-select-transitions.md). Pure C#, one per run (Services).
    /// The Game scene <see cref="Begin"/>s it when the last stage of a planet is cleared and the flow returns to StageSelect.
    /// The Title scene starts its camera close on that planet, pulls back to the map, then <see cref="Settle"/>s it.
    /// While <see cref="Pending"/> the map view keeps the newly lit trail unlit, holds its focus and does not move the camera
    /// or the bodies; on <see cref="Settled"/> it lights the trail and focuses the next planet (gameplay-ui).
    /// </summary>
    public sealed class JourneyReturn
    {
        /// A return is under way: set by the Game scene, cleared by the Title scene's pullback.
        public bool Pending { get; private set; }
        /// 0-based planet (chapters.json order) just completed; -1 when none is pending.
        public int Planet { get; private set; } = -1;
        /// Its chapter key (levels.json "chapter"); "" when none is pending.
        public string PlanetKey { get; private set; } = "";

        /// The pullback is over (or was skipped): the map may light the trail and move its focus. Raised once per Begin.
        public event Action Settled;

        /// The Game scene leaves for the map after completing <paramref name="planet"/>.
        public void Begin(int planet, string planetKey)
        {
            if (planet < 0) throw new ArgumentOutOfRangeException(nameof(planet), planet, "JourneyReturn: planet must be 0 or more");
            if (planetKey == null) throw new ArgumentNullException(nameof(planetKey));
            if (Pending) throw new InvalidOperationException($"JourneyReturn: planet {Planet} ({PlanetKey}) is still pending");
            Pending = true;
            Planet = planet;
            PlanetKey = planetKey;
        }

        /// The Title scene's pullback finished (or was skipped, or the map was shown without one).
        public void Settle()
        {
            if (!Pending) throw new InvalidOperationException("JourneyReturn: nothing is pending");
            Pending = false;
            Planet = -1;
            PlanetKey = "";
            Settled?.Invoke();
        }
    }
}
