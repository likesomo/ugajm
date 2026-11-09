using System;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// App flow, save/settings, launch options, services (docs/architecture.md 11장). Owned by the architect.
    public sealed class AppFrameworkTests
    {
        sealed class FakeScenes : ISceneLoader
        {
            public string Current { get; set; } = AppScenes.Boot;
            public int Loads;
            public void Load(string sceneName) { Current = sceneName; Loads++; Loaded?.Invoke(sceneName); }
            public event Action<string> Loaded;
        }

        [Test]
        public void FlowLoadsSceneOnlyWhenItChanges()
        {
            var scenes = new FakeScenes();
            var flow = new AppFlow(scenes, 17);
            flow.Go(AppState.Title);
            flow.Go(AppState.StageSelect);
            Assert.AreEqual(1, scenes.Loads, "Title and StageSelect share a scene");
            flow.GoToGame(3);
            flow.Go(AppState.Result);
            flow.GoToGame(4);
            Assert.AreEqual(2, scenes.Loads, "Game and Result share a scene");
            Assert.AreEqual(4, flow.Stage);
            Assert.Throws<InvalidOperationException>(() => flow.Go(AppState.Boot));
        }

        [Test]
        public void SaveRoundTripAndLegacyMigration()
        {
            var store = new MemoryStore();
            store.Set("seven-steps-cleared", "0,2");
            var save = new SaveSystem(store);
            Assert.AreEqual(2, save.Progress.ClearedCount, "v0 list migrated by the frozen 17-stage names (11.3.1)");
            var five = Level.Parse(ProgressMigration.V1StageNames[5], "", 3, new[] { "#####", "#S.E#", "#####" });

            save.Progress.MarkCleared(five, 3);
            save.Progress.MarkCleared(five, 2);
            save.Settings.sfxVolume = 0.25f;
            save.SaveProgress();
            save.SaveSettings();

            var again = new SaveSystem(store);
            Assert.IsTrue(again.Progress.IsCleared(five));
            Assert.AreEqual(2, again.Progress.BestBodies(five));
            Assert.AreEqual(0.25f, again.Settings.sfxVolume);
        }

        [Test]
        public void BrokenSaveFallsBackToDefaults()
        {
            var store = new MemoryStore();
            store.Set(SaveSystem.SettingsKey, "{not json");
            var save = new SaveSystem(store);
            Assert.AreEqual(1f, save.Settings.masterVolume);
        }

        [Test]
        public void LaunchOptionsFromArgsAndUrl()
        {
            var a = LaunchOptions.Parse(new[] { "game.exe", "-level", "8", "-replay", "LRRK" }, "");
            Assert.AreEqual(8, a.Level);
            Assert.AreEqual("LRRK", a.Replay);
            var u = LaunchOptions.Parse(new string[0], "https://x.itch.io/g/index.html?level=3&replay=DUR&telemetry=0");
            Assert.AreEqual(3, u.Level);
            Assert.AreEqual("DUR", u.Replay);
            Assert.IsTrue(u.TelemetryOff);
            Assert.IsFalse(LaunchOptions.Parse(null, null).DirectToGame);
        }

        [Test]
        public void ServicesRegisterAndGet()
        {
            Services.Clear();
            Services.Register<ITelemetry>(new NullTelemetry());
            Assert.IsFalse(Services.Get<ITelemetry>().Enabled);
            Assert.IsFalse(Services.TryGet<ISaveStore>(out _));
            Assert.Throws<InvalidOperationException>(() => Services.Get<ISaveStore>());
            Services.Clear();
        }

        [Test]
        public void ServicesResetForPlayDropsPreviousPlay()
        {
            Services.Clear();
            Services.Register<ITelemetry>(new NullTelemetry());
            Services.ResetForPlay();
            Assert.IsFalse(Services.Has<ITelemetry>());
        }
    }
}
