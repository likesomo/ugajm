using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Stage names per language through LevelCatalog.TextOf (docs/architecture.md 13.5): ko = levels.json name (the save
    /// key), other languages = the "text" entry; a missing translation throws.
    public sealed class LevelCatalogTextTests
    {
        const string Json = @"{ ""steps"": 7, ""levels"": [
            { ""name"": ""첫 걸음"", ""hint"": ""힌트"", ""bodies"": 3, ""text"": [ { ""lang"": ""en"", ""name"": ""First Step"", ""hint"": ""Hint"", ""story"": """" } ], ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] },
            { ""name"": ""둘"", ""hint"": """", ""bodies"": 3, ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] } ] }";

        GameAssets assets;

        static void ResetCatalog() =>
            typeof(LevelCatalog).GetMethod("ResetForPlay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);

        [SetUp]
        public void SetUp()
        {
            ResetCatalog();
            assets = ScriptableObject.CreateInstance<GameAssets>();
            assets.levelsJson = new TextAsset(Json);
        }

        [TearDown]
        public void TearDown()
        {
            ResetCatalog();
            UnityEngine.Object.DestroyImmediate(assets.levelsJson);
            UnityEngine.Object.DestroyImmediate(assets);
        }

        [Test]
        public void NameIsInTheAskedLanguage()
        {
            Assert.Throws<InvalidOperationException>(() => LevelCatalog.TextOf(0, Language.En));
            var levels = LevelCatalog.Load(assets);
            Assert.AreEqual("첫 걸음", LevelCatalog.TextOf(0, Language.Ko).name);
            Assert.AreEqual("첫 걸음", levels[0].Name, "the save key stays the ko name");
            Assert.AreEqual("First Step", LevelCatalog.TextOf(0, Language.En).name);
            Assert.AreEqual("Hint", LevelCatalog.TextOf(0, Language.En).hint);
            Assert.Throws<KeyNotFoundException>(() => LevelCatalog.TextOf(1, Language.En));
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelCatalog.TextOf(2, Language.Ko));
        }

        [Test]
        public void RealLevelsHaveANameInEveryLanguage()
        {
            var real = UnityEditor.AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.IsNotNull(real, "Data/GameAssets.asset");
            int n = LevelCatalog.Load(real).Length;
            foreach (var lang in Languages.All)
                for (int k = 0; k < n; k++) Assert.IsNotEmpty(LevelCatalog.TextOf(k, lang).name, $"stage {k + 1} {Languages.Code(lang)}");
        }
    }
}
