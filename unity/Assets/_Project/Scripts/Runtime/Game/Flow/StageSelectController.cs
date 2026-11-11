using System;
using System.Collections.Generic;
using SevenSteps.Game.UI.Planets;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// <summary>Where a stage was chosen.</summary>
    public enum StageChoiceSource { Map, Surface }

    /// <summary>
    /// A stage chosen on the v4 stage select, handed to the dive transition (docs/stage-select-transitions.md 1).
    /// The receiver ends it exactly once: <see cref="Commit"/> (enter the stage) or <see cref="Cancel"/> (back to the map
    /// with input on again). A second call is a bug: it asserts and throws.
    /// </summary>
    public sealed class StageChoice
    {
        /// Planet index (map order) and its chapter key.
        public int Planet { get; }
        public string PlanetKey { get; }
        /// 0-based stage index (levels.json).
        public int Stage { get; }
        public StageChoiceSource Source { get; }
        /// The chosen body in the Title scene (world space; map body or the surface body).
        public PlanetBody Body { get; }
        /// The Title map camera (scene-flow moves it during the dive).
        public Camera Camera { get; }
        /// Commit or Cancel was called.
        public bool Ended { get; private set; }

        readonly Func<bool> commit;
        readonly Action cancel;

        public StageChoice(int planet, string planetKey, int stage, StageChoiceSource source, PlanetBody body, Camera camera, Func<bool> commit, Action cancel)
        {
            if (planet < 0) throw new ArgumentOutOfRangeException(nameof(planet));
            if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage));
            Planet = planet;
            PlanetKey = planetKey ?? throw new ArgumentNullException(nameof(planetKey));
            Stage = stage;
            Source = source;
            Body = body != null ? body : throw new ArgumentNullException(nameof(body));
            Camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
            this.cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        }

        /// Enter the stage (select announcement + AppFlow.GoToGame). Returns false when the flow cannot go now (a scene
        /// is loading): the choice is then cancelled (map input back on) and the caller restores its camera.
        public bool Commit()
        {
            End(nameof(Commit));
            bool went = commit();
            if (!went) cancel();
            return went;
        }

        /// Give the choice up (Esc during the dive): the map takes input again.
        public void Cancel()
        {
            End(nameof(Cancel));
            cancel();
        }

        void End(string what)
        {
            Debug.Assert(!Ended, $"StageChoice.{what} after the choice already ended");
            if (Ended) throw new InvalidOperationException($"[SevenSteps] StageChoice.{what}: stage {Stage + 1} was already committed or cancelled");
            Ended = true;
        }
    }

    /// <summary>What <see cref="StageSelectController"/> needs from the v4 map and surface views (display only).</summary>
    public interface IStageSelectView
    {
        /// Map click / tap / Enter on a planet.
        event Action<int> PlanetChosen;
        /// Surface place click / tap / Enter: planet, slot.
        event Action<int, int> PlaceChosen;

        /// Draws the map. <paramref name="held"/>: planet whose new trail stays unlit for now (-1 = none).
        void ShowMap(StageSelectModel model, int focus, int held);
        /// The journey return settled: light <paramref name="planet"/>'s outgoing trail (animated) and focus <paramref name="focus"/>.
        void LightTrail(StageSelectModel model, int planet, int focus);
        /// Opens a planet's surface path over the map.
        void OpenSurface(StageSelectModel model, int planet);
        /// A choice is pending (dive running): input off, labels / back / star rows / marker hidden, camera and bodies still.
        void SetPending(bool pending);
        /// The body a choice dives into.
        PlanetBody Body(int planet, StageChoiceSource source);
        Camera MapCamera { get; }
        /// The settled map camera pose for the current screen (the journey return pullback ends here).
        void MapPose(out Vector3 position, out Quaternion rotation, out float orthoSize);
    }

    /// <summary>
    /// Stage select v4 controller (pure C#, docs/stage-select-transitions.md): builds the model from the save, tells the
    /// view what to draw, turns view events into stage choices. A choice goes to <see cref="StageChosen"/> (the dive,
    /// scene-flow); with no subscriber it plays the stage at once (the old flow). Reads JourneyReturn: while it is pending
    /// the new trail and the focus wait for Settled.
    /// </summary>
    public sealed class StageSelectController : IDisposable
    {
        readonly IStageSelectView view;
        readonly Func<StageSelectModel> buildModel;
        readonly IReadOnlyList<IReadOnlyList<int>> stages;
        readonly Func<int, bool> canPlay;
        readonly Action<int> play;
        readonly JourneyReturn journey;
        StageSelectModel model;
        StageChoice pending;
        int heldPlanet = -1;

        /// A stage was chosen; the subscriber runs the dive and ends the choice once.
        public event Action<StageChoice> StageChosen;

        /// The current choice, null when none is pending.
        public StageChoice Pending => pending != null && !pending.Ended ? pending : null;
        public StageSelectModel Model => model;
        public Camera MapCamera => view.MapCamera;
        public PlanetBody Body(int planet) => view.Body(planet, StageChoiceSource.Map);
        /// The settled map camera pose (end of the journey return pullback; the camera itself is not there while JourneyReturn is pending).
        public void MapPose(out Vector3 position, out Quaternion rotation, out float orthoSize) => view.MapPose(out position, out rotation, out orthoSize);

        /// <param name="buildModel">The model for the current save (called on every Show).</param>
        /// <param name="stages">Each planet's stage indices (PlanetEntry.Stages), map order.</param>
        /// <param name="canPlay">The flow can enter this stage now (AppFlow.CanGoToGame).</param>
        /// <param name="play">Enter the stage (TitleFlowController.PlayStage: select announcement + GoToGame).</param>
        /// <param name="journey">The run's JourneyReturn (Services), or null.</param>
        public StageSelectController(IStageSelectView view, Func<StageSelectModel> buildModel, IReadOnlyList<IReadOnlyList<int>> stages,
            Func<int, bool> canPlay, Action<int> play, JourneyReturn journey)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.buildModel = buildModel ?? throw new ArgumentNullException(nameof(buildModel));
            this.stages = stages ?? throw new ArgumentNullException(nameof(stages));
            this.canPlay = canPlay ?? throw new ArgumentNullException(nameof(canPlay));
            this.play = play ?? throw new ArgumentNullException(nameof(play));
            this.journey = journey;
            view.PlanetChosen += OnPlanetChosen;
            view.PlaceChosen += OnPlaceChosen;
            if (journey != null) journey.Settled += OnSettled;
        }

        public void Dispose()
        {
            view.PlanetChosen -= OnPlanetChosen;
            view.PlaceChosen -= OnPlaceChosen;
            if (journey != null) journey.Settled -= OnSettled;
        }

        /// The map is shown (state StageSelect). <paramref name="focus"/>: planet to focus, -1 = the model's default.
        public void Show(int focus = -1)
        {
            model = buildModel() ?? throw new InvalidOperationException("[SevenSteps] StageSelectController: buildModel returned null");
            if (model.PlanetCount != stages.Count) throw new InvalidOperationException($"[SevenSteps] model has {model.PlanetCount} planets, stages {stages.Count}");
            heldPlanet = journey != null && journey.Pending ? journey.Planet : -1;
            if (heldPlanet >= model.PlanetCount) throw new InvalidOperationException($"[SevenSteps] JourneyReturn planet {heldPlanet} is not on the map");
            if (focus < 0) focus = heldPlanet >= 0 ? heldPlanet : model.DefaultFocus;
            view.ShowMap(model, focus, heldPlanet);
        }

        void OnSettled()
        {
            if (model == null || heldPlanet < 0) return; // the map was not shown with a pending return
            int planet = heldPlanet;
            heldPlanet = -1;
            view.LightTrail(model, planet, model.NextAlong(planet));
        }

        void OnPlanetChosen(int planet)
        {
            if (model == null || Pending != null || heldPlanet >= 0) return; // a dive or a return pullback is running
            if (model.ChoosePlays(planet, out int slot)) Choose(planet, slot, StageChoiceSource.Map);
            else view.OpenSurface(model, planet);
        }

        void OnPlaceChosen(int planet, int slot)
        {
            if (model == null || Pending != null) return;
            Choose(planet, slot, StageChoiceSource.Surface);
        }

        void Choose(int planet, int slot, StageChoiceSource source)
        {
            if (slot < 0 || slot >= stages[planet].Count) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"slot on planet {planet}");
            int stage = stages[planet][slot];
            if (StageChosen == null) { play(stage); return; } // journey off: the old flow
            pending = new StageChoice(planet, model.Key(planet), stage, source, view.Body(planet, source), view.MapCamera,
                () =>
                {
                    if (!canPlay(stage)) return false;
                    play(stage);
                    return true;
                },
                () => view.SetPending(false));
            view.SetPending(true);
            StageChosen.Invoke(pending);
        }
    }
}
