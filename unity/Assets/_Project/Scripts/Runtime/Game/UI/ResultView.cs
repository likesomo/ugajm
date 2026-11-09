using System;
using System.Collections.Generic;
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

        public ResultModel(int stage, bool cleared, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast,
            int stars = 0, int bestStars = 0)
        {
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
        }

        public static ResultModel Clear(int stage, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast,
            int stars = 0, int bestStars = 0) =>
            new ResultModel(stage, true, levelBodies, bodiesUsed, minBodies, bestBodies, isLast, stars, bestStars);

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

        public void Show(ResultModel m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            Wire();
            if (m.Cleared)
            {
                // "몸 2개 (최소 1개 · 최고 기록 2개)". Minimum = JS solver's expected.minBodies.
                string detail = m.MinBodies > 0 ? $"최소 {m.MinBodies}개" : "";
                if (m.BestBodies > 0) detail += (detail.Length > 0 ? " · " : "") + $"최고 기록 {m.BestBodies}개";
                title.text = "클리어";
                body.text = $"몸 {m.BodiesUsed}개" + (detail.Length > 0 ? $" ({detail})" : "") + (m.IsLast ? "\n모든 스테이지를 끝냈다. 고맙습니다!" : "");
            }
            else
            {
                title.text = "몸을 모두 잃었다";
                body.text = "되돌리거나 처음부터 다시.";
            }
            next.gameObject.SetActive(m.Cleared && !m.IsLast);
            again.gameObject.SetActive(m.Cleared);
            select.gameObject.SetActive(true);
            undo.gameObject.SetActive(!m.Cleared);
            restart.gameObject.SetActive(!m.Cleared);
            overlay.SetActive(true);
        }

        public void Hide()
        {
            Wire();
            overlay.SetActive(false);
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
