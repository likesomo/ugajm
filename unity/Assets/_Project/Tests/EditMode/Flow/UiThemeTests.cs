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
                Assert.AreEqual(7, issues.Count, string.Join("\n", issues)); // font, settingsPanel, resultPanel, hud, number, ko fonts, en fonts
                Assert.Throws<System.InvalidOperationException>(() => Ui.Theme = theme);
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void FontsPerLanguageAndRole()
        {
            TestTheme.Use();
            var t = Ui.Theme;
            Assert.AreEqual("Gaegu-Regular", t.FontFor(SevenSteps.Contracts.Language.Ko, FontRole.Title).name);
            Assert.AreEqual("KaushanScript-Regular", t.FontFor(SevenSteps.Contracts.Language.En, FontRole.Title).name);
            Assert.AreEqual("PatrickHand-Regular", t.FontFor(SevenSteps.Contracts.Language.En, FontRole.Body).name);
            Assert.AreSame(t.FontFor(SevenSteps.Contracts.Language.Ko, FontRole.Number), t.FontFor(SevenSteps.Contracts.Language.En, FontRole.Number), "numbers do not change with the language");
            Assert.AreEqual(0.9f, t.FontsFor(SevenSteps.Contracts.Language.En).BodyLineSpacing, 1e-4f);
            Assert.AreEqual(1f, t.FontsFor(SevenSteps.Contracts.Language.En).TitleScale, "0 means 1");
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
