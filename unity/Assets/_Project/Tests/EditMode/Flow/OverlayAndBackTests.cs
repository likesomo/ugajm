using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// S4 (docs/scene-flow.md 5): overlays, Esc in one place, settings saved on change, chapter hook.
    public sealed class OverlayAndBackTests
    {
        sealed class FakeOverlay : IOverlay
        {
            public bool Visible;
            public event Action CloseRequested;
            public void Show() => Visible = true;
            public void Hide() => Visible = false;
            public void ClickClose() => CloseRequested?.Invoke();
        }

        [Test]
        public void StackOpensAndClosesTopFirst()
        {
            var stack = new OverlayStack();
            var settings = new FakeOverlay();
            var credits = new FakeOverlay();
            int changes = 0;
            stack.Changed += () => changes++;

            stack.Push(credits);
            stack.Push(settings);
            Assert.AreEqual(2, stack.Count);
            Assert.AreSame(settings, stack.Top);
            Assert.IsTrue(settings.Visible && credits.Visible);
            Assert.Throws<InvalidOperationException>(() => stack.Push(settings), "already open");
            Assert.Throws<InvalidOperationException>(() => stack.Close(credits), "not the top one");

            settings.ClickClose();     // its own close button
            Assert.IsFalse(settings.Visible);
            Assert.AreSame(credits, stack.Top);
            Assert.IsTrue(stack.Pop());
            Assert.IsFalse(credits.Visible);
            Assert.IsFalse(stack.Pop());
            Assert.AreEqual(4, changes);

            settings.ClickClose();     // closed overlays are unsubscribed: nothing happens
            Assert.AreEqual(0, stack.Count);
        }

        [Test]
        public void BackClosesTheOverlayBeforeLeavingTheScreen()
        {
            var stack = new OverlayStack();
            int flowBacks = 0;
            var back = new BackNavigator(stack, () => flowBacks++);
            var settings = new FakeOverlay();
            stack.Push(settings);
            back.Back();
            Assert.IsFalse(settings.Visible);
            Assert.AreEqual(0, flowBacks, "first Esc only closes settings");
            back.Back();
            Assert.AreEqual(1, flowBacks);
        }

        [Test]
        public void EscOrderOnStageSelect()
        {
            // Stage Select with settings open: Esc closes settings, the next Esc goes to Title.
            var flow = new AppFlow(new SameScene(), 17);
            flow.Go(AppState.Title);
            flow.Go(AppState.StageSelect);
            var stack = new OverlayStack();
            var back = new BackNavigator(stack, () => { if (flow.CanBack && flow.TryGetBackTarget(out var t)) flow.Go(t); });
            stack.Push(new FakeOverlay());
            back.Back();
            Assert.AreEqual(AppState.StageSelect, flow.State);
            back.Back();
            Assert.AreEqual(AppState.Title, flow.State);
            back.Back();               // Title has no back target
            Assert.AreEqual(AppState.Title, flow.State);
        }

        sealed class SameScene : ISceneLoader
        {
            public string Current => AppScenes.Title;
            public void Load(string sceneName) => throw new InvalidOperationException("no load expected");
            public event Action<string> Loaded { add { } remove { } }
        }

        [Test]
        public void SettingsAreSavedAtEveryChange()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            int changed = 0;
            save.SettingsChanged += _ => changed++;
            TestTheme.Use();
            var canvas = new UnityEngine.GameObject("canvas", typeof(UnityEngine.RectTransform));
            try
            {
                var view = SettingsPanel.Create(canvas.transform, save, telemetryOnByDefault: true);
                using var ctl = new SettingsController(save, view, telemetryOnByDefault: true);
                save.Settings.masterVolume = 1f;
                Press(canvas, "전체 음량 -");
                Assert.AreEqual(0.9f, save.Settings.masterVolume, 1e-4f);
                Assert.AreEqual(1, changed, "saved at once, not on close");
                Assert.AreEqual(0.9f, new SaveSystem(store).Settings.masterVolume, 1e-4f, "persisted");

                save.Settings.playerId = "abc";
                Press(canvas, "TelemetryToggle");   // default on -> off
                Assert.AreEqual(2, save.Settings.telemetryChoice);
                Assert.AreEqual("", save.Settings.playerId, "opt-out forgets the id");
                Assert.IsFalse(save.TelemetryOn(true));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        static void Press(UnityEngine.GameObject root, string name)
        {
            foreach (var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (b.name == name) { b.onClick.Invoke(); return; }
            Assert.Fail($"button {name}");
        }
    }
}
