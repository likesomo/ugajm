using System;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>The storybook viewer (design/story/cutscenes.md 2): plain card without art, lines one by one, fits. Owner: gameplay-ui.</summary>
    public sealed class U5CutsceneViewTests
    {
        U5CutsceneView view;
        Localizer loc;
        GameAssets assets;

        [SetUp]
        public void Build()
        {
            assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Ui.Theme = assets.uiTheme;
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            loc = new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
            view = U5CutsceneView.Build(assets.cutsceneImages, assets.uiIcons, loc);
        }

        [TearDown]
        public void Clean()
        {
            if (view != null) Object.DestroyImmediate(view.gameObject);
        }

        CutsceneCatalog Data() => CutsceneCatalog.FromJson(assets.cutscenesJson.text);

        [Test]
        public void ThePrologueShowsItsPictureAndItsLinesOneByOne()
        {
            var stack = new OverlayStack();
            var c = new CutsceneController(stack, view, () => loc.Current);
            Assert.IsFalse(view.Visible, "built hidden");
            bool? skipped = null;
            c.Begin(Data().Scene(CutsceneCatalog.Prologue), s => skipped = s);
            Assert.IsTrue(view.Visible);
            Assert.IsTrue(view.HasPicture, "the Codex picture of page 1");
            Assert.AreSame(assets.cutsceneImages.Get("cutscene.prologue.1"), view.Picture.sprite);
            Assert.IsFalse(view.Card.gameObject.activeSelf, "no plain card under a picture");
            Assert.AreEqual("고향 하늘의 일곱 별이 해마다 흐려진다.", view.ShownText);
            StringAssert.Contains("\n<color=#00000000>별이 다 꺼지면", view.Text.text, "laid out whole: the next line is there, clear");
            view.Next.Press();
            StringAssert.Contains("\n별이 다 꺼지면", view.ShownText, "the second line under the first");
            StringAssert.DoesNotContain("<color", view.Text.text, "every line out");
            view.Skip.Press();
            Assert.AreEqual(true, skipped);
            Assert.IsFalse(view.Visible);
            c.Dispose();
        }

        [Test]
        public void ASpriteForTheKeyIsShown()
        {
            var set = ScriptableObject.CreateInstance<StoryImageSet>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            set.entries = new[] { new StoryImageSet.Entry { key = "cutscene.prologue.1", sprite = sprite } };
            var v = U5CutsceneView.Build(set, assets.uiIcons, loc);
            try
            {
                v.ShowFrame(new CutsceneFrame("prologue", 0, 7, "cutscene.prologue.1", new[] { "가" }, 1));
                Assert.IsTrue(v.HasPicture);
                Assert.AreSame(sprite, v.Picture.sprite);
                Assert.IsFalse(v.Card.gameObject.activeSelf);
                v.ShowFrame(new CutsceneFrame("prologue", 1, 7, "cutscene.prologue.2", new[] { "가" }, 1));
                Assert.IsFalse(v.HasPicture, "the next page has no art yet");
            }
            finally
            {
                Object.DestroyImmediate(v.gameObject);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void TheRuleCaptionComesOutWithTheLastLineUnderTheLines()
        {
            var page = Data().Scene(CutsceneCatalog.Prologue).pages[5]; // 5b: SWITCH, two lines and a caption
            var lines = CutsceneCatalog.Lines(page, Language.Ko);
            string rule = CutsceneCatalog.Rule(page, Language.Ko);
            view.ShowFrame(new CutsceneFrame("prologue", 5, 8, page.image, lines, 1, rule));
            Assert.IsFalse(view.RuleVisible, "not before the last line");
            float tall = view.Box.rectTransform.offsetMax.y;
            Assert.AreEqual(U5CutsceneView.Inset + U5CutsceneView.BoxHeight + U5CutsceneView.RuleRoom, tall, 0.01f, "the room is there from line 1");
            view.ShowFrame(new CutsceneFrame("prologue", 5, 8, page.image, lines, lines.Length, rule));
            Assert.IsTrue(view.RuleVisible);
            Assert.AreEqual(rule, view.Rule.text);
            Assert.AreEqual(Ui.Gold, view.Rule.color, "the rule voice: gold");
            Assert.AreSame(Ui.Theme.FontFor(Language.Ko, FontRole.Title), view.Rule.font);
            Assert.Less(view.Rule.fontSize, U5CutsceneView.LineSize, "smaller than the story lines");
            Assert.LessOrEqual(view.Rule.rectTransform.offsetMax.y, view.Text.rectTransform.offsetMin.y, "under the lines, not over them");
            var first = Data().Scene(CutsceneCatalog.Prologue).pages[0];
            view.ShowFrame(new CutsceneFrame("prologue", 0, 8, first.image, CutsceneCatalog.Lines(first, Language.Ko), 2));
            Assert.IsFalse(view.RuleVisible);
            Assert.AreEqual(U5CutsceneView.Inset + U5CutsceneView.BoxHeight, view.Box.rectTransform.offsetMax.y, 0.01f, "no caption: the usual plate");
        }

        [Test]
        public void EveryRuleCaptionFitsItsRowsInBothLanguages()
        {
            foreach (var lang in Languages.All)
            {
                view.Rule.font = Ui.Theme.FontFor(lang, FontRole.Title);
                foreach (var s in Data().Scenes)
                    foreach (var p in s.pages)
                    {
                        if (!CutsceneCatalog.HasRule(p)) continue;
                        int rows = FocusPlate.LineCount(view.Rule, CutsceneCatalog.Rule(p, lang), U5CutsceneView.TextWidth);
                        Assert.LessOrEqual(rows, U5CutsceneView.RuleRows, $"{p.image} rule ({lang}): {rows} rows");
                    }
            }
        }

        [Test]
        public void EveryPageFitsTheTextPlateInBothLanguages()
        {
            foreach (var lang in Languages.All)
            {
                loc.Set(lang);
                view.Text.font = Ui.Theme.FontFor(lang, FontRole.Body);
                foreach (var s in Data().Scenes)
                    foreach (var p in s.pages)
                    {
                        int rows = U5CutsceneView.Rows(view.Text, CutsceneCatalog.Lines(p, lang));
                        Assert.LessOrEqual(rows, U5CutsceneView.MaxRows, $"{p.image} ({lang}): {rows} rows");
                    }
            }
        }

        [Test]
        public void SkipHasTheKitBackingAndBothIconsHaveTheirWords()
        {
            Assert.IsNotNull(view.Skip.transform.Find("Backing"), "3:1 over any picture");
            Assert.AreEqual(U5CutsceneView.NextKey, view.Next.TipKey);
            Assert.AreEqual(IconButton.KeyOf(UiIcon.Skip), view.Skip.TipKey);
            foreach (var lang in Languages.All)
            {
                loc.Set(lang);
                Assert.That(loc.Get(U5CutsceneView.NextKey), Is.Not.Empty.And.Not.EqualTo(U5CutsceneView.NextKey));
                Assert.That(loc.Get(IconButton.KeyOf(UiIcon.Skip)), Is.Not.Empty.And.Not.EqualTo(IconButton.KeyOf(UiIcon.Skip)));
                Assert.That(loc.Get(IconButton.KeyOf(UiIcon.Story)), Is.Not.Empty.And.Not.EqualTo(IconButton.KeyOf(UiIcon.Story)));
            }
            Assert.AreEqual(U5CutsceneView.SortingOrder, view.GetComponent<Canvas>().sortingOrder);
        }

        [Test]
        public void AssertsItsInputs()
        {
            Assert.Throws<ArgumentNullException>(() => U5CutsceneView.Build(null, null, null));
            Assert.Throws<ArgumentNullException>(() => view.ShowFrame(null));
            Assert.Throws<InvalidOperationException>(() => view.Show(), "Show before a frame");
        }
    }
}
