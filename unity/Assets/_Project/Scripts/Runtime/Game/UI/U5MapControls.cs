using System;
using SevenSteps.Game.UI.U5;
using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// U5 star map and planet surface controls (concept U5-map / U5-surface; docs/ui-u5-kit.md 3: back 48 top left, settings
    /// 48 top right; the tutorial entry beside settings, docs/tutorial.md 2.4). The map and the surface keep their own back chevrons hidden: back here raises
    /// <see cref="BackRequested"/> and the title root presses whichever is showing, so their guards (a dive or a return
    /// under way) stay in one place. View only.
    /// </summary>
    public sealed class U5MapControls
    {
        public const float Safe = 24;

        readonly GameObject panel;
        readonly IconButton back, settings, tutorial;

        public event Action BackRequested, SettingsRequested, TutorialRequested;

        public IconButton Back => back;
        public IconButton Settings => settings;
        /// The tutorial entry (docs/tutorial.md 2.4: optional, replayable, T1..T3); hidden without boards (SetTutorialShown).
        public IconButton Tutorial => tutorial;
        public const float Gap = 16;
        public bool Visible => panel.activeSelf;

        public U5MapControls(Transform root, UiIconSet icons, FocusPlate tips)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (tips == null) throw new ArgumentNullException(nameof(tips));
            panel = Ui.Rect("Map controls U5", root).Stretch().gameObject;
            back = IconButton.Build(panel.transform, UiIcon.Back, icons, "Esc", () => BackRequested?.Invoke(), name: "Back");
            back.Rect.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Safe, -Safe), new Vector2(IconButton.Size, IconButton.Size));
            settings = IconButton.Build(panel.transform, UiIcon.Settings, icons, null, () => SettingsRequested?.Invoke(), name: "Settings");
            settings.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Safe, -Safe), new Vector2(IconButton.Size, IconButton.Size));
            tips.Track(back);
            tips.Track(settings);
            tutorial = IconButton.Build(panel.transform, UiIcon.Tutorial, icons, null, () => TutorialRequested?.Invoke(), name: "Tutorial");
            tutorial.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Safe - IconButton.Size - Gap, -Safe), new Vector2(IconButton.Size, IconButton.Size));
            tips.Track(tutorial);
            IconNav.Row(new[] { back, tutorial, settings });
            panel.SetActive(false);
        }

        /// Shows or hides the tutorial entry (TitleScreen: levels.json has tutorial boards).
        public void SetTutorialShown(bool shown) => tutorial.gameObject.SetActive(shown);

        /// Shown over the map and the surface; hidden while a dive or a return runs, or another panel is open. Idempotent.
        public void SetShown(bool shown)
        {
            if (panel.activeSelf == shown) return;
            panel.SetActive(shown);
            if (shown) panel.transform.SetAsLastSibling();
        }
    }
}
