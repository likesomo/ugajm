using System;
using System.Collections;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// -newgamecheck (with -newplayer; unity.sh smoke, lead 2026-11-13): the new game path end to end in a player build.
    /// Title: play on the fresh save, the prologue read through line by line. Game: each tutorial board solved with its
    /// solver path (levels.json expected.path) through the real board input, the run moving on by itself, until stage 1
    /// is on the board. Logs "[SevenSteps] New game check: arrived stage 1 via tutorial" or "New game check: FAIL ..."
    /// and quits.
    /// </summary>
    public static class NewGameCheck
    {
        public const string Ok = "[SevenSteps] New game check: arrived stage 1 via tutorial";
        const float BoardTimeout = 20f, TitleTimeout = 15f;

        static void Fail(string why)
        {
            Debug.Log("[SevenSteps] New game check: FAIL " + why);
            Application.Quit();
        }

        /// The Title half: <paramref name="play"/> presses the play stone; the prologue must open and is read to its end.
        public static IEnumerator RunTitle(OverlayStack overlays, Action play, Func<CutsceneController> cutscene)
        {
            yield return new WaitForSecondsRealtime(1f);
            while (overlays.Count > 0) overlays.Pop(); // e.g. the first-run language picker
            play();
            yield return null;
            var c = cutscene();
            if (c == null || !c.IsOpen) { Fail("the prologue did not open on play (needs -newplayer and cutscenes.json)"); yield break; }
            int presses = 0;
            while (c.IsOpen)
            {
                if (++presses > 60) { Fail("the prologue did not end after 60 presses"); yield break; }
                c.Next();
                yield return null;
            }
            Debug.Log($"[SevenSteps] New game check: prologue read ({presses} presses)");
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < TitleTimeout) yield return null; // the Game scene takes over (this object goes)
            Fail("still on the title after the prologue");
        }

        /// The Game half: every tutorial board from the one on the board, solved, then stage 1.
        public static IEnumerator RunGame(AppFlow flow, BoardController board, TutorialRunController run)
        {
            yield return null;
            if (run == null || !flow.InTutorial || !run.Active) { Fail($"the Game scene did not open on a tutorial board (state {flow.State}, tutorial {flow.Tutorial})"); yield break; }
            var boards = LevelCatalog.Tutorials;
            for (int i = run.Index; i < boards.Count; i++)
            {
                if (run.Index != i) { Fail($"expected tutorial board {i + 1}, on {run.Index + 1}"); yield break; }
                string path = boards[i].level.expected?.path ?? "";
                if (path.Length == 0) { Fail($"tutorial {boards[i].id} has no solver path"); yield break; }
                foreach (char letter in path)
                {
                    if (!Rules.TryParseLetter(letter, out var act)) { Fail($"tutorial {boards[i].id}: bad path letter '{letter}'"); yield break; }
                    float t = Time.unscaledTime;
                    while (board.Session.IsLocked || board.Session.QueuedCount > 0)
                    {
                        if (Time.unscaledTime - t > BoardTimeout) { Fail($"tutorial {boards[i].id}: the board stayed busy"); yield break; }
                        yield return null;
                    }
                    board.Send(Command.FromAct(act));
                    yield return null;
                }
                float t1 = Time.unscaledTime;
                while (run.Index == i)
                {
                    if (Time.unscaledTime - t1 > BoardTimeout)
                    {
                        Fail($"tutorial {boards[i].id}: no win after its path (status {board.State?.Status})");
                        yield break;
                    }
                    yield return null;
                }
                Debug.Log($"[SevenSteps] New game check: tutorial {boards[i].id} cleared");
            }
            float t2 = Time.unscaledTime;
            while (flow.InTutorial || flow.IsLoading || board.LevelIndex != 0)
            {
                if (Time.unscaledTime - t2 > BoardTimeout) { Fail($"no stage 1 after the tutorial (state {flow.State}, stage {flow.Stage + 1}, board {board.LevelIndex + 1})"); yield break; }
                yield return null;
            }
            Debug.Log(Ok);
            Application.Quit();
        }
    }
}
