using NUnit.Framework;
using SevenSteps.Game.UI;
using UnityEditor;

namespace SevenSteps.Tests
{
    /// Views read Ui.Theme (docs/scene-flow.md S6); tests that build views use the default theme asset.
    static class TestTheme
    {
        public const string Path = "Assets/_Project/Data/UiTheme_Default.asset";

        public static void Use()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(Path);
            Assert.IsNotNull(theme, Path + " (Seven Steps > Scene Flow > Build UI)");
            Ui.Theme = theme;
        }
    }
}
