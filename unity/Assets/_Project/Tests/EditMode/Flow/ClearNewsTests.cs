using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// <summary>ClearNews (architect 2026-11-12, the U5 first-clear trace): merge per stage, take a planet's range, asserts.</summary>
    public sealed class ClearNewsTests
    {
        readonly List<ClearNote> taken = new List<ClearNote>();

        [Test]
        public void RecordsMergePerStageUntilTaken()
        {
            var n = new ClearNews();
            n.Record(4, true, 0, 1);
            n.Record(4, false, 1, 3); // replayed before the surface opened
            n.Record(9, false, 2, 3);
            Assert.AreEqual(2, n.Count, "one note per stage, not a single slot");
            Assert.IsTrue(n.TryTake(new[] { 3, 4, 5 }, taken));
            Assert.AreEqual(1, taken.Count, "only the planet's stages");
            var note = taken[0];
            Assert.AreEqual(4, note.Stage);
            Assert.IsTrue(note.FirstClear, "first clear OR");
            Assert.AreEqual(0, note.BestBefore, "the first best before");
            Assert.AreEqual(3, note.BestAfter, "the latest best after");
            Assert.IsFalse(n.TryTake(new[] { 3, 4, 5 }, taken), "taken once");
            Assert.AreEqual(0, taken.Count, "into is cleared");
            Assert.AreEqual(1, n.Count, "stage 9 stays for its planet");
            n.Clear();
            Assert.AreEqual(0, n.Count);
        }

        [Test]
        public void NothingNewIsIgnoredAndBadNotesThrow()
        {
            var n = new ClearNews();
            n.Record(2, false, 2, 2);
            Assert.AreEqual(0, n.Count, "a replay with the same best");
            n.Record(2, true, 0, 0); // a first clear with no stars is news
            Assert.AreEqual(1, n.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => n.Record(-1, true, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => n.Record(1, false, 0, 4));
            Assert.Throws<ArgumentException>(() => n.Record(1, false, 3, 2), "best never goes down");
            Assert.Throws<ArgumentException>(() => n.Record(1, true, 1, 2), "a first clear had no best");
            Assert.Throws<ArgumentNullException>(() => n.TryTake(null, taken));
            Assert.Throws<ArgumentNullException>(() => n.TryTake(new[] { 1 }, null));
        }
    }
}
