using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// A U5 short icon row over the board (docs/ui-u5-kit.md 3, 6 "모달"): not a full-screen panel - a light scrim (0.25,
    /// it still catches clicks so the board takes none; the scene root hides the HUDs meanwhile, 14.7), a small caption
    /// plate and one <see cref="IconRow"/> centred near the bottom (y 604), with one focus plate. The pause and settings
    /// rows are built from it. Own canvas under the overlays (order 9): panels opened from it (language, the full settings)
    /// draw over it.
    /// </summary>
    public sealed class U5RowOverlay : MonoBehaviour
    {
        public const float RowTop = 604, CaptionGap = 12, ScrimAlpha = 0.25f;
        public const int SortingOrder = 9;

        IconRow row;
        Image scrim;
        Image captionPlate;
        Text caption;
        FocusPlate plate;
        Localizer loc;
        string captionKey;

        public IconRow Row => row;
        public FocusPlate Plate => plate;
        public Text Caption => caption;
        public Image Scrim => scrim;
        public bool Visible => gameObject.activeSelf;

        /// Raised by a click on the scrim (outside the row): the owner treats it as close.
        public event Action ScrimClicked;

        public static U5RowOverlay Build(string name, string captionKey, UiIconSet icons, Localizer loc)
        {
            if (string.IsNullOrEmpty(captionKey)) throw new ArgumentException("no caption key", nameof(captionKey));
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var o = go.AddComponent<U5RowOverlay>();
            o.loc = loc;
            o.captionKey = captionKey;
            o.scrim = Ui.MakeImage(go.transform, new Color(0.02f, 0.04f, 0.08f, ScrimAlpha), null, "Scrim");
            o.scrim.rectTransform.Stretch();
            o.scrim.raycastTarget = true;
            var click = o.scrim.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.navigation = new Navigation { mode = Navigation.Mode.None };
            click.onClick.AddListener(() => o.ScrimClicked?.Invoke());
            Ui.Exempt(o.scrim.gameObject, UiLayoutRule.Board | UiLayoutRule.Overlap, "U5 row modal scrim (14.7: 0.25 with the HUD hidden)");
            o.row = IconRow.Build(go.transform, new Vector2(0.5f, 0), new Vector2(0, 720 - RowTop - IconButton.Size / 2));
            o.captionPlate = Ui.Plate(go.transform, "Caption plate");
            o.captionPlate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 720 - RowTop + CaptionGap), new Vector2(200, 40));
            o.caption = Ui.Label(o.captionPlate.transform, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Caption");
            o.caption.rectTransform.Stretch();
            o.plate = FocusPlate.Build(go.transform, loc, icons);
            o.plate.Avoid = o.PlateAvoid;
            o.Apply(loc);
            go.SetActive(false);
            return o;
        }

        readonly List<Rect> avoid = new List<Rect>();

        IReadOnlyList<Rect> PlateAvoid()
        {
            avoid.Clear();
            var c = new Vector2(1280, 720);
            var r = captionPlate.rectTransform;
            avoid.Add(new Rect(c.x / 2 - r.sizeDelta.x / 2, c.y - r.anchoredPosition.y - r.sizeDelta.y, r.sizeDelta.x, r.sizeDelta.y));
            return avoid;
        }

        public IconButton Add(UiIcon icon, UiIconSet icons, string shortcut, Action onClick, string tipKey = null)
        {
            var b = row.Add(icon, icons, shortcut, onClick, tipKey);
            plate.Track(b);
            return b;
        }

        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            caption.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            caption.text = loc.Get(captionKey);
            float w = caption.cachedTextGeneratorForLayout.GetPreferredWidth(caption.text, caption.GetGenerationSettings(Vector2.zero))
                      / Mathf.Max(0.01f, caption.pixelsPerUnit);
            captionPlate.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 32, FocusPlate.PlateHeight(caption, caption.text, w, 8));
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = UnityEngine.EventSystems.EventSystem.current;
            var first = row.First;
            if (es != null && first != null) es.SetSelectedGameObject(first.gameObject); // keyboard: Left / Right along the row
        }

        public void Hide()
        {
            plate.Model.Clear();
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// U5 pause (U5-pause): continue, restart, map, hint, settings on one short row (touch-targets.csv U5 pause). The
    /// in-game menu as MenuController sees it (<see cref="IMenuPanel"/>), so settings / stages / solution go through the
    /// same controller as the current menu; restart is the board's (the scene root wires <see cref="RestartRequested"/>).
    /// No controls guide and start-line rows: the U5 row keeps five actions.
    /// </summary>
    public sealed class U5PauseView : IMenuPanel
    {
        readonly U5RowOverlay overlay;

        public event Action SettingsRequested, StagesRequested, SolutionRequested, ControlsRequested, ReplayLineRequested, CloseRequested;
        /// The restart icon: the scene root closes the row and restarts the board.
        public event Action RestartRequested;

        public U5RowOverlay Overlay => overlay;
        public IconButton Continue { get; }
        public IconButton Restart { get; }
        public IconButton Map { get; }
        public IconButton Hint { get; }
        public IconButton Settings { get; }

        public U5PauseView(UiIconSet icons, Localizer loc)
        {
            overlay = U5RowOverlay.Build("U5 Pause", "ui5.pause.title", icons, loc);
            Continue = overlay.Add(UiIcon.Play, icons, "Esc", () => CloseRequested?.Invoke(), "ui5.pause.continue");
            Restart = overlay.Add(UiIcon.Restart, icons, "R", () => RestartRequested?.Invoke());
            Map = overlay.Add(UiIcon.Map, icons, null, () => StagesRequested?.Invoke());
            Hint = overlay.Add(UiIcon.Hint, icons, "H", () => SolutionRequested?.Invoke());
            Settings = overlay.Add(UiIcon.Settings, icons, null, () => SettingsRequested?.Invoke());
            overlay.ScrimClicked += () => CloseRequested?.Invoke();
            _ = ControlsRequested; _ = ReplayLineRequested; // part of IMenuPanel; the U5 row has no such rows
        }

        public void ShowReplayLine(bool shown) { } // no start-line row on the U5 pause (the story plate replays nothing here)
        public void Apply(Localizer loc) => overlay.Apply(loc);
        public void Show() => overlay.Show();
        public void Hide() => overlay.Hide();
        public bool Visible => overlay.Visible;
    }

    /// <summary>
    /// U5 settings (U5-settings): sound on / off, reduce motion, language, more settings (the full current panel: volumes,
    /// play data, step numbers), back - one short row. View only: <see cref="U5SettingsController"/> saves and redraws.
    /// </summary>
    public sealed class U5SettingsView : IOverlay
    {
        readonly U5RowOverlay overlay;
        readonly UiIconSet icons;

        public event Action SoundToggled, MotionToggled, LanguageRequested, MoreRequested, CloseRequested;

        public U5RowOverlay Overlay => overlay;
        public IconButton Sound { get; }
        public IconButton Motion { get; }
        public IconButton Language { get; }
        public IconButton More { get; }
        public IconButton Back { get; }

        public U5SettingsView(UiIconSet icons, Localizer loc)
        {
            this.icons = icons;
            overlay = U5RowOverlay.Build("U5 Settings", "ui5.settings.title", icons, loc);
            Sound = overlay.Add(UiIcon.SoundOn, icons, null, () => SoundToggled?.Invoke());
            Motion = overlay.Add(UiIcon.Motion, icons, null, () => MotionToggled?.Invoke(), "ui5.settings.motion_off");
            Language = overlay.Add(UiIcon.Language, icons, null, () => LanguageRequested?.Invoke());
            More = overlay.Add(UiIcon.Info, icons, null, () => MoreRequested?.Invoke(), "ui5.settings.more");
            Back = overlay.Add(UiIcon.Back, icons, "Esc", () => CloseRequested?.Invoke());
            overlay.ScrimClicked += () => CloseRequested?.Invoke();
        }

        /// Draws the saved state: the sound icon and words (on / off), the motion icon lit while reduce motion is on.
        public void SetState(bool soundOn, bool reduceMotion)
        {
            Sound.SetIcon(soundOn ? UiIcon.SoundOn : UiIcon.SoundOff, icons);
            Motion.Glyph.color = reduceMotion ? Ui.Gold : Ui.Ink;
            Motion.SetTip(reduceMotion ? "ui5.settings.motion_on" : "ui5.settings.motion_off");
        }

        public void Apply(Localizer loc) => overlay.Apply(loc);
        public void Show() => overlay.Show();
        public void Hide() => overlay.Hide();
        public bool Visible => overlay.Visible;
    }
}
