using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>U5 chapter card (lead 2026-11-12): the planet name and its line only, U5 kit icon + focus plate. Owner: gameplay-ui.</summary>
    public sealed class U5ChapterCardTests
    {
        U5ChapterCardView card;
        Localizer loc;

        static readonly string[] PlanetsKo = { "이끼의 궤도", "별구멍 달", "새김돌 행성", "서리 위성", "결구의 행성", "무너지는 행성", "원형 제단" };

        static Localizer Loc()
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
        }

        static ChapterCardModel Frost(string intro = "국자를 닫아야 자루를 뻗는다.") => new ChapterCardModel("frost", 4, "서리 위성", intro, PlanetsKo);

        [SetUp]
        public void Build()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = a.uiTheme;
            loc = Loc();
            card = U5ChapterCardView.Build(a.uiIcons, loc);
        }

        [TearDown]
        public void Clean()
        {
            if (card != null) Object.DestroyImmediate(card.gameObject);
        }

        static Rect Box(RectTransform r) => new Rect(r.anchorMin.x * 1280 + r.anchoredPosition.x - r.pivot.x * r.sizeDelta.x,
            (1 - r.anchorMin.y) * 720 - r.anchoredPosition.y - (1 - r.pivot.y) * r.sizeDelta.y, r.sizeDelta.x, r.sizeDelta.y);

        [Test]
        public void ShowsOnlyThePlanetNameAndItsLine()
        {
            Assert.IsFalse(card.Visible, "built hidden");
            card.Bind(Frost());
            card.Show();
            Assert.IsTrue(card.Visible);
            Assert.That(card.Planet.text, Is.EqualTo("서리 위성"));
            Assert.That(card.Line.text, Is.EqualTo("국자를 닫아야 자루를 뻗는다."));
            // No chapter number, no row of the other planets.
            foreach (var t in card.GetComponentsInChildren<Text>(true))
            {
                Assert.That(t.text, Does.Not.Contain("챕터").And.Not.Contain("Chapter"), t.name);
                foreach (var other in PlanetsKo) if (other != "서리 위성") Assert.That(t.text, Does.Not.Contain(other), t.name);
            }
            var name = Box(card.NamePlate);
            var line = Box(card.LinePlate);
            Assert.That(name.xMin, Is.GreaterThanOrEqualTo(24f));
            Assert.That(name.xMax, Is.LessThanOrEqualTo(1256f));
            Assert.That(line.yMin, Is.GreaterThanOrEqualTo(name.yMax), "the line under the name");
            Assert.That(line.width, Is.LessThanOrEqualTo(U5ChapterCardView.LineWidth));
            Assert.That(Box(card.Continue.Rect).yMin, Is.GreaterThanOrEqualTo(line.yMax), "the icon under the line");
            Assert.That(card.Planet.fontSize, Is.EqualTo(U5ChapterCardView.NameSize));
        }

        [Test]
        public void NoLineHidesItsPlate()
        {
            card.Bind(Frost(""));
            card.Show();
            Assert.IsFalse(card.LinePlate.gameObject.activeSelf);
            Assert.IsTrue(card.NamePlate.gameObject.activeSelf);
        }

        [Test]
        public void ContinueIconHasTheKitBackingItsWordsAndCloses()
        {
            card.Bind(Frost());
            card.Show();
            Assert.IsNotNull(card.Continue.transform.Find("Backing"), "round backing (14.3)");
            Assert.That(card.Continue.Rect.sizeDelta, Is.EqualTo(new Vector2(IconButton.Size, IconButton.Size)));
            card.Continue.OnSelect(null);
            Assert.IsTrue(card.Tips.Showing);
            Assert.That(card.Tips.Text.text, Is.EqualTo("계속하기"));
            loc.Set(Language.En);
            Assert.That(card.Tips.Text.text, Is.EqualTo("Continue"));
            int closes = 0;
            card.CloseRequested += () => closes++;
            card.Continue.Press();
            Assert.AreEqual(1, closes);
            card.Hide();
            Assert.IsFalse(card.Visible);
            Assert.IsFalse(card.Tips.Showing, "no tip left over for the next card");
        }

        [Test]
        public void AssertsItsInputs()
        {
            Assert.Throws<InvalidOperationException>(() => card.Show(), "Show before Bind");
            Assert.Throws<ArgumentNullException>(() => card.Bind(null));
            Assert.Throws<ArgumentNullException>(() => U5ChapterCardView.Build(null, null));
            LogAssert.Expect(LogType.Error, new Regex("U5 chapter card: line .* lines > 2"));
            card.Bind(Frost(string.Concat(System.Linq.Enumerable.Repeat("국자를 닫아야 자루를 뻗는다. ", 8))));
        }

        [Test]
        public void EveryChapterCardFitsInBothLanguages()
        {
            LevelCatalog.Load(AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset"));
            foreach (var lang in new[] { Language.Ko, Language.En })
            {
                loc.Set(lang);
                foreach (var ch in LevelCatalog.Chapters)
                {
                    var m = LevelCatalog.ChapterCard(ch.id, lang);
                    Assert.IsNotNull(m, ch.id);
                    card.Bind(m); // logs an error (fails) past 2 lines
                    var name = Box(card.NamePlate);
                    Assert.That(name.xMin, Is.GreaterThanOrEqualTo(24f), $"{ch.id} {lang}");
                    Assert.That(name.xMax, Is.LessThanOrEqualTo(1256f), $"{ch.id} {lang}");
                }
            }
        }
    }
}
