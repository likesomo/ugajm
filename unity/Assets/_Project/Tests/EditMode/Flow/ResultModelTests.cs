using System;
using NUnit.Framework;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// ResultModel ranges agreed with gameplay/UI (docs/scene-flow.md 4.7): bodies per stage are 2..5, 0 = unknown / no record.
    public sealed class ResultModelTests
    {
        [Test]
        public void ClearChecksBodyCountsAgainstTheStage()
        {
            var m = ResultModel.Clear(stage: 15, levelBodies: 5, bodiesUsed: 4, minBodies: 3, bestBodies: 0, isLast: false);
            Assert.IsTrue(m.Cleared);
            Assert.AreEqual(4, m.BodiesUsed);
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(0, 2, 3, 0, 0, false));  // used > level bodies
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(0, 3, 0, 0, 0, false));  // a clear uses a body
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(0, 3, 1, 4, 0, false));  // min > level bodies
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(0, 3, 1, 0, -1, false)); // best negative
            Assert.Throws<ArgumentOutOfRangeException>(() => ResultModel.Clear(-1, 3, 1, 0, 0, false));
        }

        [Test]
        public void LossHasNoBodyCounts()
        {
            var m = ResultModel.Loss(stage: 2, levelBodies: 3, isLast: true);
            Assert.IsFalse(m.Cleared);
            Assert.AreEqual(0, m.BodiesUsed);
            Assert.IsTrue(m.IsLast);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResultModel(2, false, 3, 1, 0, 0, false));
        }

        /// The panel in edit mode (replays snap to the end and never open it, so smoke cannot see it).
        [Test]
        public void ViewShowsTheRightButtonsAndRaisesEvents()
        {
            TestTheme.Use();
            var view = ResultView.Create(); // the theme's prefab (UI/Prefabs/ResultPanel.prefab)
            var canvas = view.gameObject;
            var loc = GameStrings();
            try
            {
                Assert.Throws<InvalidOperationException>(() => view.Show(ResultModel.Loss(0, 3, false)), "words before Show");
                view.Apply(loc);
                Assert.IsFalse(view.IsShown);
                view.Show(ResultModel.Clear(0, 3, 2, 1, 2, isLast: false));
                Assert.IsTrue(view.IsShown);
                Assert.IsTrue(Button(canvas, "다음 스테이지 (Enter)").activeSelf);
                Assert.IsFalse(Button(canvas, "한 걸음 되돌리기 (Z)").activeSelf);
                StringAssert.Contains("형제 2명 (최소 1명, 최고 기록 2명)", Text(canvas, "Body"));

                int next = 0;
                view.NextRequested += () => next++;
                Button(canvas, "다음 스테이지 (Enter)").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(1, next);

                view.Show(ResultModel.Clear(16, 3, 3, 0, 0, isLast: true));
                Assert.IsFalse(Button(canvas, "다음 스테이지 (Enter)").activeSelf, "no next after the last stage");

                view.Show(ResultModel.Loss(4, 3, isLast: false));
                Assert.IsFalse(Button(canvas, "다음 스테이지 (Enter)").activeSelf, "a loss never offers next");
                Assert.IsFalse(Button(canvas, "다시 하기 (R)").activeSelf);
                Assert.IsTrue(Button(canvas, "한 걸음 되돌리기 (Z)").activeSelf);
                Assert.IsTrue(Button(canvas, "다시 시작 (R)").activeSelf);
                Assert.AreEqual("형제를 모두 잃었다", Text(canvas, "Title"));

                // Star criteria under the stars: shown with the stars, the minimum in ★2, hidden for a loss.
                view.Show(ResultModel.Clear(0, 3, 1, 1, 0, isLast: false, stars: 2, bestStars: 2));
                Assert.AreEqual("풀이를 보지 않고\n형제 1명으로", Text(canvas, "Label 2"));
                Assert.IsTrue(view.starLabels[0].transform.parent.gameObject.activeSelf);
                view.Show(ResultModel.Loss(4, 3, isLast: false));
                Assert.IsFalse(view.starLabels[0].transform.parent.gameObject.activeSelf);

                // Language switch while open: the panel rewords itself (docs/architecture.md 13.3).
                loc.Set(SevenSteps.Contracts.Language.En);
                view.Apply(loc);
                Assert.AreEqual("Every brother is lost", Text(canvas, "Title"));
                Assert.AreEqual("Restart (R)", Button(canvas, "다시 시작 (R)").GetComponentInChildren<UnityEngine.UI.Text>().text);

                view.Hide();
                Assert.IsFalse(view.IsShown);
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        /// The game's tables (GameAssets.strings files), ko first.
        static SevenSteps.Contracts.Localizer GameStrings()
        {
            SevenSteps.Contracts.StringTable Load(SevenSteps.Contracts.Language lang)
            {
                string path = $"Assets/_Project/Data/Strings/{SevenSteps.Contracts.Languages.Code(lang)}.json";
                var json = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path);
                Assert.IsNotNull(json, path);
                return SevenSteps.Contracts.StringTable.FromJson(lang, json.text);
            }
            return new SevenSteps.Contracts.Localizer(new[] { Load(SevenSteps.Contracts.Language.Ko), Load(SevenSteps.Contracts.Language.En) },
                SevenSteps.Contracts.Language.Ko);
        }

        static UnityEngine.GameObject Button(UnityEngine.GameObject canvas, string name)
        {
            foreach (var t in canvas.GetComponentsInChildren<UnityEngine.Transform>(true))
                if (t.name == name && t.GetComponent<UnityEngine.UI.Button>() != null) return t.gameObject;
            Assert.Fail($"button {name}");
            return null;
        }

        static string Text(UnityEngine.GameObject canvas, string name)
        {
            foreach (var t in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (t.name == name) return t.text;
            Assert.Fail($"text {name}");
            return null;
        }
    }
}
