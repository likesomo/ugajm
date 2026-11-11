using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The ending cards (view only, story-integration.md 6 "엔딩"): the card's picture over the whole screen (StoryImageSet;
    /// none yet = the menu background), one line on a plate in the bottom 30% (the picture leaves it free), [skip (Esc)]
    /// top right and [next (Enter)] / [to the map (Enter)] bottom right. A click anywhere else on the card is next.
    /// <see cref="EndingController"/> decides. Code-built so UiLayoutCheck can build it in Edit Mode.
    /// </summary>
    public sealed class EndingView : IEndingView
    {
        public const float PlateX = 120, PlateY = 516, PlateWidth = 1040, PlateHeight = 96;
        const float Safe = 24, ButtonHeight = 72, NextWidth = 300, SkipWidth = 240; // 72: the label + 8 inside the button border (14.4)

        readonly GameObject panel;
        readonly Image art;
        readonly Text line, next, skip;
        readonly Button nextButton;
        readonly StoryImageSet images;
        Localizer loc;
        int index = -1, count;
        EndingPage page;

        public event Action NextRequested, SkipRequested;
        /// Never raised: Esc goes through the BackNavigator, the skip button through SkipRequested.
        public event Action CloseRequested { add { } remove { } }

        /// The plate with the line (UiLayoutCheck).
        public RectTransform Plate { get; }
        /// The line on screen (flow check).
        public string Text => line.text;
        /// The card's picture is shown (a sprite for its key).
        public bool HasArt => art.enabled;

        /// <param name="images">GameAssets.storyImages; null = text only.</param>
        public EndingView(Transform root, StoryImageSet images, Localizer loc)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            this.images = images;
            panel = Ui.Rect("Ending", root).Stretch().gameObject;
            if (Ui.MenuBackground(panel.transform) == null) Ui.Wash(panel.transform, 1f, "Background");
            // Modal scrim (14.7) over the game and the menu background; the whole card is the next button. The picture is
            // drawn above it without taking clicks, the plate and the two buttons on top.
            var scrim = Ui.Wash(panel.transform, 0.6f, "Scrim");
            scrim.raycastTarget = true;
            scrim.gameObject.AddComponent<Button>().onClick.AddListener(() => NextRequested?.Invoke());
            art = Ui.MakeImage(panel.transform, Color.white, null, "Art");
            art.rectTransform.Stretch();
            art.preserveAspect = true;
            art.raycastTarget = false;

            var plate = Ui.Plate(panel.transform, "LinePlate");
            // Anchored to the screen's bottom / corners so other aspects (1280x800) keep the 24 safe margin.
            Plate = plate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 720 - PlateY - PlateHeight), new Vector2(PlateWidth, PlateHeight));
            line = Ui.Label(plate.transform, "", 34, Ui.Ink, TextAnchor.MiddleCenter, "Line");
            line.rectTransform.Stretch();
            line.rectTransform.offsetMin = new Vector2(24, 8);
            line.rectTransform.offsetMax = new Vector2(-24, -8);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;

            nextButton = Ui.MenuButton(panel.transform, 26, () => NextRequested?.Invoke(), out next, "Next");
            ((RectTransform)nextButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-Safe, Safe), new Vector2(NextWidth, ButtonHeight));
            Ui.FitButtonSkin(nextButton);
            next.resizeTextForBestFit = false;
            var skipButton = Ui.MenuButton(panel.transform, 22, () => SkipRequested?.Invoke(), out skip, "Skip");
            ((RectTransform)skipButton.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Safe, -Safe), new Vector2(SkipWidth, ButtonHeight));
            Ui.FitButtonSkin(skipButton);
            skip.resizeTextForBestFit = false;
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
            Redraw();
        }

        /// Language change (the buttons; the card's line was resolved when the ending began).
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            if (index >= 0) Redraw();
        }

        void Redraw()
        {
            line.text = page.Line;
            line.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            UiText.Set(next, loc, index == count - 1 ? "ending.to_map" : "ending.next", FontRole.Body);
            UiText.Set(skip, loc, "ending.skip", FontRole.Body);
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            // One keyboard path (review R1): Enter / Space / Right are GameController's, so no button stays selected to Submit too.
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        public void Hide() => panel.SetActive(false);
    }
}
