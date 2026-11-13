using System.Collections;
using NUnit.Framework;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>
    /// The choose screen's wake points and the wake highlight after KEEP / SWITCH (user request 2026-11-13: where does the
    /// next brother wake). World x / z map straight onto the canvas here. Owner: gameplay-ui.
    /// </summary>
    public sealed class BoardMarksViewTests
    {
        GameObject root;
        BoardMarksView marks;
        const float Cell = 77f;

        [SetUp]
        public void Build()
        {
            Ui.Theme = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").uiTheme;
            root = new GameObject("Marks test", typeof(RectTransform), typeof(Canvas));
            var layer = Ui.Rect("Layer", root.transform).Stretch();
            marks = layer.gameObject.AddComponent<BoardMarksView>();
            marks.Init(layer, w => new Vector2(w.x * Cell, w.z * Cell));
            marks.SetLook(false, Cell);
            marks.WakeWords = keep => (keep ? "KEEP: wakes here" : "SWITCH: wakes here", null);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(root);

        [Test]
        public void BothWakePointsAreTaggedWhileChoosingTheFocusedOneFull()
        {
            var brother = new Vector3(3, 0, 1);
            var start = new Vector3(0, 0, 0);
            marks.SetChoice(true, brother, start, Color.cyan, Color.yellow, BoardMarksView.ChoiceFocus.Keep);
            Assert.IsTrue(marks.WakeTagsShown, "KEEP and SWITCH both named at once");
            Assert.IsTrue(marks.KeepWakeShown, "a ring on the start: KEEP wakes there");
            Assert.AreEqual("KEEP: wakes here", marks.KeepTagText.text);
            Assert.AreEqual("SWITCH: wakes here", marks.SwitchTagText.text);
            Assert.AreEqual(0f, marks.KeepTagAt.x, 0.01f, "under the start");
            Assert.AreEqual(3 * Cell, marks.SwitchTagAt.x, 0.01f, "under the brother");
            Assert.Less(marks.SwitchTagAt.y, 1 * Cell, "below his cell, off his head");
            Assert.AreEqual(1f, marks.KeepTagText.GetComponentInParent<CanvasGroup>().alpha, "focused: full");
            Assert.AreEqual(BoardMarksView.WakeOtherAlpha, marks.SwitchTagText.GetComponentInParent<CanvasGroup>().alpha);
            marks.SetChoice(true, brother, start, Color.cyan, Color.yellow, BoardMarksView.ChoiceFocus.Switch);
            Assert.AreEqual(1f, marks.SwitchTagText.GetComponentInParent<CanvasGroup>().alpha, "focus moved to SWITCH");
            Assert.IsTrue(marks.WakeTagsShown && marks.KeepWakeShown, "the other choice stays marked");
            marks.Clear();
            Assert.IsFalse(marks.WakeTagsShown || marks.KeepWakeShown, "gone with the choice");
        }

        [Test]
        public void OnTheStartBothTagsStackAndNoExtraRing()
        {
            var here = new Vector3(2, 0, 2);
            marks.SetChoice(true, here, here, Color.cyan, Color.yellow, BoardMarksView.ChoiceFocus.Keep);
            Assert.IsFalse(marks.KeepWakeShown, "the brother's ring already marks the cell");
            Assert.AreEqual(marks.KeepTagAt.x, marks.SwitchTagAt.x, 0.01f);
            Assert.Less(marks.SwitchTagAt.y, marks.KeepTagAt.y - 20f, "SWITCH's tag under KEEP's, not on it");
        }

        [Test]
        public void AdjacentWakeCellsDoNotCrossTheirTags()
        {
            // playcapture r2-38 (KO): KEEP's start right beside the brother read "KEEP: 여SWITCH하 여기서 깬다".
            var start = new Vector3(1, 0, 2);
            foreach (var brother in new[] { new Vector3(2, 0, 2), new Vector3(0, 0, 2), new Vector3(3, 0, 2), new Vector3(1, 0, 3), new Vector3(2, 0, 1) })
            {
                marks.SetChoice(true, brother, start, Color.cyan, Color.yellow, BoardMarksView.ChoiceFocus.Switch);
                var k = marks.KeepTagText.transform.parent as RectTransform;
                var s = marks.SwitchTagText.transform.parent as RectTransform;
                var rk = new Rect(k.anchoredPosition - k.sizeDelta / 2, k.sizeDelta);
                var rs = new Rect(s.anchoredPosition - s.sizeDelta / 2, s.sizeDelta);
                Assert.IsFalse(rk.Overlaps(rs), $"brother {brother}: KEEP {rk} and SWITCH {rs} cross");
            }
        }

        [Test]
        public void NoWordsNoTags()
        {
            marks.WakeWords = null; // the current (-ui4) HUD has its own cards
            marks.SetChoice(true, Vector3.one, Vector3.zero, Color.cyan, Color.yellow, BoardMarksView.ChoiceFocus.Keep);
            Assert.IsFalse(marks.WakeTagsShown);
        }

        [Test]
        public void AfterAKeepTheWakeCellPulsesAndTheStoneIsRinged()
        {
            IEnumerator keep = marks.Wake(Vector3.zero, Color.cyan, new Vector3(3, 0, 1), 1.4f);
            Assert.IsTrue(keep.MoveNext());
            Assert.AreEqual(2, marks.WakeRingsShown, "the wake cell and the new stone");
            marks.StopBursts(); // the next step / Undo drops it
            Assert.AreEqual(0, marks.WakeRingsShown);
            IEnumerator sw = marks.Wake(Vector3.zero, Color.cyan, null, 1.4f);
            Assert.IsTrue(sw.MoveNext());
            Assert.AreEqual(1, marks.WakeRingsShown, "SWITCH: no stone, only the wake cell");
            marks.StopBursts();
            Assert.IsFalse(marks.Wake(Vector3.zero, Color.cyan, null, 0f).MoveNext(), "0 s: nothing");
        }
    }
}
