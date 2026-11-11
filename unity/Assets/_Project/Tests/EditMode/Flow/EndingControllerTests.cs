using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// The ending cards as an overlay (docs/architecture.md 16.8): next, last card, skip, Esc.
    public sealed class EndingControllerTests
    {
        sealed class View : IEndingView
        {
            public readonly List<(int index, int count, string line)> Pages = new List<(int, int, string)>();
            public bool Visible;
            public event Action NextRequested, SkipRequested, CloseRequested;
            public void ShowPage(int index, int count, EndingPage page) => Pages.Add((index, count, page.Line));
            public void Show() => Visible = true;
            public void Hide() => Visible = false;
            public void Next() => NextRequested?.Invoke();
            public void Skip() => SkipRequested?.Invoke();
            public void Close() => CloseRequested?.Invoke();
        }

        static EndingModel Cards() => new EndingModel(new[]
        {
            new EndingPage("one", "ending.1", "e1"), new EndingPage("two", "ending.2", "e2"),
            new EndingPage("three", "ending.3", "e3"), new EndingPage("four", "ending.4", "e4"),
        }, 3, 7);

        OverlayStack overlays;
        View view;
        EndingController ctl;
        readonly List<(int seen, bool skipped)> finished = new List<(int, bool)>();

        [SetUp]
        public void SetUp()
        {
            overlays = new OverlayStack();
            view = new View();
            ctl = new EndingController(overlays, view);
            finished.Clear();
            ctl.Finished += (m, seen, skipped) => finished.Add((seen, skipped));
        }

        [TearDown] public void TearDown() => ctl.Dispose();

        [Test]
        public void NextThroughFourCardsThenFinished()
        {
            ctl.Begin(Cards());
            Assert.IsTrue(ctl.IsOpen && view.Visible && overlays.Contains(view));
            ctl.Begin(Cards()); // open: ignored
            for (int i = 1; i < 4; i++) view.Next();
            Assert.AreEqual(3, ctl.Page);
            CollectionAssert.AreEqual(new[] { (0, 4, "one"), (1, 4, "two"), (2, 4, "three"), (3, 4, "four") }, view.Pages);
            Assert.IsEmpty(finished);
            view.Next();
            Assert.IsFalse(ctl.IsOpen);
            Assert.IsFalse(overlays.Contains(view));
            Assert.AreEqual(-1, ctl.Page);
            CollectionAssert.AreEqual(new[] { (4, false) }, finished);
            ctl.Next(); // closed: nothing
            Assert.AreEqual(1, finished.Count);
        }

        [Test]
        public void SkipButtonEndsAtOnce()
        {
            ctl.Begin(Cards());
            view.Next();
            view.Skip();
            CollectionAssert.AreEqual(new[] { (2, true) }, finished);
            Assert.AreEqual(0, overlays.Count);
        }

        [Test]
        public void EscPoppingTheOverlayIsASkip()
        {
            ctl.Begin(Cards());
            new BackNavigator(overlays, () => Assert.Fail("Esc reached the flow")).Back();
            CollectionAssert.AreEqual(new[] { (1, true) }, finished);
            Assert.IsFalse(ctl.IsOpen);
        }

        [Test]
        public void ModelChecksItsCards()
        {
            Assert.Throws<ArgumentException>(() => new EndingModel(new[] { new EndingPage("one", "", "e1") }, 0, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EndingModel(Cards().Pages, 8, 7));
            Assert.Throws<ArgumentException>(() => new EndingPage("", "", "e1"));
            Assert.Throws<ArgumentNullException>(() => ctl.Begin(null));
        }
    }
}
