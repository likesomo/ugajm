using System;
using NUnit.Framework;
using SevenSteps.Game;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// levels.json "chapter" through LevelCatalog.ChapterIdOf (docs/architecture.md 6.4): string id, "" when missing.
    public sealed class LevelCatalogChapterTests
    {
        const string Json = @"{ ""steps"": 7, ""levels"": [
            { ""name"": ""a"", ""hint"": """", ""bodies"": 3, ""chapter"": ""moss"", ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] },
            { ""name"": ""b"", ""hint"": """", ""bodies"": 3, ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] } ] }";

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
        public void ChapterIdIsReadOrEmpty()
        {
            Assert.Throws<InvalidOperationException>(() => LevelCatalog.ChapterIdOf(0));
            Assert.AreEqual(2, LevelCatalog.Load(assets).Length);
            Assert.AreEqual("moss", LevelCatalog.ChapterIdOf(0));
            Assert.AreEqual("", LevelCatalog.ChapterIdOf(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelCatalog.ChapterIdOf(2));
        }

        [Test]
        public void RealLevelsHaveAChapterEntryPerStage()
        {
            var real = UnityEditor.AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.IsNotNull(real, "Data/GameAssets.asset");
            int n = LevelCatalog.Load(real).Length;
            for (int k = 0; k < n; k++) Assert.IsNotNull(LevelCatalog.ChapterIdOf(k));
        }
    }
}
