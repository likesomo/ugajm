using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenSteps.Tests
{
    /// Stage select v4 (docs/stage-select-transitions.md): model states, stage choices handed to the dive, the toggle,
    /// map layout, and the run-time stone meshes.
    public sealed class StageSelectTests
    {
        static readonly string[] Keys = { "moss", "hollow", "carved" };
        static readonly (int, int)[] Chain = { (0, 1), (1, 2) };

        static StageSelectModel Model(bool[][] cleared, int markerPlanet = -1, int markerSlot = -1) =>
            new StageSelectModel(Keys, cleared, markerPlanet, markerSlot, Chain);

        static bool[][] Rows(params bool[][] rows) => rows;

        [Test]
        public void FreshSaveHasNoMarkerNoLitTrailAndEveryPlanetDims()
        {
            var m = Model(Rows(new bool[3], new bool[3], new bool[2]));
            Assert.AreEqual(-1, m.MarkerPlanet);
            for (int p = 0; p < 3; p++) Assert.IsFalse(m.Visited(p));
            Assert.IsFalse(m.TrailLit(0));
            Assert.AreEqual(0, m.DefaultFocus);
            Assert.AreEqual(PlaceState.Current, m.Place(0, 0), "the brothers stand on the first stage");
            Assert.AreEqual(PlaceState.Untouched, m.Place(0, 1));
        }

        [Test]
        public void ClickPlaysTheFirstUnfinishedStageOrOpensACompletePlanet()
        {
            var m = Model(Rows(new[] { true, false, true }, new[] { true, true, true }, new bool[2]));
            Assert.IsTrue(m.ChoosePlays(0, out int slot));
            Assert.AreEqual(1, slot);
            Assert.IsFalse(m.ChoosePlays(1, out slot), "complete planet: the surface path");
            Assert.AreEqual(-1, slot);
        }

        [Test]
        public void ACompletedPlanetLightsItsOutgoingTrailUnlessHeld()
        {
            var m = Model(Rows(new[] { true, true, true }, new[] { false, false, false }, new bool[2]), 0, 2);
            Assert.IsTrue(m.TrailLit(0), "moss -> hollow lights though hollow is unfinished");
            Assert.IsFalse(m.TrailLit(1));
            Assert.IsFalse(m.TrailLit(0, held: 0), "journey return pending: waits for Settled");
            Assert.AreEqual(1, m.NextAlong(0));
            Assert.AreEqual(2, m.NextAlong(2), "no outgoing trail: stays");
        }

        [Test]
        public void TheFinaleLightsTheTrailBeforeEveryStageIsCleared()
        {
            // As the journey return (scene-flow) and the story: the planet's last stage cleared = completed for the trails.
            var m = Model(Rows(new[] { true, false, true }, new bool[3], new bool[2]), 0, 2);
            Assert.IsTrue(m.FinaleCleared(0));
            Assert.IsFalse(m.Complete(0));
            Assert.IsTrue(m.TrailLit(0));
            Assert.IsTrue(m.ChoosePlays(0, out int slot), "a click still plays the unfinished stage 2");
            Assert.AreEqual(1, slot);
        }

        [Test]
        public void MarkerAndCurrentPlace()
        {
            var m = Model(Rows(new[] { true, false, false }, new bool[3], new bool[2]), 0, 2);
            Assert.AreEqual(0, m.MarkerPlanet);
            Assert.IsTrue(m.Visited(0));
            Assert.AreEqual(2, m.CurrentSlot(0), "last played and not cleared");
            Assert.AreEqual(PlaceState.Cleared, m.Place(0, 0));
            Assert.AreEqual(PlaceState.Untouched, m.Place(0, 1));
            Assert.AreEqual(PlaceState.Current, m.Place(0, 2));
            Assert.AreEqual(0, m.DefaultFocus);
            var done = Model(Rows(new[] { true, true, true }, new bool[3], new bool[2]), 0, 1);
            Assert.AreEqual(1, done.CurrentSlot(0), "complete planet: where the brothers were");
            Assert.AreEqual(0, done.CurrentSlot(2), "other planets start at their first stage");
        }

        [Test]
        public void FromReadsTheSave()
        {
            var stages = new List<IReadOnlyList<int>> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7 } };
            var cleared = new HashSet<int> { 0, 3, 4, 5 };
            var m = StageSelectModel.From(Keys, stages, s => cleared.Contains(s), 4, Chain);
            Assert.AreEqual(1, m.MarkerPlanet);
            Assert.AreEqual(1, m.MarkerSlot);
            Assert.IsTrue(m.Complete(1));
            Assert.AreEqual(1, m.ClearedCount(0));
            Assert.IsTrue(m.TrailLit(1));
        }

        [Test]
        public void TrailsByKeySkipsPlanetsNotOnTheMap()
        {
            var t = StageSelectModel.TrailsByKey(Keys, new[] { ("moss", "hollow"), ("carved", "circle"), ("hollow", "carved") });
            CollectionAssert.AreEqual(new[] { (0, 1), (1, 2) }, t);
        }

        [Test]
        public void BadInputThrows()
        {
            Assert.Throws<ArgumentException>(() => new StageSelectModel(Keys, Rows(new bool[1], new bool[1]), -1, -1, Chain));
            Assert.Throws<ArgumentException>(() => new StageSelectModel(Keys, Rows(new bool[1], new bool[0], new bool[1]), -1, -1, Chain));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StageSelectModel(Keys, Rows(new bool[1], new bool[1], new bool[1]), 0, 3, Chain));
            Assert.Throws<ArgumentException>(() => new StageSelectModel(Keys, Rows(new bool[1], new bool[1], new bool[1]), -1, -1, new[] { (0, 0) }));
        }

        [Test]
        public void ArrowsFollowOrderLeftRightAndNearestUpDown()
        {
            // Screen points, y down: 0 top-left, 1 below it, 2 right of 1.
            var pts = new[] { new Vector2(150, 170), new Vector2(150, 430), new Vector2(410, 480) };
            Assert.AreEqual(1, StageSelectModel.Neighbor(pts, 0, 1, 0));
            Assert.AreEqual(0, StageSelectModel.Neighbor(pts, 0, -1, 0), "left end stays");
            Assert.AreEqual(1, StageSelectModel.Neighbor(pts, 0, 0, 1), "down: straight below beats down-right");
            Assert.AreEqual(0, StageSelectModel.Neighbor(pts, 1, 0, -1));
            Assert.AreEqual(0, StageSelectModel.Neighbor(pts, 0, 0, -1), "nothing above: stay");
        }

        // ---- StageChoice / controller ----

        sealed class FakeView : IStageSelectView
        {
            public event Action<int> PlanetChosen;
            public event Action<int, int> PlaceChosen;
            public readonly List<string> calls = new List<string>();
            public PlanetBody body;
            public Camera camera;
            public void Choose(int p) => PlanetChosen?.Invoke(p);
            public void Place(int p, int s) => PlaceChosen?.Invoke(p, s);
            public void ShowMap(StageSelectModel model, int focus, int held) => calls.Add($"map {focus} {held}");
            public void LightTrail(StageSelectModel model, int planet, int focus) => calls.Add($"light {planet} {focus}");
            public void OpenSurface(StageSelectModel model, int planet) => calls.Add($"surface {planet}");
            public void SetPending(bool pending) => calls.Add($"pending {pending}");
            public PlanetBody Body(int planet, StageChoiceSource source) => body;
            public Camera MapCamera => camera;
            public void MapPose(out Vector3 position, out Quaternion rotation, out float orthoSize) { position = Vector3.zero; rotation = Quaternion.identity; orthoSize = 1; }
        }

        GameObject bodyGo, camGo;
        FakeView view;
        List<int> played;
        bool canPlay;

        [SetUp]
        public void SetUp()
        {
            bodyGo = new GameObject("body");
            camGo = new GameObject("cam", typeof(Camera));
            view = new FakeView { body = bodyGo.AddComponent<PlanetBody>(), camera = camGo.GetComponent<Camera>() };
            played = new List<int>();
            canPlay = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(bodyGo);
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        StageSelectController Controller(StageSelectModel m, JourneyReturn journey = null)
        {
            var stages = new List<IReadOnlyList<int>> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7 } };
            return new StageSelectController(view, () => m, stages, _ => canPlay, s => played.Add(s), journey);
        }

        [Test]
        public void WithoutASubscriberAChoicePlaysAtOnce()
        {
            var c = Controller(Model(Rows(new[] { true, false, false }, new bool[3], new bool[2])));
            c.Show();
            view.Choose(0);
            CollectionAssert.AreEqual(new[] { 1 }, played);
        }

        [Test]
        public void ACompletePlanetOpensItsSurfaceAndAPlacePlaysThatStage()
        {
            var c = Controller(Model(Rows(new[] { true, true, true }, new bool[3], new bool[2])));
            c.Show();
            view.Choose(0);
            CollectionAssert.Contains(view.calls, "surface 0");
            CollectionAssert.IsEmpty(played);
            view.Place(0, 2);
            CollectionAssert.AreEqual(new[] { 2 }, played);
        }

        [Test]
        public void TheDiveGetsOneChoiceThatEndsOnce()
        {
            var c = Controller(Model(Rows(new bool[3], new bool[3], new bool[2])));
            StageChoice got = null;
            c.StageChosen += ch => got = ch;
            c.Show();
            view.Choose(1);
            Assert.IsNotNull(got);
            Assert.AreEqual(1, got.Planet);
            Assert.AreEqual("hollow", got.PlanetKey);
            Assert.AreEqual(3, got.Stage);
            Assert.AreEqual(StageChoiceSource.Map, got.Source);
            CollectionAssert.Contains(view.calls, "pending True");
            view.Choose(2); // a second click while pending: ignored
            Assert.AreSame(got, c.Pending);
            Assert.IsTrue(got.Commit());
            CollectionAssert.AreEqual(new[] { 3 }, played);
            LogAssert.ignoreFailingMessages = true; // Debug.Assert logs before the throw
            Assert.Throws<InvalidOperationException>(() => got.Commit());
            Assert.Throws<InvalidOperationException>(() => got.Cancel());
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ARefusedCommitCancelsAndInputComesBack()
        {
            var c = Controller(Model(Rows(new bool[3], new bool[3], new bool[2])));
            StageChoice got = null;
            c.StageChosen += ch => got = ch;
            c.Show();
            view.Choose(0);
            canPlay = false;
            Assert.IsFalse(got.Commit());
            CollectionAssert.IsEmpty(played);
            Assert.AreEqual("pending False", view.calls[view.calls.Count - 1]);
            Assert.IsNull(c.Pending);
        }

        [Test]
        public void CancelGivesInputBack()
        {
            var c = Controller(Model(Rows(new bool[3], new bool[3], new bool[2])));
            StageChoice got = null;
            c.StageChosen += ch => got = ch;
            c.Show();
            view.Choose(0);
            var first = got;
            first.Cancel();
            Assert.AreEqual("pending False", view.calls[view.calls.Count - 1]);
            view.Choose(2);
            Assert.AreNotSame(first, c.Pending, "a new choice after the cancel");
            Assert.AreSame(got, c.Pending);
            Assert.AreEqual(6, c.Pending.Stage);
        }

        [Test]
        public void AJourneyReturnHoldsTheTrailUntilSettled()
        {
            var journey = new JourneyReturn();
            journey.Begin(0, "moss");
            var c = Controller(Model(Rows(new[] { true, true, true }, new bool[3], new bool[2]), 0, 2), journey);
            c.Show();
            Assert.AreEqual("map 0 0", view.calls[0], "focus and held on the completed planet");
            view.Choose(1);
            CollectionAssert.IsEmpty(played, "no choice during the pullback");
            journey.Settle();
            Assert.AreEqual("light 0 1", view.calls[view.calls.Count - 1]);
            view.Choose(1);
            CollectionAssert.AreEqual(new[] { 3 }, played);
            c.Dispose();
        }

        // ---- toggle ----

        [Test]
        public void ToggleDefaultsOffAndFlagsTurnItOn()
        {
            Assert.AreEqual(StageSelectToggle.DefaultOn, StageSelectToggle.On(LaunchOptions.Parse(new string[0], "")));
            Assert.IsTrue(StageSelectToggle.On(LaunchOptions.Parse(new[] { "x.exe", "-newmap" }, "")));
            Assert.IsFalse(StageSelectToggle.On(LaunchOptions.Parse(new[] { "x.exe", "-oldmap" }, "")));
            Assert.IsTrue(StageSelectToggle.On(LaunchOptions.Parse(null, "http://h/?newmap=1")));
            Assert.IsFalse(StageSelectToggle.On(LaunchOptions.Parse(null, "http://h/?newmap=0")));
            Assert.IsFalse(StageSelectToggle.On(LaunchOptions.Parse(null, "http://h/?oldmap=1")));
            Assert.IsNull(LaunchOptions.Parse(null, "http://h/?newmap=maybe").NewMap);
        }

        // ---- layout ----

        [Test]
        public void MapFitsNarrowScreens()
        {
            Assert.AreEqual(PlanetMapLayout.OrthoSize, PlanetMapLayout.OrthoFor(16f / 9f), 1e-4f);
            Assert.Greater(PlanetMapLayout.OrthoFor(4f / 3f), PlanetMapLayout.OrthoSize);
            // Every Dipper planet stays inside the visible width at 4:3 (with its radius).
            float ortho = PlanetMapLayout.OrthoFor(4f / 3f), halfW = ortho * 4f / 3f;
            for (int i = 0; i < 7; i++)
            {
                var w = PlanetMapLayout.World(PlanetMapLayout.Reference(i, 7));
                Assert.Less(Mathf.Abs(w.x) + PlanetStoneMesh.Radius * PlanetMapLayout.BodyScale, halfW, $"planet {i + 1}");
            }
        }

        [Test]
        public void LabelMovesOffATrail()
        {
            var bounds = new Rect(-616, -336, 1232, 672);
            var c = new Vector2(0, 0);
            var none = PlanetMapLayout.LabelSpot(c, 90, new Vector2(300, 60), 26, new List<(Vector2, Vector2)>(), bounds);
            Assert.AreEqual(0, none.x, 1e-3f, "no trails: under the body");
            Assert.Less(none.y, -90);
            var down = new List<(Vector2, Vector2)> { (new Vector2(0, -100), new Vector2(0, -330)) }; // a trail straight down
            var moved = PlanetMapLayout.LabelSpot(c, 90, new Vector2(300, 60), 26, down, bounds);
            Assert.Greater(Mathf.Abs(moved.x), 150, "beside the body");
            Assert.IsFalse(PlanetMapLayout.SegmentHitsRect(down[0].Item1, down[0].Item2, new Rect(moved - new Vector2(150, 30), new Vector2(300, 60))));
        }

        [Test]
        public void SegmentRect()
        {
            var r = new Rect(0, 0, 10, 10);
            Assert.IsTrue(PlanetMapLayout.SegmentHitsRect(new Vector2(-5, 5), new Vector2(15, 5), r));
            Assert.IsTrue(PlanetMapLayout.SegmentHitsRect(new Vector2(2, 2), new Vector2(3, 3), r), "inside");
            Assert.IsFalse(PlanetMapLayout.SegmentHitsRect(new Vector2(-5, -5), new Vector2(-1, 20), r));
        }

        [Test]
        public void TrailSegmentsStopAtTheRims()
        {
            Assert.IsTrue(PlanetMapLayout.TrailSegment(Vector2.zero, 10, new Vector2(100, 0), 20, 5, out var a, out var b));
            Assert.AreEqual(15, a.x, 1e-4f);
            Assert.AreEqual(75, b.x, 1e-4f);
            Assert.IsFalse(PlanetMapLayout.TrailSegment(Vector2.zero, 40, new Vector2(80, 0), 40, 5, out _, out _));
        }

        // ---- bodies ----

        [Test]
        public void EveryStoneBodyIsInsideTheTriangleBudget()
        {
            for (int look = 0; look < PlanetStoneMesh.Looks.Length; look++)
                foreach (bool seated in new[] { false, true })
                {
                    var mesh = PlanetStoneMesh.Get(look, seated); // throws outside 4k..6k
                    int tris = mesh.triangles.Length / 3;
                    Assert.That(tris, Is.InRange(PlanetStoneMesh.MinTriangles, PlanetStoneMesh.MaxTriangles), $"{PlanetStoneMesh.Looks[look]} seated {seated}");
                    Assert.AreSame(mesh, PlanetStoneMesh.Get(look, seated), "built once");
                }
        }

        [Test]
        public void SeatedBodiesHaveAFlatSummitUnderTheBoard()
        {
            for (int look = 0; look < PlanetStoneMesh.Looks.Length; look++)
            {
                var top = PlanetStoneMesh.SurfacePoint(look, Vector3.up, seated: true);
                Assert.AreEqual(PlanetStoneMesh.SeatCut, top.y, 1e-4f, PlanetStoneMesh.Looks[look]);
                Assert.Less(top.y, PlanetStoneMesh.SeatTop, "the board stands just above the cut");
            }
        }

        [Test]
        public void SurfacePlacesSitOnTheFront()
        {
            for (int s = 0; s < 8; s++)
            {
                var d = PlanetSurfaceView.PlaceDirection(s, 8);
                Assert.Less(d.z, 0, $"place {s + 1} faces the camera (-z)");
                Assert.Greater(d.y, 0, $"place {s + 1} is on the upper half");
            }
        }

        [Test]
        public void TheDataAssetNamesEveryChapter()
        {
            var set = UnityEditor.AssetDatabase.LoadAssetAtPath<PlanetBodySet>("Assets/_Project/Data/PlanetBodies.asset");
            Assert.IsNotNull(set, "Data/PlanetBodies.asset");
            Assert.IsNotNull(set.material);
            Assert.IsNotNull(set.marksMaterial);
            foreach (var key in PlanetStoneMesh.Looks) Assert.IsNotNull(set.Find(key), key);
            var assets = UnityEditor.AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.AreSame(set, assets.planetBodies);
        }
    }
}
