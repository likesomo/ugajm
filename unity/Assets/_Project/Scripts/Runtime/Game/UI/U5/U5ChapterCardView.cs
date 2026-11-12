using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// U5 chapter card (UiStyleToggle on; lead 2026-11-12): entering a planet from the board ([ ], next across a chapter)
    /// shows only the planet name on a plate, its one short line under it (the D2b surface's plain words: no "Chapter 01",
    /// no row of planets), and a continue icon (48, round backing) whose name is on the canvas's one
    /// <see cref="FocusPlate"/>. The board stays dimmed behind it. Closes after <see cref="Seconds"/> (unscaled), on a
    /// click anywhere or the icon, by Esc (BackNavigator) or a board key (scene root). View only:
    /// <see cref="ChapterIntroController"/> decides when. -ui4 keeps <see cref="ChapterEntryView"/>.
    /// Code-built on its own canvas (one FocusPlate per canvas) so UiLayoutCheck can build it in Edit Mode.
    /// </summary>
    public sealed class U5ChapterCardView : MonoBehaviour, IChapterCard
    {
        public const float Seconds = 2.5f;
        /// Under the overlays (10: menu, settings) so one opened on top covers it; over the U5 HUD (5) and clear (6).
        public const int SortingOrder = 8;
        public const int NameSize = 56, LineSize = 28;
        /// Name plate centre and line plate top (1280 x 720 reference, top-left origin); the line wraps inside LineWidth.
        public const float NameCentreY = 300, LineGap = 16, LineWidth = 880, IconTop = 560, PadX = 24, PadY = 8;
        public const string ContinueKey = "ui5.pause.continue"; // the pause row's "Continue" (KO/EN)

        Localizer loc;
        Text planet, line;
        RectTransform namePlate, linePlate;
        IconButton next;
        FocusPlate tips;
        ChapterCardModel shown;
        float shownAt;

        public event Action CloseRequested;

        public Text Planet => planet;
        public Text Line => line;
        public RectTransform NamePlate => namePlate;
        public RectTransform LinePlate => linePlate;
        public IconButton Continue => next;
        public FocusPlate Tips => tips;
        public bool Visible => gameObject.activeSelf;

        /// Builds the card on its own overlay canvas, hidden. <paramref name="icons"/> may be null (line placeholders).
        public static U5ChapterCardView Build(UiIconSet icons, Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject("U5 Chapter card", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var v = go.AddComponent<U5ChapterCardView>();
            v.Assemble(icons, loc);
            go.SetActive(false);
            return v;
        }

        void Assemble(UiIconSet icons, Localizer localizer)
        {
            loc = localizer;
            var root = transform;
            var scrim = Ui.Wash(root, 0.72f, "Scrim"); // the board dimmed behind; a click anywhere continues
            scrim.raycastTarget = true;
            scrim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseRequested?.Invoke());

            namePlate = Ui.Plate(root, "Name plate").rectTransform;
            namePlate.anchorMin = namePlate.anchorMax = new Vector2(0.5f, 1);
            namePlate.pivot = new Vector2(0.5f, 0.5f);
            planet = Ui.Label(namePlate, "", NameSize, Ui.Gold, TextAnchor.MiddleCenter, "Planet");
            planet.rectTransform.Stretch();

            linePlate = Ui.Plate(root, "Line plate").rectTransform;
            linePlate.anchorMin = linePlate.anchorMax = new Vector2(0.5f, 1);
            linePlate.pivot = new Vector2(0.5f, 1);
            line = Ui.Label(linePlate, "", LineSize, Ui.Ink, TextAnchor.MiddleCenter, "Line");
            line.rectTransform.Stretch();
            line.rectTransform.offsetMin = new Vector2(PadX, 0);
            line.rectTransform.offsetMax = new Vector2(-PadX, 0);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;

            next = IconButton.Build(root, UiIcon.Play, icons, null, () => CloseRequested?.Invoke(), ContinueKey, "Continue");
            next.Rect.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -IconTop), new Vector2(IconButton.Size, IconButton.Size));
            next.Backing(); // 3:1 over the dimmed board (14.3), as the U5 clear's and ending's icons
            tips = FocusPlate.Build(root, loc, icons);
            tips.Track(next);
            loc.Changed += OnLanguage;
        }

        void OnDestroy()
        {
            if (loc != null) loc.Changed -= OnLanguage;
        }

        void OnLanguage(Language _)
        {
            if (shown != null) Draw(shown);
        }

        /// The model's words are in the language it was made for (LevelCatalog.ChapterCard); fonts follow the localizer.
        public void Bind(ChapterCardModel model)
        {
            shown = model ?? throw new ArgumentNullException(nameof(model));
            Draw(model);
        }

        void Draw(ChapterCardModel model)
        {
            var lang = loc.Current;
            planet.font = Ui.Theme.FontFor(lang, FontRole.Title);
            line.font = Ui.Theme.FontFor(lang, FontRole.Body);
            planet.text = model.Planet;
            float w = Preferred(planet);
            float h = FocusPlate.PlateHeight(planet, planet.text, w, PadY);
            namePlate.sizeDelta = new Vector2(Mathf.Ceil(w) + 2 * PadX + 16, h);
            namePlate.anchoredPosition = new Vector2(0, -NameCentreY);

            line.text = model.Intro;
            bool hasLine = model.Intro.Length > 0;
            linePlate.gameObject.SetActive(hasLine);
            if (!hasLine) return;
            float inner = Mathf.Min(Preferred(line), LineWidth - 2 * PadX);
            int lines = FocusPlate.LineCount(line, line.text, LineWidth - 2 * PadX);
            if (lines > 2) Debug.LogError($"[SevenSteps] U5 chapter card: line \"{line.text}\" takes {lines} lines > 2 (shorten it)");
            float lh = FocusPlate.PlateHeight(line, line.text, LineWidth - 2 * PadX, PadY, wrap: true);
            linePlate.sizeDelta = new Vector2(Mathf.Ceil(inner) + 2 * PadX + 2, lh);
            linePlate.anchoredPosition = new Vector2(0, -(NameCentreY + h / 2 + LineGap));
        }

        static float Preferred(Text t)
        {
            var s = t.GetGenerationSettings(Vector2.zero);
            s.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, s) / Mathf.Max(0.01f, t.pixelsPerUnit);
        }

        public void Show()
        {
            if (shown == null) throw new InvalidOperationException("U5ChapterCardView.Show before Bind");
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
        }

        public void Hide()
        {
            if (tips != null) tips.Model.Clear();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (Time.unscaledTime - shownAt >= Seconds) CloseRequested?.Invoke();
        }
    }
}
