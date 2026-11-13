using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// Storybook cutscene viewer (design/story/cutscenes.md 2; user request 2026-11-12 evening B), U5 look: the page's
    /// picture fills the screen (envelope, 16:9 art), the lines come out one by one on a text plate in the bottom quarter
    /// (Gaegu body font, wrapped) with the page's rule caption under them once the last is out (title font, gold), a next icon in the plate's corner and a skip icon (48, round backing) top right, their
    /// names on the canvas's one <see cref="FocusPlate"/>. A page without a sprite (art not in yet) shows the plain card:
    /// the night colour and a lighter card where the picture goes, never an error (scene-validate warns).
    /// Input: a tap / click anywhere or the next icon, Space, Enter, Right = next; the skip icon = skip; Esc reaches the
    /// controller through the scene's BackNavigator (overlay pop). View only: <see cref="CutsceneController"/> decides.
    /// Own overlay canvas over everything (one FocusPlate per canvas); code-built so UiLayoutCheck can build it in Edit Mode.
    /// </summary>
    public sealed class U5CutsceneView : MonoBehaviour, ICutsceneView
    {
        public const int SortingOrder = 30; // over the U5 HUD (5), clear (6), chapter card (8) and the overlays (10)
        public const int LineSize = 26;
        /// The text plate: the bottom quarter of the 1280 x 720 reference, inset; rows of text it holds at LineSize.
        public const float Inset = 20, BoxHeight = 156, PadX = 28, PadY = 12, NextRoom = 64, MaxRows = 4;
        /// Text width inside the plate (the next icon keeps its corner; the same room on the left keeps the lines centred).
        public const float TextWidth = 1280 - 2 * Inset - 2 * PadX - 2 * NextRoom;
        /// The rule caption (cutscenes v2, user request 2026-11-13): under the lines in the plain UI voice - the title font,
        /// smaller, gold (3:1+ on the 0.9 plate, 14.3) - up to <see cref="RuleRows"/> rows. A page with a rule has a taller
        /// plate from its first line (<see cref="RuleRoom"/>) so nothing moves when the caption comes out with the last line.
        public const int RuleSize = 22, RuleRows = 2;
        public const float RuleRowHeight = 28, RuleGap = 10, RuleRoom = RuleGap + RuleRows * RuleRowHeight;
        /// The plain card's height under a plate without a rule caption (a rule takes RuleRoom off it).
        public const float CardHeight = 470;
        public const string NextKey = "cutscene.next";
        public static readonly Color NightColor = new Color(0.09f, 0.1f, 0.17f, 1f);
        public static readonly Color CardColor = new Color(0.16f, 0.18f, 0.27f, 1f);

        StoryImageSet images;
        Localizer loc;
        Image picture, card;
        Image box;
        Text text, rule;
        Image ruleLine;
        IconButton next, skip;
        FocusPlate tips;
        CutsceneFrame frame;
        int shownFrame = -1;

        public event Action NextRequested, SkipRequested;
        /// The stack closes the view; the view has no close button of its own (skip is a request to the controller).
        public event Action CloseRequested { add { } remove { } }

        public Image Picture => picture;
        /// The plain card (shown when the page has no sprite).
        public Image Card => card;
        public Image Box => box;
        public Text Text => text;
        /// The lines out so far, one per row (the Text also holds the clear rest of the page).
        public string ShownText { get; private set; } = "";
        /// The rule caption's text (laid out for a page with a rule) and whether it is out.
        public Text Rule => rule;
        public bool RuleVisible => rule.gameObject.activeSelf;
        public IconButton Next => next;
        public IconButton Skip => skip;
        public FocusPlate Tips => tips;
        public CutsceneFrame Frame => frame;
        public bool Visible => gameObject.activeSelf;
        /// The page has its picture (else the plain card).
        public bool HasPicture => picture.gameObject.activeSelf;

        /// Builds the viewer on its own overlay canvas, hidden. <paramref name="images"/> and <paramref name="icons"/> may be
        /// null (plain cards; line placeholder icons).
        public static U5CutsceneView Build(StoryImageSet images, UiIconSet icons, Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject("U5 Cutscene", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var v = go.AddComponent<U5CutsceneView>();
            v.Assemble(images, icons, loc);
            go.SetActive(false);
            return v;
        }

        void Assemble(StoryImageSet set, UiIconSet icons, Localizer localizer)
        {
            images = set;
            loc = localizer;
            var root = transform;
            // The whole screen is the next button (a tap anywhere turns the page); the skip and next icons sit over it.
            var night = Ui.MakeImage(root, NightColor, null, "Night");
            night.rectTransform.Stretch();
            night.raycastTarget = true;
            night.gameObject.AddComponent<Button>().onClick.AddListener(() => NextRequested?.Invoke());

            card = Ui.MakeImage(root, CardColor, null, "Plain card");
            card.raycastTarget = false;
            card.rectTransform.TopLeft(120, 40, 1040, CardHeight); // where the picture's busy part goes, above the text plate

            picture = Ui.MakeImage(root, Color.white, null, "Picture");
            picture.raycastTarget = false;
            var fit = picture.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;

            box = Ui.Plate(root, "Text plate");
            var br = box.rectTransform;
            br.anchorMin = new Vector2(0, 0);
            br.anchorMax = new Vector2(1, 0);
            br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(Inset, Inset);
            br.offsetMax = new Vector2(-Inset, Inset + BoxHeight);
            text = Ui.Label(box.transform, "", LineSize, Ui.Ink, TextAnchor.MiddleCenter, "Lines");
            text.rectTransform.Stretch();
            text.rectTransform.offsetMin = new Vector2(PadX + NextRoom, PadY);
            text.rectTransform.offsetMax = new Vector2(-PadX - NextRoom, -PadY);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            ruleLine = Ui.MakeImage(box.transform, Ui.Gold, null, "Rule line"); // full gold: 3:1 on the plate (UiLayout line contrast)
            ruleLine.raycastTarget = false;
            rule = Ui.Label(box.transform, "", RuleSize, Ui.Gold, TextAnchor.MiddleCenter, "Rule");
            foreach (var r in new[] { ruleLine.rectTransform, rule.rectTransform })
            {
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(0.5f, 0);
            }
            rule.rectTransform.offsetMin = new Vector2(PadX + NextRoom, PadY);
            rule.rectTransform.offsetMax = new Vector2(-PadX - NextRoom, PadY + RuleRows * RuleRowHeight);
            ruleLine.rectTransform.offsetMin = new Vector2(PadX + NextRoom + 120, PadY + RuleRoom - RuleGap / 2 - 1);
            ruleLine.rectTransform.offsetMax = new Vector2(-PadX - NextRoom - 120, PadY + RuleRoom - RuleGap / 2 + 1);
            rule.horizontalOverflow = HorizontalWrapMode.Wrap;
            rule.verticalOverflow = VerticalWrapMode.Overflow;
            rule.gameObject.SetActive(false);
            ruleLine.gameObject.SetActive(false);

            next = IconButton.Build(box.transform, UiIcon.Next, icons, null, () => NextRequested?.Invoke(), NextKey, "Next");
            next.Rect.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 12), new Vector2(IconButton.Size, IconButton.Size));
            skip = IconButton.Build(root, UiIcon.Skip, icons, "Esc", () => SkipRequested?.Invoke(), name: "Skip");
            skip.Rect.Place(Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(IconButton.Size, IconButton.Size));
            skip.Backing(); // 3:1 over any picture (14.3)
            tips = FocusPlate.Build(root, loc, icons);
            tips.Track(next);
            tips.Track(skip);
            loc.Changed += OnLanguage;
        }

        void OnDestroy()
        {
            if (loc != null) loc.Changed -= OnLanguage;
        }

        void OnLanguage(Language _)
        {
            if (frame != null) Draw(frame); // the controller redraws with the new words (CutsceneController.Redraw); fonts here
        }

        public void ShowFrame(CutsceneFrame f)
        {
            frame = f ?? throw new ArgumentNullException(nameof(f));
            Draw(f);
        }

        void Draw(CutsceneFrame f)
        {
            var sprite = images != null ? images.Get(f.ImageKey) : null;
            picture.sprite = sprite;
            picture.gameObject.SetActive(sprite != null);
            card.gameObject.SetActive(sprite == null);
            text.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            int rows = Rows(text, f.Lines);
            if (rows > MaxRows) Debug.LogError($"[SevenSteps] Cutscene {f.SceneId} page {f.Page + 1}: {rows} rows > {MaxRows} in the text plate (shorten the lines)");
            // The whole page is laid out from the first line (centred, so no row moves when the next one comes out); lines not
            // out yet are drawn clear.
            var page = new string[f.Lines.Count];
            for (int i = 0; i < page.Length; i++) page[i] = i < f.Shown ? f.Lines[i] : $"<color=#00000000>{f.Lines[i]}</color>";
            text.supportRichText = true;
            text.text = string.Join("\n", page);
            ShownText = string.Join("\n", System.Linq.Enumerable.Take(f.Lines, f.Shown));

            // The rule caption: its room is there from the page's first line; the words come out with the last line.
            bool hasRule = f.Rule.Length > 0;
            float room = hasRule ? RuleRoom : 0;
            box.rectTransform.offsetMax = new Vector2(-Inset, Inset + BoxHeight + room);
            card.rectTransform.TopLeft(120, 40, 1040, CardHeight - room); // the plain card stays clear of the taller plate
            text.rectTransform.offsetMin = new Vector2(PadX + NextRoom, PadY + room);
            rule.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            rule.text = f.Rule;
            if (hasRule)
            {
                int ruleRows = FocusPlate.LineCount(rule, f.Rule, TextWidth);
                if (ruleRows > RuleRows) Debug.LogError($"[SevenSteps] Cutscene {f.SceneId} page {f.Page + 1}: rule caption takes {ruleRows} rows > {RuleRows} (shorten it)");
            }
            rule.gameObject.SetActive(f.RuleShown);
            ruleLine.gameObject.SetActive(f.RuleShown);
        }

        /// Rows the page's lines take in the text plate (each line starts a row; long ones wrap at <see cref="TextWidth"/>).
        public static int Rows(Text t, IReadOnlyList<string> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            int rows = 0;
            foreach (var l in lines) rows += Math.Max(1, FocusPlate.LineCount(t, l, TextWidth));
            return rows;
        }

        public void Show()
        {
            if (frame == null) throw new InvalidOperationException("U5CutsceneView.Show before ShowFrame");
            gameObject.SetActive(true);
            shownFrame = Time.frameCount; // the key that opened it (Enter on the play stone) does not turn the page too
        }

        public void Hide()
        {
            if (tips != null) tips.Model.Clear();
            frame = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Time.frameCount == shownFrame) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                NextRequested?.Invoke();
        }
    }
}
