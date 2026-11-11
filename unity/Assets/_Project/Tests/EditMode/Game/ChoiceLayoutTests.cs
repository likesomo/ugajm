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
        public void NoRoomIsReported()
        {
            var board = Rect.MinMaxRect(40, 40, 1240, 680);
            HudView.LayoutChoice(board, new Vector2(640, 360), Canvas, guide: true, out _, out bool fits);
            Assert.IsFalse(fits);
        }

        [Test]
        public void ConfirmFitsTheLimit()
        {
            Assert.LessOrEqual(BoardView.ConfirmSeconds(false) + 0.12f, ChoicePreview.ConfirmSecondsMax, "burst + a door's 0.12 s");
            Assert.AreEqual(0f, BoardView.ConfirmSeconds(true), "reduce motion");
        }
    }
}
