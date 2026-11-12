using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>U5 clear (docs/ui-u5-kit.md 3, U5-result): star stones, Next by the exit, Map / Retry. Owner: gameplay-ui.</summary>
    public sealed class U5ClearViewTests
    {
        sealed class FakePanel : SevenSteps.Game.Flow.IResultPanel
        {
            public readonly List<string> Calls = new List<string>();
            public void Show(ResultModel m) => Calls.Add(m.Cleared ? "clear" : "loss");
            public void Hide() => Calls.Add("hide");
        }

        U5ClearView view;
        FakePanel loss;

        static Localizer Loc()
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
        }

        [SetUp]
        public void Build()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = a.uiTheme;
            loss = new FakePanel();
            view = U5ClearView.Build(a.uiIcons, Loc(), loss);
        }

        [TearDown]
        public void Clean()
        {
            if (view != null) Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void AClearShowsLitStonesHidesTheHudsAndPutsTheWordsOnNext()
        {
            var hud = new List<bool>();
            view.HudHidden += hud.Add;
            view.Show(ResultModel.Clear(3, 3, 3, 3, false, stars: 2, bestStars: 2));
            Assert.That(view.IsShown, Is.True);
            Assert.That(view.LitStones, Is.EqualTo(2), "Edit Mode: earned stones lit at once");
            Assert.That(hud, Is.EqualTo(new[] { true }));
            Assert.That(view.Next.TipKey, Is.EqualTo("ui5.icon.next"));
            Assert.That(view.Next.Rect.sizeDelta, Is.EqualTo(new Vector2(56, 56)));
            Assert.That(view.Stones[2].TipKey, Is.EqualTo("result.star3_many"));
            Assert.That(view.Stones[2].TipArgs, Is.EqualTo(new object[] { 3 }));
            Assert.That(loss.Calls, Is.Empty);
            view.Hide();
            Assert.That(view.IsShown, Is.False);
            Assert.That(hud, Is.EqualTo(new[] { true, false }));
            Assert.That(loss.Calls, Is.EqualTo(new[] { "hide" }), "the loss panel is told too");
        }

        [Test]
        public void TheLastStageAsksForTheEnding()
        {
            view.Show(ResultModel.Clear(49, 3, 2, 2, true, stars: 1, bestStars: 1));
            Assert.That(view.Next.TipKey, Is.EqualTo("result.ending"));
        }

        [Test]
        public void ALossGoesToTheCurrentPanel()
        {
            view.Show(ResultModel.Loss(3, 3, false));
            Assert.That(view.IsShown, Is.False);
            Assert.That(loss.Calls, Is.EqualTo(new[] { "loss" }));
        }

        [Test]
        public void ButtonsRaiseTheFlowEvents()
        {
            var got = new List<string>();
            view.NextRequested += () => got.Add("next");
            view.RetryRequested += () => got.Add("retry");
            view.StageSelectRequested += () => got.Add("map");
            view.Show(ResultModel.Clear(3, 3, 3, 3, false, stars: 3, bestStars: 3));
            view.Next.Press();
            view.Retry.Press();
            view.Map.Press();
            Assert.That(got, Is.EqualTo(new[] { "next", "retry", "map" }));
        }

        [Test]
        public void StonesStandOverTheBrotherUnderTheTopButtons()
        {
            var canvas = new Vector2(1280, 720);
            var r = U5ClearView.StoneRow(new Vector2(780, 300), canvas);
            Assert.That(r.center.x, Is.EqualTo(780).Within(0.01f));
            Assert.That(r.yMax, Is.EqualTo(300 - U5ClearView.StoneLift).Within(0.01f));
            r = U5ClearView.StoneRow(new Vector2(1270, 40), canvas);
            Assert.That(r.xMax, Is.LessThanOrEqualTo(1256));
            Assert.That(r.y, Is.GreaterThanOrEqualTo(24 + 48 + 8), "under the Map / Retry band");
        }

        [Test]
        public void NextGoesBesideTheBoardOnTheExitsSide()
        {
            var canvas = new Vector2(1280, 720);
            var board = new Rect(300, 150, 680, 380);
            var right = U5ClearView.NextSpot(board, new Vector2(940, 300), canvas);
            Assert.That(right.x, Is.EqualTo(board.xMax + U5ClearView.NextGap).Within(0.01f));
            Assert.That(right.Overlaps(board), Is.False);
            var below = U5ClearView.NextSpot(board, new Vector2(640, 510), canvas);
            Assert.That(below.y, Is.EqualTo(board.yMax + U5ClearView.NextGap).Within(0.01f));
            Assert.That(below.size, Is.EqualTo(new Vector2(56, 56)));
        }

        [Test]
        public void BuildAsserts()
        {
            Assert.Throws<ArgumentNullException>(() => U5ClearView.Build(null, null, loss));
            Assert.Throws<ArgumentNullException>(() => U5ClearView.Build(null, Loc(), null));
            Assert.Throws<ArgumentNullException>(() => view.Show(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => IconButton.Build(view.transform, UiIcon.Next, null, size: 40));
        }
    }
}
