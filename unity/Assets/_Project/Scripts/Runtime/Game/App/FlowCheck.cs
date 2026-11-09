using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// -flowcheck (development / smoke, docs/scene-flow.md S8): checks the stage-end flow in a real player.
    /// The replay's moves but the last are snapped, the last is played (so the win is presented like a player's), then:
    /// the Result shows; its text names the planet when this was the planet's last stage (result.chapter_done) or ends
    /// the journey on the last stage (result.journey_done), and neither otherwise; Next enters the next stage; at a
    /// planet boundary the chapter entry card shows the new planet and the chapter look switches to its preset.
    /// Logs "[SevenSteps] Flow check stage N: OK" or "... FAIL: reasons", saves screenshots, and quits.
    /// </summary>
    public sealed class FlowCheck
    {
        const float Timeout = 20f;
        /// Result star reveal: 0.25 s wait + 0.22 s pop per star (ResultView), about 1.4 s for three; a slow first frame can
        /// shift it, so the screenshot waits longer (core finding 2026-11-10: 1 s caught the third star still empty).
        const float RevealSeconds = 2.2f;

        readonly AppFlow flow;
        readonly BoardController board;
        readonly GameFlowController flowController;
        readonly OverlayStack overlays;
        readonly ResultView result;
        readonly ChapterStage chapterStage;
        readonly Localizer loc;
        readonly Func<int, ChapterPreset> presetFor;
        ChapterCardModel cardShown;
        int stars = -1;

        public FlowCheck(AppFlow flow, BoardController board, GameFlowController flowController, OverlayStack overlays, ResultView result,
            ChapterStage chapterStage, ChapterIntroController chapterIntro, Localizer loc, Func<int, ChapterPreset> presetFor)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.flowController = flowController ?? throw new ArgumentNullException(nameof(flowController));
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.result = result != null ? result : throw new ArgumentNullException(nameof(result));
            this.chapterStage = chapterStage ?? throw new ArgumentNullException(nameof(chapterStage));
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            this.presetFor = presetFor ?? throw new ArgumentNullException(nameof(presetFor));
            if (chapterIntro == null) throw new ArgumentNullException(nameof(chapterIntro));
            chapterIntro.Shown += m => cardShown = m;
        }

        public IEnumerator Run(string moves, string screenshot)
        {
            var fails = new List<string>();
            int stage = flow.Stage;
            string shotBase = string.IsNullOrEmpty(screenshot) ? null : screenshot.Substring(0, screenshot.Length - (screenshot.EndsWith(".png") ? 4 : 0));

            yield return new WaitForSeconds(0.5f);
            while (overlays.Count > 0) overlays.Pop(); // the first-entry chapter card of this launch
            if (moves.Length > 1) board.Replay(moves.Substring(0, moves.Length - 1));
            if (!Rules.TryParseLetter(moves[moves.Length - 1], out var act)) fails.Add($"last replay letter {moves[moves.Length - 1]} is not a move");
            else board.Send(Command.FromAct(act));

            float t0 = Time.unscaledTime;
            while (flow.State != AppState.Result && Time.unscaledTime - t0 < Timeout) yield return null;
            if (flow.State != AppState.Result) fails.Add($"no Result after the replay (state {flow.State}, status {board.State?.Status})");
            else
            {
                yield return new WaitForSecondsRealtime(RevealSeconds);
                var level = board.State?.Level;
                stars = level != null && Services.TryGet<SaveSystem>(out var save) ? save.Progress.BestStars(level) : -1;
                CheckResultText(stage, fails);
                if (shotBase != null) { ScreenCapture.CaptureScreenshot(shotBase + "-result.png"); yield return new WaitForSeconds(0.5f); }

                bool last = stage == flow.StageCount - 1;
                if (!last)
                {
                    bool boundary = LevelCatalog.ChapterKeyOf(stage) != LevelCatalog.ChapterKeyOf(stage + 1);
                    cardShown = null;
                    flowController.Next();
                    yield return new WaitForSeconds(1f);
                    if (flow.State != AppState.Game || flow.Stage != stage + 1) fails.Add($"Next did not enter stage {stage + 2} (state {flow.State}, stage {flow.Stage + 1})");
                    CheckChapterEntry(stage + 1, boundary, fails);
                    if (shotBase != null) { ScreenCapture.CaptureScreenshot(shotBase + "-next.png"); yield return new WaitForSeconds(0.5f); }
                }
            }

            Debug.Log(fails.Count == 0 ? $"[SevenSteps] Flow check stage {stage + 1}: OK (stars {stars})"
                : $"[SevenSteps] Flow check stage {stage + 1}: FAIL: {string.Join("; ", fails)}");
            Application.Quit();
        }

        void CheckResultText(int stage, List<string> fails)
        {
            string body = result.body != null ? result.body.text : "";
            bool last = stage == flow.StageCount - 1;
            bool boundary = last || LevelCatalog.ChapterKeyOf(stage) != LevelCatalog.ChapterKeyOf(stage + 1);
            string journey = loc.Get("result.journey_done");
            var planet = LevelCatalog.ChapterOf(stage);
            string planetDone = planet != null ? loc.Format("result.chapter_done", planet.Planet(loc.Current)) : null;
            if (!result.IsShown) fails.Add("result panel not shown");
            if (last && !body.Contains(journey)) fails.Add($"last stage: result has no journey_done ({Flat(body)})");
            if (!last && boundary && planetDone != null && !body.Contains(planetDone)) fails.Add($"planet end: result has no chapter_done for {planet.Planet(loc.Current)} ({Flat(body)})");
            if (!boundary && (body.Contains(journey) || (planetDone != null && body.Contains(planetDone)))) fails.Add($"mid-planet stage: result says the planet or journey ended ({Flat(body)})");
            if (result.next != null && result.next.gameObject.activeSelf == last) fails.Add(last ? "last stage shows Next" : "Next button hidden");
        }

        void CheckChapterEntry(int stage, bool boundary, List<string> fails)
        {
            string key = LevelCatalog.ChapterKeyOf(stage);
            if (boundary)
            {
                if (cardShown == null) fails.Add($"no chapter entry card for stage {stage + 1}");
                else if (cardShown.ChapterId != key) fails.Add($"chapter card shows {cardShown.ChapterId}, expected {key}");
                var expected = presetFor(stage);
                if (expected != null && chapterStage.Current != expected)
                    fails.Add($"chapter look is {(chapterStage.Current != null ? chapterStage.Current.id : "none")}, expected {expected.id}");
            }
            else if (cardShown != null) fails.Add($"chapter card shown inside a planet (stage {stage + 1})");
        }

        static string Flat(string s) => s.Replace("\n", " / ");
    }
}
