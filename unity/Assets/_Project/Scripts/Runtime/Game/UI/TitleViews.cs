using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Title sky (story-integration.md 0.5 #4): one star per chapter in the Dipper at the top right, faint until that
        /// chapter's finale is cleared, then bright. <paramref name="lit"/> null = no sky (story off or more than 7 chapters).
        public void ShowSky(IReadOnlyList<bool> lit)
        {
            if (lit == null || lit.Count == 0 || lit.Count > 7) { if (sky != null) sky.SetActive(false); return; }
            if (sky == null || skyStars.Length != lit.Count)
            {
                if (sky != null) UnityEngine.Object.DestroyImmediate(sky);
                sky = Ui.Rect("Sky", panel.transform).Stretch().gameObject;
                sky.transform.SetSiblingIndex(1); // over the wash, under the menu
                skyStars = new Image[lit.Count];
                for (int i = 0; i < lit.Count; i++)
                {
                    var d = Ui.Theme.Dipper[i]; // x .10..0.98, y .12..0.40 -> the free sky right of the menu, below the badge
                    var p = new Vector2(700 + (d.x - 0.10f) / 0.88f * 460, 84 + (d.y - 0.12f) / 0.28f * 150);
                    skyStars[i] = Ui.Star(sky.transform, true, "SkyStar " + (i + 1));
                    skyStars[i].rectTransform.TopLeft(p.x - 14, p.y - 14, 28, 28);
                    skyStars[i].raycastTarget = false;
                }
            }
            sky.SetActive(true);
            for (int i = 0; i < lit.Count; i++)
            {
                Ui.SetStar(skyStars[i], lit[i]);
                var c = skyStars[i].color;
                skyStars[i].color = new Color(c.r, c.g, c.b, lit[i] ? 1f : 0.35f); // faint, nearly out (the crisis)
            }
        }

        /// The title sky's stars that are bright (tests, UiLayoutCheck); -1 when there is no sky.
        public int SkyLit => sky == null || !sky.activeSelf ? -1 : skyStars.Count(s => s.color.a > 0.5f);

        GameObject sky;
        Image[] skyStars = new Image[0];

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
    /// star earned, the detail line is the still-star line. A carving rises once the first time it is shown
    /// (StoryProgress.CarvingSeen, saved), and focusing or hovering a card with a carving brings back that planet's truth
    /// (its finale line) in the detail row (0.5 #3). With every truth found and the final truth not seen yet, a star marks the planet of the last stage:
    /// play it again to see the ending's last truth (0.5 #7).
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
        readonly Image polaris;
        readonly int polarisPlanet;
        readonly Vector2[] pos; // card centers on the 1280x720 reference
        Localizer loc;
        int truthShown = -1, focused = -1;
        string detailDefault = "";

        /// The run's story (StoryBook): carvings, lit lines, still-star line. Null or off = the plain map.
        public StoryBook Story { get; set; }
        /// SettingsData.reduceMotion: a new carving appears without rising.
        public bool ReduceMotion { get; set; }
        /// The polaris mark (every truth found, the final one not seen) is showing (UiLayoutCheck, tests).
        public bool PolarisShown => polaris.gameObject.activeSelf;
        /// The detail row shows this planet's truth (-1: none).
        public int TruthShown => truthShown;

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
            TopStretch(head.rectTransform, 40, 30, 170, 72);
            detail = Ui.Label(s, "", 24, Ui.Dim, TextAnchor.MiddleLeft, "Detail");
            TopStretch(detail.rectTransform, 42, 100, 78, 48);

            // Card centers on the 1280-wide reference; FitToWidth places the cards for narrower canvases (1280x800: 1214,
            // 4:3: 1108 units) inside the safe margin without overlaps (CardCenters; QA 1: planet 7 at 1280x800).
            pos = new Vector2[planets.Count];
            for (int i = 0; i < pos.Length; i++) pos[i] = Position(i, pos.Length);
            // Dipper lines from card edge to card edge (never under a card's text), placed again for the canvas width.
            if (planets.Count <= 7)
                foreach (var (a, b) in UiTheme.DipperEdges)
                    if (a < pos.Length && b < pos.Length && Cut(pos[a], pos[b]) < 0.5f) // cards side by side: no room for a line
                    {
                        var line = Ui.MakeImage(s, Ui.Dim, null, "Edge");
                        line.raycastTarget = false;
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
                card.rectTransform.TopLeft(p.x - CardW / 2, p.y - CardH / 2, CardW, CardH); // FitToWidth places it for the canvas
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
                // Truth again (0.5 #3, review cd38eb8 R2 (a)): the card itself, focused (arrows, Tab) or hovered, shows its
                // planet's truth in the detail row while it has a carving. No extra button: navigation and Enter stay the card's.
                var trigger = card.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                void On(UnityEngine.EventSystems.EventTriggerType type, Action act)
                {
                    var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
                    e.callback.AddListener(_ => act());
                    trigger.triggers.Add(e);
                }
                On(UnityEngine.EventSystems.EventTriggerType.Select, () => { focused = idx; Hint(idx); });
                On(UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => Hint(idx));
                On(UnityEngine.EventSystems.EventTriggerType.PointerExit, () => Hint(focused));
            }
            // Polaris (story 0.5 #7): a star above the top right corner of the planet holding the last stage.
            polarisPlanet = PlanetOf(levels.Count - 1);
            polaris = Ui.Star(cards[polarisPlanet].transform, true, "Polaris");
            polaris.rectTransform.TopLeft(CardW - 14, -12, 26, 26);
            polaris.raycastTarget = false;
            polaris.gameObject.SetActive(false);

            // Bottom band: back, all stars, note on one plate.
            var band = Ui.MakeImage(s, new Color(Ui.PanelBg.r, Ui.PanelBg.g, Ui.PanelBg.b, 0.92f), null, "Band");
            band.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0, 720 - BandY)); // the bottom edge, full width
            band.rectTransform.anchorMax = new Vector2(1, 0);
            var bt = band.transform; // children of the band: one plate, positions from the band top
            totalLabel = Ui.Label(bt, "", 28, Ui.Gold, TextAnchor.MiddleLeft, "TotalLabel");
            totalLabel.rectTransform.TopLeft(300, 24, 300, 40);
            totalValue = Ui.Label(bt, "", 28, Ui.Gold, TextAnchor.MiddleLeft, "TotalValue");
            var backButton = Ui.MenuButton(bt, 25, () => BackRequested?.Invoke(), out back, "Back");
            ((RectTransform)backButton.transform).TopLeft(40, 14, 230, 72);
            note = Ui.Label(bt, "", 22, Ui.Ink, TextAnchor.MiddleRight, "Note");
            note.rectTransform.Place(Vector2.one, Vector2.one, new Vector2(-40, -26), new Vector2(600, 36)); // from the right edge
            panel.AddComponent<ResizeHook>().Resized = FitToWidth;
            FitToWidth();
            panel.SetActive(false);
        }

        /// Line from a to b: the share of it under each card, plus 6 units of air.
        static float Cut(Vector2 a, Vector2 b)
        {
            var d = b - a;
            return Mathf.Min(d.x != 0 ? CardW / 2 / Mathf.Abs(d.x) : float.MaxValue, d.y != 0 ? CardH / 2 / Mathf.Abs(d.y) : float.MaxValue) + 6f / d.magnitude;
        }

        static void TopStretch(RectTransform r, float left, float y, float right, float h)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(left, -y);
            r.sizeDelta = new Vector2(-(left + right), h);
        }

        /// Card centers for a canvas width: the reference positions pulled in between the side insets (down to the 24 safe
        /// margin), then cards whose rows overlap kept a card width + 36 apart (a line still fits) (4:3: the handle 4-5-6-7 and the bowl 2-3).
        /// 1280 wide: the reference positions, the handle 5-6-7 within ~15 units of them.
        public static Vector2[] CardCenters(IReadOnlyList<Vector2> reference, float width)
        {
            const float Gap = 36, Edge = UiSafe + CardW / 2; // 36: room for a Dipper line between cards side by side
            int n = reference.Count;
            float refL = reference.Min(p => p.x), refR = 1280 - reference.Max(p => p.x);
            float shrink = Mathf.Max(0, (1280 - width) / 2);
            float l = Mathf.Max(Mathf.Min(refL, Edge), refL - shrink), r = Mathf.Max(Mathf.Min(refR, Edge), refR - shrink);
            float refSpan = 1280 - refL - refR, span = width - l - r;
            var c = new Vector2[n];
            for (int i = 0; i < n; i++)
                c[i] = new Vector2(refSpan > 0 ? l + (reference[i].x - refL) / refSpan * span : width / 2, reference[i].y);
            var order = Enumerable.Range(0, n).OrderBy(i => reference[i].x).ToArray();
            bool SameRow(int a, int b) => Mathf.Abs(c[a].y - c[b].y) < CardH + 12;
            const float Need = CardW + Gap;
            for (int j = 0; j < n; j++) // left to right: push right
                for (int i = 0; i < j; i++)
                    if (SameRow(order[i], order[j])) c[order[j]].x = Mathf.Max(c[order[j]].x, c[order[i]].x + Need);
            for (int j = n - 1; j >= 0; j--) // right to left: back inside the right inset
            {
                c[order[j]].x = Mathf.Min(c[order[j]].x, width - r);
                for (int k = j + 1; k < n; k++)
                    if (SameRow(order[j], order[k])) c[order[j]].x = Mathf.Min(c[order[j]].x, c[order[k]].x - Need);
            }
            return c;
        }

        const float UiSafe = 24; // docs/architecture.md 14 safe margin (UiLayoutCheck.Safe)

        /// Cards and Dipper lines for the panel's width. A line with no room left (cards side by side) hides.
        void FitToWidth()
        {
            float w = ((RectTransform)panel.transform).rect.width;
            if (w <= 0) w = 1280;
            var at = CardCenters(pos, w);
            for (int i = 0; i < cards.Length; i++)
                ((RectTransform)cards[i].transform).TopLeft(at[i].x - CardW / 2, at[i].y - CardH / 2, CardW, CardH);
            foreach (var (a, b, line) in edges)
            {
                Vector2 pa = at[a], pb = at[b];
                float cut = Cut(pa, pb);
                bool room = cut < 0.5f && (1 - 2 * cut) * (pb - pa).magnitude >= 12;
                line.gameObject.SetActive(room);
                if (!room) continue;
                var d = pb - pa;
                Vector2 from = pa + d * cut, to = pb - d * cut;
                var r = line.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 0.5f);
                r.anchoredPosition = new Vector2(from.x, -from.y);
                r.sizeDelta = new Vector2(Vector2.Distance(from, to), 3);
                r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            }
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
            else detailDefault = detail.text; // review cd38eb8 N1: the closed map's default in the new language (Show redoes it)
        }

        /// A card was focused or hovered: its truth when it has a carving, else the usual detail line.
        void Hint(int planet)
        {
            ShowTruth(planet);
            if (truthShown == planet && planet >= 0) return;
            truthShown = -1;
            detail.text = detailDefault;
            detail.color = Ui.Dim;
        }

        /// The detail row says planet <paramref name="planet"/>'s truth (its finale line, story.json "finale"): its card is
        /// focused or hovered. Nothing without a story or before its finale is cleared.
        public void ShowTruth(int planet)
        {
            var story = Story != null && Story.On ? Story : null;
            if (planet < 0 || planet >= planets.Count || story == null || !story.FinaleCleared(planets[planet].Key)) return;
            string line = story.FinaleLine(planets[planet].Key, loc.Current);
            if (string.IsNullOrEmpty(line)) return;
            truthShown = planet;
            detail.text = line;
            detail.color = Ui.Gold;
        }

        /// Shows the map with this progress (read only). <paramref name="focus"/>: planet to select, -1 = the last played one.
        public void Show(ProgressData progress, int focus)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (loc == null) throw new InvalidOperationException("PlanetMapView.Show before Apply");
            shown = progress;
            panel.SetActive(true);
            FitToWidth(); // the canvas may have changed size while the map was closed
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
            bool newCarving = false;
            for (int i = 0; i < planets.Count; i++)
            {
                bool on = story != null && story.FinaleCleared(planets[i].Key);
                carvings[i].gameObject.SetActive(on);
                if (!on || story.Progress.CarvingSeen(planets[i].Key)) continue;
                UiPop.Play(carvings[i].rectTransform, ReduceMotion); // the first time: it rises once
                story.Progress.MarkCarvingSeen(planets[i].Key);
                newCarving = true;
            }
            if (newCarving) story.Save(); // 16.5: after a star-map carving appears
            polaris.gameObject.SetActive(story != null && story.Truths == story.ChapterCount && !story.Progress.FinalTruthSeen);
            truthShown = -1;
            detail.color = Ui.Dim;
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
            detailDefault = detail.text;
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
            var frame = Ui.Panel(panel.transform, "Frame").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 600)); // centered: 4:3 is 1108 units wide
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
        /// <param name="truth">The planet's truth (StoryBook.FinaleLine) once its finale is cleared: shown in gold in the
        /// tagline row instead of the tagline (review cd38eb8 N2: a tap opens this panel over the map's detail row). Null = the tagline.</param>
        public void Bind(PlanetEntry planet, ProgressData progress, IReadOnlyList<Level> levels, Localizer loc, string truth = null)
        {
            if (planet == null) throw new ArgumentNullException(nameof(planet));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (planet.Stages.Count > MaxCards) throw new ArgumentException($"planet {planet.Key}: {planet.Stages.Count} stages, the panel holds {MaxCards}", nameof(planet));
            title.text = planet.Name(loc);
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            bool isTruth = !string.IsNullOrEmpty(truth);
            string line = isTruth ? truth : planet.Tagline(loc);
            tagline.text = line;
            tagline.color = isTruth ? Ui.Gold : Ui.Dim;
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
