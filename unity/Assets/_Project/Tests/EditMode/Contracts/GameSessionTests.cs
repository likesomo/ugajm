using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Input lock and snapshot-undo contract of GameSession (docs/architecture.md). Owned by the architect.
    public sealed class GameSessionTests
    {
        sealed class FakeBoard : IBoardPresenter
        {
            public readonly List<string> Calls = new List<string>();
            public Action Pending;
            public State Drawn;
            public bool Instant;

            public void Rebuild(State s) { Pending = null; Drawn = s; Calls.Add("Rebuild"); }
            public void Snap(State s) { Pending = null; Drawn = s; Calls.Add("Snap"); }
            public void Present(State prev, StepResult r, Action done)
            {
                Calls.Add("Present");
                if (Instant) { Drawn = r.State; done(); return; }
                Pending = () => { Drawn = r.State; done(); };
            }
            public void Finish() { var p = Pending; Pending = null; p?.Invoke(); }
        }

        static Level Corridor() => Level.Parse("t", "", 3, new[] { "#########", "#S......#", "#########" });
        /// A pit right next to the start: one step right and the brother falls.
        static Level PitNextToStart() => Level.Parse("t", "", 3, new[] { "#########", "#So.....#", "#########" });

        [Test]
        public void PressesQueuedWhileABrotherFallsDoNotMoveTheNextOne()
        {
            // Blind QA-2: after a fall the waiting presses went to the next brother.
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(PitNextToStart());
            int bodies = s.State.Bodies;

            Assert.AreEqual(SendOutcome.Applied, s.Send(Command.Move(Dir.Right)));
            Assert.AreEqual(bodies - 1, s.State.Bodies, "the step into the pit ends the brother");
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right)), "pressed during the fall");
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right)));
            var after = s.State;

            board.Finish();
            Assert.AreEqual(0, s.QueuedCount, "the presses meant for the fallen brother are dropped");
            Assert.IsFalse(s.IsLocked);
            Assert.AreSame(after, s.State, "the next brother has not moved");
            Assert.AreEqual(1, board.Calls.FindAll(c => c == "Present").Count);

            Assert.AreEqual(SendOutcome.Applied, s.Send(Command.Move(Dir.Right)), "a press after the fall moves him as usual");
        }

        [Test]
        public void AQueuedStepThatEndsTheBrotherDropsThePressesBehindIt()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Level.Parse("t", "", 3, new[] { "#########", "#S.o....#", "#########" }));
            int bodies = s.State.Bodies;

            s.Send(Command.Move(Dir.Right));                          // to the cell before the pit
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right))); // into the pit
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right))); // meant for him too
            board.Finish();                                            // the queued step runs: he falls
            Assert.AreEqual(bodies - 1, s.State.Bodies);
            Assert.AreEqual(0, s.QueuedCount, "the press behind the fatal step is dropped at once");
            var after = s.State;
            board.Finish();
            Assert.AreSame(after, s.State, "the next brother has not moved");
            Assert.IsFalse(s.IsLocked);
        }

        [Test]
        public void MovesDuringPresentationAreQueuedInOrder()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Corridor());
            int start = s.State.Pos;

            Assert.AreEqual(SendOutcome.Applied, s.Send(Command.Move(Dir.Right)));
            Assert.IsTrue(s.IsLocked);
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right)));
            Assert.AreEqual(SendOutcome.Queued, s.Send(Command.Move(Dir.Right)));
            Assert.AreEqual(SendOutcome.Ignored, s.Send(Command.Move(Dir.Right)), "queue is capped");
            Assert.AreEqual(start + 1, s.State.Pos, "queued commands are not dispatched while locked");

            board.Finish();
            Assert.AreEqual(start + 2, s.State.Pos);
            board.Finish();
            Assert.AreEqual(start + 3, s.State.Pos);
            board.Finish();
            Assert.IsFalse(s.IsLocked);
            Assert.AreEqual(0, s.QueuedCount);
            Assert.AreSame(s.State, board.Drawn);
        }

        [Test]
        public void UndoInterruptsAndSnapsToSnapshot()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Corridor());
            var before = s.State;

            s.Send(Command.Move(Dir.Right));
            var stale = board.Pending;
            Assert.AreEqual(SendOutcome.Applied, s.Send(Command.Undo));

            Assert.IsFalse(s.IsLocked);
            Assert.AreEqual(0, s.QueuedCount);
            Assert.AreSame(before, s.State, "undo returns the previous snapshot object");
            Assert.AreSame(before, board.Drawn);
            Assert.AreEqual("Snap", board.Calls[board.Calls.Count - 1]);

            stale(); // a cancelled presentation finishing late must not unlock or drain anything
            Assert.AreSame(before, s.State);
        }

        [Test]
        public void UndoCancelsNewestQueuedInputFirst()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Corridor());
            int start = s.State.Pos;

            s.Send(Command.Move(Dir.Right));
            s.Send(Command.Move(Dir.Right)); // queued
            Assert.AreEqual(SendOutcome.Applied, s.Send(Command.Undo));
            Assert.AreEqual(0, s.QueuedCount);
            Assert.IsTrue(s.IsLocked, "the running presentation continues");
            board.Finish();
            Assert.AreEqual(start + 1, s.State.Pos, "R, R, Z = one step");
        }

        [Test]
        public void PresentedFiresAfterAnimationNotOnCancel()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Corridor());
            int fired = 0;
            s.Presented += _ => fired++;

            s.Send(Command.Move(Dir.Right));
            Assert.AreEqual(0, fired, "not before the animation ends");
            board.Finish();
            Assert.AreEqual(1, fired);

            s.Send(Command.Move(Dir.Right));
            var stale = board.Pending;
            s.Send(Command.Restart);
            stale();
            Assert.AreEqual(1, fired, "cancelled presentation does not fire");
        }

        [Test]
        public void BumpIsFeedbackAndDoesNotLock()
        {
            var board = new FakeBoard();
            var s = new GameSession(board);
            s.Load(Corridor());
            var kinds = new List<SessionUpdateKind>();
            s.Updated += u => kinds.Add(u.Kind);

            Assert.AreEqual(SendOutcome.Rejected, s.Send(Command.Move(Dir.Left)));
            Assert.IsFalse(s.IsLocked);
            CollectionAssert.AreEqual(new[] { SessionUpdateKind.Feedback }, kinds);
        }

        [Test]
        public void InstantPresenterDrainsWithoutDeadlock()
        {
            var board = new FakeBoard { Instant = true };
            var s = new GameSession(board);
            s.Load(Corridor());
            int start = s.State.Pos;
            s.Send(Command.Move(Dir.Right));
            s.Send(Command.Move(Dir.Right));
            Assert.IsFalse(s.IsLocked);
            Assert.AreEqual(start + 2, s.State.Pos);
        }

        [Test]
        public void ReplayMatchesDispatch()
        {
            var s = new GameSession();
            s.Load(Corridor());
            s.Replay("RRR");
            Assert.AreEqual(3, s.State.Used);
        }
    }
}
