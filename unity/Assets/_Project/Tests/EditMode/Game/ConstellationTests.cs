using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests
{
    /// <summary>
    /// The U5 surface constellation (D2b Orbit sparks, user choice 2026-11-12; d2-refine IMPLEMENT.md): the model's best
    /// stars and perfect, the figures (every planet, 80 targets inside the screen without overlap), the edge and current
    /// rules, and the node glyph states. Owner: scene-flow.
    /// </summary>
    public sealed class ConstellationTests
    {
        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        static StageSelectModel Model(bool[] cleared, int[] best, int markerSlot = -1, string key = "moss") =>
            new StageSelectModel(new[] { key }, new[] { cleared }, markerSlot >= 0 ? 0 : -1, markerSlot, new List<(int, int)>(), null, new[] { best });

        [Test]
        public void BestStarsAreIndependentOfClearedAndPerfectNeedsEveryStageAtThree()
        {
            var m = Model(new[] { true, true, false }, new[] { 0, 3, 0 });
            Assert.IsTrue(m.IsCleared(0, 0));
            Assert.AreEqual(0, m.Best(0, 0), "a cleared stage may have no stars");
            Assert.AreEqual(3, m.Best(0, 1));
            Assert.IsFalse(m.Perfect(0), "not complete");
            Assert.IsFalse(Model(new[] { true, true }, new[] { 3, 2 }).Perfect(0), "complete, one below three");
            Assert.IsTrue(Model(new[] { true, true }, new[] { 3, 3 }).Perfect(0));
            Assert.AreEqual(0, new StageSelectModel(new[] { "moss" }, new[] { new[] { true } }, -1, -1, new List<(int, int)>()).Best(0, 0), "no best rows: 0");
            Assert.Throws<ArgumentOutOfRangeException>(() => Model(new[] { true }, new[] { 4 }));
            Assert.Throws<ArgumentException>(() => Model(new[] { true, false }, new[] { 1 }));
            var from = StageSelectModel.From(new[] { "moss" }, new[] { new[] { 4, 5 } }, s => s == 4, -1, new List<(int, int)>(), null, s => s == 4 ? 2 : 0);
            Assert.AreEqual(2, from.Best(0, 0), "From reads best by stage index");
        }

        [Test]
        public void EveryPlanetHasAFigureWithTargetsOnScreenAndApart()
        {
            var assets = UnityEditor.AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            var levels = LevelCatalog.Load(assets);
            var planets = PlanetEntry.FromCatalog(levels.Length);
            Assert.GreaterOrEqual(planets.Count, 7);
            foreach (var p in planets)
            {
                var fig = ConstellationFigure.For(p.Key, p.Stages.Count);
                Assert.AreEqual(p.Stages.Count, fig.Anchors.Count, p.Key);
                var targets = fig.Anchors.Select(a => new Rect(a - Vector2.one * 40, Vector2.one * 80)).ToList();
                foreach (var r in targets)
                    Assert.IsTrue(r.xMin >= 0 && r.yMin >= 0 && r.xMax <= 1280 && r.yMax <= 720, $"{p.Key}: target {r} on screen");
                for (int i = 0; i < targets.Count; i++)
                    for (int j = i + 1; j < targets.Count; j++)
                        Assert.IsFalse(targets[i].Overlaps(targets[j]), $"{p.Key}: targets {i + 1} and {j + 1} overlap");
                // Back (52, 52) and settings (1228, 52) keep their 80 targets clear; the caption plate (y 649..691) too.
                var nav = new[] { new Rect(12, 12, 80, 80), new Rect(1188, 12, 80, 80), new Rect(280, 649, 720, 42) };
                foreach (var r in targets) foreach (var n in nav) Assert.IsFalse(r.Overlaps(n), $"{p.Key}: target {r} clear of {n}");
            }
            Assert.AreEqual(8, ConstellationFigure.For("altar", 8).Anchors.Count, "altar's eighth stage");
            Assert.Throws<InvalidOperationException>(() => ConstellationFigure.For("moss", 9));
        }

        [Test]
        public void EdgesDrawAsStagesClearAndClosingEdgesOnCompletion()
        {
            var fig = ConstellationFigure.For("moss", 7);
            Assert.AreEqual(7, fig.Edges.Count, "six in order + the bowl's 7-4");
            Assert.AreEqual((6, 3, true), fig.Edges[6]);
            var mid = Model(new[] { true, true, false, false, false, false, false }, new int[7]);
            Assert.IsTrue(ConstellationFigure.Drawn(mid, 0, fig.Edges[0]));
            Assert.IsTrue(ConstellationFigure.Drawn(mid, 0, fig.Edges[1]), "the route toward the current stage opens");
            Assert.IsFalse(ConstellationFigure.Drawn(mid, 0, fig.Edges[2]));
            Assert.IsFalse(ConstellationFigure.Drawn(mid, 0, fig.Edges[6]));
            Assert.AreEqual(2, ConstellationFigure.Current(mid, 0));
            var done = Model(Enumerable.Repeat(true, 7).ToArray(), new int[7], markerSlot: 3);
            Assert.IsTrue(ConstellationFigure.Drawn(done, 0, fig.Edges[6]));
            Assert.AreEqual(-1, ConstellationFigure.Current(done, 0), "no current pointer once every stage is cleared");
            var altar = ConstellationFigure.For("altar", 8);
            Assert.AreEqual(7, altar.Edges.Count, "seven in order; the hoop stays open (8-1 would cross the landmark)");
            Assert.IsFalse(altar.Edges.Any(e => e.closing));
            Assert.AreEqual(6, ConstellationFigure.For("frost", 7).Edges.Count);
        }

        ConstellationView View(out RectTransform canvasRect)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(go);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasRect = (RectTransform)go.transform;
            canvasRect.sizeDelta = new Vector2(1280, 720);
            return new ConstellationView(canvasRect);
        }

        static Transform Find(ConstellationView v, int slot, string child) => v.Button(slot).transform.Find("Glyph/" + child) ?? v.Button(slot).transform.Find("Glyph/Current/" + child);
        static bool On(ConstellationView v, int slot, string child)
        {
            var t = Find(v, slot, child);
            Assert.IsNotNull(t, child);
            var g = t.GetComponent<UnityEngine.UI.Graphic>();
            return t.gameObject.activeInHierarchy && (g == null || g.enabled);
        }

        [Test]
        public void NodesShowClearedScoreCurrentAndPerfect()
        {
            var v = View(out _);
            // Moss mid: 1-4 cleared with 0, 1, 2, 3 stars; 5 current; 6-7 untouched.
            var m = Model(new[] { true, true, true, true, false, false, false }, new[] { 0, 1, 2, 3, 0, 0, 0 });
            v.Bind(m, 0, ConstellationFigure.For("moss", 7));
            Assert.AreEqual(7, v.Count);
            Assert.AreEqual("cleared 0", v.State(0));
            Assert.AreEqual("untouched current", v.State(4));
            Assert.AreEqual("untouched", v.State(5));
            for (int s = 0; s < 4; s++)
                for (int k = 0; k < 3; k++) Assert.AreEqual(k < s, On(v, s, "Score spark " + (k + 1)), $"stage {s + 1}: {s} sparks");
            Assert.IsFalse(On(v, 0, "Untouched centre"), "cleared zero: solid body");
            Assert.IsTrue(On(v, 5, "Untouched centre"), "untouched: hollow centre (differs from cleared zero by shape, not colour only)");
            Assert.AreNotEqual(Find(v, 0, "Stage star").GetComponent<UnityEngine.UI.Image>().color, Find(v, 5, "Stage star").GetComponent<UnityEngine.UI.Image>().color);
            Assert.IsFalse(On(v, 5, "Score spark 1"), "no empty sockets");
            Assert.IsTrue(On(v, 4, "Scarf arc 1") && On(v, 4, "Pointer left"), "the current stage's scarf arcs and pointer");
            Assert.IsFalse(On(v, 3, "Scarf arc 1"));
            Assert.IsFalse(On(v, 0, "Finale aureole"));
            Assert.AreEqual(7, v.LinkCount, "the bowl closing edge is there as a faint future link");
            Assert.IsTrue(v.LinkDrawn(3) && !v.LinkDrawn(5) && !v.LinkDrawn(6), "drawn to the current stage, future beyond, the bowl closes on completion");
            foreach (var b in Enumerable.Range(0, 7).Select(v.Button))
                Assert.AreEqual(new Vector2(ConstellationView.Target, ConstellationView.Target), ((RectTransform)b.transform).sizeDelta, "80 targets");
            Assert.AreEqual(ConstellationView.ToLocal(new Vector2(200, 475), new Vector2(1280, 720)), v.Centre(0));

            // Focus another stage than the current one: accents there, the current pointer stays.
            v.ReduceMotion = true;
            v.SetInput(1, -1, -1);
            Assert.IsTrue(On(v, 1, "Focus accent 1"));
            Assert.IsFalse(On(v, 4, "Focus accent 1"));
            Assert.IsTrue(On(v, 4, "Pointer left"));
            Assert.AreEqual(1.12f, v.GlyphScale(1), 1e-4f, "selected focus settles at 112%");
            v.SetInput(1, 2, 2);
            Assert.AreEqual(.91f, v.GlyphScale(2), 1e-4f, "pressed 91%");
            Assert.IsTrue(On(v, 2, "Focus accent 1") && !On(v, 1, "Focus accent 1"), "hover wins the accents");

            // All three stars everywhere: perfect, every edge warm (with the bowl), no current.
            var perfect = Model(Enumerable.Repeat(true, 7).ToArray(), Enumerable.Repeat(3, 7).ToArray(), markerSlot: 6);
            v.Bind(perfect, 0, ConstellationFigure.For("moss", 7));
            Assert.AreEqual("cleared 3 perfect", v.State(6));
            Assert.IsTrue(On(v, 0, "Finale aureole"));
            Assert.IsFalse(On(v, 6, "Scarf arc 1"));
            Assert.AreEqual(7, v.LinkCount);
            for (int e = 0; e < 7; e++) Assert.IsTrue(v.LinkDrawn(e));

            // Cleared but one below three: complete, not perfect.
            var almost = Model(Enumerable.Repeat(true, 7).ToArray(), new[] { 3, 3, 3, 2, 3, 3, 3 });
            v.Bind(almost, 0, ConstellationFigure.For("moss", 7));
            Assert.IsFalse(On(v, 0, "Finale aureole"));
            Assert.AreEqual("cleared 2", v.State(3));
        }

        /// The first-clear trace (ClearNews): opens on the before state, then the star turns gold, the link to the next
        /// stage traces and the sparks fade in; a press or reduce motion ends it at once; a note that does not match throws.
        [Test]
        public void ANewClearReplaysFromItsBeforeState()
        {
            var v = View(out _);
            var stages = new[] { 10, 11, 12, 13, 14, 15, 16 };
            var m = Model(new[] { true, true, false, false, false, false, false }, new[] { 1, 2, 0, 0, 0, 0, 0 });
            var news = new List<ClearNote> { new ClearNote(11, true, 0, 2) };
            v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, news);
            Assert.IsTrue(v.Replaying);
            Assert.AreEqual("untouched", v.State(1), "before: stage 2 not cleared yet");
            Assert.IsTrue(v.LinkDrawn(0) && !v.LinkDrawn(1), "before: the route from stage 2 is still faint");
            v.Tick(ConstellationView.ReplayDelay + ConstellationView.TraceSeconds / 2);
            Assert.AreEqual("cleared 2", v.State(1), "the star turns gold");
            Assert.IsTrue(v.LinkDrawn(1));
            Assert.That(v.LinkFraction(1), Is.InRange(0.3f, 0.7f), "the link traces");
            v.Tick(1);
            Assert.IsFalse(v.Replaying);
            Assert.AreEqual(1f, v.LinkFraction(1));

            v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, news);
            v.SetInput(-1, -1, 3);
            Assert.IsFalse(v.Replaying, "a press ends it");
            Assert.AreEqual("cleared 2", v.State(1));

            v.ReduceMotion = true;
            v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, news);
            Assert.IsFalse(v.Replaying, "reduce motion: the final state at once");
            Assert.AreEqual("cleared 2", v.State(1));
            v.ReduceMotion = false;

            v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, new List<ClearNote> { new ClearNote(11, false, 1, 2) });
            Assert.AreEqual("cleared 1", v.State(1), "a better best: before shows the old best");
            Assert.IsTrue(v.LinkDrawn(1), "no link to trace");
            v.Tick(1);
            Assert.AreEqual("cleared 2", v.State(1));

            Assert.Throws<InvalidOperationException>(() => v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, new List<ClearNote> { new ClearNote(12, true, 0, 1) }), "stage 3 is not cleared in the save");
            Assert.Throws<ArgumentException>(() => v.Bind(m, 0, ConstellationFigure.For("moss", 7), stages, new List<ClearNote> { new ClearNote(99, true, 0, 1) }));
        }

        [Test]
        public void AnchorsReflowWithTheCanvas()
        {
            var v = View(out var canvas);
            v.Bind(Model(new bool[7], new int[7]), 0, ConstellationFigure.For("moss", 7));
            canvas.sizeDelta = new Vector2(1600, 720); // wider than 16:9
            v.Tick(0);
            Assert.AreEqual(ConstellationView.ToLocal(new Vector2(200, 475), new Vector2(1600, 720)), v.Centre(0));
            Assert.AreEqual(new Vector2(80, 80), ((RectTransform)v.Button(0).transform).sizeDelta, "targets keep 80, not shrunk");
        }
    }
}
