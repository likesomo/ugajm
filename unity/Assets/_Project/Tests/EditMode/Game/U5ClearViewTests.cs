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
    /// <summary>U5 clear and failure (docs/ui-u5-kit.md 3, U5-result, U5-failure). Owner: gameplay-ui.</summary>
    public sealed class U5ClearViewTests
    {
        U5ClearView view;

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
            view = U5ClearView.Build(a.uiIcons, Loc());
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
            Assert.That(view.FailureShown, Is.False);
            Assert.That(view.LitStones, Is.EqualTo(2), "Edit Mode: earned stones lit at once");
            Assert.That(hud, Is.EqualTo(new[] { true }));
            Assert.That(view.Next.TipKey, Is.EqualTo("ui5.icon.next"));
            Assert.That(view.Next.Rect.sizeDelta, Is.EqualTo(new Vector2(56, 56)));
            Assert.That(view.Next.transform.Find("Backing"), Is.Not.Null, "a round backing for 3:1 over the board (R2)");
            Assert.That(view.Stones[2].TipKey, Is.EqualTo("result.star3_many"));
            Assert.That(view.Stones[2].TipArgs, Is.EqualTo(new object[] { 3 }));
            Assert.That(view.Undo.gameObject.activeInHierarchy, Is.False, "no failure buttons on a clear");
            Assert.That(view.Scrim.raycastTarget, Is.True, "the board takes no presses under the result (R3)");
            Assert.That(view.Scrim.color.a, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(view.Scrim.transform.GetSiblingIndex(), Is.EqualTo(0), "behind the buttons");
            view.Hide();
            Assert.That(view.IsShown, Is.False);
            Assert.That(hud, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TwoStarsWithABrotherLeftNudgeTowardTheThird()
        {
            // No hint, 2 of 3 brothers: two stars and the nudge (user request 2026-11-13), at the top centre.
            view.Show(ResultModel.Clear(3, 3, 2, 2, false, stars: 2, bestStars: 2));
            Assert.That(view.NudgeLine, Is.EqualTo("형제가 모두 걸었다면 선이 더 밝았을 텐데."));
            Assert.That(view.NudgePlate.anchoredPosition.y, Is.EqualTo(-U5ClearView.Safe));
            Assert.That(view.NudgePlate.sizeDelta.x, Is.LessThan(600f), "a short plate, not a band");
            view.Hide();
            // Everyone walked (3 stars), or a hint was seen (1 star: the hint, not the brothers, cost the second): no nudge.
            view.Show(ResultModel.Clear(3, 3, 3, 3, false, stars: 3, bestStars: 3));
            Assert.That(view.NudgeLine, Is.Empty);
            view.Hide();
            view.Show(ResultModel.Clear(3, 3, 2, 2, false, stars: 1, bestStars: 1));
            Assert.That(view.NudgeLine, Is.Empty);
            view.Hide();
            view.Show(ResultModel.Loss(3, 3, false));
            Assert.That(view.NudgeLine, Is.Empty);
        }

        [Test]
        public void TheNudgeGoesUnderAFinaleLineAndSpeaksEnglish()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            var loc = Loc();
            loc.Set(Language.En);
            var v = U5ClearView.Build(a.uiIcons, loc);
            try
            {
                v.Show(ResultModel.Clear(49, 2, 1, 1, true, stars: 2, bestStars: 2));
                Assert.That(v.NudgeLine, Is.EqualTo("If all of us had walked, the line would have shone brighter."));
                Assert.That(v.NudgePlate.anchoredPosition.y, Is.LessThan(-U5ClearView.Safe - 40), "under the journey line");
            }
            finally { Object.DestroyImmediate(v.gameObject); }
        }

        [Test]
        public void TheLastStageAsksForTheEnding()
        {
            view.Show(ResultModel.Clear(49, 3, 2, 2, true, stars: 1, bestStars: 1));
            Assert.That(view.Next.TipKey, Is.EqualTo("result.ending"));
        }

        [Test]
        public void ALossIsTheU5FailureLineWithUndoAndRestart()
        {
            var got = new List<string>();
            view.UndoRequested += () => got.Add("undo");
            view.RestartRequested += () => got.Add("restart");
            view.Show(ResultModel.Loss(3, 3, false));
            Assert.That(view.FailureShown, Is.True);
            Assert.That(view.Next.gameObject.activeInHierarchy || view.Map.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.Undo.Rect.sizeDelta, Is.EqualTo(new Vector2(52, 52)));
            view.Undo.Press();
            view.Restart.Press();
            Assert.That(got, Is.EqualTo(new[] { "undo", "restart" }));
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
        public void WithoutWorldStonesThe2DRowStandsAboveTheBoardArea()
        {
            var canvas = new Vector2(1280, 720);
            var board = new Rect(300, 160, 680, 380);
            var r = U5ClearView.StoneRow(board, new Vector2(780, 300), canvas);
            Assert.That(r.center.x, Is.EqualTo(780).Within(0.01f));
            Assert.That(r.yMax, Is.LessThanOrEqualTo(board.yMin), "off the board area (R1 on 1f90617)");
            r = U5ClearView.StoneRow(new Rect(0, 0, 1280, 720), new Vector2(1270, 40), canvas);
            Assert.That(r.xMax, Is.LessThanOrEqualTo(1256));
            Assert.That(r.y, Is.GreaterThanOrEqualTo(24 + 48 + 8), "under the Map / Retry band");
        }

        [Test]
        public void WorldStonesStandInARowBehindTheExit()
        {
            var spots = U5ClearView.WorldStoneSpots(new Vector3(2f, 0.4f, -1f), 1f, new Vector3(0f, -0.7f, 0.7f));
            Assert.That(spots.Length, Is.EqualTo(3));
            Assert.That(spots[1].x, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(spots[0].x, Is.LessThan(spots[1].x));
            Assert.That(spots[2].x - spots[1].x, Is.EqualTo(U5ClearView.WorldSpacing).Within(1e-5f));
            foreach (var p in spots)
            {
                Assert.That(p.z, Is.EqualTo(-1f + U5ClearView.WorldBehind).Within(1e-5f), "behind the exit");
                Assert.That(p.y, Is.EqualTo(0f), "on the tile tops");
            }
            Assert.That(view.WorldStones, Is.False, "no world hooks set: the 2D row");
        }

        [Test]
        public void TheIconSetCarriesTheWorldStone()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.That(a.uiIcons.worldStarStone, Is.Not.Null, "manifest worldStar imported");
            Assert.That(a.uiIcons.starLit, Is.Not.Null);
            Assert.That(a.uiIcons.starUnlit, Is.Not.Null);
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
            Assert.Throws<ArgumentNullException>(() => U5ClearView.Build(null, null));
            Assert.Throws<ArgumentNullException>(() => view.Show(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => IconButton.Build(view.transform, UiIcon.Next, null, size: 40));
        }
    }

    /// <summary>U5 pause and settings rows (U5-pause, U5-settings). Owner: gameplay-ui.</summary>
    public sealed class U5RowsTests
    {
        readonly List<Object> made = new List<Object>();

        static Localizer Loc()
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
        }

        static UiIconSet Icons() => AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").uiIcons;

        [SetUp]
        public void Theme() => Ui.Theme = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").uiTheme;

        [TearDown]
        public void Clean()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        [Test]
        public void PauseRowIsFiveIconsAt604AndDrivesTheMenuEvents()
        {
            var p = new U5PauseView(Icons(), Loc());
            made.Add(p.Overlay.gameObject);
            var b = p.Overlay.Row.Buttons;
            Assert.That(b.Count, Is.EqualTo(5));
            Assert.That(new[] { b[0].Icon, b[1].Icon, b[2].Icon, b[3].Icon, b[4].Icon },
                Is.EqualTo(new[] { UiIcon.Play, UiIcon.Restart, UiIcon.Map, UiIcon.Hint, UiIcon.Settings }));
            Assert.That(b[2].Rect.anchoredPosition.x - b[1].Rect.anchoredPosition.x, Is.EqualTo(IconRow.Pitch).Within(0.01f));
            var row = (RectTransform)p.Overlay.Row.transform;
            Assert.That(720 - row.anchoredPosition.y - IconButton.Size / 2, Is.EqualTo(604).Within(0.01f), "row top at y 604");
            var got = new List<string>();
            p.CloseRequested += () => got.Add("close");
            p.RestartRequested += () => got.Add("restart");
            p.StagesRequested += () => got.Add("map");
            p.SolutionRequested += () => got.Add("hint");
            p.SettingsRequested += () => got.Add("settings");
            foreach (var x in b) x.Press();
            Assert.That(got, Is.EqualTo(new[] { "close", "restart", "map", "hint", "settings" }));
            Assert.That(p.Overlay.Scrim.color.a, Is.EqualTo(U5RowOverlay.ScrimAlpha).Within(0.001f));
            p.Show();
            Assert.That(p.Visible, Is.True);
            p.Hide();
            Assert.That(p.Visible, Is.False);
        }

        [Test]
        public void SettingsRowMutesAndRestoresTheVolumeAndTogglesReduceMotion()
        {
            var save = new SaveSystem(new MemoryStore());
            save.SetMasterVolume(0.6f);
            var v = new U5SettingsView(Icons(), Loc());
            made.Add(v.Overlay.gameObject);
            int lang = 0, more = 0;
            var c = new U5SettingsController(save, v, () => lang++, () => more++);
            Assert.That(v.Sound.Icon, Is.EqualTo(UiIcon.SoundOn));
            v.Sound.Press();
            Assert.That(save.Settings.masterVolume, Is.EqualTo(0f));
            Assert.That(v.Sound.Icon, Is.EqualTo(UiIcon.SoundOff));
            Assert.That(v.Sound.TipKey, Is.EqualTo("ui5.icon.soundoff"));
            v.Sound.Press();
            Assert.That(save.Settings.masterVolume, Is.EqualTo(0.6f).Within(0.001f), "back to the level before the mute");
            bool before = save.Settings.reduceMotion;
            v.Motion.Press();
            Assert.That(save.Settings.reduceMotion, Is.EqualTo(!before));
            Assert.That(v.Motion.TipKey, Is.EqualTo(before ? "ui5.settings.motion_off" : "ui5.settings.motion_on"));
            v.Language.Press();
            v.More.Press();
            Assert.That((lang, more), Is.EqualTo((1, 1)));
            c.Dispose();
        }

        [Test]
        public void RowWordsExistInBothLanguages()
        {
            foreach (var lang in new[] { Language.Ko, Language.En })
            {
                var loc = Loc();
                loc.Set(lang);
                foreach (var k in new[] { "ui5.pause.title", "ui5.pause.continue", "ui5.settings.title", "ui5.settings.more", "ui5.settings.motion_on", "ui5.settings.motion_off" })
                    Assert.That(loc.Table.Has(k), $"{lang}: {k}");
            }
        }
    }
}
