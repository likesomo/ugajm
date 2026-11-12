using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.U5;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>U5 UI kit (docs/ui-u5-kit.md): icons, icon set, focus tip model, buttons, plate, row. Owner: gameplay-ui.</summary>
    public sealed class U5KitTests
    {
        static readonly UiIcon[] AllIcons = (UiIcon[])Enum.GetValues(typeof(UiIcon));
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

        Transform Canvas(string name = "U5 test canvas")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            made.Add(go);
            var r = (RectTransform)go.transform;
            r.sizeDelta = new Vector2(1280, 720);
            return r;
        }

        static PointerEventData Pointer(int id) => new PointerEventData(null) { pointerId = id, button = PointerEventData.InputButton.Left };

        // ---------- icons and the icon set ----------

        [Test]
        public void EveryIconHasAPlaceholderInsideItsBox()
        {
            float limit = 0.5f - IconRaster.HalfWidth;
            foreach (var id in AllIcons)
            {
                var strokes = IconShapes.Of(id);
                Assert.That(strokes.Count, Is.GreaterThan(0), id.ToString());
                foreach (var s in strokes)
                    foreach (var p in new[] { s.A, s.B })
                        Assert.That(Mathf.Abs(p.x) <= limit && Mathf.Abs(p.y) <= limit, $"{id}: {p} outside the icon box");
                var sprite = IconRaster.Placeholder(id);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect.width, Is.EqualTo(IconRaster.Size));
            }
        }

        [Test]
        public void AnUndefinedIconThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IconShapes.Of((UiIcon)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => IconRaster.Placeholder((UiIcon)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiIconSet.ManifestId((UiIcon)999));
        }

        [Test]
        public void ManifestIdsAreKebabNamesAndRoundTrip()
        {
            Assert.That(UiIconSet.ManifestId(UiIcon.SoundOn), Is.EqualTo("sound-on"));
            Assert.That(UiIconSet.ManifestId(UiIcon.Undo), Is.EqualTo("undo"));
            foreach (var id in AllIcons)
            {
                Assert.That(UiIconSet.TryIcon(UiIconSet.ManifestId(id), out var back), Is.True, id.ToString());
                Assert.That(back, Is.EqualTo(id));
            }
            foreach (var other in UiIconSet.SpriteIds) Assert.That(UiIconSet.TryIcon(other, out _), Is.False, other);
            Assert.That(UiIconSet.TryIcon("nope", out _), Is.False);
        }

        [Test]
        public void IconSetUsesArtWhenSetAndPlaceholdersOtherwise()
        {
            var set = ScriptableObject.CreateInstance<UiIconSet>();
            made.Add(set);
            Assert.That(set.Missing(), Is.EquivalentTo(AllIcons));
            Assert.That(set.Get(UiIcon.Undo), Is.SameAs(IconRaster.Placeholder(UiIcon.Undo)));
            var art = IconRaster.Placeholder(UiIcon.Close); // any sprite stands in for Codex art
            set.SetIcon(UiIcon.Undo, art);
            set.SetIcon(UiIcon.Undo, art); // replaces, no second entry
            Assert.That(set.icons.Length, Is.EqualTo(1));
            Assert.That(set.Get(UiIcon.Undo), Is.SameAs(art));
            Assert.That(set.Missing(), Has.No.Member(UiIcon.Undo));
            Assert.That(set.SetSprite("step-star", art), Is.True);
            Assert.That(set.stepStar, Is.SameAs(art));
            Assert.That(set.SetSprite("undo", art), Is.False);
            Assert.That(UiIconSet.Of(null, UiIcon.Hint), Is.SameAs(IconRaster.Placeholder(UiIcon.Hint)));
        }

        [Test]
        public void GameAssetsReferenceTheIconSet()
        {
            var a = Assets();
            Assert.That(a.uiIcons, Is.Not.Null, "GameAssets.uiIcons (Data/UiIconSet.asset)");
            Assert.That(AssetDatabase.GetAssetPath(a.uiIcons), Is.EqualTo("Assets/_Project/Data/UiIconSet.asset"));
        }

        [Test]
        public void EveryIconAndChoiceHasWordsInBothLanguagesThatFitThePlate()
        {
            var parent = Canvas();
            foreach (var lang in new[] { Language.Ko, Language.En })
            {
                var loc = Loc(lang);
                var keys = new List<string>();
                foreach (var id in AllIcons) keys.Add(IconButton.KeyOf(id));
                keys.Add("ui5.choice.keep");
                keys.Add("ui5.choice.switch");
                var t = Ui.Label(parent, "", FocusPlate.FontSize, Color.white);
                t.font = Ui.Theme.FontFor(lang, FontRole.Body);
                foreach (var k in keys)
                {
                    Assert.That(loc.Table.Has(k), $"{Languages.Code(lang)}: {k} missing");
                    var words = FocusPlate.Words(loc, new FocusTip.Tip(this, k, "Z"));
                    var size = FocusPlate.SizeFor(t, words, new Vector4(8, 8, 8, 8)); // logs an error (fails) past 320
                    Assert.That(size.x, Is.InRange(FocusPlate.BaseWidth, FocusPlate.MaxWidth), $"{k}: {words}");
                    Assert.That(words, Does.Not.Contain("\n"), k);
                }
            }
        }

        // ---------- focus tip model ----------

        [Test]
        public void StrongerSourcesWinAndFallBack()
        {
            var m = new FocusTip();
            object a = new object(), b = new object();
            int changes = 0;
            m.Changed += () => changes++;
            m.Enter(TipSource.Focus, a, "ui5.icon.undo", "Z");
            Assert.That(m.Current.Owner, Is.SameAs(a));
            m.Enter(TipSource.Hover, b, "ui5.icon.hint");
            Assert.That(m.Current.Owner, Is.SameAs(b));
            m.Exit(TipSource.Hover, b);
            Assert.That(m.Current.Owner, Is.SameAs(a), "back to the keyboard focus");
            Assert.That(m.Current.Shortcut, Is.EqualTo("Z"));
            Assert.That(changes, Is.EqualTo(3));
            m.Enter(TipSource.Focus, a, "ui5.icon.undo", "Z");
            Assert.That(changes, Is.EqualTo(3), "same tip again: no change");
            m.Forget(a);
            Assert.That(m.Current.IsEmpty, Is.True);
        }

        [Test]
        public void ALateExitOfAnotherOwnerIsIgnored()
        {
            var m = new FocusTip();
            object a = new object(), b = new object();
            m.Enter(TipSource.Hover, a, "ui5.icon.undo");
            m.Enter(TipSource.Hover, b, "ui5.icon.restart");
            m.Exit(TipSource.Hover, a);
            Assert.That(m.Current.Owner, Is.SameAs(b));
        }

        [Test]
        public void TipModelAsserts()
        {
            var m = new FocusTip();
            Assert.Throws<ArgumentNullException>(() => m.Enter(TipSource.Focus, null, "k"));
            Assert.Throws<ArgumentException>(() => m.Enter(TipSource.Focus, this, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.Enter((TipSource)9, this, "k"));
        }

        // ---------- buttons ----------

        [Test]
        public void IconButtonIsA48SquareThatClicksOnce()
        {
            var b = IconButton.Build(Canvas(), UiIcon.Undo, null, "Z");
            int clicks = 0;
            b.Clicked += () => clicks++;
            Assert.That(b.Rect.sizeDelta, Is.EqualTo(new Vector2(48, 48)));
            Assert.That(b.TipKey, Is.EqualTo("ui5.icon.undo"));
            Assert.That(b.Shortcut, Is.EqualTo("Z"));
            Assert.That(b.GetComponent<Image>().raycastTarget, Is.True, "the whole square is the target");
            b.OnPointerClick(Pointer(-1));
            Assert.That(clicks, Is.EqualTo(1));
            b.OnPointerClick(Pointer(0)); // touch tap acts at once too
            Assert.That(clicks, Is.EqualTo(2));
            b.SetShown(false);
            b.Press();
            Assert.That(clicks, Is.EqualTo(2), "hidden: no action");
        }

        [Test]
        public void GlyphConfirmsOnTheSecondTouchTapAndAtOnceWithTheMouse()
        {
            var parent = Canvas();
            var keep = GlyphButton.Build(parent, UiIcon.Keep, null, "K");
            Assert.That(keep.Rect.sizeDelta, Is.EqualTo(new Vector2(56, 56)));
            int clicks = 0;
            keep.Clicked += () => clicks++;
            keep.OnPointerClick(Pointer(0));
            Assert.That(clicks, Is.EqualTo(0), "first tap explains");
            Assert.That(keep.Armed, Is.True);
            keep.OnPointerClick(Pointer(0));
            Assert.That(clicks, Is.EqualTo(1), "second tap confirms");
            keep.OnPointerClick(Pointer(-1));
            Assert.That(clicks, Is.EqualTo(2), "mouse confirms at once");
            keep.OnPointerClick(Pointer(0));
            keep.OnDeselect(null); // the other glyph took the focus
            keep.OnPointerClick(Pointer(0));
            Assert.That(clicks, Is.EqualTo(2), "focus moved away: the next tap explains again");
            Assert.Throws<ArgumentOutOfRangeException>(() => GlyphButton.Build(parent, UiIcon.Undo, null));
        }

        [Test]
        public void BuildAsserts()
        {
            Assert.Throws<ArgumentNullException>(() => IconButton.Build(null, UiIcon.Undo, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => IconButton.Build(Canvas(), (UiIcon)999, null));
            Assert.Throws<ArgumentNullException>(() => FocusPlate.Build(Canvas(), null, null));
            Assert.Throws<ArgumentNullException>(() => IconRow.Build(null, Vector2.zero, Vector2.zero));
        }

        // ---------- focus plate ----------

        [Test]
        public void OnePlatePerCanvas()
        {
            var c = Canvas();
            var loc = Loc(Language.Ko);
            FocusPlate.Build(c, loc, null);
            var child = Ui.Rect("child", c);
            Assert.Throws<InvalidOperationException>(() => FocusPlate.Build(child, loc, null));
            Assert.DoesNotThrow(() => FocusPlate.Build(Canvas("other canvas"), loc, null));
        }

        [Test]
        public void PlateShowsTheFocusedButtonWithItsShortcut()
        {
            var c = Canvas();
            var loc = Loc(Language.Ko);
            var plate = FocusPlate.Build(c, loc, null);
            var undo = IconButton.Build(c, UiIcon.Undo, null, "Z");
            undo.Rect.TopLeft(1148, 24, 48, 48);
            plate.Track(undo);
            Assert.That(plate.Showing, Is.False);
            undo.OnSelect(null);
            Assert.That(plate.Showing, Is.True);
            Assert.That(plate.Text.text, Is.EqualTo(loc.Get("ui5.icon.undo") + " · Z"));
            var size = ((RectTransform)plate.transform).sizeDelta;
            Assert.That(size.x, Is.InRange(FocusPlate.BaseWidth, FocusPlate.MaxWidth));
            Assert.That(size.y, Is.GreaterThanOrEqualTo(FocusPlate.FontSize + 16));
            loc.Set(Language.En);
            Assert.That(plate.Text.text, Is.EqualTo("Undo · Z"));
            undo.OnDeselect(null);
            Assert.That(plate.Showing, Is.False);
        }

        [Test]
        public void PlateGoesUnderTopButtonsAndOverBottomOnesInsideTheSafeArea()
        {
            var canvas = new Vector2(1280, 720);
            var size = new Vector2(250, 40);
            var top = FocusPlate.Place(new Rect(1208, 24, 48, 48), size, canvas);
            Assert.That(top.y, Is.EqualTo(24 + 48 + FocusPlate.Gap));
            Assert.That(top.xMax, Is.LessThanOrEqualTo(1280 - 24), "kept inside the right safe edge");
            var bottom = FocusPlate.Place(new Rect(616, 604, 48, 48), size, canvas);
            Assert.That(bottom.yMax, Is.EqualTo(604 - FocusPlate.Gap));
            Assert.That(bottom.center.x, Is.EqualTo(640).Within(0.01f), "centred over the button");
            var left = FocusPlate.Place(new Rect(24, 24, 48, 48), size, canvas);
            Assert.That(left.x, Is.EqualTo(24));
        }

        // ---------- icon row ----------

        [Test]
        public void RowCentresButtons76ApartAndClosesUpAroundHiddenOnes()
        {
            var row = IconRow.Build(Canvas(), new Vector2(0.5f, 0), new Vector2(0, 92));
            var icons = new[] { UiIcon.Play, UiIcon.Restart, UiIcon.Map, UiIcon.Hint, UiIcon.Settings };
            foreach (var i in icons) row.Add(i, null);
            var b = row.Buttons;
            for (int i = 0; i < 5; i++)
                Assert.That(b[i].Rect.anchoredPosition.x, Is.EqualTo((i - 2) * IconRow.Pitch).Within(0.01f));
            Assert.That(IconRow.Pitch, Is.EqualTo(76));
            Assert.That(b[1].Selectable.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(b[1].Selectable.navigation.selectOnLeft, Is.SameAs(b[0].Selectable));
            Assert.That(b[1].Selectable.navigation.selectOnRight, Is.SameAs(b[2].Selectable));
            Assert.That(b[0].Selectable.navigation.selectOnLeft, Is.Null, "no wrap-around");

            row.SetShown(b[3], false);
            Assert.That(b[3].Selectable.interactable, Is.False);
            Assert.That(b[2].Selectable.navigation.selectOnRight, Is.SameAs(b[4].Selectable), "hidden hint skipped");
            Assert.That(b[0].Rect.anchoredPosition.x, Is.EqualTo(-1.5f * IconRow.Pitch).Within(0.01f));
            Assert.That(((RectTransform)row.transform).sizeDelta.x, Is.EqualTo(4 * 48 + 3 * 28));
            Assert.That(row.First, Is.SameAs(b[0]));
        }
    }
}
