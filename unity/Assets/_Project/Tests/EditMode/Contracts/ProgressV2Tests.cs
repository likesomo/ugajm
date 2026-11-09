using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 11.3.1: progress keyed by stage name, levelKey for changed maps, v0/v1 migration.
    public sealed class ProgressV2Tests
    {
        static Level L(string name, int bodies = 3, string row = "#S.....E#") =>
            Level.Parse(name, "", bodies, new[] { "#########", row, "#########" });

        [Test]
        public void RecordsFollowTheNameNotTheNumber()
        {
            var save = new SaveSystem(new MemoryStore());
            var a = L("a"); var b = L("b");
            save.Progress.MarkCleared(b, 2);
            save.Progress.SetLastStage(b);
            // b moves from index 1 to index 0 (a stage was reordered): records stay with b.
            var reordered = new[] { b, a };
            Assert.IsTrue(save.Progress.IsCleared(b));
            Assert.IsFalse(save.Progress.IsCleared(a));
            Assert.AreEqual(2, save.Progress.BestBodies(b));
            Assert.AreEqual(0, save.Progress.LastStageIndex(reordered));
            Assert.AreEqual(1, save.Progress.ClearedCount);
        }

        [Test]
        public void SameNameOtherMapKeepsTheClearButNotTheBest()
        {
            var p = new ProgressData();
            p.MarkCleared(L("x"), 1);
            var edited = L("x", 3, "#S.#...E#");
            Assert.IsTrue(p.IsCleared(edited));
            Assert.AreEqual(0, p.BestBodies(edited), "best was made on another map");
            p.MarkCleared(edited, 3);
            Assert.AreEqual(3, p.BestBodies(edited), "new map, new best");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => p.MarkCleared(edited, 4));
        }

        [Test]
        public void V1IsMigratedByTheFrozenNamesAndBackedUp()
        {
            var store = new MemoryStore();
            string v1 = "{\"version\":1,\"cleared\":[0,3,40],\"bestStage\":[3],\"bestBodies\":[2],\"lastStage\":3,\"starts\":[4,0,0,1]}";
            store.Set(SaveSystem.ProgressKey, v1);
            var save = new SaveSystem(store);
            var first = L(ProgressMigration.V1StageNames[0]);
            var fourth = L(ProgressMigration.V1StageNames[3]);
            Assert.IsTrue(save.Progress.IsCleared(first));
            Assert.IsTrue(save.Progress.IsCleared(fourth));
            Assert.AreEqual(2, save.Progress.ClearedCount, "stage 40 is outside the 17-stage table");
            Assert.AreEqual(2, save.Progress.BestBodies(fourth), "unknown levelKey: kept while it fits the body budget");
            Assert.AreEqual(5, save.Progress.CountStart(first));
            Assert.AreEqual(ProgressMigration.V1StageNames[3], save.Progress.lastStage);
            Assert.AreEqual(v1, store.Get(SaveSystem.ProgressBackupKey));
            StringAssert.Contains("\"version\":2", store.Get(SaveSystem.ProgressKey), "written as v2 at once");
        }

        [Test]
        public void NewerSaveIsNeitherLoadedNorOverwritten()
        {
            var store = new MemoryStore();
            string v9 = "{\"version\":9,\"stages\":[]}";
            store.Set(SaveSystem.ProgressKey, v9);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer than this build"));
            var save = new SaveSystem(store);
            save.Progress.MarkCleared(L("a"), 1);
            save.SaveProgress();
            Assert.AreEqual(v9, store.Get(SaveSystem.ProgressKey));
        }
    }
}
