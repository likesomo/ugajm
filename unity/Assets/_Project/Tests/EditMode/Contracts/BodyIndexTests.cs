using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 12.6: body index and statue owner come from the State undo chain only.
    public class BodyIndexTests
    {
        const int W = 11;
        static Level Field() => Level.Parse("t", "", 3, new[]
        {
            "###########",
            "#S........#",
            "#.........#",
            "###########",
        });

        static State Do(State s, Command c) => Rules.Dispatch(s, c).State;
        static State Walk(State s, params Dir[] dirs) { foreach (var d in dirs) s = Do(s, Command.Move(d)); return s; }
        static readonly Dir R = Dir.Right, D = Dir.Down;

        [Test]
        public void KeepStatuesRememberTheirBody()
        {
            var s = Rules.NewState(Field());
            Assert.AreEqual(0, BodyIndex.Current(s));

            s = Walk(s, R, R, R, R, R, R, R);                 // body 0 ends on (8,1)
            Assert.AreEqual(Status.Choose, s.Status);
            s = Do(s, Command.Keep);
            Assert.AreEqual(1, BodyIndex.Current(s));
            Assert.AreEqual(0, BodyIndex.OfStatue(s, 1 * W + 8));

            s = Walk(s, D, R, R, R, R, R, R);                 // body 1 ends on (7,2)
            s = Do(s, Command.Keep);
            Assert.AreEqual(2, BodyIndex.Current(s));
            Assert.AreEqual(0, BodyIndex.OfStatue(s, 1 * W + 8));
            Assert.AreEqual(1, BodyIndex.OfStatue(s, 2 * W + 7));
            Assert.AreEqual(-1, BodyIndex.OfStatue(s, 1 * W + 1));

            s = Do(s, Command.Undo);                          // back to body 1 choosing: its statue is gone
            Assert.AreEqual(1, BodyIndex.Current(s));
            Assert.AreEqual(-1, BodyIndex.OfStatue(s, 2 * W + 7));
            Assert.AreEqual(0, BodyIndex.OfStatue(s, 1 * W + 8));

            s = Do(s, Command.Restart);
            Assert.AreEqual(0, BodyIndex.Current(s));
            Assert.AreEqual(-1, BodyIndex.OfStatue(s, 1 * W + 8));
        }

        [Test]
        public void TileSetFallsBackAndCycles()
        {
            var set = ScriptableObject.CreateInstance<TileSet>();
            var a = new GameObject("A");
            try
            {
                set.player = new TileVisual { prefab = a };
                Assert.AreSame(a, set.PlayerFor(2).prefab, "empty playerBodies -> player");
                Assert.AreEqual(set.BodyColor(0), set.BodyColor(3), "colors cycle by 3");
                Assert.AreNotEqual(set.BodyColor(0), set.BodyColor(1));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(set);
            }
        }
    }
}
