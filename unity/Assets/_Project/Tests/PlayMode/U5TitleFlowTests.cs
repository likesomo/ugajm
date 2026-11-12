using System.Collections;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// The U5 title in Play Mode (-ui5; docs/ui-u5-kit.md 3): Boot → the U5 title over the title planet (no old menu),
    /// the map icon opens the v4 map and back returns, language and credits open over the planet with the title's own
    /// controls hidden and close again, and the play stone begins stage 1 on a fresh save. Same environment as SceneFlowTests.
    /// </summary>
    public sealed class U5TitleFlowTests
    {
        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        static IEnumerator Boot(params string[] extra)
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch(extra));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title);
        }

        static GameObject TitleWorldRoot() => GameObject.Find("TitleWorld");

        [UnityTest]
        public IEnumerator U5TitleOpensMapLanguageCreditsAndComesBack()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNotNull(title.U5Title, "-ui5: the U5 title");
            Assert.IsTrue(title.U5Title.Visible);
            Assert.IsNull(GameObject.Find("TitleUI/Title"), "no old menu panel");
            Assert.IsNotNull(TitleWorldRoot(), "the title planet is up");
            Assert.AreEqual(3, CountBrothers(), "three brothers on it");
            var flow = Services.Get<AppFlow>();

            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            Assert.IsNotNull(title.StageSelect, "U5: the v4 map");
            Assert.IsFalse(title.U5Title.Visible);
            Assert.IsNull(TitleWorldRoot(), "the title planet hides on the map");
            flow.Go(AppState.Title);
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title);
            Assert.IsTrue(title.U5Title.Visible, "back on the title");
            Assert.IsNotNull(TitleWorldRoot());

            title.U5Title.Language.Press();
            yield return null;
            Assert.IsFalse(title.U5Title.Visible, "the title's controls make way for the picker");
            title.U5Language.Next.Press();
            yield return null;
            Assert.IsTrue(title.U5Title.Visible, "keep: the picker closes, the title is back");

            title.U5Title.Credits.Press();
            yield return null;
            Assert.IsFalse(title.U5Title.Visible);
            title.U5Credits.Back.Press();
            yield return null;
            Assert.IsTrue(title.U5Title.Visible);
        }

        [UnityTest]
        public IEnumerator PlayStoneBeginsStageOneOnAFreshSave()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.AreEqual(Services.Get<Localizer>().Get("ui5.title.begin"), title.U5Title.Caption, "nothing played yet: first step");
            title.U5Title.Play.Press();
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            Assert.AreEqual(0, Services.Get<AppFlow>().Stage, "stage 1");
        }

        [UnityTest]
        public IEnumerator ToggleOffKeepsTheCurrentTitle()
        {
            yield return Boot("-ui4");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNull(title.U5Title, "-ui4: the current title");
            Assert.IsNull(TitleWorldRoot());
        }

        static int CountBrothers()
        {
            int n = 0;
            foreach (Transform t in TitleWorldRoot().transform) if (t.name.StartsWith("Brother ")) n++;
            return n;
        }
    }
}
