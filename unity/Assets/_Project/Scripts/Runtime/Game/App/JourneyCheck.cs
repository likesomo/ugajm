using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.Planets;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// -journeycheck advance|complete (smoke, docs/stage-select-transitions.md): checks the stage select v4 journey in a real
    /// player, with -newmap -level N -replay (the solver's path). The replay's moves but the last are snapped and the last is
    /// played, so the win is presented like a player's (as FlowCheck does). Then:
    /// advance: the next stage of the planet is entered with no result panel, and once the transition settles the snapshot is
    /// gone, the board root is home, the camera is the board's again and the player is visible.
    /// complete: the Game scene pulls back and the Title scene shows the star map; the pullback settles JourneyReturn and the
    /// map lights the trail (TrailLit); the camera ends on the map pose.
    /// Logs "[SevenSteps] Journey check KIND: OK" or "... FAIL: reasons", saves KIND-mid / KIND-end screenshots, and quits.
    /// </summary>
    public static class JourneyCheck
    {
        const float Timeout = 20f;

        static string ShotBase(string screenshot) =>
            string.IsNullOrEmpty(screenshot) ? null : screenshot.Substring(0, screenshot.Length - (screenshot.EndsWith(".png") ? 4 : 0));

        /// Game scene: plays the replay, waits for the clear (the journey never skips the result, lead 2026-11-12) and presses
        /// Next (<paramref name="next"/>, GameFlowController.Next). For advance it then checks the arrival; for complete it only
        /// fails if the scene stays.
        public static IEnumerator RunGame(string kind, AppFlow flow, BoardController board, GameJourney journey, ResultView result, OverlayStack overlays,
            string moves, string screenshot, Action next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            var fails = new List<string>();
            string shot = ShotBase(screenshot);
            int stage = flow.Stage;
            if (journey == null) { Finish(kind, new List<string> { "journey is off (needs -newmap, not -journey off)" }); yield break; }

            yield return new WaitForSeconds(0.5f);
            while (overlays.Count > 0) overlays.Pop(); // a chapter card of this launch
            if (moves.Length > 1) board.Replay(moves.Substring(0, moves.Length - 1));
            if (!Rules.TryParseLetter(moves[moves.Length - 1], out var act)) { Finish(kind, new List<string> { "last replay letter is not a move" }); yield break; }
            board.Send(Command.FromAct(act));

            float t0 = Time.unscaledTime;
            while (flow.State != AppState.Result && Time.unscaledTime - t0 < Timeout) yield return null;
            if (flow.State != AppState.Result) { Finish(kind, new List<string> { $"no clear after the replay (state {flow.State}, status {board.State?.Status})" }); yield break; }
            yield return new WaitForSecondsRealtime(0.3f);
            next();
            t0 = Time.unscaledTime;
            while (!journey.Blocks && Time.unscaledTime - t0 < Timeout) yield return null;
            if (!journey.Blocks) { Finish(kind, new List<string> { $"no transition after Next (state {flow.State}, status {board.State?.Status})" }); yield break; }
            yield return null;
            if (kind == "complete")
            {
                // The pullback ends in a scene change: this coroutine dies with the Game scene; the Title scene finishes the check.
                if (shot != null) ScreenCapture.CaptureScreenshot(shot + "-pullback.png");
                t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < Timeout) yield return null;
                Finish(kind, new List<string> { $"still in the Game scene {Timeout} s after the clear (state {flow.State})" });
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.6f);
            if (shot != null) ScreenCapture.CaptureScreenshot(shot + "-mid.png");
            t0 = Time.unscaledTime;
            while (journey.Blocks && Time.unscaledTime - t0 < Timeout) yield return null;
            yield return null;

            var view = board.View;
            if (flow.State != AppState.Game) fails.Add($"state {flow.State}, want Game");
            if (flow.Stage != stage + 1) fails.Add($"stage {flow.Stage + 1}, want {stage + 2}");
            if (result.IsShown) fails.Add("the result panel is up");
            if (journey.Blocks) fails.Add("the transition did not settle");
            if (view.CameraHeld) fails.Add("the camera is still held");
            if (view.BoardRoot == null || view.BoardRoot.localPosition != Vector3.zero) fails.Add($"board root at {view.BoardRoot?.localPosition}");
            if (view.Player == null || !view.Player.activeSelf) fails.Add("the player is hidden");
            if (GameObject.Find("Board (snapshot)") != null) fails.Add("the cleared board's snapshot is still there");
            if (shot != null) { ScreenCapture.CaptureScreenshot(shot + "-end.png"); yield return new WaitForSecondsRealtime(0.5f); }
            Finish(kind, fails);
        }

        /// Title scene, after a planet-complete return began (TitleScreen.Start): the pullback, JourneyReturn and the trail.
        public static IEnumerator RunTitle(AppFlow flow, StageSelectController select, PlanetMap3DView map, JourneyReturn journeyReturn, string screenshot)
        {
            var fails = new List<string>();
            string shot = ShotBase(screenshot);
            bool lit = false;
            map.TrailLit += () => lit = true;
            if (!journeyReturn.Pending) fails.Add("no planet-complete return was pending when the Title scene started");

            yield return new WaitForSecondsRealtime(0.45f);
            if (shot != null) ScreenCapture.CaptureScreenshot(shot + "-mid.png");
            float t0 = Time.unscaledTime;
            while ((journeyReturn.Pending || !lit) && Time.unscaledTime - t0 < Timeout) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            if (flow.State != AppState.StageSelect) fails.Add($"state {flow.State}, want StageSelect");
            if (journeyReturn.Pending) fails.Add("JourneyReturn never settled");
            if (!lit) fails.Add("the map did not light the trail (TrailLit)");
            select.MapPose(out var position, out _, out float size);
            var cam = select.MapCamera;
            if (cam == null) fails.Add("no map camera");
            else if (Vector3.Distance(cam.transform.position, position) > 0.01f || Mathf.Abs(cam.orthographicSize - size) > 0.01f)
                fails.Add($"camera at {cam.transform.position} size {cam.orthographicSize}, map pose {position} size {size}");
            if (shot != null) { ScreenCapture.CaptureScreenshot(shot + "-end.png"); yield return new WaitForSecondsRealtime(0.5f); }
            Finish("complete", fails);
        }

        static void Finish(string kind, List<string> fails)
        {
            Debug.Log(fails.Count == 0 ? $"[SevenSteps] Journey check {kind}: OK" : $"[SevenSteps] Journey check {kind}: FAIL: {string.Join("; ", fails)}");
            Application.Quit();
        }
    }
}
