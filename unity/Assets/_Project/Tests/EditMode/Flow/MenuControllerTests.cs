using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// The minimal HUD's one menu (production/ui-minimal-plan.md): settings, stages, solution, controls.
    public sealed class MenuControllerTests
    {
        bool hasLine;
        sealed class FakeMenu : IMenuPanel
        {
            public bool Shown, ReplayShown = true;
            public event Action SettingsRequested, StagesRequested, SolutionRequested, ControlsRequested, ReplayLineRequested, CloseRequested;
            public void ShowReplayLine(bool shown) => ReplayShown = shown;
            public void ReplayLine() => ReplayLineRequested?.Invoke();
            public void Show() => Shown = true;
            public void Hide() => Shown = false;
            public void Settings() => SettingsRequested?.Invoke();
            public void Stages() => StagesRequested?.Invoke();
            public void Solution() => SolutionRequested?.Invoke();
            public void Controls() => ControlsRequested?.Invoke();
            public void Close() => CloseRequested?.Invoke();
        }

        sealed class FakeOverlay : IOverlay
        {
            public event Action CloseRequested;
            public void Show() { }
            public void Hide() { }
            public void Close() => CloseRequested?.Invoke();
        }

        OverlayStack overlays;
        FakeMenu panel;
        FakeOverlay settings;
        List<string> calls;
        MenuController menu;

        [SetUp]
        public void SetUp()
        {
            overlays = new OverlayStack();
            panel = new FakeMenu();
            settings = new FakeOverlay();
            calls = new List<string>();
            menu = new MenuController(overlays, panel,
                () => { calls.Add("settings"); overlays.Push(settings); },
                () => calls.Add("stages"), () => calls.Add("solution"), () => calls.Add("controls"),
                () => hasLine, () => { calls.Add("line open " + menu.IsOpen); return hasLine; });
            hasLine = true;
        }

        [TearDown]
        public void TearDown() => menu.Dispose();

        [Test]
        public void OpensAsAnOverlayAndClosesWithEsc()
        {
            menu.Open();
            Assert.IsTrue(menu.IsOpen && panel.Shown);
            Assert.AreEqual(1, overlays.Count); // board input blocked
            new BackNavigator(overlays, () => calls.Add("flow back")).Back();
            Assert.IsFalse(menu.IsOpen || panel.Shown);
            CollectionAssert.IsEmpty(calls);
        }

        [Test]
        public void ToggleAndCloseButton()
        {
            menu.Toggle();
            Assert.IsTrue(menu.IsOpen);
            menu.Toggle();
            Assert.IsFalse(menu.IsOpen);
            menu.Open();
            panel.Close();
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void EachItemClosesTheMenuFirst()
        {
            foreach (var (press, name) in new (Action, string)[] { (panel.Stages, "stages"), (panel.Solution, "solution"), (panel.Controls, "controls") })
            {
                calls.Clear();
                menu.Open();
                press();
                Assert.IsFalse(menu.IsOpen, name);
                Assert.AreEqual(0, overlays.Count, name);
                CollectionAssert.AreEqual(new[] { name }, calls);
            }
        }

        [Test]
        public void SettingsOpensOverTheGame()
        {
            menu.Open();
            panel.Settings();
            Assert.AreSame(settings, overlays.Top);
            Assert.AreEqual(1, overlays.Count);
            settings.Close();
            Assert.AreEqual(0, overlays.Count);
        }

        [Test]
        public void NotOverAnotherOverlayAndNoLateClicks()
        {
            overlays.Push(settings);
            menu.Open();
            Assert.IsFalse(menu.IsOpen);
            settings.Close();
            panel.Solution(); // menu closed: ignored
            CollectionAssert.IsEmpty(calls);
        }

        [Test]
        public void StartLineRowOnlyWhenTheStageHasOneAndItClosesTheMenuFirst()
        {
            hasLine = false; // no start line, or a result is up (review ebdb653 N1)
            menu.Open();
            Assert.IsFalse(panel.ReplayShown);
            menu.Toggle();
            hasLine = true;
            menu.Open();
            Assert.IsTrue(panel.ReplayShown);
            panel.ReplayLine();
            Assert.IsFalse(menu.IsOpen);
            CollectionAssert.AreEqual(new[] { "line open False" }, calls);
        }

        [Test]
        public void StartLineIsOptional()
        {
            var bare = new MenuController(overlays, new FakeMenu(), () => { }, () => { }, () => { }, () => { });
            bare.Open();
            Assert.Throws<ArgumentException>(() => new MenuController(overlays, new FakeMenu(), () => { }, () => { }, () => { }, () => { }, () => true));
            bare.Dispose();
        }
    }
}
