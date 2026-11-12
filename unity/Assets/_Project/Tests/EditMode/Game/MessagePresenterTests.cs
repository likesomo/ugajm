using System;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.Tests.Game
{
    /// <summary>Message line words for the hinted restart (user decision 2026-11-12-b (2)) and one-time guide lines.</summary>
    public sealed class MessagePresenterTests
    {
        static Localizer Loc(Language lang)
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, lang);
        }

        static Level Long() => Level.Parse("long", "", 3, new[] { "#############", "#S.........E#", "#############" });

        static (GameSession, MessagePresenter, Localizer) Make(Func<bool> hinted)
        {
            var loc = Loc(Language.Ko);
            var m = new MessagePresenter(loc);
            var s = new GameSession();
            s.Updated += u => m.Apply(u, hinted());
            s.Load(Long());
            return (s, m, loc);
        }

        [Test]
        public void ARestartInAHintedAttemptSaysTheStarCap()
        {
            bool hinted = false;
            var (s, m, loc) = Make(() => hinted);
            s.Send(Command.Move(Dir.Right));
            s.Send(Command.Restart);
            Assert.That(m.Current, Is.EqualTo(loc.Get("msg.restart")));
            hinted = true;
            s.Send(Command.Move(Dir.Right));
            s.Send(Command.Restart);
            Assert.That(m.Current, Is.EqualTo("힌트를 본 판은 다시 해도 별 최대 1개."));
            loc.Set(Language.En);
            Assert.That(m.Current, Is.EqualTo("Hinted run: max 1 star even after restart."), "worded on each read");
            s.Send(Command.Move(Dir.Right));
            Assert.That(m.Current, Is.EqualTo(""), "the next step clears it");
        }

        [Test]
        public void AttemptCountsAreHintedOnlyAfterACostingHint()
        {
            Assert.IsFalse(default(AttemptCounts).Hinted);
            Assert.IsFalse(default(AttemptCounts).WithUndo().WithRestart().Hinted);
            Assert.IsTrue(default(AttemptCounts).WithHint().Hinted);
            Assert.IsTrue(default(AttemptCounts).WithHint().WithRestart().Hinted, "a restart stays in the attempt");
            Assert.AreEqual(1, Stars.Of(3, 3, default(AttemptCounts).WithHint().Hints), "the cap the mark stands for");
        }

        [Test]
        public void GuideLinesComeFromTheTableInBothLanguages()
        {
            var (_, m, loc) = Make(() => false);
            m.ShowKey(TutorialTracker.SlideLineKey);
            Assert.That(m.Current, Does.Contain("얼음"));
            loc.Set(Language.En);
            Assert.That(m.Current, Does.StartWith("On ice"));
            Assert.Throws<ArgumentException>(() => m.ShowKey("tutorial.no_such_line"));
            Assert.Throws<ArgumentException>(() => m.ShowKey(""));
        }
    }
}
