using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 13.2-13.5: languages, string tables, localizer, language setting, -lang, chapter text.
    public sealed class LocalizationContractTests
    {
        static StringTable T(Language lang, params (string key, string text)[] rows)
        {
            var list = new List<StringTable.Entry>();
            foreach (var (k, t) in rows) list.Add(new StringTable.Entry { key = k, text = t });
            return new StringTable(lang, list);
        }

        [Test]
        public void CodesRoundTrip()
        {
            foreach (var l in Languages.All)
            {
                Assert.IsTrue(Languages.TryParse(Languages.Code(l), out var back));
                Assert.AreEqual(l, back);
            }
            Assert.IsTrue(Languages.TryParse(" EN ", out var en));
            Assert.AreEqual(Language.En, en);
            Assert.IsFalse(Languages.TryParse("fr", out _));
            Assert.AreEqual(Language.Ko, Languages.FromSystem(SystemLanguage.Korean));
            Assert.AreEqual(Language.En, Languages.FromSystem(SystemLanguage.French));
        }

        [Test]
        public void TableGetFormatAndErrors()
        {
            var ko = StringTable.FromJson(Language.Ko, "{\"entries\":[{\"key\":\"hud.bodies\",\"text\":\"몸 {0}개\"}]}");
            Assert.AreEqual("몸 2개", ko.Format("hud.bodies", 2));
            Assert.IsTrue(ko.Has("hud.bodies"));
            Assert.Throws<KeyNotFoundException>(() => ko.Get("hud.none"));
            Assert.Throws<ArgumentException>(() => T(Language.Ko, ("a", "1"), ("a", "2")), "duplicate key");
            Assert.AreEqual(0, StringTable.FromJson(Language.En, "{\"entries\":[]}").Count, "empty skeleton parses");
        }

        [Test]
        public void LocalizerSwitchesAndRequiresKo()
        {
            var loc = new Localizer(new[] { T(Language.Ko, ("k", "가")), T(Language.En, ("k", "A")) }, Language.Ko);
            Language? changed = null;
            loc.Changed += l => changed = l;
            Assert.AreEqual("가", loc.Get("k"));
            loc.Set(Language.En);
            Assert.AreEqual("A", loc.Get("k"));
            Assert.AreEqual(Language.En, changed);
            Assert.Throws<ArgumentException>(() => new Localizer(new[] { T(Language.En, ("k", "A")) }, Language.En), "ko is required");
            var koOnly = new Localizer(new[] { T(Language.Ko, ("k", "가")) }, Language.Ko);
            Assert.Throws<ArgumentException>(() => koOnly.Set(Language.En));
        }

        [Test]
        public void LanguageSettingAndLaunchOption()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            Assert.IsTrue(Languages.TryParse(save.Settings.language, out _), "first run picks ko or en from the system");
            save.SetLanguage(Language.En);
            Assert.AreEqual(Language.En, new SaveSystem(store).Language);
            save.Settings.language = "xx";
            Assert.AreEqual(Language.Ko, save.Language, "unknown value reads as the base language");

            Assert.AreEqual(Language.En, LaunchOptions.Parse(new[] { "x", "-lang", "en" }, "").Language);
            Assert.AreEqual(Language.Ko, LaunchOptions.Parse(null, "http://h/?lang=ko").Language);
            var bad = LaunchOptions.Parse(new[] { "x", "-lang", "fr" }, "");
            Assert.IsNull(bad.Language);
            Assert.AreEqual(1, bad.Warnings.Count);
        }

        [Test]
        public void LanguageChosenOnlyBySetLanguage()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            Assert.IsFalse(save.LanguageChosen, "first run: system default, not a choice");
            save.SetMasterVolume(0.5f);
            Assert.IsFalse(new SaveSystem(store).LanguageChosen, "saving other settings does not mark it");

            store.Set(SaveSystem.SettingsKey, "{\"version\":1,\"language\":\"en\"}"); // a save from before the field
            Assert.IsFalse(new SaveSystem(store).LanguageChosen, "older saves read as not chosen");

            var again = new SaveSystem(store);
            again.SetLanguage(Language.Ko);
            Assert.IsTrue(again.LanguageChosen);
            Assert.IsTrue(new SaveSystem(store).LanguageChosen, "kept in the save");
            Assert.AreEqual(Language.Ko, new SaveSystem(store).Language);
        }

        [Test]
        public void ChapterTextFallsToBaseAndThrowsWhenMissing()
        {
            var c = JsonUtility.FromJson<ChapterFileDto>(
                "{\"chapters\":[{\"id\":\"moss\",\"planet\":\"이끼의 궤도\",\"intro\":\"가\",\"text\":[{\"lang\":\"en\",\"planet\":\"Moss Orbit\",\"intro\":\"A\"}]},{\"id\":\"hollow\",\"planet\":\"별구멍 달\"}]}");
            Assert.AreEqual("이끼의 궤도", c.Get("moss").Planet(Language.Ko));
            Assert.AreEqual("Moss Orbit", c.Get("moss").Planet(Language.En));
            Assert.Throws<KeyNotFoundException>(() => c.Get("hollow").Planet(Language.En));
        }

        [Test]
        public void ChapterTaglineIsOptionalUntilFilled()
        {
            var c = JsonUtility.FromJson<ChapterFileDto>(
                "{\"chapters\":[{\"id\":\"moss\",\"planet\":\"가\",\"tagline\":\"작은 이끼 행성\",\"text\":[{\"lang\":\"en\",\"planet\":\"A\",\"tagline\":\"A small moss planet\"}]}," +
                "{\"id\":\"hollow\",\"planet\":\"나\"},{\"id\":\"carved\",\"planet\":\"다\",\"tagline\":\"새긴 달\"}]}");
            Assert.AreEqual("작은 이끼 행성", c.Get("moss").Tagline(Language.Ko));
            Assert.AreEqual("A small moss planet", c.Get("moss").Tagline(Language.En));
            Assert.AreEqual("", c.Get("hollow").Tagline(Language.En), "no tagline yet: empty in every language, no throw");
            Assert.Throws<KeyNotFoundException>(() => c.Get("carved").Tagline(Language.En), "ko tagline without a translation");
        }
    }
}
