using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// Tutorial guide (docs/tutorial.md 2.3, U5 look): the cue's one line on a plate at the bottom centre (the HUD message line's place; it is muted on a tutorial board) (Gaegu
    /// body, wraps to two lines inside <see cref="MaxWidth"/>), and a skip icon (48, round backing) at the top right under the HUD's restart icon
    /// whose name is on the canvas's one <see cref="FocusPlate"/>. The cue's cell ring is the board's (BoardController.Highlight).
    /// View only: <see cref="TutorialRunController"/> decides the line. Own canvas between the U5 HUD (5) and the overlays (10).
    /// </summary>
    public sealed class U5TutorialCueView : MonoBehaviour, ITutorialCueView
    {
        public const int SortingOrder = 7;
        public const int LineSize = 26, MaxLines = 2;
        /// Plate bottom edge above the screen bottom (1280 x 720): where the HUD message line was (muted on tutorial boards).
        public const float Bottom = 24, MaxWidth = 760, PadX = 24, PadY = 10, Safe = 24;
        public const string SkipKey = "tutorial.skip";

        Localizer loc;
        RectTransform plate;
        Text text;
        IconButton skip;
        FocusPlate tips;

        public event Action SkipRequested;

        public RectTransform Plate => plate;
        public Text Text => text;
        public IconButton Skip => skip;
        public FocusPlate Tips => tips;
        public bool Visible => gameObject.activeSelf;
        /// The line on screen ("" = the plate is hidden).
        public string Line => plate.gameObject.activeSelf ? text.text : "";

        public static U5TutorialCueView Build(UiIconSet icons, Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject("U5 Tutorial cue", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var v = go.AddComponent<U5TutorialCueView>();
            v.Assemble(icons, loc);
            go.SetActive(false);
            return v;
        }

        void Assemble(UiIconSet icons, Localizer localizer)
        {
            loc = localizer;
            var root = transform;
            plate = Ui.Plate(root, "Cue plate").rectTransform;
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0);
            plate.pivot = new Vector2(0.5f, 0);
            text = Ui.Label(plate, "", LineSize, Ui.Ink, TextAnchor.MiddleCenter, "Cue");
            text.rectTransform.Stretch();
            text.rectTransform.offsetMin = new Vector2(PadX, 0);
            text.rectTransform.offsetMax = new Vector2(-PadX, 0);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            plate.gameObject.SetActive(false);

            skip = IconButton.Build(root, UiIcon.Skip, icons, "Esc", () => SkipRequested?.Invoke(), SkipKey, "Skip");
            // Top right under the HUD's restart icon (24 + 48 + 16): the bottom is the cue plate's.
            skip.Rect.Place(Vector2.one, Vector2.one, new Vector2(-Safe, -(Safe + IconButton.Size + 16)), new Vector2(IconButton.Size, IconButton.Size));
            skip.Backing();
            tips = FocusPlate.Build(root, loc, icons);
            tips.Track(skip);
        }

        public void ShowCue(string line, int board, int boards)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (boards < 1 || board < 0 || board >= boards) throw new ArgumentOutOfRangeException(nameof(board), board, $"0..{boards - 1}");
            bool has = line.Length > 0;
            plate.gameObject.SetActive(has);
            text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            text.text = line;
            if (!has) return;
            float inner = MaxWidth - 2 * PadX;
            int lines = FocusPlate.LineCount(text, line, inner);
            if (lines > MaxLines) Debug.LogError($"[SevenSteps] Tutorial cue \"{line}\" takes {lines} lines > {MaxLines} (shorten it)");
            float w = Mathf.Min(Preferred(text), inner);
            float h = FocusPlate.PlateHeight(text, line, inner, PadY, wrap: true);
            plate.sizeDelta = new Vector2(Mathf.Ceil(w) + 2 * PadX + 2, h);
            plate.anchoredPosition = new Vector2(0, Bottom);
        }

        static float Preferred(Text t)
        {
            var s = t.GetGenerationSettings(Vector2.zero);
            s.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, s) / Mathf.Max(0.01f, t.pixelsPerUnit);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide()
        {
            if (tips != null) tips.Model.Clear();
            gameObject.SetActive(false);
        }
    }
}
