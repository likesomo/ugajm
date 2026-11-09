using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// What the result panel shows (docs/scene-flow.md 4.7). Read-only; the constructor checks the ranges.
    /// A clear (AppState.Result) and a loss (board status Lose, AppState stays Game) use the same panel.
    /// </summary>
    public sealed class ResultModel
    {
        public int Stage { get; }
        public bool Cleared { get; }
        /// Bodies the level gives (levels.json bodies). The other body counts are checked against it.
        public int LevelBodies { get; }
        /// Bodies used for the clear, 1..LevelBodies. 0 for a loss.
        public int BodiesUsed { get; }
        /// Fewest bodies the solver needs, 0 = unknown.
        public int MinBodies { get; }
        /// Player's best for this stage, 0 = no record.
        public int BestBodies { get; }
        public bool IsLast { get; }
        /// Stars of this clear, 0..3 (0 = unknown minimum or a loss). docs/architecture.md 6.5.
        public int Stars { get; }
        /// Best stars recorded for the stage, 0..3, at least <see cref="Stars"/> (the clear is saved first).
        public int BestStars { get; }
        /// The planet this clear finishes (its last stage, not the final stage), null otherwise: the panel says goodbye to it.
        public ChapterInfo FinishedChapter { get; }

        public ResultModel(int stage, bool cleared, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast,
            int stars = 0, int bestStars = 0, ChapterInfo finishedChapter = null)
        {
            if (!cleared && finishedChapter != null) throw new ArgumentException("a loss finishes no planet", nameof(finishedChapter));
            if (stars < 0 || stars > Core.Stars.Max) throw new ArgumentOutOfRangeException(nameof(stars), stars, $"0..{Core.Stars.Max}");
            if (bestStars < 0 || bestStars > Core.Stars.Max) throw new ArgumentOutOfRangeException(nameof(bestStars), bestStars, $"0..{Core.Stars.Max}");
            if (!cleared && stars != 0) throw new ArgumentOutOfRangeException(nameof(stars), stars, "a loss has no stars");
            if (bestStars < stars) throw new ArgumentOutOfRangeException(nameof(bestStars), bestStars, $"best below this clear's {stars} (save the clear first)");
            if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage), stage, "0-based stage");
            if (levelBodies < 1) throw new ArgumentOutOfRangeException(nameof(levelBodies), levelBodies, "at least one body");
            if (cleared && (bodiesUsed < 1 || bodiesUsed > levelBodies))
                throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, $"a clear uses 1..{levelBodies} bodies");
            if (!cleared && bodiesUsed != 0) throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, "a loss has no body count");
            if (minBodies < 0 || minBodies > levelBodies) throw new ArgumentOutOfRangeException(nameof(minBodies), minBodies, $"0 (unknown) or 1..{levelBodies}");
            if (bestBodies < 0 || bestBodies > levelBodies) throw new ArgumentOutOfRangeException(nameof(bestBodies), bestBodies, $"0 (no record) or 1..{levelBodies}");
            Stage = stage;
            Cleared = cleared;
            LevelBodies = levelBodies;
            BodiesUsed = bodiesUsed;
            MinBodies = minBodies;
            BestBodies = bestBodies;
            IsLast = isLast;
            Stars = stars;
            BestStars = bestStars;
            FinishedChapter = finishedChapter;
        }

        public static ResultModel Clear(int stage, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast,
            int stars = 0, int bestStars = 0, ChapterInfo finishedChapter = null) =>
            new ResultModel(stage, true, levelBodies, bodiesUsed, minBodies, bestBodies, isLast, stars, bestStars, finishedChapter);

        public static ResultModel Loss(int stage, int levelBodies, bool isLast) =>
            new ResultModel(stage, false, levelBodies, 0, 0, 0, isLast);
    }

    /// <summary>
    /// Clear / loss panel (view only, docs/scene-flow.md 4.7, S6). Prefab UI/Prefabs/ResultPanel.prefab (its own canvas,
    /// sorting 5, above the HUD), referenced by UiTheme.resultPanel. Shows a <see cref="ResultModel"/> and raises button
    /// events; the flow controller decides what they do. Layout lives in the prefab (first built by UiBuilder).
    /// </summary>
    public sealed class ResultView : MonoBehaviour, Flow.IResultPanel
    {
        [Tooltip("Dim + panel, toggled by Show/Hide (the canvas itself stays active). Saved inactive in the prefab.")]
        public GameObject overlay;
        public Text title, body;
        public Button next, again, select, undo, restart;
        [Tooltip("3 stars of this clear (docs/architecture.md 6.5), revealed one by one; the row hides for a loss or unknown stars")]
        public Image[] stars = new Image[Core.Stars.Max];
        public Sprite starEmpty, starFilled;
        [Tooltip("What each star asks (result.star1..3, under the stars); earned ones in ink, the rest dimmed. Hidden with the stars.")]
        public Text[] starLabels = new Text[Core.Stars.Max];

        public event Action NextRequested, RetryRequested, StageSelectRequested;
        /// Loss only: the board actions stay available on the panel (Z / R also work, the loss is not an overlay).
        public event Action UndoRequested, RestartRequested;

        public bool IsShown => overlay.activeSelf;

        /// Required references missing in the prefab, one line each (OnValidate, Awake, SceneFlowValidator).
        public void Validate(List<string> issues)
        {
            void Need(Object o, string field) { if (o == null) issues.Add($"ResultView {name}: {field} is not set"); }
            Need(overlay, nameof(overlay)); Need(title, nameof(title)); Need(body, nameof(body));
            Need(next, nameof(next)); Need(again, nameof(again)); Need(select, nameof(select));
            Need(undo, nameof(undo)); Need(restart, nameof(restart));
            if (stars == null || stars.Length != Core.Stars.Max) issues.Add($"ResultView {name}: stars needs {Core.Stars.Max} entries");
            else for (int k = 0; k < stars.Length; k++) Need(stars[k], $"stars[{k}]");
            Need(starEmpty, nameof(starEmpty)); Need(starFilled, nameof(starFilled));
            if (starLabels == null || starLabels.Length != Core.Stars.Max) issues.Add($"ResultView {name}: starLabels needs {Core.Stars.Max} entries");
            else for (int k = 0; k < starLabels.Length; k++) Need(starLabels[k], $"starLabels[{k}]");
        }

        void OnValidate()
        {
            var issues = new List<string>();
            Validate(issues);
            foreach (var i in issues) Debug.LogError("[SevenSteps] " + i, this);
        }

        bool wired;

        void Awake() => Wire();

        /// Checks the references and connects the buttons once (also from Show/Hide: Awake does not run in edit-mode tests).
        void Wire()
        {
            if (wired) return;
            var issues = new List<string>();
            Validate(issues);
            if (issues.Count > 0) throw new MissingReferenceException("[SevenSteps] " + string.Join("; ", issues));
            next.onClick.AddListener(() => NextRequested?.Invoke());
            again.onClick.AddListener(() => RetryRequested?.Invoke());
            select.onClick.AddListener(() => StageSelectRequested?.Invoke());
            undo.onClick.AddListener(() => UndoRequested?.Invoke());
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            wired = true;
        }

        Localizer loc;
        ResultModel shown;

        /// Words and fonts in <paramref name="localizer"/>'s current language (result.*, docs/architecture.md 13.2, 13.4).
        /// Call once before Show, then on Localizer.Changed; an open panel is redrawn in the new language.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            Wire();
            Set(Label(next), "result.next");
            Set(Label(again), "result.again");
            Set(Label(select), "result.select");
            Set(Label(undo), "result.undo");
            Set(Label(restart), "result.restart");
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            body.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            if (shown != null && IsShown) Show(shown);
        }

        void Set(Text text, string key)
        {
            text.text = loc.Get(key);
            text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
        }

        static Text Label(Button b) =>
            b.GetComponentInChildren<Text>(true) ?? throw new MissingReferenceException($"[SevenSteps] ResultView: button {b.name} has no Text label");

        public void Show(ResultModel m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (loc == null) throw new InvalidOperationException("ResultView.Show before Apply(Localizer)");
            Wire();
            bool redraw = ReferenceEquals(m, shown) && IsShown; // language change: keep the stars as they are
            shown = m;
            if (m.Cleared)
            {
                // "Bodies 2 (fewest 1, your best 2)". Minimum = JS solver's expected.minBodies.
                string detail = m.MinBodies > 0 && m.BestBodies > 0 ? loc.Format("result.detail_both", m.MinBodies, m.BestBodies)
                    : m.MinBodies > 0 ? loc.Format("result.detail_min", m.MinBodies)
                    : m.BestBodies > 0 ? loc.Format("result.detail_best", m.BestBodies)
                    : "";
                title.text = loc.Get("result.clear");
                body.text = loc.Format("result.bodies", m.BodiesUsed) + detail + (m.IsLast ? "\n" + loc.Get("result.journey_done")
                    : m.FinishedChapter != null ? "\n" + loc.Format("result.chapter_done", m.FinishedChapter.Planet(loc.Current)) : "");
                if (m.BestStars > m.Stars) body.text += "\n" + loc.Format("result.best_stars", m.BestStars);
            }
            else
            {
                title.text = loc.Get("result.lost_title");
                body.text = loc.Get("result.lost_body");
            }
            ShowStarLabels(m);
            if (redraw) return;
            next.gameObject.SetActive(m.Cleared && !m.IsLast);
            again.gameObject.SetActive(m.Cleared);
            select.gameObject.SetActive(true);
            undo.gameObject.SetActive(!m.Cleared);
            restart.gameObject.SetActive(!m.Cleared);
            overlay.SetActive(true);
            ShowStars(m.Cleared ? m.Stars : 0);
        }

        public void Hide()
        {
            Wire();
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            overlay.SetActive(false);
        }

        Coroutine reveal;

        /// 0 hides the row (loss, or no minimum to judge by). Otherwise all three start empty and the earned ones fill in
        /// order with a small pop (unscaled time; outside play mode they are set at once).
        /// The three criteria (docs/architecture.md 6.5) under the stars: clear / no solution shown and at most the fewest
        /// brothers / no undo or restart. Words only; whether a star was earned comes from the model.
        void ShowStarLabels(ResultModel m)
        {
            bool on = m.Cleared && m.Stars > 0;
            starLabels[0].transform.parent.gameObject.SetActive(on);
            if (!on) return;
            var font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            starLabels[0].text = loc.Get("result.star1");
            starLabels[1].text = loc.Format(m.MinBodies == 1 ? "result.star2_one" : "result.star2_many", m.MinBodies);
            starLabels[2].text = loc.Get("result.star3");
            for (int k = 0; k < starLabels.Length; k++)
            {
                starLabels[k].font = font;
                starLabels[k].color = k < m.Stars ? Ui.Ink : Ui.Dim;
            }
        }

        void ShowStars(int earned)
        {
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            var row = stars[0].transform.parent.gameObject;
            row.SetActive(earned > 0);
            if (earned <= 0) return;
            foreach (var s in stars) { s.sprite = starEmpty; s.rectTransform.localScale = Vector3.one; }
            if (Application.isPlaying && isActiveAndEnabled) reveal = StartCoroutine(Reveal(earned));
            else for (int k = 0; k < earned; k++) stars[k].sprite = starFilled;
        }

        System.Collections.IEnumerator Reveal(int earned)
        {
            for (int k = 0; k < earned; k++)
            {
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) yield return null; // pause before each star
                stars[k].sprite = starFilled;
                var r = stars[k].rectTransform;
                for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
                {
                    r.localScale = Vector3.one * (1f + 0.45f * Mathf.Sin(t / 0.22f * Mathf.PI));
                    yield return null;
                }
                r.localScale = Vector3.one;
            }
            reveal = null;
        }

        /// Places the theme's result prefab (its own canvas) in the current scene.
        public static ResultView Create()
        {
            var prefab = Ui.Theme.resultPanel != null ? Ui.Theme.resultPanel
                : throw new MissingReferenceException($"[SevenSteps] UiTheme {Ui.Theme.name}: resultPanel is not set (Seven Steps > Scene Flow > Build UI)");
            var view = Instantiate(prefab);
            view.name = prefab.name;
            return view;
        }
    }
}
