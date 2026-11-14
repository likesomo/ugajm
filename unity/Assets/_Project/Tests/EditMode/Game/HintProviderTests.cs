using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;

namespace SevenSteps.Tests
{
    /// H key text (HintProvider): repeated moves grouped with hint.repeat so the longest route fits the HUD side column.
    public sealed class HintProviderTests
    {
        static Localizer TwoLanguages(Language current) => new Localizer(new[]
        {
            StringTable.FromJson(Language.Ko, UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/_Project/Data/Strings/ko.json").text),
            StringTable.FromJson(Language.En, UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/_Project/Data/Strings/en.json").text),
        }, current);

        static readonly Level[] Levels =
        {
            Level.Parse("straight", "", 3, new[] { "##########", "#S......E#", "##########" }),
            Level.Parse("late", "", 3, new[] { "#######", "#.....#", "#S..E.#", "#.....#", "#######" }),
        };

        [Test]
        public void RepeatedMovesAreGrouped()
        {
            var loc = TwoLanguages(Language.En);
            var hints = new HintProvider(Levels, loc);
            Assert.AreEqual("Shortest solution from the start: Right x7", hints.For(0));
            loc.Set(Language.Ko);
            Assert.AreEqual("처음부터 푸는 최단 풀이: 우 7번", hints.For(0), "reworded in the new language, route cached");
        }

        [Test]
        public void SingleMovesStayWordsSeparatedByCommas()
        {
            var hints = new HintProvider(Levels, TwoLanguages(Language.En));
            string text = hints.For(1);
            StringAssert.StartsWith("Shortest solution from the start: ", text);
            string route = text.Substring("Shortest solution from the start: ".Length);
            int steps = 0;
            foreach (var part in route.Split(new[] { ", " }, System.StringSplitOptions.None))
            {
                var bits = part.Split(new[] { " x" }, System.StringSplitOptions.None);
                CollectionAssert.Contains(new[] { "Up", "Down", "Left", "Right", "KEEP", "SWITCH" }, bits[0], part);
                steps += bits.Length == 2 ? int.Parse(bits[1]) : 1;
            }
            Assert.AreEqual(Solver.Solve(Levels[1]).Path.Count, steps, "every move counted once");
        }
    }
}
