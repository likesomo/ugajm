using System;
using System.Collections.Generic;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Chapter card (view only, docs/scene-flow.md 12.8 hook): planet name, chapter number, intro line, shown as an overlay
    /// when a new chapter starts. The prefab is gameplay/UI's (UiTheme.chapterCard, optional: no prefab = no card); this
    /// class is the contract the prefab uses. It asks to close after <see cref="seconds"/> (unscaled) or on a click;
    /// Esc closes it through the BackNavigator.
    /// </summary>
    public sealed class ChapterCardView : MonoBehaviour, IChapterCard
    {
        public Text planet;
        [Tooltip("Optional: \"1\", \"Chapter 1\"... (format below)")]
        public Text number;
        [Tooltip("Optional")]
        public Text intro;
        [Tooltip("Optional: a full-screen button that closes the card on click")]
        public Button closeArea;
        [Tooltip("{0} = 1-based chapter number")]
        public string numberFormat = "{0}";
        [Tooltip("Seconds before the card asks to close by itself (0 = only on click / Esc)")]
        public float seconds = 2.5f;

        float shownAt;
        bool wired;

        public event Action CloseRequested;

        public void Validate(List<string> issues)
        {
            if (planet == null) issues.Add($"ChapterCardView {name}: planet is not set");
            if (seconds < 0) issues.Add($"ChapterCardView {name}: seconds must be 0 or more");
            try { string.Format(numberFormat ?? "", 1); }
            catch (FormatException) { issues.Add($"ChapterCardView {name}: numberFormat \"{numberFormat}\" is not a valid format"); }
        }

        void OnValidate()
        {
            var issues = new List<string>();
            Validate(issues);
            foreach (var i in issues) Debug.LogError("[SevenSteps] " + i, this);
        }

        void Awake() => Wire();

        void Wire()
        {
            if (wired) return;
            var issues = new List<string>();
            Validate(issues);
            if (issues.Count > 0) throw new MissingReferenceException("[SevenSteps] " + string.Join("; ", issues));
            if (closeArea != null) closeArea.onClick.AddListener(() => CloseRequested?.Invoke());
            wired = true;
        }

        public void Bind(ChapterCardModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Wire();
            planet.text = model.Planet;
            if (number != null) number.text = string.Format(numberFormat, model.Number);
            if (intro != null) intro.text = model.Intro;
        }

        public void Show()
        {
            Wire();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (seconds > 0 && Time.unscaledTime - shownAt >= seconds) CloseRequested?.Invoke();
        }

        /// Places the theme's card prefab under <paramref name="parent"/> (an overlay canvas), hidden. Null when the theme has none.
        public static ChapterCardView CreateOptional(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var prefab = Ui.Theme.chapterCard;
            if (prefab == null) return null;
            var card = Instantiate(prefab, parent, false);
            card.name = prefab.name;
            card.gameObject.SetActive(false);
            return card;
        }
    }
}
