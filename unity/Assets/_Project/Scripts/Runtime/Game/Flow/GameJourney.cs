using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// The Game scene half of the stage select v4 journey (docs/stage-select-transitions.md): the dive's arrival (camera
    /// settles from the planet onto the board, the board rises from the surface), the advance (the brothers walk out to the
    /// right, the next board rises there, the camera follows; no result panel) and the planet-complete pullback (camera
    /// pulls back, then JourneyReturn and the star map). Controller only: it moves the camera and the board root through the
    /// BoardView hooks, and the flow through GameFlowController. Created by the scene root only when the journey is on.
    /// </summary>
    public sealed class GameJourney : IDisposable
    {
        /// Halves of the 1.5 s dive: the Title scene plays the first, this scene the second.
        public const float ArriveSeconds = 0.75f;
        public const float AdvanceSeconds = 1.5f;
        /// Game scene part of the 1.5 s planet-complete return; the Title scene pulls back the rest.
        public const float PullbackSeconds = 0.6f;
        /// How far zoomed out (orthographic size factor) the dive arrival starts and the pullback ends.
        public const float WideZoom = 3.5f;
        /// Empty cells between the cleared board's island and the next one's (between the boards when there is no planet).
        public const float GapCells = 1f;
        /// Depth (cells) a board rises from / sinks to.
        public const float RiseCells = 2f;

        readonly GameFlowController flow;
        readonly BoardController board;
        readonly Camera cam;
        readonly JourneyRunner runner;
        readonly JourneyReturn journeyReturn;
        readonly JourneyDive dive;
        readonly ChapterStage ground;
        readonly Func<int, int> planetOf;
        readonly Func<int, string> chapterOf;
        bool deferred; // a transition waits for the next frame (it was asked for inside a presentation callback)

        /// <param name="ground">The chapter stage: the island under the board (copied for the cleared board on the advance).</param>
        /// <param name="planetOf">0-based planet of a stage (the star map's planet order); -1 = none (JourneyReturn is then not begun).</param>
        /// <param name="chapterOf">Chapter key of a stage (LevelCatalog.ChapterKeyOf).</param>
        public GameJourney(GameFlowController flow, BoardController board, Camera cam, JourneyRunner runner, JourneyReturn journeyReturn, JourneyDive dive,
            ChapterStage ground, Func<int, int> planetOf, Func<int, string> chapterOf)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.board = board != null ? board : throw new ArgumentNullException(nameof(board));
            this.cam = cam != null ? cam : throw new ArgumentNullException(nameof(cam));
            this.runner = runner != null ? runner : throw new ArgumentNullException(nameof(runner));
            this.journeyReturn = journeyReturn ?? throw new ArgumentNullException(nameof(journeyReturn));
            this.dive = dive ?? throw new ArgumentNullException(nameof(dive));
            this.ground = ground ?? throw new ArgumentNullException(nameof(ground));
            this.planetOf = planetOf ?? throw new ArgumentNullException(nameof(planetOf));
            this.chapterOf = chapterOf ?? throw new ArgumentNullException(nameof(chapterOf));
            flow.Journey = true;
            flow.Advancing += OnAdvancing;
            flow.PlanetCompleting += OnPlanetCompleting;
        }

        public void Dispose()
        {
            flow.Journey = false;
            flow.Advancing -= OnAdvancing;
            flow.PlanetCompleting -= OnPlanetCompleting;
        }

        /// Board input waits: a transition is playing or about to start, or this frame's press skipped one.
        public bool Blocks => deferred || runner.Blocks;

        // ---------- Dive arrival ----------

        /// After the scene's first stage entry (GameController.Start): when it is the dive's destination, play the arrival.
        public void ArriveIfDiving(int stage)
        {
            if (!dive.Arrive(stage)) return;
            var view = board.View;
            var root = view.BoardRoot ?? throw new InvalidOperationException("GameJourney: dive arrival before the board was built");
            view.CameraHeld = true;
            var end = CameraPose.Capture(cam); // the board fitted itself on Rebuild
            var start = end.Zoomed(WideZoom);
            float depth = RiseCells * view.Grid.CellSize;
            runner.Play(ArriveSeconds, t =>
            {
                CameraPose.Lerp(start, end, t).Apply(cam);
                root.localPosition = new Vector3(0f, -depth * (1f - TransitionClock.Ease(TransitionClock.Window(t, 0.2f, 1f))), 0f);
            }, () =>
            {
                root.localPosition = Vector3.zero;
                view.CameraHeld = false;
            });
        }

        // ---------- Advance ----------

        void OnAdvancing(int next) => Defer(() => Advance(next));

        void Advance(int next)
        {
            var view = board.View;
            view.CameraHeld = true;
            var from = CameraPose.Capture(cam);
            var snap = view.DetachSnapshot();
            // The cleared board keeps its island: a still copy, taken before the planet refits to the next board.
            Bounds oldB = ground.GroundBounds(snap.Grid) ?? snap.Grid.Bounds;
            var oldGround = ground.CopyGround(snap.Grid);
            if (oldGround != null) oldGround.transform.SetParent(snap.Root.transform, true);
            flow.ContinueTo(next); // LoadStage: the next board is built at the origin (no refit while held)

            var grid = view.Grid;
            var root = view.BoardRoot ?? throw new InvalidOperationException("GameJourney: no board after ContinueTo");
            var to = CameraPose.Fitted(cam, grid, view.Rig);
            // The cleared board (with its island) moves to the left of the new one (and its island); the camera moves with
            // it, so nothing jumps on screen.
            Bounds newB = ground.GroundBounds(grid) ?? grid.Bounds;
            var shift = new Vector3(newB.min.x - GapCells * grid.CellSize - oldB.max.x, 0f, grid.Bounds.center.z - snap.Grid.Bounds.center.z);
            snap.Root.transform.position += shift;
            from = from.Moved(shift);
            from.Apply(cam);

            var walker = snap.Player;
            Vector3 walkFrom = walker != null ? walker.position : Vector3.zero;
            var newPlayer = view.Player;
            Vector3 walkTo = newPlayer != null ? newPlayer.transform.position : walkFrom;
            if (newPlayer != null) newPlayer.SetActive(false);
            float depth = RiseCells * grid.CellSize;
            var snapRoot = snap.Root.transform;
            Vector3 snapAt = snapRoot.position;

            runner.Play(AdvanceSeconds, t =>
            {
                CameraPose.Lerp(from, to, t).Apply(cam);
                root.localPosition = new Vector3(0f, -depth * (1f - TransitionClock.Ease(TransitionClock.Window(t, 0f, 0.67f))), 0f);
                if (walker != null)
                {
                    float w = TransitionClock.Window(t, 0.1f, 0.85f);
                    var p = Vector3.Lerp(walkFrom, walkTo, w);
                    p.y = walkFrom.y;
                    walker.position = p;
                    walker.gameObject.SetActive(w < 1f);
                }
                if (newPlayer != null) newPlayer.SetActive(t >= 0.85f);
                snapRoot.position = snapAt + Vector3.down * depth * TransitionClock.Ease(TransitionClock.Window(t, 0.6f, 1f));
            }, () =>
            {
                root.localPosition = Vector3.zero;
                if (newPlayer != null) newPlayer.SetActive(true);
                Object.Destroy(snap.Root);
                view.CameraHeld = false;
            });
        }

        // ---------- Planet complete ----------

        void OnPlanetCompleting(int stage) => Defer(() => Pullback(stage));

        void Pullback(int stage)
        {
            board.View.CameraHeld = true;
            var from = CameraPose.Capture(cam);
            var to = from.Zoomed(WideZoom);
            runner.Play(PullbackSeconds, t => CameraPose.Lerp(from, to, t).Apply(cam), () =>
            {
                int planet = planetOf(stage);
                if (planet >= 0) journeyReturn.Begin(planet, chapterOf(stage) ?? "");
                flow.OpenStageSelect(); // the scene changes; the held camera goes with it
            });
        }

        // ---------- ----------

        /// Runs <paramref name="act"/> on the next frame: the clear arrives inside the session's presentation callback, and
        /// loading a stage from there would re-enter the session.
        void Defer(Action act)
        {
            if (deferred || runner.Busy) throw new InvalidOperationException("GameJourney: a transition is already under way");
            deferred = true;
            runner.StartCoroutine(NextFrame(act));
        }

        IEnumerator NextFrame(Action act)
        {
            yield return null;
            deferred = false;
            act();
        }
    }
}
