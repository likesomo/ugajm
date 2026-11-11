using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// HudView.LookOf: the three brother dots of the minimal HUD (walking / waiting / statue / spent), from the State alone.
    public sealed class HudDotTests
    {
        static State Play(State s, string moves)
        {
            foreach (char c in moves) { Assert.IsTrue(Rules.TryParseLetter(c, out var a)); s = Rules.Dispatch(s, Command.FromAct(a)).State; }
            return s;
        }

        static HudView.DotLook[] Looks(State s)
        {
            var owners = new List<int>();
            foreach (int cell in s.Statues) owners.Add(BodyIndex.OfStatue(s, cell));
            var looks = new HudView.DotLook[s.Level.Bodies];
            for (int k = 0; k < looks.Length; k++) looks[k] = HudView.LookOf(s, k, owners);
            return looks;
        }

        [Test]
        public void WalkingWaitingStoneAndSpent()
        {
            var level = Level.Parse("t", "", 3, new[] { "S.......", "........" });
            var s = Rules.NewState(level);
            CollectionAssert.AreEqual(new[] { HudView.DotLook.Walking, HudView.DotLook.Waiting, HudView.DotLook.Waiting }, Looks(s));
            s = Play(s, "RRRRRRRK"); // first brother leaves a statue
            CollectionAssert.AreEqual(new[] { HudView.DotLook.Stone, HudView.DotLook.Walking, HudView.DotLook.Waiting }, Looks(s));
            s = Play(s, "DRRRRRRX"); // second brother hands over without a statue
            CollectionAssert.AreEqual(new[] { HudView.DotLook.Stone, HudView.DotLook.Spent, HudView.DotLook.Walking }, Looks(s));
        }
    }
}
