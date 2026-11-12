using System;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI.U5;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// U5 title (docs/ui-u5-kit.md 3, concept U5-title.png; UiStyleToggle on): the name carved on a stone above the title
    /// planet (TitleWorld draws the planet and the brothers), one 64 play stone with its caption (continue / first step),
    /// and four 48 corner icons: map top left, settings top right, language bottom right, credits bottom left. The story's
    /// title sentence, when there is one, sits under the stone. No menu panel. Names of the icons show on the canvas's one
    /// <see cref="FocusPlate"/>. View only: it raises the requests, the title root decides (docs/ui-u5-kit.md 7).
    /// Positions are 1280x720 canvas units, top-left origin (touch-targets.csv U5 rows).
    /// </summary>
    public sealed class U5TitleView
    {
        public const float PlaySize = 64, Safe = 24;
        /// Play stone centre and the corner icons' top-left corners (touch-targets.csv, U5 title).
        public static readonly Vector2 PlayCenter = new Vector2(640, 535);
        public static readonly Rect StoneRect = new Rect(480, 120, 320, 88);

        readonly GameObject panel;
        readonly Text name, tagline, caption;
        readonly Image tagPlate, captionPlate;
        readonly IconButton play, map, settings, language, credits;
        Localizer loc;
        bool canContinue;

        public event Action PlayRequested, MapRequested, SettingsRequested, LanguageRequested, CreditsRequested;

        public IconButton Play => play;
        /// The button the first arrow / Enter selects (TitleScreen.Update): the play stone.
        public IconButton DefaultButton => play;
        public IconButton Map => map;
        public IconButton Settings => settings;
        public IconButton Language => language;
        public IconButton Credits => credits;
        public bool Visible => panel.activeSelf;
        /// The caption under the play stone now (tests).
        public string Caption => caption.text;

        public U5TitleView(Transform root, UiIconSet icons, FocusPlate tips)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (tips == null) throw new ArgumentNullException(nameof(tips));
            panel = Ui.Rect("Title U5", root).Stretch().gameObject;
            var t = panel.transform;

            var stone = Ui.MakeImage(t, StoneColor, StoneSprite.Get(), "Name stone");
            stone.raycastTarget = false;
            stone.rectTransform.TopLeft(StoneRect.x, StoneRect.y, StoneRect.width, StoneRect.height);
            name = Ui.Label(stone.transform, "", 46, Ui.Gold, TextAnchor.MiddleCenter, "Name");
            name.rectTransform.Stretch();
            name.rectTransform.offsetMin = new Vector2(24, 10);
            name.rectTransform.offsetMax = new Vector2(-24, -10);

            tagPlate = Ui.Plate(t, "Tagline plate");
            tagline = Ui.Label(t, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Tagline");

            play = IconButton.Build(t, UiIcon.Play, icons, null, () => PlayRequested?.Invoke(), name: "Play");
            play.Rect.sizeDelta = new Vector2(PlaySize, PlaySize);
            play.Rect.TopLeft(PlayCenter.x - PlaySize / 2, PlayCenter.y - PlaySize / 2, PlaySize, PlaySize);
            var playStone = Ui.MakeImage(play.transform, PlayStoneColor, StoneSprite.Get(), "Play stone");
            playStone.raycastTarget = false;
            playStone.rectTransform.Stretch();
            playStone.transform.SetAsFirstSibling(); // under the glyph
            play.Glyph.color = Ui.Gold;
            captionPlate = Ui.Plate(t, "Caption plate");
            caption = Ui.Label(t, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Caption");

            map = Corner(t, UiIcon.Map, new Vector2(0, 1), () => MapRequested?.Invoke());
            settings = Corner(t, UiIcon.Settings, new Vector2(1, 1), () => SettingsRequested?.Invoke());
            language = Corner(t, UiIcon.Language, new Vector2(1, 0), () => LanguageRequested?.Invoke());
            credits = Corner(t, UiIcon.Credits, new Vector2(0, 0), () => CreditsRequested?.Invoke());
            foreach (var b in new[] { play, map, settings, language, credits }) tips.Track(b);
            // Keyboard: one row in reading order around the play stone (explicit links, docs/ui-u5-kit.md 4).
            IconNav.Row(new[] { credits, map, play, settings, language });
            panel.SetActive(false);

            // Anchored to its corner, 24 in (14.6 safe area), so 4:3 and 16:10 canvases keep the margin.
            IconButton Corner(Transform parent, UiIcon icon, Vector2 corner, Action click)
            {
                var b = IconButton.Build(parent, icon, icons, null, click);
                var inset = new Vector2(corner.x == 0 ? Safe : -Safe, corner.y == 0 ? Safe : -Safe);
                b.Rect.Place(corner, corner, inset, new Vector2(IconButton.Size, IconButton.Size));
                return b;
            }
        }

        /// Stone face and the play stone (dark enough for gold lettering at 4.5:1, gold glyph at 3:1; 14.3).
        public static readonly Color StoneColor = new Color(0.17f, 0.2f, 0.19f, 1f);
        public static readonly Color PlayStoneColor = new Color(0.24f, 0.25f, 0.19f, 1f);

        /// Text in the current language (call once, then on Localizer.Changed). <paramref name="storyLine"/>: the story's
        /// title sentence (StoryBook.TitleLine); null or empty = none under the stone.
        public void Apply(Localizer localizer, string storyLine = null)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            UiText.Set(name, loc, "title.name", FontRole.Title);
            tagline.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            tagline.text = storyLine ?? "";
            bool hasLine = !string.IsNullOrEmpty(tagline.text);
            tagline.gameObject.SetActive(hasLine);
            tagPlate.gameObject.SetActive(hasLine);
            if (hasLine) PlateAround(tagPlate, tagline, new Vector2(640, StoneRect.yMax + 34), 900);
            caption.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            SetCaption();
        }

        void SetCaption()
        {
            if (loc == null) return;
            caption.text = loc.Get(canContinue ? "ui5.title.continue" : "ui5.title.begin");
            PlateAround(captionPlate, caption, new Vector2(640, PlayCenter.y + PlaySize / 2 + 30), 400);
        }

        /// Shows the title; the play stone continues when there is a last played stage, else begins (the caption says
        /// which). Focus on the play stone (Enter plays).
        public void Show(bool canContinueNow)
        {
            canContinue = canContinueNow;
            SetCaption();
            panel.transform.SetAsFirstSibling(); // under every overlay of the title canvas
            panel.SetActive(true);
            // Nothing selected: keyboard focus would show the explanation at once. The first arrow / Enter selects DefaultButton (TitleScreen).
        }

        public void Hide() => panel.SetActive(false);

        /// Sizes <paramref name="t"/> to its one line and its plate around it (14.4: glyphs + 8 inside the plate).
        public static void PlateAround(Image plate, Text t, Vector2 center, float maxWidth)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            float w = Mathf.Min(maxWidth, t.preferredWidth + 8), h = Mathf.Max(t.fontSize + 6, t.preferredHeight + 4);
            t.rectTransform.TopLeft(center.x - w / 2, center.y - h / 2, w, h);
            plate.rectTransform.TopLeft(center.x - w / 2 - 12, center.y - h / 2 - 9, w + 24, h + 18);
            if (plate.transform.GetSiblingIndex() > t.transform.GetSiblingIndex())
                throw new InvalidOperationException($"{plate.name} must be drawn before {t.name} (14.4)");
        }
    }

    /// <summary>
    /// U5 language picker (concept U5-language.png; UiStyleToggle on): over the title planet, both language names on one
    /// plate (each in its own script, the shown one in gold; tap one to preview it), the language icon (the next
    /// language) and the next icon (keep this one). Same behaviour as <see cref="LanguagePanel"/> through
    /// <see cref="LanguageController"/>: a pick previews, next saves, Esc closes without saving. No dim: the title's own
    /// controls are hidden while an overlay is open (TitleScreen).
    /// </summary>
    public sealed class U5LanguageView : ILanguagePanel
    {
        readonly GameObject panel;
        readonly Image plate;
        readonly Text slash;
        readonly Button[] names;
        readonly Text[] labels;
        readonly IconButton cycle, next;
        readonly FocusPlate tips;
        Language current;

        public event Action<Language> LanguagePicked;
        public event Action ContinueRequested;
        /// The picker has no close button of its own (next saves, Esc closes through the stack).
        public event Action CloseRequested { add { } remove { } }

        public IconButton Cycle => cycle;
        public IconButton Next => next;
        /// The button the first arrow / Enter selects (TitleScreen.Update).
        public IconButton DefaultButton => next;
        public Button NameButton(Language lang) => names[Array.IndexOf(Languages.All, lang)];

        public U5LanguageView(Transform root, UiIconSet icons, FocusPlate tips)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.tips = tips ?? throw new ArgumentNullException(nameof(tips));
            panel = Ui.Rect("Language U5", root).Stretch().gameObject;
            var blocker = panel.AddComponent<Image>(); // takes the clicks meant for the title under it
            blocker.color = new Color(0, 0, 0, 0);
            blocker.raycastTarget = true;
            var t = panel.transform;
            plate = Ui.Plate(t, "Names plate");
            var p = plate.transform; // the names are the plate's children (one box for the overlap rule, 14.5)
            names = new Button[Languages.All.Length];
            labels = new Text[Languages.All.Length];
            for (int i = 0; i < Languages.All.Length; i++)
            {
                var lang = Languages.All[i];
                names[i] = Ui.MakeButton(p, "", 26, () => LanguagePicked?.Invoke(lang), out labels[i], "Lang " + Languages.Code(lang));
                ((Image)names[i].targetGraphic).color = new Color(0, 0, 0, 0);
                names[i].navigation = new Navigation { mode = Navigation.Mode.None };
            }
            slash = Ui.Label(p, "/", 26, Ui.Dim, TextAnchor.MiddleCenter, "Slash");
            cycle = IconButton.Build(t, UiIcon.Language, icons, null, () => LanguagePicked?.Invoke(After(current)), name: "Cycle");
            cycle.Rect.TopLeft(544, 613, IconButton.Size, IconButton.Size);
            next = IconButton.Build(t, UiIcon.Next, icons, "Enter", () => ContinueRequested?.Invoke(), tipKey: "language.continue", name: "Next"); // "keep this language", not the next stage
            next.Rect.TopLeft(688, 613, IconButton.Size, IconButton.Size);
            tips.Track(cycle);
            tips.Track(next);
            IconNav.Row(new[] { cycle, next });
            panel.SetActive(false);
        }

        static Language After(Language lang) => Languages.All[(Array.IndexOf(Languages.All, lang) + 1) % Languages.All.Length];

        /// Names in their own script (13.4), the shown language in gold; the plate fits both.
        public void Apply(Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            current = loc.Current;
            float total = 0, gap = 36;
            for (int i = 0; i < names.Length; i++)
            {
                var lang = Languages.All[i];
                labels[i].text = SettingsPanel.NativeName(lang);
                labels[i].font = Ui.Theme.FontFor(lang, FontRole.Body);
                labels[i].color = lang == current ? Ui.Gold : Ui.Ink;
                labels[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                names[i].interactable = loc.Has(lang);
                total += labels[i].preferredWidth + 8;
            }
            slash.font = Ui.Theme.FontFor(current, FontRole.Number);
            total += gap * (names.Length - 1);
            const float padX = 14, padY = 9, h = 40, top = 496; // clear of the focus plate over the icons (y 613 - 8 - ~41 - 8)
            plate.rectTransform.TopLeft(640 - total / 2 - padX, top, total + 2 * padX, h + 2 * padY);
            float x = padX;
            for (int i = 0; i < names.Length; i++)
            {
                float w = labels[i].preferredWidth + 8;
                ((RectTransform)names[i].transform).TopLeft(x, padY, w, h);
                labels[i].rectTransform.Stretch();
                if (i + 1 < names.Length) slash.rectTransform.TopLeft(x + w + gap / 2 - 8, padY, 16, h);
                x += w + gap;
            }
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            tips.transform.SetAsLastSibling(); // the one explanation plate stays on top
            panel.SetActive(true);
            // Nothing selected: keyboard focus would show the explanation at once. The first arrow / Enter selects DefaultButton (TitleScreen).
        }

        public void Hide() => panel.SetActive(false);
    }

    /// <summary>
    /// U5 credits (concept U5-credits.png; UiStyleToggle on): over the title planet, one plate with the heading and a
    /// scroll of the credit lines and the license texts that ship with the assets (attribution and the OFL must stay
    /// reachable), and one back icon under it. An overlay: back and Esc close it.
    /// </summary>
    public sealed class U5CreditsView : IOverlay
    {
        public static readonly Rect PlateRect = new Rect(240, 108, 800, 440); // ends at 548: the focus plate over the back icon fits above 621

        readonly GameObject panel;
        readonly Text head, body;
        readonly ScrollRect scroll;
        readonly IconButton back;
        readonly FocusPlate tips;
        readonly string creditLines, licenseText;

        public event Action CloseRequested;
        public IconButton Back => back;
        /// The button the first arrow / Enter selects (TitleScreen.Update).
        public IconButton DefaultButton => back;

        public U5CreditsView(Transform root, UiIconSet icons, FocusPlate tips, TextAsset credits, TextAsset[] licenses)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.tips = tips ?? throw new ArgumentNullException(nameof(tips));
            panel = Ui.Rect("Credits U5", root).Stretch().gameObject;
            var blocker = panel.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0);
            blocker.raycastTarget = true;
            var t = panel.transform;
            var plate = Ui.Plate(t, "Credits plate");
            plate.rectTransform.TopLeft(PlateRect.x, PlateRect.y, PlateRect.width, PlateRect.height);
            head = Ui.Label(plate.transform, "", 30, Ui.Gold, TextAnchor.MiddleCenter, "Head");
            head.rectTransform.TopLeft(16, 12, PlateRect.width - 32, 44);

            var view = Ui.Rect("Scroll", plate.transform);
            view.TopLeft(16, 64, PlateRect.width - 32, PlateRect.height - 76);
            var hit = view.gameObject.AddComponent<Image>(); // the wheel and drag land here
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(8, 0);
            content.offsetMax = new Vector2(-8, 0);
            body = Ui.Secondary(content.gameObject.AddComponent<Text>()); // 14.2 secondary: credit lines and license texts
            body.fontSize = 18;
            body.color = Ui.Ink;
            body.alignment = TextAnchor.UpperCenter;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.raycastTarget = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            creditLines = credits != null ? credits.text.Replace("\r\n", "\n").Trim() : "";
            var sb = new StringBuilder();
            if (licenses != null)
                foreach (var l in licenses)
                    if (l != null) sb.Append("-- ").Append(l.name).Append(" --\n").Append(l.text.Replace("\r\n", "\n").Trim()).Append("\n\n");
            licenseText = sb.ToString().TrimEnd();

            back = IconButton.Build(t, UiIcon.Back, icons, "Esc", () => CloseRequested?.Invoke(), name: "Back");
            back.Rect.TopLeft(616, 621, IconButton.Size, IconButton.Size);
            tips.Track(back);
            panel.SetActive(false);
        }

        /// Heading and the game's name in the current language (credits.*); the credit lines and licenses stay as written.
        public void Apply(Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            UiText.Set(head, loc, "credits.title", FontRole.Title);
            body.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            var sb = new StringBuilder(loc.Get("credits.game"));
            if (creditLines.Length > 0) sb.Append("\n\n").Append(creditLines);
            if (licenseText.Length > 0) sb.Append("\n\n").Append(licenseText);
            body.text = sb.ToString();
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            tips.transform.SetAsLastSibling(); // the one explanation plate stays on top
            panel.SetActive(true);
            scroll.verticalNormalizedPosition = 1f;
            // Nothing selected: keyboard focus would show the explanation at once. The first arrow / Enter selects DefaultButton (TitleScreen).
        }

        public void Hide() => panel.SetActive(false);
    }
}
