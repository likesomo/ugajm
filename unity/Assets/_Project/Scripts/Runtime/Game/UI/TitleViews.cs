using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>Title panel (view only): name, tagline, start / settings / credits buttons. S6 moves it to a prefab.</summary>
    public sealed class TitleView
    {
        readonly GameObject panel;

        public event Action StartRequested, SettingsRequested, CreditsRequested;

        public TitleView(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("Title", root).Stretch().gameObject;
            var t = panel.transform;
            var name = Ui.Label(t, "일곱 걸음", 96, Ui.Gold, TextAnchor.MiddleCenter, "Name");
            name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(800, 120));
            var sub = Ui.Label(t, "Seven Steps", 26, Ui.Dim, TextAnchor.MiddleCenter, "Sub");
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 76), new Vector2(400, 36));
            var tag = Ui.Label(t, "한 몸에 딱 일곱 걸음. 일곱 번째 걸음은 행운이다.", 20, Ui.Ink, TextAnchor.MiddleCenter, "Tagline");
            tag.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 30));
            var start = Ui.MakeButton(t, "시작 (Enter)", 24, () => StartRequested?.Invoke(), out _, "Start");
            ((RectTransform)start.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(260, 54));
            var set = Ui.MakeButton(t, "설정", 20, () => SettingsRequested?.Invoke(), out _, "Settings");
            ((RectTransform)set.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(260, 44));
            var cr = Ui.MakeButton(t, "크레딧", 20, () => CreditsRequested?.Invoke(), out _, "Credits");
            ((RectTransform)cr.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -182), new Vector2(260, 44));
            var ver = Ui.Label(t, Application.version, 14, Ui.Dim, TextAnchor.LowerRight, "Version");
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(300, 20));
            panel.SetActive(false);
        }

        public void Show() => panel.SetActive(true);
        public void Hide() => panel.SetActive(false);
    }

    /// <summary>Stage select panel (view only): stage grid with clear marks and best bodies, back / settings buttons.</summary>
    public sealed class StageSelectView
    {
        readonly GameObject panel;
        readonly Level[] levels;
        readonly Button[] stageButtons;
        readonly Text[] stageTexts;

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
            var head = Ui.Label(s, "스테이지 선택", 40, Ui.Gold, TextAnchor.MiddleCenter, "Head");
            head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 56));
            var grid = Ui.Rect("Grid", s);
            grid.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 420));
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
            var back = Ui.MakeButton(s, "뒤로 (Esc)", 18, () => BackRequested?.Invoke(), out _, "Back");
            ((RectTransform)back.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 40), new Vector2(180, 44));
            var set = Ui.MakeButton(s, "설정", 18, () => SettingsRequested?.Invoke(), out _, "Settings");
            ((RectTransform)set.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-110, 40), new Vector2(180, 44));
            panel.SetActive(false);
        }

        /// Shows the grid with this progress (read only) and selects the last played stage.
        public void Show(ProgressData progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            panel.SetActive(true);
            int last = progress.LastStageIndex(levels);
            for (int k = 0; k < levels.Length; k++)
            {
                bool done = progress.IsCleared(levels[k]);
                int b = progress.BestBodies(levels[k]);
                string best = b > 0 ? $"  · 몸 {b}" : "";
                stageTexts[k].text = $"{k + 1}. {levels[k].Name}\n<size=14>{(done ? "클리어" + best : "")}</size>";
                stageTexts[k].color = done ? Ui.Gold : Ui.Ink;
                if (k == last) stageButtons[k].Select();
            }
        }

        public void Hide() => panel.SetActive(false);
    }
}
