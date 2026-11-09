using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Title panel (view only): name, tagline, start / settings / credits buttons. Text comes from the string table
    /// (keys title.*, docs/architecture.md 13.2) in the role fonts of the current language (13.4); <see cref="Apply"/>
    /// redraws it when the language changes. Built from code (prefab later, with the art theme).
    /// </summary>
    public sealed class TitleView
    {
        readonly GameObject panel;
        readonly Text name, sub, tagline, start, settings, credits;

        public event Action StartRequested, SettingsRequested, CreditsRequested;

        public TitleView(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("Title", root).Stretch().gameObject;
            var t = panel.transform;
            name = Ui.Label(t, "", 96, Ui.Gold, TextAnchor.MiddleCenter, "Name");
            name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(800, 120));
            sub = Ui.Label(t, "", 26, Ui.Dim, TextAnchor.MiddleCenter, "Sub");
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 76), new Vector2(400, 36));
            tagline = Ui.Label(t, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Tagline");
            tagline.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 30));
            var startButton = Ui.MakeButton(t, "", 24, () => StartRequested?.Invoke(), out start, "Start");
            ((RectTransform)startButton.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(260, 54));
            var set = Ui.MakeButton(t, "", 20, () => SettingsRequested?.Invoke(), out settings, "Settings");
            ((RectTransform)set.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(260, 44));
            var cr = Ui.MakeButton(t, "", 20, () => CreditsRequested?.Invoke(), out credits, "Credits");
            ((RectTransform)cr.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -182), new Vector2(260, 44));
            var ver = Ui.Label(t, Application.version, 14, Ui.Dim, TextAnchor.LowerRight, "Version");
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(300, 20));
            panel.SetActive(false);
        }

        /// Text and fonts for the current language (call once, then on Localizer.Changed).
        public void Apply(Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            UiText.Set(name, loc, "title.name", FontRole.Title);
            UiText.Set(sub, loc, "title.subtitle", FontRole.Body);
            UiText.Set(tagline, loc, "title.tagline", FontRole.Body);
            UiText.Set(start, loc, "title.start", FontRole.Body);
            UiText.Set(settings, loc, "title.settings", FontRole.Body);
            UiText.Set(credits, loc, "title.credits", FontRole.Body);
        }

        public void Show() => panel.SetActive(true);
        public void Hide() => panel.SetActive(false);
    }

    /// <summary>
    /// Stage select panel (view only): stage grid with clear marks and best bodies, back / settings buttons.
    /// Text from the string table (keys stageselect.*), role fonts of the current language; stage names are levels.json
    /// names until the per-language level texts land (LevelDto.text, 13.5).
    /// </summary>
    public sealed class StageSelectView
    {
        readonly GameObject panel;
        readonly Level[] levels;
        readonly Button[] stageButtons;
        readonly Text[] stageTexts;
        readonly Text head, back, settings;
        readonly ScrollRect scroll;
        Localizer loc;
        ProgressData shown;

        /// 0-based stage.
        public event Action<int> StageClicked;
        public event Action BackRequested, SettingsRequested;

        public StageSelectView(Transform root, Level[] levels)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            if (levels.Length == 0) throw new ArgumentException("no stages", nameof(levels));
            panel = Ui.Rect("StageSelect", root).Stretch().gameObject;
            var s = panel.transform;
            head = Ui.Label(s, "", 40, Ui.Gold, TextAnchor.MiddleCenter, "Head");
            head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 56));
            // 50 stages do not fit: the grid scrolls inside a masked viewport between the header and the bottom buttons.
            var viewport = Ui.MakeImage(s, new Color(0, 0, 0, 0.01f), null, "Viewport");
            viewport.rectTransform.anchorMin = new Vector2(0.5f, 0);
            viewport.rectTransform.anchorMax = new Vector2(0.5f, 1);
            viewport.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            viewport.rectTransform.offsetMin = new Vector2(-500, 90);
            viewport.rectTransform.offsetMax = new Vector2(500, -110);
            viewport.raycastTarget = true; // wheel and drag
            viewport.gameObject.AddComponent<RectMask2D>();
            var grid = Ui.Rect("Grid", viewport.transform);
            grid.anchorMin = new Vector2(0, 1);
            grid.anchorMax = new Vector2(1, 1);
            grid.pivot = new Vector2(0.5f, 1);
            grid.anchoredPosition = Vector2.zero;
            grid.sizeDelta = Vector2.zero;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(232, 84);
            g.spacing = new Vector2(14, 14);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            stageButtons = new Button[levels.Length];
            stageTexts = new Text[levels.Length];
            for (int k = 0; k < levels.Length; k++)
            {
                int idx = k;
                stageButtons[k] = Ui.MakeButton(grid, "", 18, () => StageClicked?.Invoke(idx), out stageTexts[k], $"Stage {k + 1}");
                stageTexts[k].horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            var backButton = Ui.MakeButton(s, "", 18, () => BackRequested?.Invoke(), out back, "Back");
            ((RectTransform)backButton.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 40), new Vector2(180, 44));
            var set = Ui.MakeButton(s, "", 18, () => SettingsRequested?.Invoke(), out settings, "Settings");
            ((RectTransform)set.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-110, 40), new Vector2(180, 44));
            panel.SetActive(false);
        }

        /// Text and fonts for the current language (call once, then on Localizer.Changed); redraws the grid if shown.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            UiText.Set(head, loc, "stageselect.head", FontRole.Title);
            UiText.Set(back, loc, "stageselect.back", FontRole.Body);
            UiText.Set(settings, loc, "stageselect.settings", FontRole.Body);
            var body = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            foreach (var t in stageTexts) t.font = body;
            if (shown != null && panel.activeSelf) Show(shown);
        }

        /// Shows the grid with this progress (read only) and selects the last played stage.
        public void Show(ProgressData progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (loc == null) throw new InvalidOperationException("StageSelectView.Show before Apply");
            shown = progress;
            panel.SetActive(true);
            int last = progress.LastStageIndex(levels);
            for (int k = 0; k < levels.Length; k++)
            {
                bool done = progress.IsCleared(levels[k]);
                int b = progress.BestBodies(levels[k]);
                string mark = done ? loc.Get("stageselect.cleared") + (b > 0 ? loc.Format("stageselect.best_bodies", b) : "") : "";
                stageTexts[k].text = $"{k + 1}. {levels[k].Name}\n<size=14>{mark}</size>";
                stageTexts[k].color = done ? Ui.Gold : Ui.Ink;
                if (k == last) stageButtons[k].Select();
            }
            ScrollTo(last);
        }

        /// Puts the row of a stage in view (4 per row): the last played stage when the panel opens.
        void ScrollTo(int stage)
        {
            int rows = (levels.Length + 3) / 4;
            if (rows <= 1) return;
            Canvas.ForceUpdateCanvases();
            float t = 1f - (float)(stage / 4) / (rows - 1); // 1 = top
            scroll.verticalNormalizedPosition = Mathf.Clamp01(t);
        }

        public void Hide() => panel.SetActive(false);
    }

    /// <summary>Puts a table string on a Text in the current language's role font (docs/architecture.md 13.2, 13.4).</summary>
    public static class UiText
    {
        public static void Set(Text text, Localizer loc, string key, FontRole role)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            text.text = loc.Get(key);
            text.font = Ui.Theme.FontFor(loc.Current, role);
        }
    }
}
