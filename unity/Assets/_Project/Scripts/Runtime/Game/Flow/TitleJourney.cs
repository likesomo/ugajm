using System;
using SevenSteps.Game.UI.Planets;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// The Title scene half of the stage select v4 journey (docs/stage-select-transitions.md): the first half of the dive
    /// (the map camera moves onto the chosen planet's summit, then the choice commits and the Game scene finishes it) and the
    /// planet-complete return (the camera starts on the completed planet and pulls back to the map, then JourneyReturn
    /// settles so the map lights the new trail). Controller only: it moves the map camera; the map view (gameplay-ui) hides
    /// its labels and ignores input while a choice is pending or a return is pending. Created by TitleScreen only when
    /// JourneyToggle is on; without it StageSelectController plays the stage itself (the old flow).
    /// </summary>
    public sealed class TitleJourney : IDisposable
    {
        /// Title scene half of the 1.5 s dive (GameJourney.ArriveSeconds is the other half).
        public const float DiveSeconds = 0.75f;
        /// Title scene part of the 1.5 s return (GameJourney.PullbackSeconds is the rest).
        public const float ReturnSeconds = 0.9f;
        /// Orthographic size on a summit, in planet radii (v3/v4 storyboards: size 1.8 on a summit at radius ~2.25).
        public const float SummitRadii = 0.8f;

        readonly StageSelectController select;
        readonly JourneyRunner runner;
        readonly JourneyDive dive;
        readonly JourneyReturn journeyReturn;
        readonly Func<int, bool> canGoToGame;

        /// <param name="canGoToGame">AppFlow.CanGoToGame: a choice the flow cannot take now is cancelled, not committed.</param>
        public TitleJourney(StageSelectController select, JourneyRunner runner, JourneyDive dive, JourneyReturn journeyReturn, Func<int, bool> canGoToGame)
        {
            this.select = select ?? throw new ArgumentNullException(nameof(select));
            this.runner = runner != null ? runner : throw new ArgumentNullException(nameof(runner));
            this.dive = dive ?? throw new ArgumentNullException(nameof(dive));
            this.journeyReturn = journeyReturn ?? throw new ArgumentNullException(nameof(journeyReturn));
            this.canGoToGame = canGoToGame ?? throw new ArgumentNullException(nameof(canGoToGame));
            select.StageChosen += OnStageChosen;
        }

        public void Dispose() => select.StageChosen -= OnStageChosen;

        /// Input waits (a transition plays, or this frame's press skipped one).
        public bool Blocks => runner.Blocks;

        // ---------- Dive ----------

        void OnStageChosen(StageChoice choice)
        {
            if (runner.Busy) throw new InvalidOperationException("TitleJourney: a stage was chosen during a transition (the map must ignore input while a choice is pending)");
            var cam = choice.Camera != null ? choice.Camera : throw new ArgumentException("StageChoice without a camera", nameof(choice));
            var body = choice.Body != null ? choice.Body : throw new ArgumentException("StageChoice without a planet body", nameof(choice));
            var from = CameraPose.Capture(cam);
            var to = Summit(body, from);
            runner.Play(DiveSeconds, t => CameraPose.Lerp(from, to, t).Apply(cam), () =>
            {
                if (!canGoToGame(choice.Stage))
                {
                    from.Apply(cam);
                    choice.Cancel();
                    return;
                }
                dive.Begin(choice.Stage);
                // The scene loader fades out from the summit; GameJourney.ArriveIfDiving finishes the dive. False = the flow
                // refused and the choice is already cancelled: undo the hand-off and the camera.
                if (!choice.Commit())
                {
                    dive.Cancel();
                    from.Apply(cam);
                }
            }, cancel: () =>
            {
                from.Apply(cam); // Esc: back on the map, nothing chosen
                choice.Cancel();
            });
        }

        // ---------- Return ----------

        /// The Title scene has started (TitleScreen.Start, after the flow showed StageSelect): the planet-complete pullback when one is pending.
        public void ReturnIfPending()
        {
            if (!journeyReturn.Pending) return;
            var cam = select.MapCamera != null ? select.MapCamera : throw new InvalidOperationException("TitleJourney: no map camera for the return");
            var body = select.Body(journeyReturn.Planet);
            var map = CameraPose.Capture(cam);
            var start = Summit(body, map);
            start.Apply(cam);
            runner.Play(ReturnSeconds, t => CameraPose.Lerp(start, map, t).Apply(cam), journeyReturn.Settle);
        }

        /// The camera on the body's summit (where the board sits), same rotation and distance as <paramref name="from"/>.
        static CameraPose Summit(PlanetBody body, CameraPose from)
        {
            var seat = body.transform.TransformPoint(body.SeatLocal);
            float radius = body.Radius; // world units already (PlanetBody: mesh radius x largest lossy scale)
            if (!(radius > 0f)) throw new InvalidOperationException($"TitleJourney: planet body {body.Key} has radius {radius}");
            float distance = Mathf.Max(5f, Vector3.Dot(seat - from.Position, from.Rotation * Vector3.forward));
            return CameraPose.LookingAt(seat, from.Rotation, radius * SummitRadii, distance);
        }
    }
}
