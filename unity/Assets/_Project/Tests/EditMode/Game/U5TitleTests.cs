using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>
    /// U5 title, language and credits (docs/ui-u5-kit.md 3; UiStyleToggle on): sizes and corners from touch-targets.csv,
    /// the requests each control raises, the caption for continue / first step, the language picker's preview / keep, the
    /// credits' close, plates before their text (14.4), argument asserts, and the toggle that turns the v4 map on with U5.
    /// Owner: scene-flow.
    /// </summary>
    public sealed class U5TitleTests
    {
        readonly List<Object> made = new List<Object>();

        static Localizer Loc(Language lang)
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, lang);
        }

        static GameAssets Assets() => AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");

        [SetUp]
        public void SetTheme() => Ui.Theme = Assets().uiTheme;

        [TearDown]
        public void Clean()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        RectTransform Canvas()
        {
            var go = new GameObject("U5 title test canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(go);
            var r = (RectTransform)go.transform;
            r.sizeDelta = new Vector2(1280, 720);
            return r;
        }

        /// A control's rect on the 1280x720 canvas, top-left origin.
        static Rect OnCanvas(RectTransform canvas, RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            Vector2 a = canvas.InverseTransformPoint(c[0]), b = canvas.InverseTransformPoint(c[2]);
            var size = canvas.rect.size;
            return Rect.MinMaxRect(a.x + size.x / 2, size.y / 2 - b.y, b.x + size.x / 2, size.y / 2 - a.y);
        }

        static void Near(Rect expected, Rect actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 0.6f, what + " x");
            Assert.AreEqual(expected.y, actual.y, 0.6f, what + " y");
            Assert.AreEqual(expected.width, actual.width, 0.6f, what + " width");
            Assert.AreEqual(expected.height, actual.height, 0.6f, what + " height");
        }

        [Test]
        public void TitleControlsSitWhereTheTouchTargetsSay()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var v = new U5TitleView(canvas, icons, FocusPlate.Build(canvas, Loc(Language.Ko), icons));
            v.Apply(Loc(Language.Ko));
            v.Show(true);
            UnityEngine.Canvas.ForceUpdateCanvases();
            Near(new Rect(608, 503, 64, 64), OnCanvas(canvas, v.Play.Rect), "play");
            Near(new Rect(24, 24, 48, 48), OnCanvas(canvas, v.Map.Rect), "map");
            Near(new Rect(1208, 24, 48, 48), OnCanvas(canvas, v.Settings.Rect), "settings");
            Near(new Rect(1208, 648, 48, 48), OnCanvas(canvas, v.Language.Rect), "language");
            Near(new Rect(24, 648, 48, 48), OnCanvas(canvas, v.Credits.Rect), "credits");
            Assert.AreSame(v.Play, v.DefaultButton);
        }

        [Test]
        public void TitleRaisesOneRequestPerControlAndCaptionsThePlayStone()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var loc = Loc(Language.Ko);
            var v = new U5TitleView(canvas, icons, FocusPlate.Build(canvas, loc, icons));
            v.Apply(loc);
            var got = new List<string>();
            v.PlayRequested += () => got.Add("play");
            v.MapRequested += () => got.Add("map");
            v.SettingsRequested += () => got.Add("settings");
            v.LanguageRequested += () => got.Add("language");
            v.CreditsRequested += () => got.Add("credits");
            foreach (var b in new[] { v.Play, v.Map, v.Settings, v.Language, v.Credits }) b.Press();
            CollectionAssert.AreEqual(new[] { "play", "map", "settings", "language", "credits" }, got);

            v.Show(true);
            Assert.AreEqual(loc.Get("ui5.title.continue"), v.Caption);
            v.Show(false);
            Assert.AreEqual(loc.Get("ui5.title.begin"), v.Caption);
            loc.Set(Language.En);
            v.Apply(loc);
            Assert.AreEqual(loc.Get("ui5.title.begin"), v.Caption, "the caption follows the language");
            Assert.IsTrue(v.Visible);
            v.Hide();
            Assert.IsFalse(v.Visible);
        }

        [Test]
        public void TitleTaglineShowsOnlyWithAStoryLineOnItsOwnPlate()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var v = new U5TitleView(canvas, icons, FocusPlate.Build(canvas, Loc(Language.Ko), icons));
            v.Apply(Loc(Language.Ko));
            var tagline = canvas.Find("Title U5/Tagline");
            var plate = canvas.Find("Title U5/Tagline plate");
            Assert.IsFalse(tagline.gameObject.activeSelf, "no story line: no tagline");
            v.Apply(Loc(Language.Ko), "별이 다 꺼지기 전에, 다시 잇는다.");
            Assert.IsTrue(tagline.gameObject.activeSelf);
            Assert.Less(plate.GetSiblingIndex(), tagline.GetSiblingIndex(), "the plate is drawn before its text (14.4)");
            Assert.Less(canvas.Find("Title U5/Caption plate").GetSiblingIndex(), canvas.Find("Title U5/Caption").GetSiblingIndex());
        }

        [Test]
        public void LanguagePickerPreviewsTheNextLanguageAndKeepsWithNext()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var loc = Loc(Language.Ko);
            var v = new U5LanguageView(canvas, icons, FocusPlate.Build(canvas, loc, icons));
            v.Apply(loc);
            var picked = new List<Language>();
            int kept = 0;
            v.LanguagePicked += picked.Add;
            v.ContinueRequested += () => kept++;
            v.Cycle.Press();
            CollectionAssert.AreEqual(new[] { Language.En }, picked, "the language icon previews the next language");
            v.NameButton(Language.Ko).onClick.Invoke();
            Assert.AreEqual(Language.Ko, picked[1], "a name previews its language");
            v.Next.Press();
            Assert.AreEqual(1, kept);
            Assert.AreEqual("language.continue", v.Next.TipKey, "next keeps this language (not the next stage)");
            Assert.AreSame(v.Next, v.DefaultButton);
            Assert.AreEqual(Ui.Gold, v.NameButton(Language.Ko).GetComponentInChildren<Text>().color, "the shown language in gold");
        }

        [Test]
        public void CreditsCloseWithBackAndCarryTheCreditLines()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var loc = Loc(Language.Ko);
            var credits = new TextAsset("Font: Jua (SIL OFL 1.1)");
            made.Add(credits);
            var license = new TextAsset("SIL OPEN FONT LICENSE Version 1.1") { name = "Jua-OFL" };
            made.Add(license);
            var v = new U5CreditsView(canvas, icons, FocusPlate.Build(canvas, loc, icons), credits, new[] { license });
            v.Apply(loc);
            int closed = 0;
            v.CloseRequested += () => closed++;
            v.Back.Press();
            Assert.AreEqual(1, closed);
            var body = canvas.Find("Credits U5/Credits plate/Scroll/Content").GetComponent<Text>();
            StringAssert.Contains(loc.Get("credits.game"), body.text);
            StringAssert.Contains("Jua (SIL OFL 1.1)", body.text);
            StringAssert.Contains("-- Jua-OFL --", body.text, "license texts travel with the credits");
            Assert.IsTrue(body.GetComponent<UiLayout>().secondary, "credit text is 14.2 secondary");
        }

        [Test]
        public void MapControlsAreBackAndSettingsInTheTopCorners()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var v = new U5MapControls(canvas, icons, FocusPlate.Build(canvas, Loc(Language.Ko), icons));
            Assert.IsFalse(v.Visible);
            v.SetShown(true);
            v.SetShown(true);
            UnityEngine.Canvas.ForceUpdateCanvases();
            Near(new Rect(24, 24, 48, 48), OnCanvas(canvas, v.Back.Rect), "back");
            Near(new Rect(1208, 24, 48, 48), OnCanvas(canvas, v.Settings.Rect), "settings");
            var got = new List<string>();
            v.BackRequested += () => got.Add("back");
            v.SettingsRequested += () => got.Add("settings");
            v.Back.Press();
            v.Settings.Press();
            CollectionAssert.AreEqual(new[] { "back", "settings" }, got);
            Assert.AreEqual("Esc", v.Back.Shortcut);
            v.SetShown(false);
            Assert.IsFalse(v.Visible);
            Assert.Throws<ArgumentNullException>(() => new U5MapControls(canvas, icons, null));
        }

        [Test]
        public void BootSparksAreThreeScarvesLightingInTurn()
        {
            var canvas = Canvas();
            var v = new ScarfSparks(canvas);
            UnityEngine.Canvas.ForceUpdateCanvases();
            for (int k = 0; k < 3; k++)
            {
                var r = OnCanvas(canvas, v.Spark(k).rectTransform);
                Assert.AreEqual(640 + (k - 1) * ScarfSparks.Pitch, r.center.x, 0.6f, $"spark {k + 1} x");
                Assert.AreEqual(ScarfSparks.CenterY, r.center.y, 0.6f, $"spark {k + 1} y");
                var scarf = SevenSteps.Game.UI.Planets.PlanetMap3DView.Scarves[k];
                Assert.AreEqual(scarf.r, v.Spark(k).color.r, 0.001f, "the brother's scarf colour");
            }
            Assert.AreEqual(0, canvas.GetComponentsInChildren<Text>(true).Length, "no words, no progress");
            int Brightest(float t)
            {
                v.Animate(t);
                int best = 0;
                for (int k = 1; k < 3; k++) if (v.Spark(k).color.a > v.Spark(best).color.a) best = k;
                return best;
            }
            float third = ScarfSparks.Period / 3f;
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { Brightest(third * 0.5f), Brightest(third * 1.5f), Brightest(third * 2.5f) }, "left to right");
            Assert.Throws<ArgumentNullException>(() => new ScarfSparks(null));
        }

        [Test]
        public void EndingCardsUseIconsAndTheLastOneOffersTheMap()
        {
            var canvas = Canvas();
            var loc = Loc(Language.Ko);
            var v = new U5EndingView(canvas, null, loc, Assets().uiIcons);
            int next = 0, skip = 0;
            v.NextRequested += () => next++;
            v.SkipRequested += () => skip++;
            v.ShowPage(0, 4, new EndingPage("첫 장", "", "a"));
            v.Show();
            Assert.IsTrue(v.Visible);
            Assert.AreEqual("첫 장", v.Text);
            Assert.IsTrue(v.Next.Shown && v.Skip.Shown && !v.Map.Shown, "a middle card: next and skip");
            v.Next.Press();
            v.Skip.Press();
            Assert.AreEqual((1, 1), (next, skip));
            v.ShowPage(3, 4, new EndingPage("함께, 다시 한 걸음", "", "d"));
            Assert.IsTrue(v.Map.Shown && !v.Next.Shown && !v.Skip.Shown, "the last card: the map only");
            v.Map.Press();
            Assert.AreEqual(2, next, "the map is the last card's next (to the map)");
            Assert.AreEqual("ending.to_map", v.Map.TipKey);
            foreach (var b in new[] { v.Next, v.Map, v.Skip }) Assert.IsNotNull(b.transform.Find("Backing"), $"{b.name}: the round backing (3:1 over the picture)");
            Assert.AreEqual(0, canvas.GetComponentsInChildren<Button>(true).Length - 1, "no text buttons: only the card's own click (scrim)");
            Assert.Throws<ArgumentOutOfRangeException>(() => v.ShowPage(4, 4, new EndingPage("x", "", "x")));
            Assert.Throws<ArgumentNullException>(() => new U5EndingView(null, null, loc, null));
        }

        [Test]
        public void ViewsAssertTheirArguments()
        {
            var canvas = Canvas();
            var icons = Assets().uiIcons;
            var tips = FocusPlate.Build(canvas, Loc(Language.Ko), icons);
            Assert.Throws<ArgumentNullException>(() => new U5TitleView(null, icons, tips));
            Assert.Throws<ArgumentNullException>(() => new U5TitleView(canvas, icons, null));
            Assert.Throws<ArgumentNullException>(() => new U5LanguageView(canvas, icons, null));
            Assert.Throws<ArgumentNullException>(() => new U5CreditsView(null, icons, tips, null, null));
            var v = new U5TitleView(canvas, icons, tips);
            Assert.Throws<ArgumentNullException>(() => v.Apply(null));
        }

        [Test]
        public void StoneSpriteIsMadeOnce()
        {
            var a = StoneSprite.Get();
            Assert.AreSame(a, StoneSprite.Get());
            Assert.AreEqual(StoneSprite.Width, (int)a.rect.width);
            Assert.IsFalse(a.texture.isReadable, "no CPU copy kept (made once, GPU only)");
        }

        [Test]
        public void U5TurnsTheV4MapOnUnlessOldMapIsAsked()
        {
            LaunchOptions L(params string[] a) { var all = new List<string> { "x.exe" }; all.AddRange(a); return LaunchOptions.Parse(all.ToArray(), ""); }
            Assert.IsTrue(StageSelectToggle.On(L("-ui5")), "U5: the v4 map is its stage select");
            Assert.IsFalse(StageSelectToggle.On(L("-ui5", "-oldmap")), "an explicit -oldmap wins");
            Assert.AreEqual(StageSelectToggle.DefaultOn || UiStyleToggle.DefaultOn, StageSelectToggle.On(L()));
            Assert.AreEqual(StageSelectToggle.DefaultOn, JourneyToggle.On(L("-ui5")), "the U5 map alone does not bring the journey (its clear is the result)");
            Assert.IsTrue(JourneyToggle.On(L("-ui5", "-newmap")), "-newmap still does");
        }
    }
}
