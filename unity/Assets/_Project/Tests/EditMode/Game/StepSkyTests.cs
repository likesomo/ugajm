using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests
{
    /// Step constellation (user decision 2026-11-12 "C"): the model follows State, the layout stays above the playable
    /// cells and under the HUD top band on every shipped stage.
    public sealed class StepSkyTests
    {
        static readonly string[] Corridor = { "##########", "#S......E#", "##########" };

        static State Walk(State s, int steps, Dir d = Dir.Right)
        {
            for (int i = 0; i < steps; i++) s = Rules.Dispatch(s, Command.Move(d)).State;
            return s;
        }

        [Test]
        public void FollowsTheStepsOfTheWalkingBody()
        {
            var s = Rules.NewState(Level.Parse("t", "", 2, new[] { "##########", "#S.......#", "##########" }));
            Assert.AreEqual(new StepSky(true, 0, false), StepSky.Of(s));
            Assert.AreEqual(new StepSky(true, 3, false), StepSky.Of(Walk(s, 3)));
            Assert.AreEqual(2, StepSky.Of(Walk(s, 3)).Links);
            var six = Walk(s, 6);
            Assert.AreEqual(new StepSky(true, 6, true), StepSky.Of(six), "the 7th star calls");
            var seven = Walk(six, 1);
            Assert.AreEqual(Status.Choose, seven.Status);
            Assert.AreEqual(new StepSky(true, 7, false), StepSky.Of(seven), "complete while choosing");
            Assert.AreEqual(6, StepSky.Of(seven).Links);
            var next = Rules.Dispatch(seven, Command.Keep).State;
            Assert.AreEqual(new StepSky(true, 0, false), StepSky.Of(next), "the next brother starts dark");
        }

        [Test]
        public void HiddenOnWinAndLose()
        {
            var won = Walk(Rules.NewState(Level.Parse("t", "", 1, Corridor)), 7);
            Assert.AreEqual(Status.Win, won.Status);
            Assert.IsFalse(StepSky.Of(won).Shown);
            var lost = Walk(Rules.NewState(Level.Parse("t", "", 1, new[] { "#########", "#S......#", "#########" })), 6);
            lost = Rules.Dispatch(lost, Command.Move(Dir.Left)).State;
            Assert.AreEqual(Status.Lose, lost.Status);
            Assert.IsFalse(StepSky.Of(lost).Shown);
        }

        [Test]
        public void RejectsImpossibleSkies()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StepSky(true, 8, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StepSky(true, -1, false));
            Assert.Throws<ArgumentException>(() => new StepSky(true, 3, true));
            Assert.Throws<ArgumentException>(() => new StepSky(false, 6, true));
            Assert.Throws<ArgumentNullException>(() => StepSky.Of(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepSkyView.Layout(0, 0, 100, 0));
        }

        [Test]
        public void LayoutIsAnArchAboveThePlayableCells()
        {
            var p = StepSkyView.Layout(playableTop: 100f, centreX: 20f, ceiling: 280f, cellPx: 77f);
            Assert.AreEqual(Rules.Steps, p.Points.Length);
            Assert.Greater(p.Bottom, 100f, "above the playable cells");
            Assert.LessOrEqual(p.Top, 280f, "under the HUD band");
            Assert.AreEqual(StepSkyView.Star, p.Size, 1e-3f, "full size when there is room");
            Assert.AreEqual(20f, (p.Points[0].x + p.Points[6].x) / 2f, 1e-3f, "centred on the board");
            Assert.Greater(p.Points[3].y, p.Points[0].y, "arched: the middle star is highest");
            Assert.AreEqual(p.Points[0].y, p.Points[6].y, 1e-3f, "symmetric ends");
            for (int k = 1; k < Rules.Steps; k++) Assert.Greater(p.Points[k].x, p.Points[k - 1].x, "step 1 on the left");
        }

        [Test]
        public void LayoutCanFillACallersArray()
        {
            var into = new Vector2[Rules.Steps];
            var p = StepSkyView.Layout(100f, 0f, 280f, 77f, into);
            Assert.AreSame(into, p.Points, "no new array (architect R1)");
            Assert.AreEqual(StepSkyView.Layout(100f, 0f, 280f, 77f).Points, into, "same points as the allocating form");
            Assert.Throws<ArgumentException>(() => StepSkyView.Layout(100f, 0f, 280f, 77f, new Vector2[6]));
            Assert.Throws<ArgumentNullException>(() => StepSkyView.Layout(100f, 0f, 280f, 77f, null));
        }

        /// Architect R1: the marks are rewritten only when the sky, the board box, cellPx, motion or the projection change.
        [Test]
        public void RewritesTheMarksOnlyWhenAnInputChanges()
        {
            var go = new GameObject("StepSkyTests Layer", typeof(RectTransform));
            try
            {
                var layer = (RectTransform)go.transform;
                layer.sizeDelta = new Vector2(1280f, 720f);
                var view = go.AddComponent<StepSkyView>();
                Vector2 shift = Vector2.zero;
                view.Init(layer, w => new Vector2(w.x * 77f, w.z * 77f) + shift);
                var box = new Bounds(Vector3.zero, new Vector3(6f, 1.2f, 4f));
                int n = view.Rebuilds;
                view.Show(new StepSky(true, 2, false), box, false, 77f);
                Assert.AreEqual(n + 1, view.Rebuilds, "shown");
                Assert.IsTrue(view.Shown);
                view.Show(new StepSky(true, 2, false), box, false, 77f);
                Assert.AreEqual(n + 1, view.Rebuilds, "same inputs: nothing rewritten");
                view.Show(new StepSky(true, 3, false), box, false, 77f);
                Assert.AreEqual(n + 2, view.Rebuilds, "a step");
                Assert.IsTrue(view.StarLit(2));
                Assert.IsTrue(view.LinkShown(1));
                shift = new Vector2(0f, 5f);
                view.Show(new StepSky(true, 3, false), box, false, 77f);
                Assert.AreEqual(n + 3, view.Rebuilds, "the camera moved");
                view.Show(new StepSky(true, 6, true), box, false, 77f);
                int calling = view.Rebuilds;
                view.Show(new StepSky(true, 6, true), box, false, 77f);
                Assert.AreEqual(calling, view.Rebuilds, "the pulse alone rewrites nothing");
                view.Clear();
                Assert.IsFalse(view.Shown);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThinBandFlattensThenShrinksButNeverDropsOntoTheBoard()
        {
            var flat = StepSkyView.Layout(240f, 0f, 280f, 77f);
            Assert.LessOrEqual(flat.Top, 280f + 1e-3f);
            Assert.Greater(flat.Bottom, 240f);
            var tiny = StepSkyView.Layout(275f, 0f, 280f, 77f);
            Assert.AreEqual(StepSkyView.MinStar, tiny.Size, 1e-3f);
            Assert.Greater(tiny.Bottom, 275f, "the board wins over the HUD band");
        }

        /// Every shipped stage, at the shipped camera rigs: the constellation fits, full size or close, between the
        /// playable cells (with the actors standing on them) and the U5 HUD top band.
        [Test]
        public void FitsAboveEveryShippedStage([Values(50f, 58f)] float pitch, [Values(0.6f, 0.7f)] float padding)
        {
            var file = JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "levels.json")));
            var go = new GameObject("StepSkyTests Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = 16f / 9f;
                var rig = new CameraRig { orthographic = true, pitch = pitch, yaw = 0f, fieldOfView = 30f, distance = 30f, padding = padding };
                float ceiling = 360f - StepSkyView.HudTopBand;
                foreach (var level in file.levels.Select(d => d.ToLevel()))
                {
                    var grid = new BoardGrid(level);
                    BoardView.Fit(cam, grid, rig);
                    var box = BoardView.PlayableBox(level, grid);
                    float top = float.MinValue;
                    for (int c = 0; c < 8; c++)
                    {
                        var w = box.center + Vector3.Scale(box.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        top = Mathf.Max(top, (cam.WorldToViewportPoint(w).y - 0.5f) * 720f);
                    }
                    float cellPx = 720f / (2f * cam.orthographicSize) * grid.CellSize;
                    var p = StepSkyView.Layout(top, 0f, ceiling, cellPx);
                    string at = $"{level.Name} pitch {pitch} padding {padding}";
                    Assert.Greater(p.Bottom, top, at + ": over the playable cells");
                    Assert.LessOrEqual(p.Top, ceiling + 1e-3f, at + $": into the HUD band (board top {top:0}, star {p.Size:0})");
                    Assert.GreaterOrEqual(p.Size, 18f, at + ": stars too small");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
