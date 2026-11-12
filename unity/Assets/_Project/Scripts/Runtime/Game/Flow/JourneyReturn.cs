using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>Why the Game scene returns to the map (architect review 2026-11-12, journey after the clear).</summary>
    public enum ReturnKind
    {
        /// Next on the last stage of a planet: the pullback, then the planet's outgoing trail lights.
        PlanetComplete = 0,
        /// Map / Esc from the clear: the same pullback, nothing lights, the map stays on that planet.
        Back = 1,
    }

    /// <summary>
    /// Game scene → star map hand-off (stage select v4, docs/stage-select-transitions.md). Pure C#, one per run (Services).
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
        /// The kind of the last return begun. Kept after <see cref="Settle"/> so a Settled handler can read it.
        public ReturnKind LastKind { get; private set; }

        /// The pullback is over (or was skipped): the map may light the trail and move its focus. Raised once per Begin.
        public event Action Settled;

        /// The Game scene leaves for the map from <paramref name="planet"/>: completed (<see cref="ReturnKind.PlanetComplete"/>,
        /// the trail lights) or just back from its clear (<see cref="ReturnKind.Back"/>).
        public void Begin(int planet, string planetKey, ReturnKind kind = ReturnKind.PlanetComplete)
        {
            if (!Enum.IsDefined(typeof(ReturnKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "JourneyReturn: unknown kind");
            if (planet < 0) throw new ArgumentOutOfRangeException(nameof(planet), planet, "JourneyReturn: planet must be 0 or more");
            if (planetKey == null) throw new ArgumentNullException(nameof(planetKey));
            if (Pending) throw new InvalidOperationException($"JourneyReturn: planet {Planet} ({PlanetKey}) is still pending");
            Pending = true;
            LastKind = kind;
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
