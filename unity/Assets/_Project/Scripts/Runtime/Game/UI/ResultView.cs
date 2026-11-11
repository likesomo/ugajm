using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
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
        /// Player's best for this stage, 0 = no record.
        public int BestBodies { get; }
        public bool IsLast { get; }
        /// Stars of this clear, 0..3 (0 = a loss, or not judged). docs/architecture.md 6.5.
        public int Stars { get; }
        /// Best stars recorded for the stage, 0..3, at least <see cref="Stars"/> (the clear is saved first).
        public int BestStars { get; }
        /// The planet this clear finishes (its last stage, not the final stage), null otherwise: the panel says goodbye to it.
        public ChapterInfo FinishedChapter { get; }

        public ResultModel(int stage, bool cleared, int levelBodies, int bodiesUsed, int bestBodies, bool isLast,
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
            if (bestBodies < 0 || bestBodies > levelBodies) throw new ArgumentOutOfRangeException(nameof(bestBodies), bestBodies, $"0 (no record) or 1..{levelBodies}");
            Stage = stage;
            Cleared = cleared;
            LevelBodies = levelBodies;
            BodiesUsed = bodiesUsed;
            BestBodies = bestBodies;
            IsLast = isLast;
            Stars = stars;
            BestStars = bestStars;
            FinishedChapter = finishedChapter;
        }

        public static ResultModel Clear(int stage, int levelBodies, int bodiesUsed, int bestBodies, bool isLast,
            int stars = 0, int bestStars = 0, ChapterInfo finishedChapter = null) =>
            new ResultModel(stage, true, levelBodies, bodiesUsed, bestBodies, isLast, stars, bestStars, finishedChapter);

        public static ResultModel Loss(int stage, int levelBodies, bool isLast) =>
            new ResultModel(stage, false, levelBodies, 0, 0, isLast);
    }

    /// <summary>
    /// Clear / loss panel (view only, docs/scene-flow.md 4.7, S6). Prefab UI/Prefabs/ResultPanel.prefab (its own canvas,
    /// sorting 5, above the HUD), referenced by UiTheme.resultPanel. Shows a <see cref="ResultModel"/> and raises button
    /// events; the flow controller decides what they do. Layout lives in the prefab (ResultLayoutBuilder).
    ///
    /// Minimal result (production/ui-minimal-plan.md: stars, constellation, next only; design/art/ui-minimal.md): a clear
    /// is shown over the board, so the finale constellation stays in view: a light scrim (input still blocked), the HUD
    /// hidden (<see cref="HudHidden"/>), the three stars on a small plate in the band above the board, one Next button
    /// bottom right (the last stage: [끝까지 보기] -> GameFlowController.EndingRequested), and the planet / journey line on a
    /// plate at the bottom centre when this clear ends one. What a star asks shows on hover or a tap of that star, on a
    /// plate beside the stars (clear of the board). No again / stage select buttons: R and Esc do that (docs 14.7,
    /// 9eda0b0). A loss keeps the modal panel (title, line, undo / restart / stage select).
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
        [Tooltip("What each star asks (result.star1..3), each on a tip plate under its star shown on hover / tap; earned in ink, the rest dimmed.")]
        public Text[] starLabels = new Text[Core.Stars.Max];
        [Tooltip("Full-screen scrim (the Overlay image): light for a clear over the board, modal for a loss.")]
        public Image scrim;
        [Tooltip("The loss modal panel (title, body, buttons).")]
        public RectTransform panel;
        [Tooltip("Clear over the board: stars plate, next slot, finale plate.")]
        public RectTransform clearGroup;
        [Tooltip("Bottom right: Next (on the last stage: on to the ending, result.ending).")]
        public RectTransform nextSlot;
        [Tooltip("Bottom centre: the planet / journey line (story FinaleLine later); body sits here for a clear.")]
        public RectTransform finalePlate;

        /// Scrim alphas: a clear over the board (the constellation shows through), a loss (docs 14.7 modal).
        public const float ClearScrimAlpha = 0.25f, LossScrimAlpha = 0.72f;

        public event Action NextRequested, RetryRequested, StageSelectRequested;
        /// Loss only: the board actions stay available on the panel (Z / R also work, the loss is not an overlay).
        public event Action UndoRequested, RestartRequested;
        /// true: a clear is shown over the board, hide the HUD (docs 14.7: a scrim under 0.6 needs the UI under it hidden);
        /// false: show it again (Hide, or a loss with the modal scrim).
        public event Action<bool> HudHidden;

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
            Need(scrim, nameof(scrim)); Need(panel, nameof(panel)); Need(clearGroup, nameof(clearGroup));
            Need(nextSlot, nameof(nextSlot)); Need(finalePlate, nameof(finalePlate));
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
            bodyHome = Home.Of(body.rectTransform);
            for (int k = 0; k < stars.Length; k++)
            {
                int star = k;
                if (!stars[k].TryGetComponent<EventTrigger>(out var trigger)) trigger = stars[k].gameObject.AddComponent<EventTrigger>();
                void On(EventTriggerType type, Action act)
                {
                    var e = new EventTrigger.Entry { eventID = type };
                    e.callback.AddListener(_ => act());
                    trigger.triggers.Add(e);
                }
                On(EventTriggerType.PointerEnter, () => ShowTip(star));
                On(EventTriggerType.PointerExit, () => { if (tip == star && !tipPinned) ShowTip(-1); });
                On(EventTriggerType.PointerClick, () => { bool same = tip == star && tipPinned; ShowTip(same ? -1 : star); tipPinned = !same; });
                stars[k].raycastTarget = true;
            }
            ShowTip(-1);
            wired = true;
        }

        /// Where an element sits in the loss panel, to put it back after a clear borrowed it.
        readonly struct Home
        {
            readonly Transform parent;
            readonly int sibling;
            readonly Vector2 min, max, pivot, pos, size;
            Home(RectTransform r)
            {
                parent = r.parent; sibling = r.GetSiblingIndex();
                min = r.anchorMin; max = r.anchorMax; pivot = r.pivot; pos = r.anchoredPosition; size = r.sizeDelta;
            }
            public static Home Of(RectTransform r) => new Home(r);
            public void Restore(RectTransform r)
            {
                if (r.parent != parent) { r.SetParent(parent, false); r.SetSiblingIndex(sibling); }
                r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            }
        }

        Home bodyHome;
        int tip = -1;
        bool tipPinned;

        /// Shows what star <paramref name="k"/> asks on its tip plate (-1: none); that star is drawn a little larger.
        void ShowTip(int k)
        {
            tip = k;
            if (k < 0) tipPinned = false;
            for (int i = 0; i < starLabels.Length; i++) starLabels[i].transform.parent.gameObject.SetActive(i == k);
            if (reveal == null)
                for (int i = 0; i < stars.Length; i++) stars[i].rectTransform.localScale = Vector3.one * (i == k ? FocusScale : 1f);
        }

        const float FocusScale = 1.15f;

        /// Star tip keyboard focus (docs 14.7 hover / focus / tap, review M1 on 48ff6b4): +1 next star, -1 previous,
        /// wrapping; the first press shows star 1 (or 3 going back). Only while a clear with its star row is shown. The keys
        /// (Tab / Right, Shift+Tab / Left) are read by GameController with the other flow keys, under its overlay check
        /// (review R1 on c58dfa4: a menu over the result must not move the focus).
        public void FocusStar(int delta)
        {
            if (!StarRowShown) return;
            int n = stars.Length;
            int k = tip < 0 ? (delta >= 0 ? 0 : n - 1) : ((tip + delta) % n + n) % n;
            ShowTip(k);
            tipPinned = true;
        }

        /// A clear with stars is up (the row the tips belong to).
        public bool StarRowShown => IsShown && shown != null && shown.Cleared && stars[0].transform.parent.gameObject.activeInHierarchy;

        /// Puts <paramref name="r"/> inside <paramref name="slot"/>, filling it (inset by <paramref name="pad"/>).
        static void Dock(RectTransform r, RectTransform slot, float pad)
        {
            if (r.parent != slot) r.SetParent(slot, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = new Vector2(pad, pad / 2); r.offsetMax = new Vector2(-pad, -pad / 2);
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
                // Minimal: no title or counts (the stars say it); only the planet / journey line, when this clear ends one.
                title.text = loc.Get("result.clear");
                body.text = m.IsLast ? loc.Get("result.journey_done")
                    : m.FinishedChapter != null ? loc.Format("result.chapter_done", m.FinishedChapter.Planet(loc.Current)) : "";
            }
            else
            {
                title.text = loc.Get("result.lost_title");
                body.text = loc.Get("result.lost_body");
            }
            ShowStarLabels(m);
            Set(Label(next), m.IsLast ? "result.ending" : "result.next"); // last stage: [끝까지 보기] -> the ending (16.8)
            Arrange(m);
            if (redraw) return;
            ShowTip(-1);
            overlay.SetActive(true);
            HudHidden?.Invoke(m.Cleared);
            ShowStars(m.Cleared ? m.Stars : 0);
        }

        /// A clear over the board or the loss modal: scrim, which group shows, where body and stage select sit.
        void Arrange(ResultModel m)
        {
            bool clear = m.Cleared;
            var c = scrim.color;
            scrim.color = new Color(c.r, c.g, c.b, clear ? ClearScrimAlpha : LossScrimAlpha);
            scrim.raycastTarget = true;
            panel.gameObject.SetActive(!clear);
            clearGroup.gameObject.SetActive(clear);
            if (clear)
            {
                Dock(body.rectTransform, finalePlate, 16);
                body.alignment = TextAnchor.MiddleCenter;
                finalePlate.gameObject.SetActive(body.text.Length > 0);
            }
            else bodyHome.Restore(body.rectTransform);
            next.gameObject.SetActive(clear); // the last stage: [끝까지 보기] (GameFlowController.EndingRequested)
            again.gameObject.SetActive(false); // R in Result (GameController / flow), not a button on the minimal result
            select.gameObject.SetActive(!clear); // Esc in Result: the menu, then stage select
            undo.gameObject.SetActive(!clear);
            restart.gameObject.SetActive(!clear);
        }

        public void Hide()
        {
            Wire();
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            bool was = overlay.activeSelf;
            overlay.SetActive(false);
            if (was) HudHidden?.Invoke(false);
        }

        Coroutine reveal;

        /// 0 hides the row (loss, or no minimum to judge by). Otherwise all three start empty and the earned ones fill in
        /// order with a small pop (unscaled time; outside play mode they are set at once).
        /// The three criteria (docs/architecture.md 6.5) under the stars: clear / no solution shown / every brother used
        /// (design/levels/stars.md 0). Words only; whether a star was earned comes from the model.
        void ShowStarLabels(ResultModel m)
        {
            if (!(m.Cleared && m.Stars > 0)) { ShowTip(-1); return; }
            var font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            // One line each on the tip plate beside the stars (the table strings break for the old two-line columns).
            starLabels[0].text = OneLine(loc.Get("result.star1"));
            starLabels[1].text = OneLine(loc.Get("result.star2"));
            starLabels[2].text = OneLine(loc.Format(m.LevelBodies == 1 ? "result.star3_one" : "result.star3_many", m.LevelBodies));
            for (int k = 0; k < starLabels.Length; k++)
            {
                starLabels[k].font = font;
                starLabels[k].color = k < m.Stars ? Ui.Ink : Ui.Dim;
            }
        }

        static string OneLine(string s) => s.Replace(",\n", ", ").Replace("\n", " ");

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
