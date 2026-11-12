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
            // -tutorial N / ?tutorial=N (docs/tutorial.md 2.4): one tutorial board at once; with -replay for QA.
            var t = LaunchOptions.Parse(new[] { "game.exe", "-tutorial", "2", "-replay", "RRR" }, "");
            Assert.AreEqual(2, t.Tutorial);
            Assert.AreEqual(0, t.Level);
            Assert.IsEmpty(t.Warnings);
            Assert.AreEqual(3, LaunchOptions.Parse(new string[0], "https://x.itch.io/g/index.html?tutorial=3").Tutorial);
            var bad = LaunchOptions.Parse(new[] { "game.exe", "-tutorial", "0" }, "https://x/?tutorial=x");
            Assert.AreEqual(0, bad.Tutorial);
            Assert.AreEqual(2, bad.Warnings.Count, "both ignored with a warning");
            Assert.AreEqual(0, LaunchOptions.Parse(null, null).Tutorial);
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

        [Test]
        public void ArtPerfParses()
        {
            Assert.IsTrue(LaunchOptions.Parse(new[] { "x.exe", "-artperf" }, "").ArtPerf);
            Assert.IsTrue(LaunchOptions.Parse(null, "http://h/?level=3&artperf=1").ArtPerf);
            Assert.IsFalse(LaunchOptions.Parse(null, "http://h/?artperf=0").ArtPerf);
            Assert.IsFalse(LaunchOptions.Parse(null, "http://h/?xartperf=1").ArtPerf, "whole parameter name only");
            var bad = LaunchOptions.Parse(null, "http://h/?artperf=10");
            Assert.IsFalse(bad.ArtPerf);
            Assert.AreEqual(1, bad.Warnings.Count);
            Assert.IsFalse(LaunchOptions.Parse(null, "").ArtPerf);
        }

        [Test]
        public void SaveKeyParsesAndSeparatesSaves()
        {
            Assert.AreEqual("arch", LaunchOptions.Parse(new[] { "x.exe", "-savekey", "arch" }, "").SaveKey);
            Assert.AreEqual("scene-flow_2", LaunchOptions.Parse(null, "http://h/?savekey=scene-flow_2").SaveKey);
            Assert.AreEqual("", LaunchOptions.Parse(null, "").SaveKey);
            foreach (var bad in new[] { "a b", "x/y", "한", new string((char)97, 33) })
            {
                var o = LaunchOptions.Parse(new[] { "x.exe", "-savekey", bad }, "");
                Assert.AreEqual("", o.SaveKey, bad);
                Assert.AreEqual(1, o.Warnings.Count, bad);
            }

            var shared = new MemoryStore();
            var a = new SaveSystem(new SuffixedStore(shared, "a"));
            var b = new SaveSystem(new SuffixedStore(shared, "b"));
            a.SetMasterVolume(0.3f);
            b.SetMasterVolume(0.7f);
            Assert.AreEqual(0.3f, new SaveSystem(new SuffixedStore(shared, "a")).Settings.masterVolume, 1e-4f);
            Assert.AreEqual(0.7f, new SaveSystem(new SuffixedStore(shared, "b")).Settings.masterVolume, 1e-4f);
            Assert.IsTrue(shared.Data.ContainsKey(SaveSystem.SettingsKey + "#a"));
            Assert.IsFalse(shared.Data.ContainsKey(SaveSystem.SettingsKey), "the normal key is untouched");
            Assert.Throws<System.ArgumentException>(() => new SuffixedStore(shared, ""));
            Assert.IsInstanceOf<PlayerPrefsStore>(SaveStores.For(LaunchOptions.Parse(null, "")));
            Assert.IsInstanceOf<SuffixedStore>(SaveStores.For(LaunchOptions.Parse(new[] { "x.exe", "-savekey", "arch" }, "")));
        }
    }
}
