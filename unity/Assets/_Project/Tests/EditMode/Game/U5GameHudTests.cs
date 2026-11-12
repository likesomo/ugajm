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
    /// <summary>U5 in-game HUD and KEEP / SWITCH glyphs (docs/ui-u5-kit.md 3, 4). Owner: gameplay-ui.</summary>
    public sealed class U5GameHudTests
    {
        U5GameHud hud;

        static GameAssets Assets() => AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");

        static TileSet Tiles() => AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:TileSet")[0]));

        static Localizer Loc()
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
        }

        [SetUp]
        public void Build()
        {
            var a = Assets();
            Ui.Theme = a.uiTheme;
            hud = U5GameHud.Build(a.uiIcons, Tiles(), Loc());
        }

        [TearDown]
        public void Clean()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
        }

        /// Left edge in 1280-wide reference units, from the anchors (top-left or top-right point anchors).
        static float Left(RectTransform r) => r.anchorMin.x * 1280 + r.anchoredPosition.x - r.pivot.x * r.sizeDelta.x;
        static float Top(RectTransform r) => (1 - r.anchorMin.y) * 720 - r.anchoredPosition.y - (1 - r.pivot.y) * r.sizeDelta.y;

        [Test]
        public void TopBandMatchesTheU5TouchTargets()
        {
            // touch-targets.csv U5 hud: pause 24,24; hint 1088, undo 1148, restart 1208 at y 24; all 48.
            foreach (var (b, x) in new[] { (hud.Pause, 24f), (hud.Hint, 1088f), (hud.Undo, 1148f), (hud.Restart, 1208f) })
            {
                Assert.That(b.Rect.sizeDelta, Is.EqualTo(new Vector2(48, 48)), b.name);
                Assert.That(Left(b.Rect), Is.EqualTo(x).Within(0.01f), b.name);
                Assert.That(Top(b.Rect), Is.EqualTo(24).Within(0.01f), b.name);
            }
            Assert.That(hud.Undo.Shortcut, Is.EqualTo("Z"));
            Assert.That(hud.Restart.Shortcut, Is.EqualTo("R"));
            Assert.That(hud.Hint.Shortcut, Is.EqualTo("H"));
            Assert.That(hud.Hint.Selectable.navigation.selectOnRight, Is.SameAs(hud.Undo.Selectable));
            Assert.That(hud.GetComponentsInChildren<FocusPlate>(true).Length, Is.EqualTo(1));
            Assert.That(hud.GetComponent<Canvas>().sortingOrder, Is.InRange(1, 9), "over the current HUD, under the overlays");
        }

        [Test]
        public void ButtonsRaiseTheirEvents()
        {
            var got = new List<string>();
            hud.PauseRequested += () => got.Add("pause");
            hud.HintRequested += () => got.Add("hint");
            hud.UndoRequested += () => got.Add("undo");
            hud.RestartRequested += () => got.Add("restart");
            hud.KeepRequested += () => got.Add("keep");
            hud.SwitchRequested += () => got.Add("switch");
            hud.ShowChoice(new HudView.ChoiceCards { On = true, FocusKeep = true });
            foreach (var b in new IconButton[] { hud.Pause, hud.Hint, hud.Undo, hud.Restart, hud.Keep, hud.Switch }) b.Press();
            Assert.That(got, Is.EqualTo(new[] { "pause", "hint", "undo", "restart", "keep", "switch" }));
        }

        [Test]
        public void TheFirstChoiceExplainsTheFocusedGlyphLaterOnesOnlyWhenTheFocusMoves()
        {
            hud.ShowChoice(new HudView.ChoiceCards { On = true, Explain = true, FocusKeep = true });
            Assert.That(hud.Glyphs.gameObject.activeSelf, Is.True);
            Assert.That(hud.Plate.Model.Current.Owner, Is.SameAs(hud.Keep));
            Assert.That(hud.Plate.Model.Current.Key, Is.EqualTo("ui5.choice.keep"));
            hud.ShowChoice(default);
            Assert.That(hud.Glyphs.gameObject.activeSelf, Is.False);
            Assert.That(hud.Plate.Model.Current.IsEmpty, Is.True);

            hud.ShowChoice(new HudView.ChoiceCards { On = true, Explain = false, FocusKeep = true });
            Assert.That(hud.Plate.Model.Current.IsEmpty, Is.True, "a later visit starts with the glyphs only");
            hud.ShowChoice(new HudView.ChoiceCards { On = true, Explain = false, FocusKeep = false });
            Assert.That(hud.Plate.Model.Current.Owner, Is.SameAs(hud.Switch), "the player moved the focus: its words");
            Assert.That(hud.Plate.Model.Current.Shortcut, Is.EqualTo("X"));
        }

        [Test]
        public void GlyphFocusTellsTheController()
        {
            bool? focused = null;
            hud.ChoiceFocused += k => focused = k;
            hud.ShowChoice(new HudView.ChoiceCards { On = true, FocusKeep = true });
            hud.Switch.OnSelect(null);
            Assert.That(focused, Is.False);
            hud.Keep.OnSelect(null);
            Assert.That(focused, Is.True);
        }

        [Test]
        public void GlyphsSitBesideTheBoardNearTheBrotherAndClearOfTheSky()
        {
            var board = new Rect(276, 80, 728, 496);
            hud.ShowChoice(new HudView.ChoiceCards { On = true, FocusKeep = true });
            Assert.That(hud.PlaceChoice(board, new Vector2(300, 300), new Vector2(1280, 720)), Is.True);
            var g = new Rect(hud.Glyphs.anchoredPosition.x, -hud.Glyphs.anchoredPosition.y, hud.Glyphs.sizeDelta.x, hud.Glyphs.sizeDelta.y);
            Assert.That(g.Overlaps(board), Is.False, "beside the board area");
            Assert.That(g.xMax, Is.LessThanOrEqualTo(board.xMin), "the left side is nearest to a brother at the left edge");
            Assert.That(hud.Vertical, Is.True, "stacked beside the board");
            Assert.That(g.size, Is.EqualTo(new Vector2(56, 56 * 2 + 12)));

            var sky = new Rect(300, 0, 680, 160); // a band above a smaller board
            var small = new Rect(400, 170, 480, 380);
            hud.ChoiceAvoid = () => new List<Rect> { sky };
            hud.PlaceChoice(small, new Vector2(640, 200), new Vector2(1280, 720));
            g = new Rect(hud.Glyphs.anchoredPosition.x, -hud.Glyphs.anchoredPosition.y, hud.Glyphs.sizeDelta.x, hud.Glyphs.sizeDelta.y);
            Assert.That(g.Overlaps(sky), Is.False, "never over the step-star sky");
            Assert.That(g.Overlaps(small), Is.False);
        }

        [Test]
        public void TheSkyBandPassedByTheControllerIsAvoidedAndNoRoomMarksTheExemption()
        {
            hud.ShowChoice(new HudView.ChoiceCards { On = true, FocusKeep = true });
            var canvas = new Vector2(1280, 720);
            var board = new Rect(300, 200, 680, 300);
            var sky = new Rect(400, 100, 480, 90);
            Assert.That(hud.PlaceChoice(board, new Vector2(640, 220), canvas, new List<Rect> { sky }), Is.True);
            var g = new Rect(hud.Glyphs.anchoredPosition.x, -hud.Glyphs.anchoredPosition.y, hud.Glyphs.sizeDelta.x, hud.Glyphs.sizeDelta.y);
            Assert.That(g.Overlaps(sky), Is.False, "the top candidate under the stars is left out");
            Assert.That(g.Overlaps(board), Is.False);
            var mark = hud.Glyphs.GetComponent<UiLayout>();
            Assert.That(mark == null || (mark.exempt & UiLayoutRule.Board) == 0, "placed beside the board: no exemption");

            // A board filling the canvas: no candidate, the glyphs stand over the brother with the Board exemption and its reason.
            Assert.That(hud.PlaceChoice(new Rect(0, 0, 1280, 720), new Vector2(640, 360), canvas, null), Is.False);
            mark = hud.Glyphs.GetComponent<UiLayout>();
            Assert.That(mark != null && (mark.exempt & UiLayoutRule.Board) != 0, "wall-top fallback is exempt Board");
            Assert.That(mark.reason, Does.Contain("no room beside the board"));
            Assert.That(hud.PlaceChoice(board, new Vector2(640, 220), canvas, null), Is.True);
            Assert.That((mark.exempt & UiLayoutRule.Board) == 0, "the exemption goes with the fallback");
        }

        [Test]
        public void PlateKeepsClearOfTheBoardWhereItCan()
        {
            var canvas = new Vector2(1280, 720);
            var board = new Rect(276, 80, 728, 496);
            var size = new Vector2(250, 42);
            // A glyph pair stacked left of the board: its words slide into the left margin, still under the pair.
            var pair = new Rect(208, 250, 56, 124);
            var r = FocusPlate.Place(pair, size, canvas, new[] { board });
            Assert.That(r.Overlaps(board), Is.False);
            Assert.That(r.x, Is.EqualTo(24));
            Assert.That(r.y, Is.EqualTo(pair.yMax + FocusPlate.Gap));
            // Top-right HUD buttons: under them, right of the board.
            var restart = new Rect(1208, 24, 48, 48);
            r = FocusPlate.Place(restart, size, canvas, new[] { new Rect(276, 120, 700, 450) });
            Assert.That(r.y, Is.EqualTo(80));
            Assert.That(r.xMax, Is.LessThanOrEqualTo(1256));
        }

        [Test]
        public void TitleAndDotsFollowTheState()
        {
            var level = SevenSteps.Core.Level.Parse("t", "", 3, new[] { "#####", "#S.E#", "#####" });
            var s = SevenSteps.Core.Rules.NewState(level);
            hud.Refresh(s, 0, new SevenSteps.Core.LevelTextDto { name = "돌이 된 몸" });
            Assert.That(hud.Title.text, Is.EqualTo("돌이 된 몸"));
            Assert.That(hud.DotLookOf(0), Is.EqualTo(HudView.DotLook.Walking));
            Assert.That(hud.DotLookOf(2), Is.EqualTo(HudView.DotLook.Waiting));
            Assert.Throws<ArgumentNullException>(() => hud.Refresh(null, 0, new SevenSteps.Core.LevelTextDto()));
        }

        [Test]
        public void BuildAsserts()
        {
            var a = Assets();
            Assert.Throws<ArgumentNullException>(() => U5GameHud.Build(a.uiIcons, null, Loc()));
            Assert.Throws<ArgumentNullException>(() => U5GameHud.Build(a.uiIcons, Tiles(), null));
            Assert.Throws<ArgumentNullException>(() => U5GameHud.HideClassic(null));
        }

        // User decision 2026-11-12-b (2): a hinted attempt shows the ★1 mark on the hint button, whose tip says why.
        [Test]
        public void HintedAttemptShowsTheStarCapOnTheHintButtonAndItsTip()
        {
            Assert.IsFalse(hud.StarCapShown);
            hud.SetStarCap(false);
            Assert.IsNull(hud.Hint.transform.Find(StarCapMark.Name), "built only when first needed");
            hud.SetStarCap(true);
            var mark = (RectTransform)hud.Hint.transform.Find(StarCapMark.Name);
            Assert.IsNotNull(mark);
            Assert.IsTrue(hud.StarCapShown && mark.gameObject.activeSelf);
            Assert.IsFalse(mark.GetComponent<UnityEngine.UI.Image>().raycastTarget, "the mark never takes the hint button's press");
            Assert.That(hud.Hint.TipKey, Is.EqualTo(U5GameHud.HintCappedKey));
            hud.Hint.OnSelect(null);
            Assert.IsTrue(hud.Plate.Showing);
            Assert.That(hud.Plate.Text.text, Does.StartWith("힌트 (이번 판 별 최대 1개)"));
            hud.SetStarCap(false); // a new attempt (LoadStage)
            Assert.IsFalse(hud.StarCapShown);
            Assert.That(hud.Hint.TipKey, Is.EqualTo(IconButton.KeyOf(UiIcon.Hint)));
            Assert.That(hud.Plate.Text.text, Is.EqualTo("힌트 · H"), "the shown tip follows");
        }
    }
}
