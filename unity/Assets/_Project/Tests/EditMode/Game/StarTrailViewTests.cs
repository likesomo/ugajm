using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// StarTrailView: one quad per mark (but the feet ring) in the interim mesh, the map written for the shader (docs 15.3).
    public sealed class StarTrailViewTests
    {
        [Test]
        public void OneQuadPerMarkAndTheMap()
        {
            var level = Level.Parse("t", "", 3, new[] { "S.......", "........" });
            var s = Rules.NewState(level);
            foreach (char c in "RRRRRR") { Rules.TryParseLetter(c, out var a); s = Rules.Dispatch(s, Command.FromAct(a)).State; }
            var go = new GameObject("StarTrailViewTests");
            try
            {
                var view = go.AddComponent<StarTrailView>();
                view.Init(new BoardGrid(level.W, level.H), null, null, null);
                view.Show(s, false);
                var marks = StarTrail.Of(s).Marks();
                Assert.AreEqual(Rules.Steps, marks.RemoveAll(m => m.Kind == TrailMarkKind.Ring), "the ring at the feet is BoardMarksView's, not a quad");
                Assert.AreEqual(marks.Count, view.MarkCount);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(marks.Count * 4, mesh.vertexCount);
                Assert.IsTrue(go.GetComponent<MeshRenderer>().enabled);
                Assert.AreEqual(1 << 5, view.Map.Bytes[view.Map.Index(6, 0, 0)], "step 6 on cell 6");
                Assert.AreSame(StarTrailPreset.Default, view.Preset);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
