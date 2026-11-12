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
        public IEnumerator U5MapHasOnlyBackAndSettings()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            var flow = Services.Get<AppFlow>();
            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            yield return null;
            Assert.IsNotNull(title.U5Map, "U5 + v4 map: the map controls");
            Assert.IsTrue(title.U5Map.Visible);
            var chevron = GameObject.Find("TitleUI/PlanetMap3D/Back");
            Assert.IsTrue(chevron != null && !chevron.activeInHierarchy, "the map's own chevron is there but hidden");
            Assert.IsNull(GameObject.Find("TitleUI/LanguageBadge"), "no language badge under U5");

            title.U5Map.Settings.Press();
            yield return null;
            Assert.IsFalse(title.U5Map.Visible, "another panel is open: the controls hide");
            Object.FindAnyObjectByType<SettingsPanel>().close.onClick.Invoke(); // the overlay stack closes it
            yield return null;
            yield return null;
            Assert.IsTrue(title.U5Map.Visible, "back over the map");

            title.U5Map.Back.Press(); // presses the map's hidden chevron: flow back
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title);
            Assert.IsTrue(title.U5Title.Visible);
            Assert.IsFalse(title.U5Map.Visible);
        }

        /// The U5 surface constellation (D2b): no locks, an untouched stage plays like any other; a press that turns into a
        /// drag does not play; keyboard focus moves between nodes; the caption follows focus.
        [UnityTest]
        public IEnumerator U5ConstellationNodePlaysItsStage()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            var flow = Services.Get<AppFlow>();
            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            GameObject.Find("TitleUI/PlanetMap3D/Planet 1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float t0 = Time.realtimeSinceStartup;
            while (title.SurfaceView == null || !title.SurfaceView.Visible)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the surface");
                yield return null;
            }
            int third = SevenSteps.Game.UI.PlanetEntry.FromCatalog(flow.StageCount)[0].Stages[2];
            var surface = title.SurfaceView;
            var stars = surface.Constellation;
            Assert.IsNotNull(stars);
            Assert.AreEqual("untouched current", stars.State(0));
            StringAssert.StartsWith("01 ", surface.Step.text, "the caption names the focused (current) stage");

            // Keyboard: right moves focus to another node, the caption follows.
            var nav = stars.Button(0).navigation;
            Assert.IsNotNull(nav.selectOnRight ?? nav.selectOnUp ?? nav.selectOnDown, "a neighbour to move to");

            // A press that becomes a drag is cancelled.
            var es = UnityEngine.EventSystems.EventSystem.current;
            var data = new UnityEngine.EventSystems.PointerEventData(es);
            var node = stars.Button(2).gameObject;
            UnityEngine.EventSystems.ExecuteEvents.Execute(node, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(node, data, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            stars.Button(2).onClick.Invoke(); // a click an input module might still send before the drag ends
            yield return null;
            Assert.AreEqual(AppState.StageSelect, flow.State, "a dragged press does not play");
            UnityEngine.EventSystems.ExecuteEvents.Execute(node, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(node, data, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
            yield return null;
            Assert.AreEqual(AppState.StageSelect, flow.State);

            // After the drag (no click), keyboard Submit on the node plays it (architect R1): an untouched stage, no locks.
            UnityEngine.EventSystems.ExecuteEvents.Execute(node, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            Assert.AreEqual(third, flow.Stage, "stage 3 of the first planet");
        }

        /// A slow load under -ui5 (-loadDelay) shows the scarf sparks on the fader, not the loading screen.
        [UnityTest]
        public IEnumerator SlowLoadShowsTheScarfSparks()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch("-ui5", "-loadDelay", "2"));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            bool seen = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 10f)
            {
                var sparks = GameObject.Find("SceneFader/Scarf sparks");
                if (sparks != null && sparks.GetComponent<CanvasGroup>().alpha > 0.5f) { seen = true; break; }
                yield return null;
            }
            Assert.IsTrue(seen, "the sparks show during the slow Boot -> Title load");
            Assert.IsNull(GameObject.Find("SceneFader/Loading"), "no loading screen under U5");
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title, 15f);
            Assert.Less(GameObject.Find("SceneFader/Scarf sparks").GetComponent<CanvasGroup>().alpha, 0.5f, "gone once the title is up");
        }

        [UnityTest]
        public IEnumerator PlayStoneBeginsStageOneOnAFreshSave()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.AreEqual(Services.Get<Localizer>().Get("ui5.title.begin"), title.U5Title.Caption, "nothing played yet: first step");
            title.U5Title.Play.Press();
            // New game: the storybook prologue first (design/story/cutscenes.md 2); skipping it starts the journey.
            Assert.IsTrue(title.Cutscene.IsOpen && title.CutsceneView.Visible, "the prologue on a fresh save");
            Assert.AreEqual(SevenSteps.Contracts.CutsceneCatalog.Prologue, title.Cutscene.Scene.id);
            Assert.IsFalse(title.U5Title.Visible, "the title's controls make way");
            Assert.AreEqual(AppState.Title, Services.Get<AppFlow>().State, "no stage until the prologue ends");
            title.CutsceneView.Next.Press();
            Assert.AreEqual(2, title.Cutscene.Shown, "next: the page's second line");
            title.CutsceneView.Skip.Press();
            Assert.IsTrue(Services.Get<SaveSystem>().TutorialSeen(TutorialIds.Prologue), "seen once skipped");
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            Assert.AreEqual(0, Services.Get<AppFlow>().Stage, "stage 1 (the tutorial boards will come first once core's data is in)");
        }

        [UnityTest]
        public IEnumerator StoryIconReplaysThePrologueAndComesBack()
        {
            yield return Boot("-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsTrue(title.U5Title.Story.gameObject.activeSelf, "the story entry is on the title");
            title.U5Title.Story.Press();
            Assert.IsTrue(title.Cutscene.IsOpen, "the prologue replays");
            Assert.IsFalse(Services.Get<SaveSystem>().TutorialSeen(TutorialIds.Prologue), "a replay does not count as the new-game one");
            title.Cutscene.Skip(); // Esc does the same through the BackNavigator
            yield return null;
            Assert.IsFalse(title.CutsceneView.Visible);
            Assert.IsTrue(title.U5Title.Visible, "back on the title");
            Assert.AreEqual(AppState.Title, Services.Get<AppFlow>().State);
        }

        [UnityTest]
        public IEnumerator ASeenPrologueDoesNotPlayAgain()
        {
            yield return Boot("-ui5");
            Services.Get<SaveSystem>().MarkTutorialSeen(TutorialIds.Prologue);
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.U5Title.Play.Press();
            Assert.IsFalse(title.Cutscene.IsOpen);
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
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
