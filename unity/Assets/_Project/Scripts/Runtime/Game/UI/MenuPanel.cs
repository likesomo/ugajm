using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// In-game menu (view only; design/art/ui-minimal.md "메뉴 모달": x447 y100 w386, full-screen dim 0.74 over the game).
    /// Settings, stages, show solution, controls and close, one button per row in the body font. Raises events;
    /// <see cref="MenuController"/> decides. An overlay: Esc closes it.
    /// </summary>
    public sealed class MenuPanel : IMenuPanel
    {
        public const float X = 447, Y = 100, Width = 386;
        const float Pad = 28, RowHeight = 72, Gap = 12, TitleHeight = 56;

        readonly GameObject panel;
        readonly Text title;
        readonly Button first;
        readonly (Text label, string key)[] rows;

        public event Action SettingsRequested, StagesRequested, SolutionRequested, ControlsRequested, CloseRequested;

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
                ("menu.close", () => CloseRequested?.Invoke()),
            };
            float height = Pad + TitleHeight + Gap + items.Length * (RowHeight + Gap) - Gap + Pad;
            Frame = Ui.Panel(panel.transform, "Frame").rectTransform.TopLeft(X, Y, Width, height);
            title = Ui.Label(Frame, "", 32, Ui.Ink, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.TopLeft(Pad, Pad, Width - 2 * Pad, TitleHeight);
            rows = new (Text, string)[items.Length];
            float y = Pad + TitleHeight + Gap;
            for (int i = 0; i < items.Length; i++)
            {
                var b = Ui.MenuButton(Frame, 24, items[i].click, out var label, items[i].key);
                ((RectTransform)b.transform).TopLeft(Pad, y, Width - 2 * Pad, RowHeight);
                Ui.FitButtonSkin(b);
                label.resizeTextForBestFit = false;
                rows[i] = (label, items[i].key);
                if (i == 0) first = b;
                y += RowHeight + Gap;
            }
            panel.SetActive(false);
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
