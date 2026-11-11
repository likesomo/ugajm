using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// UI layout rules of docs/architecture.md 14, checked on screens built in Edit Mode (14.9). Each screen is built under
    /// a world-space canvas of the canvas-unit size (CanvasScaler 1280x720, match 0.5: 1280x720 and 1920x1080 are both
    /// 1280x720 units, 1280x800 is 1214x759), in every language, with the longest level / chapter texts and the
    /// longest numbers, then:
    ///   size      effective font size (fontSize x language scale, best fit = resizeTextMinSize) >= 22, or 18 when
    ///             UiLayout.secondary; 18 can not be exempted (14.2)
    ///   plate     every text has an Image behind it (drawn before) whose inner area (9-slice: inside the border) holds the
    ///             glyphs + 8 and whose alpha is >= 0.75; contrast text / stacked plates >= 4.5 over black and white (14.3, 14.4)
    ///   decoration no other Image crosses the glyphs + 4; a plate that holds the glyphs without the 8 padding is a
    ///             plate error (14.5)
    ///   overlap   glyph rects, and plates / cards / buttons (not ancestor - descendant) do not overlap (14.5)
    ///   overflow  Wrap / Truncate: the glyphs fit the text rect; Overflow: they fit the parent rect (14.5)
    ///   safe      glyphs and buttons 24 inside the canvas (14.6)
    ///   lines     thin Images (lines) are 2 thick and have 3:1 against what is behind them (14.3)
    ///   modal     a modal screen has a full-canvas scrim (alpha >= 0.6, raycast) under it; UI drawn before the scrim is
    ///             left out (14.7)
    ///   board     HUD text, plates and buttons off the board projected with BoardView.Fit (widest, tallest, longest hint / name
    ///             stages; 14.6). Glyph rects come from the text generator
    /// (the drawn quads), plate colors from the sprite PNG under the text.
    /// HUD and Result belong to gameplay-ui: their findings are warnings until <see cref="HudResultAreErrors"/>.
    /// </summary>
    public static class UiLayoutCheck
    {
        public const float BodyMin = 22, SecondaryMin = 18, PlatePad = 8, TextMargin = 4, Safe = 24;
        public const float PlateAlpha = 0.75f, ScrimAlpha = 0.6f, TextContrast = 4.5f, LineContrast = 3f, LineMin = 2;
        /// HUD / Result findings: warnings first (shared with gameplay-ui), errors once both are fixed (lead).
        public static bool HudResultAreErrors = true;
        /// Glyph quads carry the font's side bearing (hand fonts: up to ~2 units past the pen position).
        const float GlyphSlack = 2f;

        sealed class Res { public string Name; public Vector2 Units; public bool Errors; }
        static readonly Res[] Resolutions =
        {
            new Res { Name = "1280x720+1920x1080", Units = new Vector2(1280, 720), Errors = true },
            new Res { Name = "1280x800", Units = CanvasUnits(1280, 800), Errors = false },
        };

        /// Canvas size in units for a screen size (CanvasScaler ScaleWithScreenSize, 1280x720, match 0.5).
        public static Vector2 CanvasUnits(float w, float h)
        {
            float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(w / 1280f, 2f), Mathf.Log(h / 720f, 2f), 0.5f));
            return new Vector2(Mathf.Round(w / scale), Mathf.Round(h / scale));
        }

        sealed class Screen
        {
            public string Name;
            public bool GameplayUi;
            /// Builds under the canvas; returns the modal root (or null).
            public Func<Ctx, RectTransform, GameObject> Build;
        }

        sealed class Ctx
        {
            public GameAssets Assets;
            public Localizer Loc;
            public Language Lang;
            public Level[] Levels;
            public string Variant = "";
            public AppConfig Config;
            public CameraRig? RigOverride;
            public Vector2 Units;
            /// HUD screens: the board area (14.6) in canvas units, top-left origin; null elsewhere.
            public Rect? Board;
        }

        public static void Check(GameAssets assets, AppConfig config, List<string> issues)
        {
            if (assets == null || assets.uiTheme == null) { issues.Add("ERROR UiLayoutCheck: GameAssets.uiTheme is not set"); return; }
            var tables = new List<StringTable>();
            foreach (var a in assets.strings ?? new StringTableAsset[0])
                if (a.json != null) tables.Add(StringTable.FromJson(a.language, a.json.text));
            if (tables.Count == 0) { issues.Add("ERROR UiLayoutCheck: no string tables"); return; }
            Ui.Theme = assets.uiTheme;
            Level[] levels = LevelCatalog.Load(assets);

            // The Game scene's camera choice (GameController.overrideCamera) while it is still open (ValidateAll opens it last).
            var game = Object.FindAnyObjectByType<GameController>();
            CameraRig? rigOverride = game != null && game.overrideCamera ? game.cameraRig : (CameraRig?)null;
            string configPath = config != null ? AssetDatabase.GetAssetPath(config) : null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The scene's AppConfig object may be unloaded with the scene: load it again by path.
            if (!string.IsNullOrEmpty(configPath)) config = AssetDatabase.LoadAssetAtPath<AppConfig>(configPath);
            var exemptions = new SortedDictionary<string, List<string>>();
            int screensBuilt = 0;
            foreach (var res in Resolutions)
                foreach (var t in tables)
                {
                    var loc = new Localizer(tables, t.Language);
                    foreach (var (screen, variant) in Screens(levels, loc))
                    {
                        var ctx = new Ctx { Assets = assets, Loc = loc, Lang = t.Language, Levels = levels, Variant = variant, Config = config, RigOverride = rigOverride, Units = res.Units };
                        string label = $"{screen.Name}{(variant == "" ? "" : " " + variant)}/{Languages.Code(t.Language)}/{res.Name}";
                        bool errors = res.Errors && (!screen.GameplayUi || HudResultAreErrors);
                        var found = new List<string>();
                        RectTransform canvas = null;
                        try
                        {
                            canvas = MakeCanvas(res.Units);
                            var modal = screen.Build(ctx, canvas);
                            Layout(res.Units);
                            CheckScreen(label, res.Units, modal, ctx, found, exemptions, res == Resolutions[0]);
                            screensBuilt++;
                        }
                        catch (Skipped e) { found.Add(e.Message); }
                        catch (Exception e)
                        {
                            found.Add($"cannot build in Edit Mode: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
                        }
                        finally { Clear(); }
                        string level = errors ? "ERROR" : "WARNING";
                        foreach (var f in found) issues.Add($"{level} UiLayout {label}: {f}");
                    }
                }
            foreach (var tex in decoded.Values) if (tex != null) Object.DestroyImmediate(tex);
            decoded.Clear(); // S3: the PNG copies are big, do not keep them between runs
            foreach (var kv in exemptions)
                issues.Add($"NOTE UiLayout exempt {kv.Value.Count}: {kv.Key}: {string.Join("; ", kv.Value.Distinct())}");
            issues.Add($"NOTE UiLayout: {screensBuilt} screen builds checked");
        }

        // ---------------------------------------------------------------- screens

        static IEnumerable<(Screen, string)> Screens(Level[] levels, Localizer loc)
        {
            var planets = PlanetEntry.FromCatalog(levels.Length);
            yield return (new Screen { Name = "Title", Build = (c, root) => { BuildTitle(c, root); return null; } }, "");
            // Story (story-integration.md 3.3, docs/architecture.md 16): the title sentence before and after the ending, the
            // ending cards with their longest line of every branch, the loading carving. The map screen above shows every
            // carving, every lit dipper line and the still-star line (full progress).
            foreach (string after in new[] { "", StoryOns.AfterEnding })
            {
                string a = after;
                yield return (new Screen { Name = "Title", Build = (c, root) =>
                {
                    var story = StoryOf(c);
                    if (story == null) throw new Skipped("story.json does not load (SceneFlowValidator reports it)");
                    BuildTitle(c, root, LongestLine(story, c.Lang, e => e.on == "title" && e.after == a));
                    return null;
                } }, a == "" ? "story" : "story after ending");
            }
            for (int card = 1; card <= StoryRules.EndingCards; card++)
            {
                int k = card;
                yield return (new Screen { Name = "Ending", Build = (c, root) =>
                {
                    var story = StoryOf(c);
                    if (story == null) throw new Skipped("story.json does not load (SceneFlowValidator reports it)");
                    var v = new EndingView(root, null, c.Loc);
                    v.ShowPage(k - 1, StoryRules.EndingCards, new EndingPage(LongestLine(story, c.Lang, e => e.on == "ending" && e.card == k), "", "check"));
                    v.Show();
                    return Find(root, "Ending");
                } }, "card " + k);
            }
            var storyHere = StoryOfProject();
            if (storyHere != null && storyHere.Entries.Any(e => e.on == "loading"))
                yield return (new Screen { Name = "Loading", Build = (c, root) =>
                {
                    var v = new LoadingView(root);
                    v.Begin(c.Loc, 1, LongestLine(StoryOf(c), c.Lang, e => e.on == "loading"));
                    v.Progress(4f / 7f);
                    return null;
                } }, "carving");
            yield return (new Screen { Name = "Language", Build = (c, root) =>
            {
                Ui.MenuBackground(root);
                var p = new LanguagePanel(root);
                p.Apply(c.Loc);
                p.Show();
                return Find(root, "Language");
            } }, "");
            yield return (new Screen { Name = "Settings", Build = (c, root) =>
            {
                BuildTitle(c, root);
                var save = new SaveSystem(new MemoryStore());
                var s = SettingsPanel.Create(root, save, true, c.Loc);
                s.Show();
                return s.gameObject;
            } }, "");
            yield return (new Screen { Name = "Menu", Build = (c, root) =>
            {
                BuildTitle(c, root);
                var m = new MenuPanel(root);
                m.Apply(c.Loc);
                m.Show();
                return Find(root, "GameMenu");
            } }, "");
            yield return (new Screen { Name = "StageSelect", Build = (c, root) => { BuildMap(c, root, planets); return null; } }, "map");
            for (int p = 0; p < planets.Count; p++)
            {
                int planet = p;
                yield return (new Screen { Name = "StageSelect", Build = (c, root) =>
                {
                    BuildMap(c, root, planets);
                    var list = new PlanetStagesPanel(root);
                    list.Bind(planets[planet], FullProgress(c.Levels), c.Levels, c.Loc);
                    list.Show();
                    return Find(root, "PlanetStages");
                } }, "list " + planets[p].Key);
            }
            for (int tip = 1; tip <= LoadingView.Tips; tip++)
            {
                int n = tip;
                yield return (new Screen { Name = "Loading", Build = (c, root) =>
                {
                    var v = new LoadingView(root);
                    v.Begin(c.Loc, n);
                    v.Progress(4f / 7f);
                    return null;
                } }, "tip " + tip);
            }
            foreach (var chapter in LevelCatalog.Chapters)
            {
                string key = chapter.id;
                yield return (new Screen { Name = "ChapterCard", Build = (c, root) =>
                {
                    var v = ChapterEntryView.Create(root, c.Loc);
                    v.Bind(LevelCatalog.ChapterCard(key, c.Lang));
                    v.Show();
                    return v.gameObject;
                } }, key);
            }
            int longestHint = Longest(levels.Length, i => LevelCatalog.TextOf(i, loc.Current).hint);
            int longestName = Longest(levels.Length, i => LevelCatalog.TextOf(i, loc.Current).name);
            // 14.6 range: the widest and the tallest board, and the longest hint / name.
            int widest = Longest(levels.Length, i => new string('w', levels[i].W));
            int tallest = Longest(levels.Length, i => new string('h', levels[i].H));
            foreach (int stage in new[] { longestHint, longestName, widest, tallest }.Distinct())
            {
                int s = stage;
                yield return (new Screen { Name = "HUD", GameplayUi = true, Build = (c, root) => { BuildHud(c, s); c.Board = BoardArea(c, s); return null; } }, $"stage {s + 1}");
            }
            // Side column (statues, message, KEEP / SWITCH; review B1): the first choice of a statue-door stage, longest message.
            // The longest message is the longest msg.* line or the H hint of the longest solution (review B3).
            int? chooser = Enumerable.Range(0, levels.Length).Where(i => levels[i].StatueCount > 0).Concat(Enumerable.Range(0, levels.Length))
                .Cast<int?>().FirstOrDefault(i => ChooseState(levels[i.Value]) != null);
            int longestRoute = LongestRoute(levels);
            // 14.11 choice screens: the chooser stage and the widest board that reaches a choice within 7 moves, each with
            // ExplainChoice true (both result lines + the TutorialIds.Choose guide, the most text) and false (no result lines).
            int? widestChooser = Enumerable.Range(0, levels.Length).Where(i => ChooseState(levels[i]) != null)
                .OrderByDescending(i => levels[i].W).ThenBy(i => i).Cast<int?>().FirstOrDefault();
            var choosers = new List<int?> { chooser };
            if (widestChooser != null && widestChooser != chooser) choosers.Add(widestChooser);
            foreach (var who in choosers)
                foreach (bool explain in new[] { true, false })
                {
                    var stage = who;
                    bool ex = explain;
                    string label = (stage != null ? $"stage {stage + 1} choose" : "choose") + (ex ? "" : " no explain");
                    yield return (new Screen { Name = "HUD", GameplayUi = true, Build = (c, root) =>
                    {
                        if (stage == null) throw new Skipped("no stage reaches a KEEP / SWITCH choice within 7 moves: the side column is not checked");
                        int st = stage.Value;
                        string hint = new HintProvider(c.Levels, c.Loc).For(longestRoute);
                        string msg = LongestMessage(c.Loc);
                        var choose = ChooseState(c.Levels[st]);
                        var hud = BuildHud(c, st, choose, hint.Length > msg.Length ? hint : msg);
                        c.Board = BoardArea(c, st);
                        // The choice cards (docs 4.4, 14.11) as the game shows them.
                        ShowChoice(c, st, hud, choose, explain: ex, guide: ex);
                        return null;
                    } }, label);
                }
            // 14.11: the first-move guide (TutorialIds.Move) on the first stage, as a new player sees it.
            yield return (new Screen { Name = "HUD", GameplayUi = true, Build = (c, root) =>
            {
                var hud = BuildHud(c, 0);
                hud.Guide = true;
                c.Board = BoardArea(c, 0);
                return null;
            } }, "stage 1 move guide");
            // 14.11: "걸음 숫자 표시" on, widest board, a few steps walked. The numbers themselves are board world labels
            // (BoardView, inside the board area); this checks the HUD drawn with the switch on.
            yield return (new Screen { Name = "HUD", GameplayUi = true, Build = (c, root) =>
            {
                var walked = Walked(c.Levels[widest], 3);
                var hud = HudView.Create(HudTileSet(c), c.Loc);
                hud.StepNumbers = true;
                hud.Refresh(walked, widest, LevelCatalog.TextOf(widest, c.Lang), "");
                c.Board = BoardArea(c, widest);
                return null;
            } }, $"stage {widest + 1} step numbers");
            yield return (new Screen { Name = "Result", GameplayUi = true, Build = (c, root) =>
            {
                // Clear over the board (docs 14.7, 9eda0b0): the HUD hides, the result keeps clear of the board area.
                // Interim glue by gameplay-ui so the new ResultView builds; scene-flow finishes the 'Result clear' checks
                // (scrim raycast, HUD hidden, the star tip shown).
                var hud = BuildHud(c, longestHint);
                var r = ResultView.Create();
                r.HudHidden += hud.Conceal;
                r.Apply(c.Loc);
                bool last = c.Levels.Length > 0;
                c.Board = BoardArea(c, c.Levels.Length - 1);
                r.Show(ResultModel.Clear(c.Levels.Length - 1, 3, 3, 1, 1, last, 3, 3));
                return null; // not a modal: the scrim is 0.25 (14.7)
            } }, "clear");
        }

        static void BuildTitle(Ctx c, RectTransform root, string storyLine = null)
        {
            Ui.MenuBackground(root);
            var v = new TitleView(root);
            v.Apply(c.Loc, storyLine);
            v.Show(true);
        }

        static void BuildMap(Ctx c, RectTransform root, List<PlanetEntry> planets)
        {
            Ui.MenuBackground(root);
            var map = new PlanetMapView(root, planets, c.Levels);
            // Full progress: every carving, every lit dipper line, and the still-star line in the detail row.
            var story = StoryOf(c);
            if (story != null) map.Story = new StoryBook(story, new StoryProgress(), null, _ => true);
            map.Apply(c.Loc);
            map.Show(FullProgress(c.Levels), -1);
        }

        /// story.json as the player loads it, or null when it does not load (SceneFlowValidator.ValidateStory reports that).
        static StoryCatalog StoryOf(Ctx c) => LocalizationCheck.StoryCatalogOf(c.Assets);

        static StoryCatalog StoryOfProject()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            return assets != null ? LocalizationCheck.StoryCatalogOf(assets) : null;
        }

        /// The longest line (in characters, {brother} as the longest brother name) of the matching entries in a language.
        static string LongestLine(StoryCatalog story, Language lang, Func<StoryEntryDto, bool> which)
        {
            string brother = Enumerable.Range(1, 3).Select(n => story.BrotherName(n, lang)).OrderByDescending(s => s.Length).First();
            string best = null;
            foreach (var e in story.Entries.Where(which))
                for (int i = 0; i < e.lines.Length; i++)
                {
                    string line = story.Line(e, i, lang).Replace(StoryCatalog.BrotherToken, brother);
                    if (best == null || line.Length > best.Length) best = line;
                }
            return best ?? throw new Skipped("story.json has no matching entry");
        }

        static HudView BuildHud(Ctx c, int stage) => BuildHud(c, stage, Rules.NewState(c.Levels[stage]), "");

        static TileSet HudTileSet(Ctx c) =>
            c.Assets.tileSet != null ? c.Assets.tileSet : AssetDatabase.FindAssets("t:TileSet").Select(g => AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(t => t != null);

        /// Up to <paramref name="steps"/> applied moves from the start (first open direction each time), still in Play.
        static State Walked(Level level, int steps)
        {
            var s = Rules.NewState(level);
            for (int k = 0; k < steps; k++)
            {
                State moved = null;
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var next = Rules.Dispatch(s, Command.Move(d)).State;
                    if (!ReferenceEquals(next, s) && next.Status == Status.Play && next.Used > s.Used) { moved = next; break; }
                }
                if (moved == null) break;
                s = moved;
            }
            return s;
        }

        static HudView BuildHud(Ctx c, int stage, State state, string message)
        {
            var hud = HudView.Create(HudTileSet(c), c.Loc);
            hud.Refresh(state, stage, LevelCatalog.TextOf(stage, c.Lang), message);
            return hud;
        }

        /// 14.6 board area: the board cells and actors (height BoardView.ActorHeight) through the camera BoardView.Fit gives
        /// at this canvas aspect, as a canvas rect (top-left origin) grown by 8.
        static Rect BoardArea(Ctx c, int stage)
        {
            var info = LevelCatalog.ChapterOf(stage);
            var preset = info != null && c.Config != null ? c.Config.Chapter(info.ArtPreset) : null;
            var set = BoardController.TileSetFor(c.Config != null ? c.Config.tileSet : null, preset, c.Assets.tileSet);
            var grid = set.GridFor(c.Levels[stage]);
            var go = new GameObject("UiLayoutCheck Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = c.Units.x / c.Units.y;
                BoardView.Fit(cam, grid, c.RigOverride ?? set.cameraRig);
                var b = grid.Bounds;
                float top = BoardView.ActorHeight * grid.CellSize;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int k = 0; k < 8; k++)
                {
                    var p = b.center + new Vector3((k & 1) == 0 ? -b.extents.x : b.extents.x, (k & 2) == 0 ? 0 : top, (k & 4) == 0 ? -b.extents.z : b.extents.z);
                    var v = cam.WorldToViewportPoint(p);
                    float x = v.x * c.Units.x, y = (1 - v.y) * c.Units.y;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
                return Grow(Rect.MinMaxRect(x0, y0, x1, y1), 8);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// The first KEEP / SWITCH choice (Status.Choose) reachable by moves, depth first; null when none within 7 moves.
        /// gameplay-ui's Edit Mode path for the choice screen (14.11): the cards of <paramref name="s"/> (Status.Choose) placed
        /// beside the board area near the brother, exactly as ChoiceController does at runtime.
        static void ShowChoice(Ctx c, int stage, HudView hud, State s, bool explain, bool guide)
        {
            if (!ChoicePreview.TryOf(s, out var p)) throw new InvalidOperationException($"stage {stage + 1}: not a Choose state");
            hud.ShowChoice(new HudView.ChoiceCards { On = true, NextBody = p.NextBody, Explain = explain, Guide = guide, FocusKeep = true });
            var info = LevelCatalog.ChapterOf(stage);
            var preset = info != null && c.Config != null ? c.Config.Chapter(info.ArtPreset) : null;
            var set = BoardController.TileSetFor(c.Config != null ? c.Config.tileSet : null, preset, c.Assets.tileSet);
            var grid = set.GridFor(c.Levels[stage]);
            var go = new GameObject("UiLayoutCheck Choice Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = c.Units.x / c.Units.y;
                BoardView.Fit(cam, grid, c.RigOverride ?? set.cameraRig);
                hud.PlaceChoice(BoardArea(c, stage), BoardView.BrotherPoint(cam, grid, s.Pos, c.Units), c.Units);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static State ChooseState(Level level)
        {
            State Walk(State s, int depth)
            {
                if (s.Status == Status.Choose) return s;
                if (depth == 0 || s.Status != Status.Play) return null;
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var next = Rules.Dispatch(s, Command.Move(d)).State;
                    if (ReferenceEquals(next, s)) continue;
                    var found = Walk(next, depth - 1);
                    if (found != null) return found;
                }
                return null;
            }
            return Walk(Rules.NewState(level), Rules.Steps);
        }

        /// A screen that can not be set up for a stated reason (reported as is, not as an Edit Mode build failure).
        sealed class Skipped : Exception { public Skipped(string message) : base(message) { } }

        static int longestRouteStage = -1;
        static Level[] longestRouteOf;

        /// The stage whose shortest solution (Solver, the H hint) is the longest; solved once per level set.
        static int LongestRoute(Level[] levels)
        {
            if (longestRouteOf == levels && longestRouteStage >= 0) return longestRouteStage;
            int best = 0, len = -1;
            for (int i = 0; i < levels.Length; i++)
            {
                int n = Solver.Solve(levels[i])?.Path.Count ?? 0;
                if (n > len) { len = n; best = i; }
            }
            longestRouteOf = levels;
            return longestRouteStage = best;
        }

        /// The longest message line (msg.* table strings) in the current language.
        static string LongestMessage(Localizer loc) =>
            loc.Table.Keys.Where(k => k.StartsWith("msg.", StringComparison.Ordinal)).Select(k => loc.Get(k)).OrderByDescending(m => m.Length).FirstOrDefault() ?? "";

        /// Every stage cleared with 3 stars: the longest star counts ("150 / 150").
        static ProgressData FullProgress(Level[] levels)
        {
            var p = new ProgressData();
            foreach (var l in levels) p.MarkCleared(l, 1, ProgressData.MaxStars);
            return p;
        }

        static int Longest(int count, Func<int, string> text)
        {
            int best = 0, len = -1;
            for (int i = 0; i < count; i++) { int n = (text(i) ?? "").Length; if (n > len) { len = n; best = i; } }
            return best;
        }

        static GameObject Find(RectTransform root, string name)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            return t != null ? t.gameObject : null;
        }

        // ---------------------------------------------------------------- canvas

        static readonly List<GameObject> built = new List<GameObject>();

        static RectTransform MakeCanvas(Vector2 units)
        {
            var go = new GameObject("UiLayoutCheck", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = units;
            return rt;
        }

        /// Every root canvas (ours and the prefabs' own) becomes a world-space canvas of the unit size, then layout runs.
        static void Layout(Vector2 units)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)c.transform;
                rt.localScale = Vector3.one;
                rt.position = Vector3.zero;
                rt.rotation = Quaternion.identity;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = units;
                foreach (var r in rt.GetComponentsInChildren<RectTransform>()) LayoutRebuilder.ForceRebuildLayoutImmediate(r);
            }
            Canvas.ForceUpdateCanvases();
        }

        static void Clear()
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- checks

        sealed class Item
        {
            public Graphic G;
            public int Order;
            public Vector2[] Poly;        // canvas units, y down from the top-left
            public Rect Box;              // axis-aligned bounds of Poly
            public float Alpha;
            public UiLayout Mark;
            public bool Is(UiLayoutRule r) => Mark != null && (Mark.exempt & r) != 0;
            public string Path;
        }

        sealed class TextItem
        {
            public Item I;
            public Text T;
            public Rect Glyphs;           // canvas units
            public float Size;
        }

        static void CheckScreen(string label, Vector2 units, GameObject modal, Ctx ctx, List<string> found,
            SortedDictionary<string, List<string>> exemptions, bool collectExemptions)
        {
            var items = DrawList(units);
            // 14.7 scrim: the first full-canvas, raycast, alpha >= 0.6 Image inside the modal; everything before it is out.
            int from = 0;
            if (modal != null)
            {
                var canvasRect = new Rect(0, 0, units.x, units.y);
                var scrim = items.FirstOrDefault(i => i.G.transform.IsChildOf(modal.transform) && i.G is Image && i.G.raycastTarget
                    && i.Alpha >= ScrimAlpha - 0.001f && Contains(i.Box, Inset(canvasRect, 1)));
                if (scrim == null) found.Add($"modal: no full-canvas scrim (Image, raycast, alpha >= {ScrimAlpha}) in {PathOf(modal.transform)}");
                else from = scrim.Order;
            }
            var live = items.Where(i => i.Order >= from).ToList();
            var images = live.Where(i => !(i.G is Text)).ToList();
            var texts = new List<TextItem>();
            foreach (var i in live)
                if (i.G is Text t && !string.IsNullOrWhiteSpace(t.text)) texts.Add(Measure(i, t, ctx, found));

            foreach (var i in live)
                if (i.Mark != null && i.Mark.exempt != UiLayoutRule.None)
                {
                    if (string.IsNullOrWhiteSpace(i.Mark.reason)) found.Add($"exempt without reason: {i.Path}");
                    if (collectExemptions)
                    {
                        string key = $"{label.Split('/')[0]}/{i.Path} {i.Mark.exempt}";
                        if (!exemptions.TryGetValue(key, out var list)) exemptions[key] = list = new List<string>();
                        list.Add(i.Mark.reason);
                    }
                }

            var canvasBox = new Rect(0, 0, units.x, units.y);
            var plates = new HashSet<Item>();
            foreach (var t in texts)
            {
                // 14.6 safe area
                if (!t.I.Is(UiLayoutRule.Safe) && !Contains(Inset(canvasBox, Safe), t.Glyphs))
                    found.Add($"safe area: {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} outside the 24 margin");
                // 14.4 / 14.5 plates and decorations
                var g4 = Grow(t.Glyphs, TextMargin);
                Item plate = null;
                var stack = new List<Item>();
                foreach (var img in images)
                {
                    if (!Intersects(img.Poly, g4)) continue;
                    var inner = InnerRect(img);
                    bool before = img.Order < t.I.Order;
                    bool holds = before && Contains(img.Box, t.Glyphs) && IsAxisAligned(img);
                    if (holds) stack.Add(img);
                    if (holds && inner.HasValue && Contains(inner.Value, Grow(t.Glyphs, PlatePad - 0.5f)) && img.Alpha * CenterAlpha(img) >= PlateAlpha)
                    { plate = img; continue; }
                    if (holds && CenterAlpha(img) >= 0.5f) // an (almost) solid image under the text
                    {
                        if (inner.HasValue && !Contains(inner.Value, t.Glyphs))
                        {
                            if (!t.I.Is(UiLayoutRule.Decoration) && !img.Is(UiLayoutRule.Decoration))
                                found.Add($"decoration: the border of {img.Path} crosses {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} (inner {Fmt(inner.Value)})");
                        }
                        else if (inner.HasValue && !Contains(inner.Value, Grow(t.Glyphs, PlatePad - 0.5f)) && !t.I.Is(UiLayoutRule.Plate) && !img.Is(UiLayoutRule.Plate))
                            found.Add($"plate padding: {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} need 8 inside {img.Path} inner {Fmt(inner.Value)}");
                        continue;
                    }
                    if (holds) continue; // a see-through frame or wash that holds the text: counted in the contrast stack
                    if (!t.I.Is(UiLayoutRule.Decoration) && !img.Is(UiLayoutRule.Decoration))
                        found.Add($"decoration: {img.Path} {Fmt(img.Box)} crosses {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} (+4)");
                }
                if (plate == null)
                {
                    if (!t.I.Is(UiLayoutRule.Plate))
                        found.Add($"plate: {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} has no plate (alpha >= {PlateAlpha}, 8 inside)");
                }
                else
                {
                    plates.Add(plate);
                    float contrast = Contrast(t, stack);
                    if (contrast < TextContrast && !t.I.Is(UiLayoutRule.Contrast))
                        found.Add($"contrast: {t.I.Path} \"{Short(t.T.text)}\" {contrast:0.00}:1 on {plate.Path} (needs {TextContrast})");
                }
            }
            // 14.5 text against text
            for (int a = 0; a < texts.Count; a++)
                for (int b = a + 1; b < texts.Count; b++)
                {
                    var ra = texts[a].Glyphs; var rb = texts[b].Glyphs;
                    if (Area(Intersect(ra, rb)) > 1f && !texts[a].I.Is(UiLayoutRule.Overlap) && !texts[b].I.Is(UiLayoutRule.Overlap))
                        found.Add($"overlap: {texts[a].I.Path} \"{Short(texts[a].T.text)}\" and {texts[b].I.Path} \"{Short(texts[b].T.text)}\" ({Fmt(Intersect(ra, rb))})");
                }
            // 14.5 plates / cards / buttons against each other
            var boxes = new List<Item>(plates.Where(p => !IsFull(p, canvasBox)));
            foreach (var s in live.Select(i => i.G.GetComponentInParent<Selectable>()).Where(s => s != null && s.targetGraphic != null).Distinct())
            {
                var it = live.FirstOrDefault(i => i.G == s.targetGraphic);
                if (it == null || IsFull(it, canvasBox)) continue; // a full-screen click catcher (chapter card: click to close)
                if (!boxes.Contains(it)) boxes.Add(it);
                if (!it.Is(UiLayoutRule.Safe) && !Contains(Grow(Inset(canvasBox, Safe), 0.5f), it.Box))
                    found.Add($"safe area: button {it.Path} {Fmt(it.Box)} outside the 24 margin");
            }
            for (int a = 0; a < boxes.Count; a++)
                for (int b = a + 1; b < boxes.Count; b++)
                {
                    var A = boxes[a]; var B = boxes[b];
                    if (A.G.transform.IsChildOf(B.G.transform) || B.G.transform.IsChildOf(A.G.transform)) continue;
                    if (A.Is(UiLayoutRule.Overlap) || B.Is(UiLayoutRule.Overlap)) continue;
                    if (Area(Intersect(A.Box, B.Box)) > 1f && Intersects(A.Poly, Intersect(A.Box, B.Box)) && Intersects(B.Poly, Intersect(A.Box, B.Box)))
                        found.Add($"overlap: {A.Path} {Fmt(A.Box)} and {B.Path} {Fmt(B.Box)}");
                }
            // 14.6 board area: HUD text, plates and buttons stay off the projected board
            if (ctx.Board is Rect board)
            {
                foreach (var t in texts)
                    if (!t.I.Is(UiLayoutRule.Board) && Area(Intersect(t.Glyphs, board)) > 0)
                        found.Add($"board: {t.I.Path} \"{Short(t.T.text)}\" glyphs {Fmt(t.Glyphs)} on the board area {Fmt(board)}");
                foreach (var bx in boxes)
                    if (!bx.Is(UiLayoutRule.Board) && Area(Intersect(bx.Box, board)) > 0)
                        found.Add($"board: {bx.Path} {Fmt(bx.Box)} on the board area {Fmt(board)}");
            }
            // 14.3 lines: thickness and contrast
            foreach (var img in images)
            {
                var r = img.G.rectTransform.rect;
                float thin = Mathf.Min(Mathf.Abs(r.width * img.G.transform.lossyScale.x), Mathf.Abs(r.height * img.G.transform.lossyScale.y));
                if (thin >= 6 || img.Is(UiLayoutRule.Decoration)) continue;
                if (thin < LineMin - 0.01f) found.Add($"line: {img.Path} is {thin:0.#} thick (needs {LineMin})");
                var behind = images.Where(o => o.Order < img.Order && Contains(o.Box, Center(img.Box)) && IsAxisAligned(o)).ToList();
                float c = LineContrastOf(img, behind);
                if (c < LineContrast && !img.Is(UiLayoutRule.Contrast)) found.Add($"line: {img.Path} contrast {c:0.00}:1 (needs {LineContrast})");
            }
        }

        static List<Item> DrawList(Vector2 units)
        {
            var list = new List<Item>();
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                .Where(c => c.isRootCanvas).OrderBy(c => c.sortingOrder).ToList();
            foreach (var canvas in canvases)
            {
                var root = (RectTransform)canvas.transform;
                foreach (var g in canvas.GetComponentsInChildren<Graphic>())
                {
                    if (!g.enabled || !g.gameObject.activeInHierarchy) continue;
                    float alpha = g.color.a * GroupAlpha(g.transform);
                    if (g is Text tx) { if (string.IsNullOrEmpty(tx.text)) continue; }
                    if (alpha <= 0.01f) continue;
                    var corners = new Vector3[4];
                    g.rectTransform.GetWorldCorners(corners);
                    var poly = corners.Select(p => ToCanvas(root, p, units)).ToArray();
                    if (Masked(g, Bounds(poly), root, units)) continue;
                    list.Add(new Item
                    {
                        G = g, Order = list.Count, Poly = poly, Box = Bounds(poly), Alpha = alpha,
                        Mark = g.GetComponent<UiLayout>(), Path = PathOf(g.transform),
                    });
                }
            }
            return list;
        }

        /// 14.1 "inside the mask": out of sight behind an ancestor RectMask2D / Mask (scroll lists).
        static bool Masked(Graphic g, Rect box, RectTransform root, Vector2 units)
        {
            for (var p = g.transform.parent; p != null; p = p.parent)
            {
                bool masks = (p.TryGetComponent<RectMask2D>(out var m2) && m2.enabled) || (p.TryGetComponent<Mask>(out var m) && m.enabled);
                if (!masks) continue;
                var clip = Bounds(Corners((RectTransform)p, root, units));
                if (Area(Intersect(clip, box)) <= 0) return true;
            }
            return false;
        }

        static float GroupAlpha(Transform t)
        {
            float a = 1f;
            for (var p = t; p != null; p = p.parent)
                foreach (var g in p.GetComponents<CanvasGroup>())
                {
                    a *= g.alpha;
                    if (g.ignoreParentGroups) return a;
                }
            return a;
        }

        static Vector2 ToCanvas(RectTransform root, Vector3 world, Vector2 units)
        {
            var p = root.InverseTransformPoint(world);
            return new Vector2(p.x + units.x / 2, units.y / 2 - p.y);
        }

        static TextItem Measure(Item i, Text t, Ctx ctx, List<string> found)
        {
            var rt = t.rectTransform;
            var size = rt.rect.size;
            var settings = t.GetGenerationSettings(size);
            settings.scaleFactor = 1f;
            var gen = new TextGenerator();
            int fontSize = t.fontSize;
            if (t.resizeTextForBestFit)
            {
                gen.Populate(t.text, settings);
                fontSize = Mathf.Max(gen.fontSizeUsedForBestFit, 0);
                settings.resizeTextForBestFit = false;
                settings.fontSize = Mathf.Max(fontSize, t.resizeTextMinSize);
            }
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            gen.Populate(t.text, settings);
            var verts = gen.verts;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int k = 0; k < verts.Count; k += 4)
            {
                var a = verts[k].position; var c = verts[k + 2].position;
                if (Mathf.Approximately(a.x, c.x) || Mathf.Approximately(a.y, c.y)) continue; // spaces
                min = Vector2.Min(min, Vector2.Min(a, c)); max = Vector2.Max(max, Vector2.Max(a, c));
            }
            Rect local = min.x <= max.x ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : new Rect(rt.rect.center, Vector2.zero);
            // Generator positions are relative to the rect pivot (as Text.OnPopulateMesh draws them).
            var w0 = rt.TransformPoint(new Vector3(local.xMin, local.yMin)); var w1 = rt.TransformPoint(new Vector3(local.xMax, local.yMax));
            var canvas = (RectTransform)t.canvas.rootCanvas.transform;
            var units = canvas.sizeDelta;
            Vector2 c0 = ToCanvas(canvas, w0, units), c1 = ToCanvas(canvas, w1, units);
            var glyphs = Rect.MinMaxRect(Mathf.Min(c0.x, c1.x), Mathf.Min(c0.y, c1.y), Mathf.Max(c0.x, c1.x), Mathf.Max(c0.y, c1.y));
            var item = new TextItem { I = i, T = t, Glyphs = glyphs };

            // 14.2 size: fontSize x the language scale of the role the font plays (best fit: its minimum)
            float scale = 1f;
            var langs = Ui.Theme.languages ?? new LanguageFonts[0];
            int fi = Array.FindIndex(langs, f => f.language == ctx.Lang);
            if (fi >= 0) { var fonts = langs[fi]; if (t.font == fonts.title) scale = fonts.TitleScale; else if (t.font == fonts.body) scale = fonts.BodyScale; }
            float sizeUsed = (t.resizeTextForBestFit ? Mathf.Min(t.resizeTextMinSize, fontSize) : t.fontSize) * scale;
            item.Size = sizeUsed;
            bool secondary = i.Mark != null && i.Mark.secondary;
            float need = secondary ? SecondaryMin : BodyMin;
            if (sizeUsed < SecondaryMin - 0.01f) found.Add($"size: {i.Path} \"{Short(t.text)}\" is {sizeUsed:0.#} (nothing under {SecondaryMin}, no exemption)");
            else if (sizeUsed < need - 0.01f) found.Add($"size: {i.Path} \"{Short(t.text)}\" is {sizeUsed:0.#} (needs {need}{(secondary ? "" : "; 18 only with UiLayout.secondary")})");

            // 14.5 overflow: Wrap / Truncate fit the rect, Overflow fits the parent rect
            if (!i.Is(UiLayoutRule.Overflow))
            {
                bool clipsH = t.horizontalOverflow == HorizontalWrapMode.Wrap, clipsV = t.verticalOverflow == VerticalWrapMode.Truncate;
                var own = Bounds(Corners(rt, canvas, units));
                var parent = rt.parent as RectTransform;
                var outer = parent != null ? Bounds(Corners(parent, canvas, units)) : own;
                var limitH = clipsH ? own : outer; var limitV = clipsV ? own : outer;
                if (glyphs.xMin < limitH.xMin - GlyphSlack || glyphs.xMax > limitH.xMax + GlyphSlack)
                    found.Add($"overflow: {i.Path} \"{Short(t.text)}\" glyphs {Fmt(glyphs)} wider than {(clipsH ? "its rect" : "its parent")} {Fmt(limitH)}");
                if (glyphs.yMin < limitV.yMin - GlyphSlack || glyphs.yMax > limitV.yMax + GlyphSlack)
                    found.Add($"overflow: {i.Path} \"{Short(t.text)}\" glyphs {Fmt(glyphs)} taller than {(clipsV ? "its rect" : "its parent")} {Fmt(limitV)}");
            }
            return item;
        }

        static Vector2[] Corners(RectTransform r, RectTransform canvas, Vector2 units)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return c.Select(p => ToCanvas(canvas, p, units)).ToArray();
        }

        /// The area a plate offers its text: the rect, minus the sprite border for a sliced image. Null for RawImage etc.
        static Rect? InnerRect(Item img)
        {
            var box = img.Box;
            if (img.G is Image im && im.sprite != null && (im.type == Image.Type.Sliced || im.type == Image.Type.Tiled) && im.hasBorder)
            {
                // Image.pixelsPerUnit = sprite ppu / canvas reference ppu; the sliced border is drawn at that x pixelsPerUnitMultiplier
                // (Image.multipliedPixelsPerUnit, internal), e.g. FitButtonSkin's 2.5.
                float ppu = im.pixelsPerUnit * im.pixelsPerUnitMultiplier;
                var b = im.sprite.border / Mathf.Max(ppu, 0.0001f);
                var s = img.G.transform.lossyScale;
                return Rect.MinMaxRect(box.xMin + b.x * s.x, box.yMin + b.w * s.y, box.xMax - b.z * s.x, box.yMax - b.y * s.y);
            }
            return box;
        }

        static bool IsAxisAligned(Item i) => Mathf.Abs(Mathf.DeltaAngle(i.G.transform.eulerAngles.z, 0)) < 0.5f;
        static bool IsFull(Item i, Rect canvas) => i.Box.width >= canvas.width * 0.95f && i.Box.height >= canvas.height * 0.95f;

        // ---------------------------------------------------------------- color

        static readonly Dictionary<Texture, Texture2D> decoded = new Dictionary<Texture, Texture2D>();

        /// CPU copy of a texture from its source file (works in -nographics), null when it can not be read.
        static Texture2D Decode(Texture tex)
        {
            if (tex == null) return null;
            if (decoded.TryGetValue(tex, out var d)) return d;
            d = null;
            string path = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(path) && (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) && File.Exists(path))
            {
                d = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!d.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(d); d = null; }
            }
            decoded[tex] = d;
            return d;
        }

        /// Average sprite color (tinted) under a canvas area of the image: the sprite center for sliced images.
        static Color ColorUnder(Item img, Rect area)
        {
            var tint = img.G.color;
            Texture tex = null; Rect uv = new Rect(0, 0, 1, 1); bool sliced = false;
            if (img.G is Image im && im.sprite != null)
            {
                tex = im.sprite.texture;
                var r = im.sprite.textureRect;
                uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                sliced = im.type == Image.Type.Sliced && im.hasBorder;
            }
            else if (img.G is RawImage raw) { tex = raw.texture; uv = raw.uvRect; }
            var pixels = Decode(tex);
            if (pixels == null) return tint; // flat color image
            Rect sample;
            if (sliced) sample = new Rect(uv.x + uv.width * 0.45f, uv.y + uv.height * 0.45f, uv.width * 0.1f, uv.height * 0.1f);
            else
            {
                var box = img.Box;
                float x0 = Mathf.InverseLerp(box.xMin, box.xMax, area.xMin), x1 = Mathf.InverseLerp(box.xMin, box.xMax, area.xMax);
                float y0 = 1 - Mathf.InverseLerp(box.yMin, box.yMax, area.yMax), y1 = 1 - Mathf.InverseLerp(box.yMin, box.yMax, area.yMin);
                sample = new Rect(uv.x + uv.width * x0, uv.y + uv.height * y0, uv.width * Mathf.Max(x1 - x0, 0.001f), uv.height * Mathf.Max(y1 - y0, 0.001f));
            }
            Color sum = Color.clear; int n = 0;
            for (int sy = 0; sy < 6; sy++)
                for (int sx = 0; sx < 12; sx++)
                {
                    float u = sample.x + sample.width * (sx + 0.5f) / 12f, v = sample.y + sample.height * (sy + 0.5f) / 6f;
                    sum += pixels.GetPixelBilinear(u, v); n++;
                }
            var c = sum / n;
            return new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a * tint.a);
        }

        static float CenterAlpha(Item img)
        {
            if (!(img.G is Image im) || im.sprite == null) return img.G is RawImage raw && raw.texture != null ? ColorUnder(img, img.Box).a / Mathf.Max(img.G.color.a, 0.0001f) : 1f;
            var c = ColorUnder(img, img.Box);
            return img.G.color.a > 0 ? c.a / img.G.color.a : 0;
        }

        /// Text contrast over the plate stack, worst of a black and a white base (14.3).
        static float Contrast(TextItem t, List<Item> stack)
        {
            float worst = float.MaxValue;
            foreach (var bg in new[] { Color.black, Color.white })
            {
                var c = bg;
                foreach (var img in stack.OrderBy(s => s.Order))
                {
                    var under = ColorUnder(img, t.Glyphs);
                    float a = under.a * GroupAlpha(img.G.transform);
                    c = Color.Lerp(c, new Color(under.r, under.g, under.b, 1), a);
                }
                var fg = t.T.color;
                float fa = fg.a * GroupAlpha(t.T.transform);
                var text = Color.Lerp(c, new Color(fg.r, fg.g, fg.b, 1), fa);
                worst = Mathf.Min(worst, Ratio(text, c));
            }
            return worst;
        }

        static float LineContrastOf(Item line, List<Item> behind)
        {
            float worst = float.MaxValue;
            foreach (var bg in new[] { Color.black, Color.white })
            {
                var c = bg;
                foreach (var img in behind.OrderBy(s => s.Order))
                {
                    var under = ColorUnder(img, line.Box);
                    c = Color.Lerp(c, new Color(under.r, under.g, under.b, 1), under.a * GroupAlpha(img.G.transform));
                }
                var lc = ColorUnder(line, line.Box);
                var drawn = Color.Lerp(c, new Color(lc.r, lc.g, lc.b, 1), lc.a * GroupAlpha(line.G.transform));
                worst = Mathf.Min(worst, Ratio(drawn, c));
            }
            return worst;
        }

        static float Ratio(Color a, Color b)
        {
            float la = Lum(a), lb = Lum(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        static float Lum(Color c)
        {
            float L(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
        }

        // ---------------------------------------------------------------- geometry

        static Rect Bounds(Vector2[] p)
        {
            float x0 = p.Min(v => v.x), x1 = p.Max(v => v.x), y0 = p.Min(v => v.y), y1 = p.Max(v => v.y);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static Rect Grow(Rect r, float d) => Rect.MinMaxRect(r.xMin - d, r.yMin - d, r.xMax + d, r.yMax + d);
        static Rect Inset(Rect r, float d) => Grow(r, -d);
        static Vector2 Center(Rect r) => r.center;
        static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.01f && inner.yMin >= outer.yMin - 0.01f && inner.xMax <= outer.xMax + 0.01f && inner.yMax <= outer.yMax + 0.01f;
        static bool Contains(Rect outer, Vector2 p) => outer.Contains(p);
        static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin), x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            return x1 > x0 && y1 > y0 ? Rect.MinMaxRect(x0, y0, x1, y1) : Rect.zero;
        }
        static float Area(Rect r) => r.width * r.height;

        /// Convex polygon (a rotated rect) against an axis-aligned rect: separating axis test.
        static bool Intersects(Vector2[] poly, Rect r)
        {
            if (r.width <= 0 || r.height <= 0) return false;
            var rect = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            foreach (var axes in new[] { poly, rect })
                for (int k = 0; k < axes.Length; k++)
                {
                    var e = axes[(k + 1) % axes.Length] - axes[k];
                    var n = new Vector2(-e.y, e.x);
                    if (n.sqrMagnitude < 1e-8f) continue;
                    float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                    foreach (var p in poly) { float d = Vector2.Dot(p, n); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                    foreach (var p in rect) { float d = Vector2.Dot(p, n); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                    if (a1 <= b0 + 1e-3f || b1 <= a0 + 1e-3f) return false;
                }
            return true;
        }

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0})";
        static string Short(string s) { s = s.Replace("\n", " "); return s.Length > 24 ? s.Substring(0, 24) + "…" : s; }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            if (parts.Count > 1 && parts[0] == "UiLayoutCheck") parts.RemoveAt(0);
            return string.Join("/", parts);
        }
    }
}
