using System;
using System.Collections;
using SevenSteps.Contracts;
using SevenSteps.Game.UI;
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
    /// ("SceneFader"); it blocks clicks only while the screen is dark. A load that keeps the screen dark longer than
    /// <see cref="LoadingDelay"/> shows the loading screen (art concept loading: "Reading the star map", the Dipper filling
    /// with the load progress, a tip; S7b) so a slow WebGL load does not look frozen.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        public const float FadeSeconds = 0.15f;
        public const float LoadingDelay = 0.3f;

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
            fader.BeginLoading();
            // -loadDelay (development): hold the new scene back so the loading screen can be checked.
            float delay = Services.TryGet<LaunchOptions>(out var launch) ? launch.LoadDelay : 0f;
            if (delay > 0f)
            {
                op.allowSceneActivation = false;
                for (float t = 0; t < delay; t += Time.unscaledDeltaTime) { fader.Progress(Mathf.Min(op.progress / 0.9f, t / delay)); yield return null; }
                op.allowSceneActivation = true;
            }
            while (!op.isDone) { fader.Progress(op.progress / 0.9f); yield return null; }
            fader.Progress(1f);
            yield return null; // let the new scene's Start run behind the dark screen
            yield return fader.To(0f);
            fader.EndLoading();
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

        /// Full-screen image on a top canvas that survives scene loads; runs the fade coroutines and the loading screen.
        sealed class Fader : MonoBehaviour
        {
            const int Tips = 3;
            CanvasGroup group, loading;
            Text head, count, tip;
            Image[] stars;
            float loadingSince = -1;
            int lit = -1;

            public static Fader Create()
            {
                var go = new GameObject("SceneFader", typeof(RectTransform));
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                go.AddComponent<GraphicRaycaster>();
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
                var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                img.color = Ui.HasTheme ? new Color(Ui.PanelBg.r, Ui.PanelBg.g, Ui.PanelBg.b, 1f) : Color.black;
                var f = go.AddComponent<Fader>();
                f.group = go.AddComponent<CanvasGroup>();
                if (Ui.HasTheme) f.BuildLoading(go.transform);
                f.Set(0f);
                return f;
            }

            /// Loading screen (art layout: title top left, Dipper at 420,290 in a 330x150 box, "n / 7", tip line).
            void BuildLoading(Transform root)
            {
                var r = Ui.Rect("Loading", root).Stretch();
                loading = r.gameObject.AddComponent<CanvasGroup>();
                loading.alpha = 0f;
                loading.blocksRaycasts = false;
                head = Ui.Label(r, "", 48, Ui.Ink, TextAnchor.MiddleLeft, "Head");
                head.rectTransform.TopLeft(40, 30, 1070, 72);
                var dipper = Ui.Theme.Dipper;
                Vector2 P(int i) => new Vector2(420 + dipper[i].x * 330, 290 + dipper[i].y * 150);
                foreach (var (a, b) in UiTheme.DipperEdges) Ui.Line(r, P(a), P(b), Ui.Dim, "Edge");
                stars = new Image[7];
                for (int i = 0; i < 7; i++)
                {
                    stars[i] = Ui.Star(r, false, "Star " + (i + 1));
                    stars[i].rectTransform.TopLeft(P(i).x - 12, P(i).y - 12, 24, 24);
                }
                count = Ui.Label(r, "", 34, Ui.Gold, TextAnchor.MiddleCenter, "Count");
                count.rectTransform.TopLeft(540, 425, 200, 48);
                tip = Ui.Label(r, "", 29, Ui.Ink, TextAnchor.MiddleCenter, "Tip");
                tip.rectTransform.TopLeft(190, 543, 900, 84);
                tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            /// The scene load starts behind the dark screen: text in the current language, a random tip, hidden for now.
            public void BeginLoading()
            {
                if (loading == null || !Services.TryGet<Localizer>(out var loc)) return;
                var lang = loc.Current;
                UiText.Set(head, loc, "loading.title", FontRole.Title);
                UiText.Set(tip, loc, "loading.tip." + UnityEngine.Random.Range(1, Tips + 1), FontRole.Body);
                count.font = Ui.Theme.FontFor(lang, FontRole.Number);
                lit = -1;
                Progress(0f);
                loadingSince = Time.unscaledTime;
            }

            /// Load progress 0..1: lights that many of the 7 stars; shows the screen once the load is slower than LoadingDelay.
            public void Progress(float t)
            {
                if (loading == null || loadingSince < 0) return;
                if (Time.unscaledTime - loadingSince >= LoadingDelay) loading.alpha = 1f;
                int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * 7), 0, 7);
                if (n == lit) return;
                lit = n;
                for (int i = 0; i < 7; i++) Ui.SetStar(stars[i], i < n);
                count.text = $"{n} / 7";
            }

            public void EndLoading()
            {
                loadingSince = -1;
                if (loading != null) loading.alpha = 0f;
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
