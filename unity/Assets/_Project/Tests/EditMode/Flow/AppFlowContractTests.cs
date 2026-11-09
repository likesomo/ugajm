using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// S2 contract (docs/scene-flow.md 5 S2, 8): AppFlow transition and Back tables, stage range, no transition while a scene
    /// loads (fake loader that completes later), and EntryTracker giving the same level_start.via as before.
    public sealed class AppFlowContractTests
    {
        /// Completes a load only when Finish is called, like SceneManager (next frame).
        sealed class DeferredScenes : ISceneLoader
        {
            public string Current { get; private set; } = AppScenes.Boot;
            public string Pending { get; private set; }
            public event Action<string> Loaded;
            public void Load(string sceneName) { Pending = sceneName; }
            public void Finish() { Current = Pending; Pending = null; Loaded?.Invoke(Current); }
            public void RaiseLoaded(string name) => Loaded?.Invoke(name);
        }

        static readonly AppState[] All = (AppState[])Enum.GetValues(typeof(AppState));

        static readonly HashSet<(AppState, AppState)> Allowed = new HashSet<(AppState, AppState)>
        {
            (AppState.Boot, AppState.Title), (AppState.Boot, AppState.Game),
            (AppState.Title, AppState.StageSelect),
            (AppState.StageSelect, AppState.Title), (AppState.StageSelect, AppState.Game),
            (AppState.Game, AppState.Result), (AppState.Game, AppState.StageSelect), (AppState.Game, AppState.Game),
            (AppState.Result, AppState.Game), (AppState.Result, AppState.StageSelect),
        };

        [Test]
        public void ConstructorRejectsMissingLoaderAndEmptyStageList()
        {
            Assert.Throws<ArgumentNullException>(() => new AppFlow(null, 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AppFlow(new DeferredScenes(), 0));
            Assert.AreEqual(17, new AppFlow(new DeferredScenes(), 17).StageCount);
        }

        [Test]
        public void TransitionTableIsExactlyTheAllowedSet()
        {
            foreach (var from in All)
                foreach (var to in All)
                    Assert.AreEqual(Allowed.Contains((from, to)), AppFlow.CanGo(from, to), $"{from} -> {to}");
        }

        [Test]
        public void BackTargetsStayInsideTheTransitionTable()
        {
            var expected = new Dictionary<AppState, AppState>
            {
                { AppState.StageSelect, AppState.Title }, { AppState.Game, AppState.StageSelect }, { AppState.Result, AppState.StageSelect },
            };
            foreach (var from in All)
            {
                bool has = AppFlow.TryGetBackTarget(from, out var to);
                Assert.AreEqual(expected.ContainsKey(from), has, from.ToString());
                if (!has) continue;
                Assert.AreEqual(expected[from], to, from.ToString());
                Assert.IsTrue(AppFlow.CanGo(from, to), $"back {from} -> {to} must be an allowed transition");
            }
        }

        [Test]
        public void GoToGameChecksTheStageRange()
        {
            var scenes = new DeferredScenes();
            var flow = new AppFlow(scenes, 3);
            Assert.IsFalse(flow.CanGoToGame(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.GoToGame(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.GoToGame(-1));
            Assert.AreEqual(AppState.Boot, flow.State);
            Assert.AreEqual(0, flow.Stage);
            flow.GoToGame(2);
            Assert.AreEqual(2, flow.Stage);
        }

        [Test]
        public void NoTransitionWhileASceneIsLoading()
        {
            var scenes = new DeferredScenes();
            var flow = new AppFlow(scenes, 17);
            flow.Go(AppState.Title);
            Assert.IsTrue(flow.IsLoading);
            Assert.AreEqual(AppScenes.Title, scenes.Pending);
            Assert.IsFalse(flow.CanBack);
            Assert.IsFalse(flow.CanGoToGame(0));
            Assert.Throws<InvalidOperationException>(() => flow.Go(AppState.StageSelect));

            scenes.RaiseLoaded("SomeOtherScene"); // not the scene we asked for
            Assert.IsTrue(flow.IsLoading);

            scenes.Finish();
            Assert.IsFalse(flow.IsLoading);
            flow.Go(AppState.StageSelect);   // same scene: no load
            Assert.IsFalse(flow.IsLoading);
            Assert.IsTrue(flow.CanBack);
            Assert.IsTrue(flow.TryGetBackTarget(out var back));
            Assert.AreEqual(AppState.Title, back);

            flow.GoToGame(4);                // Title -> Game scene
            Assert.IsTrue(flow.IsLoading);
            scenes.Finish();
            flow.Go(AppState.Result);
            flow.GoToGame(5);                // Result -> Game, same scene
            Assert.IsFalse(flow.IsLoading);
            Assert.AreEqual(5, flow.Stage);
        }

        [Test]
        public void ChangedIsRaisedWhileTheNewSceneIsStillLoading()
        {
            var scenes = new DeferredScenes();
            var flow = new AppFlow(scenes, 17);
            bool loadingDuringChanged = false;
            flow.Changed += (from, to) => loadingDuringChanged = flow.IsLoading;
            flow.Go(AppState.Title);
            Assert.IsTrue(loadingDuringChanged, "the outgoing scene must not start another transition from Changed");
        }

        // ---------- EntryTracker: same via values as GameController before S2 ----------

        [Test]
        public void FirstEntryIsBootOrUrlThenSelect()
        {
            var boot = EntryTracker.ForLaunch(levelFromLaunch: false);
            Assert.IsTrue(boot.FirstPending);
            Assert.AreEqual(TelemetryNames.Via.Boot, boot.TakeVia());
            Assert.IsFalse(boot.FirstPending);
            Assert.AreEqual(TelemetryNames.Via.Select, boot.TakeVia());

            var url = EntryTracker.ForLaunch(levelFromLaunch: true);
            Assert.AreEqual(TelemetryNames.Via.Url, url.TakeVia());
        }

        [Test]
        public void AnnouncedViaAppliesToTheNextEntryOnly()
        {
            var t = EntryTracker.ForLaunch(false);
            t.TakeVia();
            t.Announce(TelemetryNames.Via.Next);
            Assert.AreEqual(TelemetryNames.Via.Next, t.TakeVia());
            Assert.AreEqual(TelemetryNames.Via.Select, t.TakeVia());
            t.Announce(TelemetryNames.Via.Again);
            Assert.AreEqual(TelemetryNames.Via.Again, t.TakeVia());
        }

        [Test]
        public void FirstEntryWinsOverAnAnnouncement()
        {
            var t = EntryTracker.ForLaunch(true);
            t.Announce(TelemetryNames.Via.Select); // a stage picked on Stage Select before the first Game scene
            Assert.AreEqual(TelemetryNames.Via.Url, t.TakeVia());
        }

        [Test]
        public void EntryTrackerRejectsUnknownVia()
        {
            Assert.Throws<ArgumentException>(() => new EntryTracker(TelemetryNames.Via.Next));
            Assert.Throws<ArgumentException>(() => EntryTracker.ForLaunch(false).Announce(TelemetryNames.Via.Boot));
        }
    }
}
