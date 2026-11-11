using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Star map cards on narrower canvases (QA 1: planet 7 past the safe margin at 1280x800; docs/architecture.md 14 4:3).
    public sealed class PlanetMapFitTests
    {
        const float CardW = 180, CardH = 124, Safe = 24;

        // PlanetMapView.Position for the default Dipper (MapX 14, MapW 1159, MapY 218, MapH 805).
        static Vector2[] Reference() => UiTheme.DefaultDipper.Select(d => new Vector2(14 + d.x * 1159, 218 + d.y * 805)).ToArray();

        [Test]
        public void At1280TheReferenceAlmostStays()
        {
            var r = Reference();
            var c = PlanetMapView.CardCenters(r, 1280);
            for (int i = 0; i < r.Length; i++) Assert.AreEqual(r[i].x, c[i].x, 20f, $"planet {i + 1}");
        }

        [TestCase(1280f)]
        [TestCase(1214f)] // 1280x800
        [TestCase(1108f)] // 1024x768
        public void NarrowerCanvasesKeepTheMarginAndNoOverlap(float width)
        {
            var c = PlanetMapView.CardCenters(Reference(), width);
            for (int i = 0; i < c.Length; i++)
            {
                Assert.GreaterOrEqual(c[i].x - CardW / 2, Safe - 0.01f, $"planet {i + 1} left");
                Assert.LessOrEqual(c[i].x + CardW / 2, width - Safe + 0.01f, $"planet {i + 1} right");
                for (int j = i + 1; j < c.Length; j++)
                    Assert.IsTrue(Mathf.Abs(c[i].x - c[j].x) >= CardW + 36 - 0.01f || Mathf.Abs(c[i].y - c[j].y) >= CardH + 12, $"planets {i + 1} and {j + 1}: no room for a line");
            }
        }
    }
}
