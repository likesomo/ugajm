using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// HudView.LayoutChoice (docs 14.11): the KEEP / SWITCH group goes beside the board area, on the side nearest the
    /// brother that fits; never on the board. BoardView.ConfirmSeconds stays within ChoicePreview.ConfirmSecondsMax.
    public sealed class ChoiceLayoutTests
    {
        static readonly Vector2 Canvas = new Vector2(1280, 720);

        static void AssertClear(Rect r, Rect board, Vector2 canvas)
        {
            Assert.IsFalse(r.Overlaps(board), $"cards {r} on the board {board}");
            Assert.GreaterOrEqual(r.xMin, 24f); Assert.GreaterOrEqual(r.yMin, 24f);
            Assert.LessOrEqual(r.xMax, canvas.x - 24f); Assert.LessOrEqual(r.yMax, canvas.y - 24f);
        }

        [Test]
        public void BrotherOnTheLeftGetsTheLeftMargin()
        {
            var board = Rect.MinMaxRect(300, 90, 980, 570); // an 8 x 6 board in the minimal frame
            var r = HudView.LayoutChoice(board, new Vector2(330, 300), Canvas, guide: true, out bool vertical, out bool fits);
            Assert.IsTrue(fits);
            Assert.IsTrue(vertical);
            Assert.Less(r.xMax, board.xMin);
            AssertClear(r, board, Canvas);
            Assert.LessOrEqual(r.yMax, 636f, "clear of undo / restart");
        }

        [Test]
        public void BrotherOnTheRightGetsTheRightMargin()
        {
            var board = Rect.MinMaxRect(300, 90, 980, 570);
            var r = HudView.LayoutChoice(board, new Vector2(950, 500), Canvas, guide: false, out bool vertical, out bool fits);
            Assert.IsTrue(fits);
            Assert.Greater(r.xMin, board.xMax);
            Assert.GreaterOrEqual(r.yMin, 140f, "clear of the statue plate");
            AssertClear(r, board, Canvas);
        }

        [Test]
        public void WideBoardPutsTheCardsAboveOrBelow()
        {
            var board = Rect.MinMaxRect(100, 200, 1180, 430); // wider than the side margins allow, short
            var r = HudView.LayoutChoice(board, new Vector2(640, 320), Canvas, guide: false, out bool vertical, out bool fits);
            Assert.IsTrue(fits);
            Assert.IsFalse(vertical, "side by side above or below");
            Assert.IsTrue(r.yMax < board.yMin || r.yMin > board.yMax);
            AssertClear(r, board, Canvas);
        }

        [Test]
        public void VisibleHudPlatesAreAvoided()
        {
            // 17 x 3 (stage 14) with the explained cards: only the bottom candidate fits, and the message plate is there.
            var board = Rect.MinMaxRect(100, 200, 1180, 430);
            var v = new Vector2(HudView.CardW, 2 * HudView.CardH + HudView.CardGap);
            var h = new Vector2(2 * HudView.CardW + HudView.CardGap, HudView.CardH);
            var r = HudView.LayoutChoice(board, new Vector2(640, 320), Canvas, v, h, null, out _, out bool fits);
            Assert.IsTrue(fits);
            var message = Rect.MinMaxRect(280, 590, 1000, 696); // the longest message (H solution), several lines
            Assert.IsTrue(r.Overlaps(message), "the case b619811 found");
            HudView.LayoutChoice(board, new Vector2(640, 320), Canvas, v, h, new[] { message }, out _, out fits);
            Assert.IsFalse(fits, "no candidate clears the message: the HUD hides the message while the cards show");
        }

        [Test]
        public void NoRoomIsReported()
        {
            var board = Rect.MinMaxRect(40, 40, 1240, 680);
            HudView.LayoutChoice(board, new Vector2(640, 360), Canvas, guide: true, out _, out bool fits);
            Assert.IsFalse(fits);
        }

        [Test]
        public void ConfirmFitsTheLimit()
        {
            Assert.LessOrEqual(BoardView.ConfirmPresentMax(false), ChoicePreview.ConfirmSecondsMax, "burst + one wait for all doors (review R2)");
            Assert.AreEqual(0f, BoardView.ConfirmPresentMax(true), "reduce motion: no burst, no door wait");
            Assert.AreEqual(0f, BoardView.ConfirmSeconds(true), "reduce motion");
        }
    }
}
