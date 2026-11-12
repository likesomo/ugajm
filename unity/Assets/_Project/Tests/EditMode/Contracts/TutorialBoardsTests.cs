using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Save ids of the tutorial boards (docs/tutorial.md 2.5).
    public class TutorialBoardsTests
    {
        static TutorialDto Board(string id, int version = 1) => new TutorialDto { id = id, version = version, level = new LevelDto { name = id } };

        [Test]
        public void SaveIdIsBoardIdAtVersion()
        {
            Assert.AreEqual("board-t1@1", TutorialBoards.SaveId(Board("t1")));
            Assert.AreEqual("board-t3@2", TutorialBoards.SaveId(Board("t3", 2)));
            Assert.IsTrue(TutorialIds.IsValid(TutorialBoards.SaveId(Board("t2"))));
            Assert.Throws<ArgumentException>(() => TutorialBoards.SaveId(Board("T1")));
            Assert.Throws<ArgumentException>(() => TutorialBoards.SaveId(Board("t1", 0)));
            Assert.Throws<ArgumentNullException>(() => TutorialBoards.SaveId(null));
        }

        [Test]
        public void FirstUndoneAndAllDoneFollowTheSeenIds()
        {
            var boards = new[] { Board("t1"), Board("t2"), Board("t3") };
            var data = new TutorialData();
            Assert.AreEqual(0, TutorialBoards.FirstUndone(boards, data.IsSeen));
            Assert.IsTrue(data.MarkSeen(TutorialBoards.SaveId(boards[0])));
            Assert.AreEqual(1, TutorialBoards.FirstUndone(boards, data.IsSeen));
            data.MarkSeen(TutorialBoards.SaveId(boards[2]));
            Assert.AreEqual(1, TutorialBoards.FirstUndone(boards, data.IsSeen), "a later board done does not skip T2");
            Assert.IsFalse(TutorialBoards.AllDone(boards, data.IsSeen));
            data.MarkSeen(TutorialBoards.SaveId(boards[1]));
            Assert.AreEqual(-1, TutorialBoards.FirstUndone(boards, data.IsSeen));
            Assert.IsTrue(TutorialBoards.AllDone(boards, data.IsSeen));
            // A new board version shows again.
            Assert.IsFalse(TutorialBoards.AllDone(new[] { Board("t1", 2) }, data.IsSeen));
            Assert.IsTrue(TutorialBoards.AllDone(new List<TutorialDto>(), data.IsSeen), "no boards = done");
        }
    }
}
