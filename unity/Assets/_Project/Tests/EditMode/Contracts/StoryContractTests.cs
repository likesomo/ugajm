using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 16 (story S2 contracts, story-integration.md A1-A13): StoryCatalog load checks and lookup,
    /// StoryRules golden trace and ending branches, StoryTruths boundaries, SaveSystem.Story.
    public sealed class StoryContractTests
    {
        static readonly string[] Chapters = { "moss", "hollow" };

        // ---------- fixture ----------

        static StoryLineDto L(string id, string ko, string en) =>
            new StoryLineDto { id = id, line = ko, text = new[] { new StoryLineTextDto { lang = "en", line = en } } };

        static StoryEntryDto E(string id, string on, string chapter = "", int brother = 0, int stones = -1, int card = 0, string after = "", string image = "", params StoryLineDto[] lines) =>
            new StoryEntryDto { id = id, on = on, chapter = chapter, brother = brother, stones = stones, card = card, after = after, image = image,
                                lines = lines.Length > 0 ? lines : new[] { L("1", "한 줄 " + id, "line " + id) } };

        static StoryFileDto Fixture()
        {
            var entries = new List<StoryEntryDto>
            {
                E("title.base", "title"),
                E("title.after", "title", after: "ending"),
                E("lose.base", "lose"),
                E("finale.moss", "finale", chapter: "moss"),
                E("finale.hollow", "finale", chapter: "hollow"),
                E("ending.3.base", "ending", card: 3, image: "ending.3"),
                E("ending.3.all", "ending", card: 3, after: "allTruths", image: "ending.3"),
                E("ending.4", "ending", card: 4, image: "ending.4"),
                E("moment.keep", "keep", lines: new[] { L("1", "{brother}가 붙들었다.", "{brother} held on."), L("2", "돌이 섰다.", "A stone stands.") }),
                E("moment.keep.b1", "keep", brother: 1),
            };
            for (int b = 1; b <= 3; b++) entries.Add(E("ending.1.b" + b, "ending", brother: b, card: 1, image: "ending.1", lines: L("1", "{brother}가 문에 닿았다.", "{brother} reached the gate.")));
            for (int s = 0; s <= 2; s++) entries.Add(E("ending.2.s" + s, "ending", stones: s, card: 2, image: "ending.2"));
            return new StoryFileDto
            {
                version = 1,
                languages = new[] { "ko", "en" },
                brothers = new[]
                {
                    new StoryBrotherDto { n = 1, name = "첫째", text = new[] { new StoryBrotherTextDto { lang = "en", name = "the eldest" } } },
                    new StoryBrotherDto { n = 2, name = "둘째", text = new[] { new StoryBrotherTextDto { lang = "en", name = "the middle one" } } },
                    new StoryBrotherDto { n = 3, name = "막내", text = new[] { new StoryBrotherTextDto { lang = "en", name = "the youngest" } } },
                },
                images = new[] { "ending.1", "ending.2", "ending.3", "ending.4" },
                dipper = new[] { new StoryDipperDto { from = "moss", to = "hollow" }, new StoryDipperDto { from = "hollow", to = "circle" } },
                entries = entries.ToArray(),
            };
        }

        static StoryCatalog Load(StoryFileDto f) => StoryCatalog.FromJson(JsonUtility.ToJson(f), Chapters);

        static void Bad(Action<StoryFileDto> change, string expect)
        {
            var f = Fixture();
            change(f);
            var ex = Assert.Throws<InvalidDataException>(() => Load(f));
            StringAssert.Contains(expect, ex.Message);
        }

        static StoryFileDto Without(StoryFileDto f, string id)
        {
            f.entries = f.entries.Where(e => e.id != id).ToArray();
            return f;
        }

        // ---------- catalog ----------

        [Test]
        public void FixtureLoadsAndFindPicksTheMostSpecific()
        {
            var c = Load(Fixture());
            Assert.AreEqual("moment.keep.b1", c.Find(new StoryCue(StoryOn.Keep, brother: 1)).id);
            Assert.AreEqual("moment.keep", c.Find(new StoryCue(StoryOn.Keep, brother: 2)).id);
            Assert.AreEqual("title.base", c.Find(new StoryCue(StoryOn.Title)).id);
            Assert.AreEqual("title.after", c.Find(new StoryCue(StoryOn.Title, after: StoryOns.AfterEnding)).id);
            Assert.AreEqual("title.base", c.Find(new StoryCue(StoryOn.Title, after: StoryOns.AfterAllStars)).id, "no allStars title: the catch-all");
            Assert.IsFalse(c.TryFind(new StoryCue(StoryOn.Bridge, brother: 1), out _), "optional points may be missing");
            Assert.Throws<KeyNotFoundException>(() => c.Find(new StoryCue(StoryOn.Map)));
            CollectionAssert.AreEqual(new[] { "ending.1", "ending.2", "ending.3", "ending.4" }, c.ImageKeys.ToArray());
            Assert.AreEqual(2, c.Dipper.Count);
        }

        [Test]
        public void TextPicksByShownCountAndPutsTheBrotherIn()
        {
            var c = Load(Fixture());
            var p = new StoryProgress();
            var cue = new StoryCue(StoryOn.Keep, brother: 2);
            Assert.AreEqual("둘째가 붙들었다.", c.Text(cue, p, Language.Ko, out var e));
            Assert.AreEqual("the middle one held on.", c.Text(cue, p, Language.En, out _));
            p.MarkShown(e.id);
            Assert.AreEqual("돌이 섰다.", c.Text(cue, p, Language.Ko, out _), "the next line of the pool");
            p.MarkShown(e.id);
            Assert.AreEqual("둘째가 붙들었다.", c.Text(cue, p, Language.Ko, out _), "wraps around");
            Assert.AreEqual(0, StoryCatalog.Pick(e, 4));
        }

        [Test]
        public void MissingBranchesAreErrors()
        {
            Bad(f => Without(f, "ending.2.s0"), "stones 0");
            Bad(f => Without(f, "ending.3.all"), "allTruths");
            Bad(f => Without(f, "finale.hollow"), "hollow");
            Bad(f => Without(f, "title.after"), "required");
            Bad(f => Without(f, "lose.base"), "lose");
            // A catch-all ending card 1 does not stand in for a written-out brother branch.
            Bad(f => { Without(f, "ending.1.b3"); f.entries = f.entries.Append(E("ending.1.any", "ending", card: 1)).ToArray(); }, "write that branch out");
        }

        [Test]
        public void AmbiguousTriggersAreErrors()
        {
            Bad(f => f.entries = f.entries.Append(E("moment.keep.moss", "keep", chapter: "moss")).ToArray(), "ambiguous");
            Bad(f => f.entries = f.entries.Append(E("moment.keep.b1x", "keep", brother: 1)).ToArray(), "ambiguous");
            // A more specific entry for the overlap resolves it.
            var ok = Fixture();
            ok.entries = ok.entries.Append(E("moment.keep.moss", "keep", chapter: "moss")).Append(E("moment.keep.moss.b1", "keep", chapter: "moss", brother: 1)).ToArray();
            Assert.AreEqual("moment.keep.moss.b1", Load(ok).Find(new StoryCue(StoryOn.Keep, "moss", 1)).id);
        }

        [Test]
        public void DataErrorsNameTheEntry()
        {
            Bad(f => f.entries[0].lines[0].text = new StoryLineTextDto[0], "title.base/1");
            Bad(f => f.entries[0].lines[0].line = "", "empty ko line");
            Bad(f => f.entries[0].chapter = "nowhere", "not in chapters.json");
            Bad(f => f.entries[0].on = "sunset", "unknown on");
            Bad(f => f.entries[0].id = "Title", "[a-z0-9.]+");
            Bad(f => f.entries[1].id = "title.base", "id used twice");
            Bad(f => f.entries[0].stones = 3, "stones 3");
            Bad(f => f.entries[0].card = 2, "card is for");
            Bad(f => f.entries[0].after = "later", "after");
            Bad(f => f.entries[0].image = "ending.9", "not in images");
            Bad(f => f.entries[0].lines = new[] { L("1", "{brother}가 왔다.", "{brother} came.") }, "names no brother");
            Bad(f => f.entries[0].lines = new[] { L("1", "{count}걸음", "{count} steps") }, "only {brother}");
            Bad(f => f.languages = new[] { "ko" }, "\"en\"");
            Bad(f => f.version = 2, "version");
            Bad(f => f.brothers = f.brothers.Take(2).ToArray(), "brother 3");
            Bad(f => f.dipper = new[] { new StoryDipperDto { from = "moss", to = "hollow" } }, "hollow\" starts no line");
            Bad(f => f.dipper[1].to = "sky", "not a chapter id");
            Assert.Throws<InvalidDataException>(() => StoryCatalog.FromJson("{", Chapters));
            Assert.Throws<InvalidDataException>(() => StoryCatalog.FromJson(JsonUtility.ToJson(Fixture()), new string[0]));
        }

        [Test]
        public void RealStoryJsonLoadsWhenItExists()
        {
            const string path = "Assets/_Project/Data/story.json";
            if (!File.Exists(path)) Assert.Ignore("Data/story.json not exported yet (S3)");
            var chapters = JsonUtility.FromJson<ChapterFileDto>(File.ReadAllText("Assets/_Project/Data/chapters.json"));
            var c = StoryCatalog.FromJson(File.ReadAllText(path), chapters.chapters.Select(x => x.id).ToArray());
            Assert.Greater(c.Entries.Count, 0);
        }

        // ---------- rules: golden trace through a headless GameSession ----------

        static List<string> Trace(Level level, string moves)
        {
            var s = new GameSession();
            var trace = new List<string>();
            s.Updated += u =>
            {
                string m = StoryRules.TryMoment(u, out var cue) ? StoryOns.Code(cue.On) + cue.Brother : "-";
                if (StoryRules.TrySeventh(u, out var seventh)) m += "+7:" + seventh.Brother;
                trace.Add(m);
            };
            s.Load(level);
            trace.Clear();
            foreach (char ch in moves)
            {
                if (ch == 'Z') s.Send(Command.Undo);
                else if (Rules.TryParseLetter(ch, out var act)) s.Send(Command.FromAct(act));
            }
            return trace;
        }

        static Level Map(int bodies, params string[] rows) => Level.Parse("t", "", bodies, rows);

        [Test]
        public void GoldenTraceKeepSwitchAndAutoStone()
        {
            var field = Map(3, "###########", "#S........#", "#.........#", "###########");
            // body 1: 7 right, KEEP. body 2: down + 6 right, SWITCH. body 3 (last): 6 left + up from the new start (7,2)
            // ends on (1,1), not its start → automatic statue, Lose.
            var t = Trace(field, "RRRRRRRK" + "DRRRRRRX" + "LLLLLLU");
            CollectionAssert.AreEqual(new[]
            {
                "-", "-", "-", "-", "-", "-", "-+7:1", "keep1",
                "-", "-", "-", "-", "-", "-", "-+7:2", "switch2",
                "-", "-", "-", "-", "-", "-", "-+7:3", // Lose: the lose panel speaks, no moment
            }, t);
        }

        [Test]
        public void GoldenTraceFallBridgeSealVanishWinAndUndo()
        {
            var pit = Map(3, "##########", "#S..o....#", "#........#", "##########");
            // body 1 falls on step 3; body 2 reaches the pit on its 7th step: a bridge; Undo snaps (no moment).
            CollectionAssert.AreEqual(new[] { "-", "-", "fall1", "-", "-", "-", "-", "-", "-", "bridge2+7:2", "-" },
                Trace(pit, "RRR" + "DRRRRUL" + "Z"));

            var crack = Map(2, "##########", "#S......x#", "##########");
            CollectionAssert.AreEqual(new[] { "-", "-", "-", "-", "-", "-", "seal1+7:1" }, Trace(crack, "RRRRRRR"));

            // Mud costs two steps: L, R (start), R mud (4), L start (5), L, R start = 7th step on the start → vanish.
            var mud = Map(2, "#######", "#.Sm..#", "#######");
            CollectionAssert.AreEqual(new[] { "-", "-", "-", "-", "-", "vanish1+7:1" }, Trace(mud, "LRRLLR"));

            var exit = Map(1, "##########", "#S......E#", "##########");
            CollectionAssert.AreEqual(new[] { "-", "-", "-", "-", "-", "-", "-+7:1" }, Trace(exit, "RRRRRRR"), "Win: no moment");

            // A bump is Feedback: nothing.
            CollectionAssert.AreEqual(new[] { "-" }, Trace(exit, "L"));
        }

        [Test]
        public void EndingBranches()
        {
            var exit = Map(3, "##########", "#S......E#", "#........#", "##########");
            var s = Rules.NewState(exit);
            foreach (char ch in "DRRRRRRK" + "RRRRRRR") s = Rules.Dispatch(s, Command.FromAct(Letter(ch))).State; // one statue, body 2 wins
            Assert.AreEqual(Status.Win, s.Status);
            Assert.AreEqual(new StoryCue(StoryOn.Ending, brother: 2, card: 1), StoryRules.Ending(s, 1, 0, 7));
            Assert.AreEqual(new StoryCue(StoryOn.Ending, stones: 1, card: 2), StoryRules.Ending(s, 2, 0, 7));
            Assert.AreEqual(new StoryCue(StoryOn.Ending, card: 3), StoryRules.Ending(s, 3, 6, 7));
            Assert.AreEqual(new StoryCue(StoryOn.Ending, card: 3, after: StoryOns.AfterAllTruths), StoryRules.Ending(s, 3, 7, 7));
            Assert.AreEqual(new StoryCue(StoryOn.Ending, card: 4), StoryRules.Ending(s, 4, 7, 7));
            Assert.Throws<ArgumentException>(() => StoryRules.Ending(Rules.NewState(exit), 1, 0, 7), "not won");
            Assert.Throws<ArgumentOutOfRangeException>(() => StoryRules.Ending(s, 5, 0, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => StoryRules.Ending(s, 3, 8, 7));
            Assert.AreEqual(new StoryCue(StoryOn.Finale, chapter: "moss"), StoryRules.Finale("moss"));
            // Every ending cue resolves in a catalog that passed the load checks.
            var c = Load(Fixture());
            for (int card = 1; card <= 4; card++) Assert.IsNotNull(c.Find(StoryRules.Ending(s, card, 2, 2)));
        }

        static Act Letter(char ch) { Assert.IsTrue(Rules.TryParseLetter(ch, out var a)); return a; }

        // ---------- truths ----------

        [Test]
        public void FinaleStagesComeFromChapterBoundaries()
        {
            var keys = new[] { "a", "a", "b", "b", "b", "c", "c" };
            Assert.AreEqual(1, StoryTruths.FinaleStage(keys, "a"));
            Assert.AreEqual(4, StoryTruths.FinaleStage(keys, "b"));
            Assert.AreEqual(5, StoryTruths.FinaleStage(keys, "c"), "the last chapter's finale is the stage before the ending");
            Assert.AreEqual(-1, StoryTruths.FinaleStage(keys, "z"));
            Assert.AreEqual(-1, StoryTruths.FinaleStage(new[] { "a", "b" }, "b"), "a chapter that is only the last stage has no finale");
            Assert.IsTrue(StoryTruths.IsFinaleStage(keys, 4));
            Assert.IsFalse(StoryTruths.IsFinaleStage(keys, 6));
            Assert.IsTrue(StoryTruths.IsEndingStage(keys, 6));

            var levels = keys.Select((k, i) => Level.Parse("s" + i, "", 1, new[] { "#####", "#S.E#", "#####" })).ToArray();
            var p = new ProgressData();
            Assert.AreEqual(0, StoryTruths.Truths(levels, keys, p, new[] { "a", "b", "c" }));
            p.MarkCleared(levels[1], 1);
            p.MarkCleared(levels[5], 1);
            Assert.IsTrue(StoryTruths.FinaleCleared(levels, keys, p, "a"));
            Assert.IsFalse(StoryTruths.FinaleCleared(levels, keys, p, "b"));
            Assert.AreEqual(2, StoryTruths.Truths(levels, keys, p, new[] { "a", "b", "c" }));
            Assert.IsFalse(StoryTruths.ChapterComplete(levels, keys, p, "a"));
            p.MarkCleared(levels[0], 1);
            Assert.IsTrue(StoryTruths.ChapterComplete(levels, keys, p, "a"));
            Assert.Throws<ArgumentException>(() => StoryTruths.Truths(levels.Take(3).ToArray(), keys, p, new[] { "a" }));
        }

        // ---------- save ----------

        [Test]
        public void StorySaveRoundTripsAndNewPlayerClearsIt()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            save.Story.MarkShown("finale.moss");
            save.Story.MarkShown("finale.moss");
            save.Story.MarkEndingSeen();
            save.Story.MarkCarvingSeen("moss");
            save.Story.MarkCarvingSeen("moss");
            save.Story.MarkFinalTruthSeen();
            Assert.IsFalse(store.Data.ContainsKey(SaveSystem.StoryKey), "Mark changes memory only");
            save.SaveStory();
            var again = new SaveSystem(store);
            Assert.AreEqual(2, again.Story.Count("finale.moss"));
            Assert.IsTrue(again.Story.EndingSeen);
            Assert.IsTrue(again.Story.CarvingSeen("moss"));
            Assert.AreEqual(1, again.Story.carvingSeen.Count);
            Assert.IsTrue(again.Story.FinalTruthSeen);
            again.SetReduceMotion(true);
            again.StartNewPlayer();
            var fresh = new SaveSystem(store);
            Assert.AreEqual(0, fresh.Story.Count("finale.moss"));
            Assert.IsFalse(fresh.Story.EndingSeen);
            Assert.IsFalse(fresh.Story.CarvingSeen("moss"));
            Assert.IsTrue(fresh.Settings.reduceMotion, "settings stay");
        }

        [Test]
        public void BrokenOrNewerStorySave()
        {
            var store = new MemoryStore();
            store.Set(SaveSystem.StoryKey, "{not json");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Story save is not valid JSON"));
            Assert.IsFalse(new SaveSystem(store).Story.EndingSeen);

            store.Set(SaveSystem.StoryKey, "{\"version\":1,\"counts\":[{\"id\":\"a\",\"n\":2},{\"id\":\"a\",\"n\":5},{\"id\":\"\",\"n\":1},{\"id\":\"b\",\"n\":-3}],\"carvingSeen\":[\"moss\",\"moss\",\"\"]}");
            var repaired = new SaveSystem(store).Story;
            Assert.AreEqual(2, repaired.Count("a"));
            Assert.AreEqual(0, repaired.Count("b"));
            Assert.AreEqual(1, repaired.counts.Count);
            CollectionAssert.AreEqual(new[] { "moss" }, repaired.carvingSeen);

            string newer = "{\"version\":9,\"endingSeen\":true}";
            store.Set(SaveSystem.StoryKey, newer);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Story save version 9"));
            var save = new SaveSystem(store);
            Assert.IsFalse(save.Story.EndingSeen);
            save.Story.MarkEndingSeen();
            save.SaveStory();
            Assert.AreEqual(newer, store.Get(SaveSystem.StoryKey), "a newer build's story save is not overwritten");
        }

        // ---------- levels.json startLine ----------

        [Test]
        public void StartLineDefaultsToStory()
        {
            Assert.AreEqual("story", JsonUtility.FromJson<LevelDto>("{\"name\":\"a\"}").StartLine);
            Assert.AreEqual("hint", JsonUtility.FromJson<LevelDto>("{\"name\":\"a\",\"startLine\":\"hint\"}").StartLine);
            Assert.AreEqual("", JsonUtility.FromJson<LevelDto>("{\"name\":\"a\",\"startLine\":\"\"}").StartLine);
            Assert.AreEqual("story", new LevelDto { startLine = null }.StartLine);
            Assert.Throws<ArgumentException>(() => _ = new LevelDto { name = "a", startLine = "both" }.StartLine);
        }
    }
}
