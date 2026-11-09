using System;
using System.Collections;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// <see cref="ISceneLoader"/> over SceneManager with a fade (docs/architecture.md 11.1, docs/scene-flow.md S5): fade to
    /// black, LoadSceneAsync, raise Loaded from SceneManager.sceneLoaded (after the new scene's Awake, before its Start), then
    /// fade back in. Leaving the Boot scene does not fade out (it shows nothing). Same-scene transitions never reach here.
    /// Failure: a scene that cannot be loaded throws in Load, before anything starts, so AppFlow is not left loading
    /// (architect S2 note); AppFlow rolls its state back. The fade canvas lives on its own DontDestroyOnLoad object
    /// ("SceneFader"); it blocks clicks only while the screen is dark.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        public const float FadeSeconds = 0.15f;

        string pending;
        Fader fader;

        public string Current => SceneManager.GetActiveScene().name;
        public event Action<string> Loaded;

        public void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("scene name is empty", nameof(sceneName));
            if (pending != null) throw new InvalidOperationException($"SceneLoader: {sceneName} requested while {pending} is loading");
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException($"SceneLoader: scene {sceneName} is not in the build (unity.sh scene-validate)");
            pending = sceneName;
            if (fader == null) fader = Fader.Create();
            bool fadeOut = Current != AppScenes.Boot;
            fader.StartCoroutine(Run(sceneName, fadeOut));
        }

        IEnumerator Run(string sceneName, bool fadeOut)
        {
            if (fadeOut) yield return fader.To(1f);
            else fader.Set(1f);
            SceneManager.sceneLoaded += OnSceneLoaded;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                // CanStreamedLevelBeLoaded said yes, but Unity refused: report the scene as loaded so AppFlow is not stuck,
                // and log loudly (the old scene stays).
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Debug.LogError($"[SevenSteps] SceneLoader: LoadSceneAsync({sceneName}) returned null");
                Finish(sceneName);
                yield return fader.To(0f);
                yield break;
            }
            while (!op.isDone) yield return null;
            yield return null; // let the new scene's Start run behind the dark screen
            yield return fader.To(0f);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != pending) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Finish(scene.name);
        }

        void Finish(string sceneName)
        {
            pending = null;
            Loaded?.Invoke(sceneName);
        }

        /// Full-screen black image on a top canvas that survives scene loads; runs the fade coroutines.
        sealed class Fader : MonoBehaviour
        {
            CanvasGroup group;

            public static Fader Create()
            {
                var go = new GameObject("SceneFader", typeof(RectTransform));
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                go.AddComponent<GraphicRaycaster>();
                var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                img.color = Color.black;
                var f = go.AddComponent<Fader>();
                f.group = go.AddComponent<CanvasGroup>();
                f.Set(0f);
                return f;
            }

            public void Set(float alpha)
            {
                group.alpha = alpha;
                group.blocksRaycasts = alpha > 0.5f;
            }

            public IEnumerator To(float target)
            {
                float start = group.alpha;
                for (float t = 0; t < FadeSeconds; t += Time.unscaledDeltaTime)
                {
                    Set(Mathf.Lerp(start, target, t / FadeSeconds));
                    yield return null;
                }
                Set(target);
            }
        }
    }
}
