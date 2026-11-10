using NUnit.Framework;
using SevenSteps.Contracts;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 7.2: TileVisual.labelScale is 0 (view default) or a fraction of the projected cell side.
    public sealed class TileVisualContractTests
    {
        [Test]
        public void LabelScaleRange()
        {
            Assert.IsTrue(TileVisual.IsValidLabelScale(0f), "0 = default");
            Assert.IsTrue(TileVisual.IsValidLabelScale(0.2f));
            Assert.IsTrue(TileVisual.IsValidLabelScale(0.64f));
            Assert.IsTrue(TileVisual.IsValidLabelScale(0.8f));
            Assert.IsFalse(TileVisual.IsValidLabelScale(0.1f), "too small to read");
            Assert.IsFalse(TileVisual.IsValidLabelScale(6.4f), "typo for 0.64");
            Assert.IsFalse(TileVisual.IsValidLabelScale(-0.5f));
            Assert.AreEqual(0f, new TileVisual().labelScale, "existing assets read as the default");
        }
    }
}
