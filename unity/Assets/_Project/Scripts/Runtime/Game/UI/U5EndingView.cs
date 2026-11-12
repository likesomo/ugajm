using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI.U5;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// U5 ending cards (concept U5-ending; docs/ui-u5-kit.md 3; UiStyleToggle on): the same cards as <see cref="EndingView"/>
    /// (the card's picture, one line on a plate in the bottom band, a click on the card is next) with icon controls instead
    /// of text buttons: next 48 under the line, skip (close) 48 top right, and on the last card the map 48 under the line
    /// (to the map) and no skip. Names on the canvas's one <see cref="FocusPlate"/>. <see cref="EndingController"/> decides.
    /// Code-built so UiLayoutCheck can build it in Edit Mode.
    /// </summary>
    public sealed class U5EndingView : IEndingView
    {
        /// The line plate sits higher than EndingView's (516) so the explanation plate over the icons fits between them.
        /// The ending strings carry their key ("다음 (Enter)"), so the icons add no shortcut.
        public const float PlateY = 468, PlateWidth = 1040, PlateHeight = 96, IconTop = 630, Safe = 24;

        readonly GameObject panel;
        readonly Image art;
        readonly Text line;
        readonly IconButton next, map, skip;
        readonly FocusPlate tips;
        readonly StoryImageSet images;
        Localizer loc;
        int index = -1, count;
        EndingPage page;

        public event Action NextRequested, SkipRequested;
        /// Never raised: Esc goes through the BackNavigator, the skip icon through SkipRequested.
        public event Action CloseRequested { add { } remove { } }

        public IconButton Next => next;
        public IconButton Map => map;
        public IconButton Skip => skip;
        /// The plate with the line (UiLayoutCheck).
        public RectTransform Plate { get; }
        public string Text => line.text;
        public bool Visible => panel.activeSelf;

        /// <param name="images">GameAssets.storyImages; null = text only.</param>
        public U5EndingView(Transform root, StoryImageSet images, Localizer loc, UiIconSet icons)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            this.images = images;
            panel = Ui.Rect("Ending U5", root).Stretch().gameObject;
            if (Ui.MenuBackground(panel.transform) == null) Ui.Wash(panel.transform, 1f, "Background");
            var scrim = Ui.Wash(panel.transform, 0.6f, "Scrim"); // modal (14.7); the whole card is next
            scrim.raycastTarget = true;
            scrim.gameObject.AddComponent<Button>().onClick.AddListener(() => NextRequested?.Invoke());
            art = Ui.MakeImage(panel.transform, Color.white, null, "Art");
            art.rectTransform.Stretch();
            art.preserveAspect = true;
            art.raycastTarget = false;

            var plate = Ui.Plate(panel.transform, "LinePlate");
            Plate = plate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 720 - PlateY - PlateHeight), new Vector2(PlateWidth, PlateHeight));
            line = Ui.Label(plate.transform, "", 34, Ui.Ink, TextAnchor.MiddleCenter, "Line");
            line.rectTransform.Stretch();
            line.rectTransform.offsetMin = new Vector2(24, 8);
            line.rectTransform.offsetMax = new Vector2(-24, -8);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;

            var bottom = new Vector2(0, 720 - IconTop - IconButton.Size);
            next = IconButton.Build(panel.transform, UiIcon.Next, icons, null, () => NextRequested?.Invoke(), "ending.next", "Next");
            next.Rect.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), bottom, new Vector2(IconButton.Size, IconButton.Size));
            map = IconButton.Build(panel.transform, UiIcon.Map, icons, null, () => NextRequested?.Invoke(), "ending.to_map", "Map");
            map.Rect.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), bottom, new Vector2(IconButton.Size, IconButton.Size));
            skip = IconButton.Build(panel.transform, UiIcon.Close, icons, null, () => SkipRequested?.Invoke(), "ending.skip", "Skip");
            skip.Rect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Safe, -Safe), new Vector2(IconButton.Size, IconButton.Size));
            tips = FocusPlate.Build(panel.transform, loc, icons);
            foreach (var b in new[] { next, map, skip }) tips.Track(b);
            panel.SetActive(false);
        }

        public void ShowPage(int index, int count, EndingPage page)
        {
            if (count < 1 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index), index, $"0..{count - 1}");
            this.index = index;
            this.count = count;
            this.page = page ?? throw new ArgumentNullException(nameof(page));
            var sprite = images != null ? images.Get(page.ImageKey) : null;
            art.sprite = sprite;
            art.enabled = sprite != null;
            bool last = index == count - 1;
            next.SetShown(!last);
            map.SetShown(last);
            skip.SetShown(!last);
            tips.Model.Clear();
            Redraw();
        }

        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            if (index >= 0) Redraw();
        }

        void Redraw()
        {
            line.text = page.Line;
            line.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            // One keyboard path (as EndingView, review R1): Enter / Space / Right are GameController's.
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        public void Hide() => panel.SetActive(false);
    }
}
