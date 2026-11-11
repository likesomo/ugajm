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
                var hudHidden = new System.Collections.Generic.List<bool>();
                view.HudHidden += hudHidden.Add;
                view.Show(ResultModel.Clear(0, 3, 2, 1, 2, isLast: false));
                Assert.IsTrue(view.IsShown);
                // Minimal clear over the board (docs 14.7): light scrim that still takes the clicks, HUD hidden, only Next.
                Assert.AreEqual(ResultView.ClearScrimAlpha, view.scrim.color.a, 1e-4f);
                Assert.IsTrue(view.scrim.raycastTarget);
                CollectionAssert.AreEqual(new[] { true }, hudHidden);
                Assert.IsFalse(view.panel.gameObject.activeSelf, "no modal panel for a clear");
                Assert.IsTrue(Button(canvas, "다음 스테이지 (Enter)").activeSelf);
                Assert.IsTrue(Button(canvas, "다음 스테이지 (Enter)").transform.IsChildOf(view.nextSlot));
                foreach (var b in new[] { view.again, view.select, view.undo, view.restart })
                    Assert.IsFalse(b.gameObject.activeInHierarchy, b.name + ": R / Esc instead (14.7)");
                Assert.AreEqual("", Text(canvas, "Body"), "mid-planet: no line");
                Assert.IsFalse(view.finalePlate.gameObject.activeSelf);

                int next = 0;
                view.NextRequested += () => next++;
                Button(canvas, "다음 스테이지 (Enter)").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(1, next);

                view.Show(ResultModel.Clear(16, 3, 3, 0, 0, isLast: true));
                Assert.IsTrue(Button(canvas, "다음 스테이지 (Enter)").activeSelf, "the last stage: [끝까지 보기] -> the ending (16.8)");
                Assert.AreEqual("끝까지 보기", view.next.GetComponentInChildren<UnityEngine.UI.Text>().text);
                Assert.IsTrue(view.body.transform.IsChildOf(view.finalePlate) && view.finalePlate.gameObject.activeSelf, "the journey line, bottom centre");

                view.Show(ResultModel.Loss(4, 3, isLast: false));
                Assert.AreEqual(ResultView.LossScrimAlpha, view.scrim.color.a, 1e-4f);
                Assert.IsTrue(view.panel.gameObject.activeSelf && !view.clearGroup.gameObject.activeSelf);
                Assert.IsTrue(view.body.transform.IsChildOf(view.panel), "the body back in the loss panel");
                Assert.IsFalse(hudHidden[hudHidden.Count - 1], "a loss is a modal over the HUD");
                Assert.IsFalse(Button(canvas, "다음 스테이지 (Enter)").activeSelf, "a loss never offers next");
                Assert.IsFalse(Button(canvas, "다시 하기 (R)").activeSelf);
                Assert.IsTrue(Button(canvas, "한 걸음 되돌리기 (Z)").activeSelf);
                Assert.IsTrue(Button(canvas, "다시 시작 (R)").activeSelf);
                Assert.AreEqual("형제를 모두 잃었다", Text(canvas, "Title"));

                // Star criteria: one line each, on tip plates that show only on hover / tap (hidden at first and for a loss).
                view.Show(ResultModel.Clear(0, 3, 1, 1, 0, isLast: false, stars: 2, bestStars: 2));
                Assert.AreEqual("풀이를 보지 않고 형제 1명으로", view.starLabels[1].text);
                Assert.IsTrue(System.Linq.Enumerable.All(view.starLabels, l => !l.transform.parent.gameObject.activeSelf));
                UnityEngine.EventSystems.ExecuteEvents.Execute(view.stars[1].gameObject,
                    new UnityEngine.EventSystems.PointerEventData(null), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                Assert.IsTrue(view.starLabels[1].transform.parent.gameObject.activeSelf, "a tap shows what the star asks");
                Assert.IsFalse(view.starLabels[0].transform.parent.gameObject.activeSelf);
                view.Show(ResultModel.Loss(4, 3, isLast: false));
                Assert.IsTrue(System.Linq.Enumerable.All(view.starLabels, l => !l.transform.parent.gameObject.activeSelf));

                // Language switch while open: the panel rewords itself (docs/architecture.md 13.3).
                loc.Set(SevenSteps.Contracts.Language.En);
                view.Apply(loc);
                Assert.AreEqual("Every brother is lost", Text(canvas, "Title"));
                Assert.AreEqual("Restart (R)", Button(canvas, "다시 시작 (R)").GetComponentInChildren<UnityEngine.UI.Text>().text);

                view.Hide();
                Assert.IsFalse(view.IsShown);
                Assert.IsFalse(hudHidden[hudHidden.Count - 1], "Hide brings the HUD back");
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
