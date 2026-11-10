using NUnit.Framework;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// HudView.BoardViewport (docs/architecture.md 14.6): the board frame in viewport fractions for any screen shape.
    public sealed class BoardFrameTests
    {
        [Test]
        public void SixteenByNineIsTheFrameItself()
        {
            var v = HudView.BoardViewport(16f / 9f);
            var f = HudView.BoardFrame;
            Assert.AreEqual(f.xMin / 1280f, v.xMin, 1e-4f);
            Assert.AreEqual(f.xMax / 1280f, v.xMax, 1e-4f);
            Assert.AreEqual(1f - f.yMax / 720f, v.yMin, 1e-4f);
            Assert.AreEqual(1f - f.yMin / 720f, v.yMax, 1e-4f);
        }

        [Test]
        public void SixteenByTenKeepsTheMarginsInCanvasUnits()
        {
            const float aspect = 16f / 10f;
            var v = HudView.BoardViewport(aspect);
            float w = Mathf.Sqrt(1280f * 720f * aspect), h = Mathf.Sqrt(1280f * 720f / aspect);
            var f = HudView.BoardFrame;
            Assert.AreEqual(f.xMin, v.xMin * w, 1e-2f, "left column width");
            Assert.AreEqual(1280f - f.xMax, (1f - v.xMax) * w, 1e-2f, "right column width");
            Assert.AreEqual(f.yMin, (1f - v.yMax) * h, 1e-2f, "top band");
            Assert.AreEqual(720f - f.yMax, v.yMin * h, 1e-2f, "footer band");
            Assert.Less(v.width, HudView.BoardViewport(16f / 9f).width, "a narrower screen leaves a narrower share for the board");
        }

        [Test]
        public void RejectsABadAspect()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HudView.BoardViewport(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HudView.BoardViewport(float.NaN));
        }
    }
}
