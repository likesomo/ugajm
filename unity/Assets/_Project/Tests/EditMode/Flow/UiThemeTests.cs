using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// UiTheme checks (docs/scene-flow.md S6): an empty theme lists what is missing, the default theme is complete,
    /// and Ui refuses a theme without a font.
    public sealed class UiThemeTests
    {
        [Test]
        public void EmptyThemeListsMissingParts()
        {
            var theme = ScriptableObject.CreateInstance<UiTheme>();
            try
            {
                var issues = new List<string>();
                theme.Validate(issues);
                Assert.AreEqual(3, issues.Count, string.Join("\n", issues)); // font, settingsPanel, resultPanel
                Assert.Throws<System.InvalidOperationException>(() => Ui.Theme = theme);
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void DefaultThemeIsComplete()
        {
            TestTheme.Use();
            var issues = new List<string>();
            Ui.Theme.Validate(issues);
            CollectionAssert.IsEmpty(issues);
        }
    }
}
