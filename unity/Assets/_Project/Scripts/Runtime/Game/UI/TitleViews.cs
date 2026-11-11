using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Title panel (view only, art layout concepts/ui title, docs/scene-flow.md S7): framed menu on the left with the name,
    /// tagline and five buttons (begin, continue, stages, settings, credits) and the language line. Text from the string
    /// table (title.*) in the current language's role fonts; <see cref="Apply"/> redraws on a language change.
    /// Positions are the art's 1280x720 top-left pixels.
    /// </summary>
    public sealed class TitleView
    {
        readonly GameObject panel;
        readonly Text name, tagline, begin, cont, stages, settings, credits, language;
        readonly Button beginButton, continueButton, stagesButton;

        /// The menu buttons (-titlecheck presses them; read only).
        public Button BeginButton => beginButton;
        public Button ContinueButton => continueButton;
        public Button StagesButton => stagesButton;

        public event Action BeginRequested, ContinueRequested, StagesRequested, SettingsRequested, CreditsRequested, LanguageRequested;

        public TitleView(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("Title", root).Stretch().gameObject;
            var t = panel.transform;
            Ui.Wash(t, 0.12f);
            var menu = Ui.Panel(t, "Menu").rectTransform.TopLeft(32, 30, 450, 664); // texts 8 inside the panel border (32)
            name = Ui.Label(menu, "", 66, Ui.Gold, TextAnchor.MiddleLeft, "Name");
            name.rectTransform.TopLeft(42, 33, 380, 105);
            // The story's one sentence (story.json "title", ko 20 / en 44 characters): up to two lines.
            tagline = Ui.Label(menu, "", 22, Ui.Ink, TextAnchor.MiddleLeft, "Tagline");
            tagline.rectTransform.TopLeft(42, 138, 380, 60);
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button Menu(int row, Action click, out Text label, string id)
            {
                var b = Ui.MenuButton(menu, 25, click, out label, id);
                ((RectTransform)b.transform).TopLeft(40, 204 + row * 76, 370, 72); // 72: the label + 8 inside the button border (14.4)
                return b;
            }
            beginButton = Menu(0, () => BeginRequested?.Invoke(), out begin, "Begin");
            continueButton = Menu(1, () => ContinueRequested?.Invoke(), out cont, "Continue");
            stagesButton = Menu(2, () => StagesRequested?.Invoke(), out stages, "Stages");
            Menu(3, () => SettingsRequested?.Invoke(), out settings, "Settings");
            Menu(4, () => CreditsRequested?.Invoke(), out credits, "Credits");
            begin.color = Ui.Gold;
            var lang = Ui.MakeButton(menu, "", 22, () => LanguageRequested?.Invoke(), out language, "Language");
            ((Image)lang.targetGraphic).color = new Color(0, 0, 0, 0);
            ((RectTransform)lang.transform).TopLeft(40, 590, 370, 36);
            language.color = Ui.Dim;
            var ver = Ui.Secondary(Ui.Label(t, Application.version, 18, Ui.Dim, TextAnchor.LowerRight, "Version")); // 14.2 secondary, inside the 24 safe margin
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 26), new Vector2(300, 24));
            panel.SetActive(false);
        }

        /// Text and fonts for the current language (call once, then on Localizer.Changed). <paramref name="storyLine"/>: the
        /// story's title sentence in that language (StoryBook.TitleLine, after-ending branch included); null = title.tagline.
        public void Apply(Localizer loc, string storyLine = null)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            UiText.Set(name, loc, "title.name", FontRole.Title);
            UiText.Set(tagline, loc, "title.tagline", FontRole.Body);
            if (!string.IsNullOrEmpty(storyLine)) tagline.text = storyLine;
            UiText.Set(begin, loc, "title.begin", FontRole.Body);
            UiText.Set(cont, loc, "title.continue", FontRole.Body);
            UiText.Set(stages, loc, "title.stages", FontRole.Body);
            UiText.Set(settings, loc, "title.settings", FontRole.Body);
            UiText.Set(credits, loc, "title.credits", FontRole.Body);
            // Both language names in their own script: the number font (Jua) has Latin and Hangul (13.4).
            var other = loc.Current == Language.Ko ? Language.En : Language.Ko;
            language.text = SettingsPanel.NativeName(loc.Current) + " / " + SettingsPanel.NativeName(other);
            language.font = Ui.Theme.FontFor(loc.Current, FontRole.Number);
        }

        /// Shows the menu; Continue is off without a last played stage. Focus: Continue when it can, else Begin (Enter plays it).
        public void Show(bool canContinue)
        {
            panel.SetActive(true);
            continueButton.interactable = canContinue;
            (canContinue ? continueButton : beginButton).Select();
        }

        public void Hide() => panel.SetActive(false);
    }

    /// <summary>One planet on the map: chapters.json chapter and its stages (0-based, levels.json order).</summary>
    public sealed class PlanetEntry
    {
        public string Key { get; }
        /// null when there is no chapters.json: one planet with every stage.
        public ChapterInfo Info { get; }
        public IReadOnlyList<int> Stages { get; }

        public PlanetEntry(string key, ChapterInfo info, IReadOnlyList<int> stages)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Info = info;
            Stages = stages ?? throw new ArgumentNullException(nameof(stages));
            if (stages.Count == 0) throw new ArgumentException($"planet {key} has no stages", nameof(stages));
        }

        public string Name(Localizer loc) => Info != null ? Info.Planet(loc.Current) : loc.Get("planets.head");
        public string Tagline(Localizer loc) => Info != null ? Info.Tagline(loc.Current) : "";

        /// Planets in chapters.json order, from LevelCatalog (after Load). A chapter without stages is left out.
        public static List<PlanetEntry> FromCatalog(int stageCount)
        {
            var list = new List<PlanetEntry>();
            foreach (var c in LevelCatalog.Chapters)
            {
                var stages = LevelCatalog.StagesOf(c.id);
                if (stages.Length > 0) list.Add(new PlanetEntry(c.id, c, stages));
            }
            if (list.Count == 0)
            {
                var all = new int[stageCount];
                for (int k = 0; k < stageCount; k++) all[k] = k;
                list.Add(new PlanetEntry("", null, all));
            }
            return list;
        }

        /// Earned and possible stars of this planet's stages.
        public (int earned, int max) Stars(ProgressData progress, IReadOnlyList<Level> levels)
        {
            int earned = 0;
            foreach (int s in Stages) earned += progress.BestStars(levels[s]);
            return (earned, Stages.Count * ProgressData.MaxStars);
        }
    }

    /// <summary>
    /// Planet select (view only, state StageSelect; art layout concepts/ui stage-select): the planets as cards on the Big
    /// Dipper (UiTheme.Dipper, edges 1-2-3-4-1 / 4-5-6-7), each with its name and star total, no locks. A click / Enter
    /// on a planet raises <see cref="PlanetClicked"/> (the root opens <see cref="PlanetStagesPanel"/>). Arrow keys move
    /// between planets (uGUI automatic navigation).
    /// Story (story-integration.md 0.5 #1, #2): with <see cref="Story"/> on, a planet whose finale is cleared shows its
    /// carving mark and lights the story.json "dipper" lines leaving it (the order of clears does not matter); with every
    /// star earned, the detail line is the still-star line.
    /// </summary>
    public sealed class PlanetMapView
    {
        // Card centers: Dipper x .10..0.98 -> 130..1150, y .12..0.40 -> 315..540 (docs/architecture.md 14.10: one card per planet).
        const float MapX = 14, MapY = 218, MapW = 1159, MapH = 805, CardW = 180, CardH = 124, BandY = 606;

        readonly GameObject panel;
        readonly IReadOnlyList<PlanetEntry> planets;
        readonly IReadOnlyList<Level> levels;
        readonly Button[] cards;
        readonly Text[] names, totals;
        readonly Text head, detail, totalLabel, totalValue, back, note;
        readonly List<(int a, int b, Image line)> edges = new List<(int, int, Image)>();
        readonly Image[] carvings;
        Localizer loc;

        /// The run's story (StoryBook): carvings, lit lines, still-star line. Null or off = the plain map.
        public StoryBook Story { get; set; }

        /// A planet's carving mark is showing (UiLayoutCheck, tests).
        public bool CarvingShown(int planet) => carvings[planet].gameObject.activeSelf;
        ProgressData shown;

        /// Index into the planets list.
        public event Action<int> PlanetClicked;
        public event Action BackRequested;

        public PlanetMapView(Transform root, IReadOnlyList<PlanetEntry> planets, IReadOnlyList<Level> levels)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.planets = planets ?? throw new ArgumentNullException(nameof(planets));
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            if (planets.Count == 0) throw new ArgumentException("no planets", nameof(planets));
            panel = Ui.Rect("PlanetSelect", root).Stretch().gameObject;
            var s = panel.transform;
            Ui.Wash(s, 0.985f); // the board and menhirs stay far behind the cards
            head = Ui.Label(s, "", 48, Ui.Ink, TextAnchor.MiddleLeft, "Head");
            head.rectTransform.TopLeft(40, 30, 1070, 72);
            detail = Ui.Label(s, "", 24, Ui.Dim, TextAnchor.MiddleLeft, "Detail");
            detail.rectTransform.TopLeft(42, 100, 1160, 48);

            var pos = new Vector2[planets.Count];
            for (int i = 0; i < pos.Length; i++) pos[i] = Position(i, pos.Length);
            // Dipper lines from card edge to card edge (never under a card's text).
            if (planets.Count <= 7)
                foreach (var (a, b) in UiTheme.DipperEdges)
                    if (a < pos.Length && b < pos.Length)
                    {
                        var d = pos[b] - pos[a];
                        float cut = Mathf.Min(d.x != 0 ? CardW / 2 / Mathf.Abs(d.x) : float.MaxValue, d.y != 0 ? CardH / 2 / Mathf.Abs(d.y) : float.MaxValue) + 6f / d.magnitude;
                        if (cut >= 0.5f) continue; // cards side by side: no room for a line
                        var line = Ui.Line(s, pos[a] + d * cut, pos[b] - d * cut, Ui.Dim, "Edge");
                        line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, 3);
                        edges.Add((a, b, line));
                    }

            cards = new Button[planets.Count];
            names = new Text[planets.Count];
            totals = new Text[planets.Count];
            carvings = new Image[planets.Count];
            for (int i = 0; i < planets.Count; i++)
            {
                int idx = i;
                var p = pos[i];
                // One card per planet: number, name (up to 2 lines), star count.
                var card = Ui.Card(s, "Planet " + (i + 1));
                card.rectTransform.TopLeft(p.x - CardW / 2, p.y - CardH / 2, CardW, CardH);
                card.raycastTarget = true;
                cards[i] = card.gameObject.AddComponent<Button>();
                var colors = cards[i].colors;
                colors.highlightedColor = colors.selectedColor = Ui.Gold;
                cards[i].colors = colors;
                cards[i].onClick.AddListener(() => PlanetClicked?.Invoke(idx));
                var c = card.transform;
                var num = Ui.Label(c, (i + 1).ToString(), 28, Ui.Gold, TextAnchor.UpperLeft, "Number");
                num.rectTransform.TopLeft(22, 21, 24, 34);
                names[i] = Ui.Label(c, "", 22, Ui.Ink, TextAnchor.UpperLeft, "Name");
                names[i].rectTransform.TopLeft(48, 22, 112, 52);
                names[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                names[i].lineSpacing = 0.9f;
                var star = Ui.Star(c, true, "Star");
                star.rectTransform.TopLeft(22, 84, 22, 22);
                totals[i] = Ui.Secondary(Ui.Label(c, "", 20, Ui.Gold, TextAnchor.MiddleLeft, "Stars"));
                totals[i].rectTransform.TopLeft(50, 80, 110, 30);
                // The observer's carving (story 0.5 #1): a small gold diamond at the card's top right until art's picture lands.
                carvings[i] = Ui.MakeImage(c, Ui.Gold, null, "Carving");
                carvings[i].rectTransform.TopLeft(26, 62, 12, 12); // under the number: the name may take two lines, the star row is the total
                carvings[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                carvings[i].raycastTarget = false;
                carvings[i].gameObject.SetActive(false);
            }

            // Bottom band: back, all stars, note on one plate.
            var band = Ui.MakeImage(s, new Color(Ui.PanelBg.r, Ui.PanelBg.g, Ui.PanelBg.b, 0.92f), null, "Band");
            band.rectTransform.TopLeft(0, BandY, 1280, 720 - BandY);
            var bt = band.transform; // children of the band: one plate, positions from the band top
            totalLabel = Ui.Label(bt, "", 28, Ui.Gold, TextAnchor.MiddleLeft, "TotalLabel");
            totalLabel.rectTransform.TopLeft(300, 24, 300, 40);
            totalValue = Ui.Label(bt, "", 28, Ui.Gold, TextAnchor.MiddleLeft, "TotalValue");
            var backButton = Ui.MenuButton(bt, 25, () => BackRequested?.Invoke(), out back, "Back");
            ((RectTransform)backButton.transform).TopLeft(40, 14, 230, 72);
            note = Ui.Label(bt, "", 22, Ui.Ink, TextAnchor.MiddleRight, "Note");
            note.rectTransform.TopLeft(640, 26, 600, 36);
            panel.SetActive(false);
        }

        /// A planet's card center in top-left reference pixels: the Dipper for up to 7 planets, else one row.
        static Vector2 Position(int i, int count)
        {
            if (count <= 7)
            {
                var d = Ui.Theme.Dipper[i];
                return new Vector2(MapX + d.x * MapW, MapY + d.y * MapH);
            }
            float step = (1280 - 80 - CardW) / (count - 1);
            return new Vector2(40 + CardW / 2 + i * step, 400);
        }

        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            UiText.Set(head, loc, "planets.head", FontRole.Title);
            UiText.Set(detail, loc, "planets.detail", FontRole.Body);
            UiText.Set(totalLabel, loc, "planets.total", FontRole.Body);
            UiText.Set(back, loc, "planets.back", FontRole.Body);
            UiText.Set(note, loc, "planets.note", FontRole.Body);
            var body = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            var number = Ui.Theme.FontFor(loc.Current, FontRole.Number);
            for (int i = 0; i < planets.Count; i++)
            {
                names[i].text = planets[i].Name(loc);
                names[i].font = body;
                totals[i].font = number;
                cards[i].transform.Find("Number").GetComponent<Text>().font = number;
            }
            totalValue.font = number;
            // The value follows the label's measured width + 12 px (art: HandwrittenUiPreview).
            totalLabel.rectTransform.sizeDelta = new Vector2(totalLabel.preferredWidth + 2, 40);
            totalValue.rectTransform.TopLeft(300 + totalLabel.preferredWidth + 12, 24, 200, 40);
            if (shown != null && panel.activeSelf) Show(shown, -1);
        }

        /// Shows the map with this progress (read only). <paramref name="focus"/>: planet to select, -1 = the last played one.
        public void Show(ProgressData progress, int focus)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (loc == null) throw new InvalidOperationException("PlanetMapView.Show before Apply");
            shown = progress;
            panel.SetActive(true);
            int earnedAll = 0, maxAll = 0;
            for (int i = 0; i < planets.Count; i++)
            {
                var (earned, max) = planets[i].Stars(progress, levels);
                totals[i].text = $"{earned} / {max}";
                earnedAll += earned;
                maxAll += max;
            }
            totalValue.text = $"{earnedAll} / {maxAll}";
            ShowStory(earnedAll == maxAll && maxAll > 0);
            if (focus < 0) focus = PlanetOf(progress.LastStageIndex(levels));
            cards[Mathf.Clamp(focus, 0, cards.Length - 1)].Select();
        }

        void ShowStory(bool allStars)
        {
            var story = Story != null && Story.On ? Story : null;
            for (int i = 0; i < planets.Count; i++) carvings[i].gameObject.SetActive(story != null && story.FinaleCleared(planets[i].Key));
            foreach (var (a, b, line) in edges)
            {
                bool lit = false;
                if (story != null)
                    foreach (var d in story.Catalog.Dipper)
                        if ((d.from == planets[a].Key && d.to == planets[b].Key) || (d.from == planets[b].Key && d.to == planets[a].Key))
                            lit |= story.FinaleCleared(d.from);
                line.color = lit ? Ui.Gold : Ui.Dim;
            }
            string still = story != null && allStars ? story.StillStarLine(loc.Current) : null;
            if (!string.IsNullOrEmpty(still)) detail.text = still;
            else UiText.Set(detail, loc, "planets.detail", FontRole.Body);
        }

        /// The planet holding a stage (0 when none does).
        public int PlanetOf(int stage)
        {
            for (int i = 0; i < planets.Count; i++)
                foreach (int s in planets[i].Stages) if (s == stage) return i;
            return 0;
        }

        public void Focus(int planet) => cards[Mathf.Clamp(planet, 0, cards.Length - 1)].Select();

        /// A planet's card (-titlecheck presses it; read only).
        public Button Card(int planet) => cards[planet];

        public void Hide() => panel.SetActive(false);
    }

    /// <summary>
    /// The stages of one planet as an overlay over the map (view only): planet name, tagline (hidden when empty), a card per
    /// stage with its number and best stars, and a back button. Esc / back closes it (OverlayStack). Arrow keys move
    /// between cards, Enter plays.
    /// </summary>
    public sealed class PlanetStagesPanel : IOverlay
    {
        const int Columns = 4, MaxCards = 12;

        readonly GameObject panel;
        readonly Text title, tagline, back;
        readonly Button[] cards = new Button[MaxCards];
        readonly Text[] labels = new Text[MaxCards], numbers = new Text[MaxCards];
        readonly Image[,] stars = new Image[MaxCards, ProgressData.MaxStars];
        readonly int[] cardStage = new int[MaxCards];
        readonly RectTransform grid;

        /// 0-based stage.
        public event Action<int> StageClicked;
        public event Action CloseRequested;

        public PlanetStagesPanel(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("PlanetStages", root).Stretch().gameObject;
            var blocker = Ui.Wash(panel.transform, 0.6f, "Blocker");
            blocker.raycastTarget = true;
            var frame = Ui.Panel(panel.transform, "Frame").rectTransform.TopLeft(140, 60, 1000, 600);
            title = Ui.Label(frame, "", 52, Ui.Gold, TextAnchor.MiddleCenter, "Planet");
            title.rectTransform.TopLeft(40, 36, 920, 72);
            tagline = Ui.Label(frame, "", 24, Ui.Dim, TextAnchor.MiddleCenter, "Tagline");
            tagline.rectTransform.TopLeft(40, 108, 920, 40);
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            grid = Ui.Rect("Grid", frame).TopLeft(40, 156, 920, 340);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(214, 112); // the card sprite's two rules sit ~22 px inside: text and stars go between them
            g.spacing = new Vector2(16, 8);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Columns;
            for (int k = 0; k < MaxCards; k++)
            {
                int slot = k;
                var card = Ui.Card(grid, "Stage " + (k + 1));
                card.raycastTarget = true;
                cards[k] = card.gameObject.AddComponent<Button>();
                var colors = cards[k].colors;
                colors.highlightedColor = colors.selectedColor = Ui.Gold;
                cards[k].colors = colors;
                cards[k].onClick.AddListener(() => StageClicked?.Invoke(cardStage[slot]));
                labels[k] = Ui.Label(card.transform, "", 22, Ui.Ink, TextAnchor.UpperLeft, "Label");
                labels[k].rectTransform.TopLeft(24, 28, 112, 30);
                numbers[k] = Ui.Label(card.transform, "", 28, Ui.Gold, TextAnchor.UpperRight, "Number");
                numbers[k].rectTransform.TopLeft(104, 25, 84, 36);
                for (int st = 0; st < ProgressData.MaxStars; st++)
                {
                    stars[k, st] = Ui.Star(card.transform, false, "Star " + (st + 1));
                    stars[k, st].rectTransform.TopLeft(22 + st * 32, 66, 24, 24);
                }
            }
            var backButton = Ui.MenuButton(frame, 25, () => CloseRequested?.Invoke(), out back, "Back");
            ((RectTransform)backButton.transform).TopLeft(385, 504, 230, 72);
            panel.SetActive(false);
        }

        /// Fills the panel for a planet (call before Show; the root pushes it on the OverlayStack).
        public void Bind(PlanetEntry planet, ProgressData progress, IReadOnlyList<Level> levels, Localizer loc)
        {
            if (planet == null) throw new ArgumentNullException(nameof(planet));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (planet.Stages.Count > MaxCards) throw new ArgumentException($"planet {planet.Key}: {planet.Stages.Count} stages, the panel holds {MaxCards}", nameof(planet));
            title.text = planet.Name(loc);
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            string line = planet.Tagline(loc);
            tagline.text = line;
            tagline.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            tagline.gameObject.SetActive(!string.IsNullOrEmpty(line));
            UiText.Set(back, loc, "planets.list_back", FontRole.Body);
            var body = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            var number = Ui.Theme.FontFor(loc.Current, FontRole.Number);
            string label = loc.Get("planets.stage");
            int last = progress.LastStageIndex(levels), focus = 0;
            for (int k = 0; k < MaxCards; k++)
            {
                bool used = k < planet.Stages.Count;
                cards[k].gameObject.SetActive(used);
                if (!used) continue;
                int stage = planet.Stages[k];
                cardStage[k] = stage;
                labels[k].text = label;
                labels[k].font = body;
                numbers[k].text = (stage + 1).ToString("00");
                numbers[k].font = number;
                int best = progress.BestStars(levels[stage]);
                for (int st = 0; st < ProgressData.MaxStars; st++) Ui.SetStar(stars[k, st], st < best);
                if (stage == last) focus = k;
            }
            focusSlot = focus;
        }

        int focusSlot;

        /// The card in a slot (-titlecheck presses it; read only).
        public Button Card(int slot) => cards[slot];

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            cards[focusSlot].Select();
        }

        public void Hide() => panel.SetActive(false);
    }

    /// <summary>Puts a table string on a Text in the current language's role font (docs/architecture.md 13.2, 13.4).</summary>
    public static class UiText
    {
        public static void Set(Text text, Localizer loc, string key, FontRole role)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            text.text = loc.Get(key);
            text.font = Ui.Theme.FontFor(loc.Current, role);
        }
    }
}
