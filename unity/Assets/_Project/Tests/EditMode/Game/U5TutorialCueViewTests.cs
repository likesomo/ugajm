using System;
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
    /// <summary>The tutorial cue plate and its skip icon (docs/tutorial.md 2.3). Owner: gameplay-ui.</summary>
    public sealed class U5TutorialCueViewTests
    {
        U5TutorialCueView view;
        Localizer loc;

        [SetUp]
        public void Build()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = a.uiTheme;
            LevelCatalog.Load(a);
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            loc = new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
            view = U5TutorialCueView.Build(a.uiIcons, loc);
        }

        [TearDown]
        public void Clean()
        {
            if (view != null) Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void EveryCueFitsTwoLinesInBothLanguages()
        {
            foreach (var lang in Languages.All)
            {
                loc.Set(lang);
                foreach (var t in LevelCatalog.Tutorials)
                    foreach (var c in t.cues)
                    {
                        string line = c.Line(Languages.Code(lang));
                        view.ShowCue(line, 0, 3);
                        int lines = FocusPlate.LineCount(view.Text, line, U5TutorialCueView.MaxWidth - 2 * U5TutorialCueView.PadX);
                        Assert.LessOrEqual(lines, U5TutorialCueView.MaxLines, $"{t.id} {c.on} ({lang}): {line}");
                        Assert.LessOrEqual(view.Plate.sizeDelta.x, U5TutorialCueView.MaxWidth + 2);
                    }
            }
        }

        [Test]
        public void AnEmptyLineHidesThePlateAndSkipIsAKitIcon()
        {
            Assert.IsFalse(view.Visible, "built hidden");
            view.Show();
            view.ShowCue("하나.", 0, 3);
            Assert.AreEqual("하나.", view.Line);
            view.ShowCue("", 1, 3);
            Assert.AreEqual("", view.Line);
            Assert.IsFalse(view.Plate.gameObject.activeSelf);
            Assert.IsNotNull(view.Skip.transform.Find("Backing"));
            Assert.AreEqual(U5TutorialCueView.SkipKey, view.Skip.TipKey);
            foreach (var lang in Languages.All)
            {
                loc.Set(lang);
                Assert.That(loc.Get(U5TutorialCueView.SkipKey), Is.Not.EqualTo(U5TutorialCueView.SkipKey));
                Assert.That(loc.Get(IconButton.KeyOf(UiIcon.Tutorial)), Is.Not.EqualTo(IconButton.KeyOf(UiIcon.Tutorial)));
            }
            bool skipped = false;
            view.SkipRequested += () => skipped = true;
            view.Skip.Press();
            Assert.IsTrue(skipped);
        }

        [Test]
        public void AssertsItsInputs()
        {
            Assert.Throws<ArgumentNullException>(() => U5TutorialCueView.Build(null, null));
            Assert.Throws<ArgumentNullException>(() => view.ShowCue(null, 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.ShowCue("a", 3, 3));
        }
    }
}
