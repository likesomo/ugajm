using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// In-game menu (view only; design/art/ui-minimal.md "메뉴 모달": x447 w386, centered on the height (concept y100), full-screen dim 0.74 over the game).
    /// Settings, stages, show solution, controls, the start line again (only when the controller says so) and close, one
    /// button per row in the body font; the frame fits the rows shown. Raises events;
    /// <see cref="MenuController"/> decides. An overlay: Esc closes it.
    /// </summary>
    public sealed class MenuPanel : IMenuPanel
    {
        public const float X = 447, Width = 386;
        const float Pad = 28, RowHeight = 72, Gap = 12, TitleHeight = 56;

        readonly GameObject panel;
        readonly Text title;
        readonly Button first;
        readonly (Text label, string key)[] rows;
        readonly Button[] buttons;
        readonly int replayRow;

        public event Action SettingsRequested, StagesRequested, SolutionRequested, ControlsRequested, ReplayLineRequested, CloseRequested;

        /// The menu's frame (UiLayoutCheck).
        public RectTransform Frame { get; }

        public MenuPanel(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("GameMenu", root).Stretch().gameObject; // not "Menu": the title screen has one
            Ui.Wash(panel.transform, 0.74f, "Blocker").raycastTarget = true;
            var items = new (string key, Action click)[]
            {
                ("menu.settings", () => SettingsRequested?.Invoke()),
                ("menu.stages", () => StagesRequested?.Invoke()),
                ("menu.solution", () => SolutionRequested?.Invoke()),
                ("menu.controls", () => ControlsRequested?.Invoke()),
                ("menu.replay_line", () => ReplayLineRequested?.Invoke()),
                ("menu.close", () => CloseRequested?.Invoke()),
            };
            replayRow = 4;
            Frame = Ui.Panel(panel.transform, "Frame").rectTransform; // placed by Layout
            title = Ui.Label(Frame, "", 32, Ui.Ink, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.TopLeft(Pad, Pad, Width - 2 * Pad, TitleHeight);
            rows = new (Text, string)[items.Length];
            buttons = new Button[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var b = Ui.MenuButton(Frame, 24, items[i].click, out var label, items[i].key);
                buttons[i] = b;
                Ui.FitButtonSkin(b);
                label.resizeTextForBestFit = false;
                rows[i] = (label, items[i].key);
                if (i == 0) first = b;
            }
            Layout();
            panel.SetActive(false);
        }

        /// The start line row (story-integration.md 0.2): shown when the stage has a start line and no result is up.
        public void ShowReplayLine(bool shown)
        {
            buttons[replayRow].gameObject.SetActive(shown);
            Layout();
        }

        /// The row is showing (UiLayoutCheck, tests).
        public bool ReplayLineShown => buttons[replayRow].gameObject.activeSelf;

        void Layout()
        {
            float y = Pad + TitleHeight + Gap;
            foreach (var b in buttons)
            {
                if (!b.gameObject.activeSelf) continue;
                ((RectTransform)b.transform).TopLeft(Pad, y, Width - 2 * Pad, RowHeight);
                y += RowHeight + Gap;
            }
            // Centered on the height (six rows reach past y 100 + the 24 safe margin at 720); x as the concept (447).
            Frame.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(X, 0), new Vector2(Width, y - Gap + Pad));
        }

        public void Apply(Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            UiText.Set(title, loc, "menu.title", FontRole.Title);
            foreach (var (label, key) in rows) UiText.Set(label, loc, key, FontRole.Body);
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            first.Select();
        }

        public void Hide() => panel.SetActive(false);
    }
}
