using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Story;

namespace SevenSteps.Tests
{
    /// The board's story lines (docs/architecture.md 16.7 / 16.8, story-integration.md 0.2, 4.3): StoryDirector.
    public sealed class StoryDirectorTests
    {
        sealed class FakeLine : ITransientLine
        {
            public readonly List<string> Log = new List<string>();
            public string Text;
            public float Seconds;
            public bool LineShown { get; set; }
            public void ShowLine(string text, float seconds) { Text = text; Seconds = seconds; LineShown = true; Log.Add("show " + text); }
            public void HideLine() { if (LineShown) Log.Add("hide"); LineShown = false; }
        }

        sealed class SlowBoard : IBoardPresenter
        {
            Action pending;
            public void Rebuild(State s) { pending = null; }
            public void Snap(State s) { pending = null; }
            public void Present(State prev, StepResult r, Action done) { pending = done; }
            public void Finish() { var p = pending; pending = null; p?.Invoke(); }
        }

        // The exit counts on the 7th step: six moves right, the seventh wins.
        static Level Short() => Level.Parse("t", "", 3, new[] { "##########", "#S......E#", "##########" });

        sealed class Rig
        {
            public readonly SlowBoard Board = new SlowBoard();
            public readonly GameSession Session;
            public readonly FakeLine Line = new FakeLine();
            public readonly StoryDirector Director;
            public string Start = "첫째가 먼저 간다";
            public string Finale; // null = not a finale stage
            public bool Covered;
            public int FinaleShown;

            public Rig(bool quiet = false)
            {
                Session = new GameSession(Board);
                Director = new StoryDirector(Session, Line, quiet, () => Start, () => Finale, () => FinaleShown++, () => Covered);
            }

            public void Move(Dir d) { Session.Send(Command.Move(d)); Board.Finish(); }
            /// Six steps, then the winning seventh sent (the board still presents it).
            public void ToTheWin() { for (int k = 0; k < 6; k++) Move(Dir.Right); Session.Send(Command.Move(Dir.Right)); }
        }

        [Test]
        public void SecondsAreAtLeastTwoAndAHalfThenPerCharacter()
        {
            Assert.AreEqual(2.5f, StoryDirector.Seconds("짧다"), 1e-4f);
            Assert.AreEqual(30 * 0.12f, StoryDirector.Seconds(new string('가', 30)), 1e-4f);
        }

        [Test]
        public void StartTextIsTheStoryOrTheHintNeverBoth()
        {
            var t = new LevelTextDto { name = "n", story = "이야기", hint = "힌트" };
            Assert.AreEqual("이야기", StoryDirector.StartText(t, "story"));
            Assert.AreEqual("힌트", StoryDirector.StartText(t, "hint"));
            Assert.AreEqual("", StoryDirector.StartText(t, ""));
            Assert.Throws<ArgumentException>(() => StoryDirector.StartText(t, "both"));
        }

        [Test]
        public void StartLineShowsOnEntryAndGoesAtTheFirstMove()
        {
            var r = new Rig();
            r.Session.Load(Short());
            Assert.IsTrue(r.Line.LineShown);
            Assert.AreEqual("첫째가 먼저 간다", r.Line.Text);
            Assert.AreEqual(2.5f, r.Line.Seconds, 1e-4f);
            r.Session.Send(Command.Move(Dir.Right));
            Assert.IsFalse(r.Line.LineShown, "gone at the first Present");
            r.Board.Finish();
            r.Session.Send(Command.Restart);
            Assert.IsFalse(r.Line.LineShown, "not again on a restart");
        }

        [Test]
        public void UndoAndEmptyStartLinesShowNothing()
        {
            var r = new Rig { Start = "" };
            r.Session.Load(Short());
            Assert.IsFalse(r.Line.LineShown);
            r.Start = "둘째";
            r.Session.Load(Short());
            r.Session.Send(Command.Undo); // nothing to undo: the start line stays
            Assert.IsTrue(r.Line.LineShown);
        }

        [Test]
        public void StartLineWaitsUnderACoverAndShowsWithItsFullTimeAfter()
        {
            var r = new Rig { Covered = true }; // the planet entry card is up
            r.Session.Load(Short());
            Assert.IsFalse(r.Line.LineShown);
            Assert.IsTrue(r.Director.StartWaiting);
            r.Covered = false;
            r.Director.CoverChanged();
            Assert.IsTrue(r.Line.LineShown);

            r.Covered = true; // the menu opens over it
            r.Director.CoverChanged();
            Assert.IsFalse(r.Line.LineShown);
            r.Covered = false;
            r.Director.CoverChanged();
            Assert.IsTrue(r.Line.LineShown, "back after the menu closes");

            r.Line.LineShown = false; // its time ran out
            r.Covered = true;
            r.Director.CoverChanged();
            r.Covered = false;
            r.Director.CoverChanged();
            Assert.IsFalse(r.Line.LineShown, "a line that ran out does not come back");
        }

        [Test]
        public void FinaleLineHeldAtTheWinShownWithTheFinaleGoneWithTheResult()
        {
            var r = new Rig { Finale = "이끼 아래에 첫 길이 있었다" };
            r.Session.Load(Short());
            r.ToTheWin(); // wins; the board presents it
            Assert.IsTrue(r.Director.FinaleHeld);
            Assert.IsFalse(r.Line.LineShown, "held, not shown at the Updated");
            float hold = r.Director.FinaleStarting();
            Assert.AreEqual(StoryDirector.Seconds(r.Finale), hold, 1e-4f);
            Assert.AreEqual(r.Finale, r.Line.Text);
            Assert.AreEqual(1, r.FinaleShown, "counted once shown");
            r.Board.Finish(); // Presented(Win): the result opens
            Assert.IsFalse(r.Line.LineShown);
            Assert.AreEqual(0f, r.Director.FinaleStarting(), "once");
        }

        [Test]
        public void NoFinaleLineOffAFinaleStageAndAHeldOneDropsWithoutTheFinale()
        {
            var r = new Rig();
            r.Session.Load(Short());
            r.ToTheWin();
            r.Board.Finish();
            Assert.AreEqual(0f, r.Director.FinaleStarting());

            r.Finale = "진실";
            r.Session.Load(Short());
            r.ToTheWin();
            Assert.IsTrue(r.Director.FinaleHeld);
            r.Board.Finish();
            Assert.IsFalse(r.Director.FinaleHeld, "Presented without the finale taking it: dropped");
            Assert.AreEqual(0, r.FinaleShown);

            r.Session.Load(Short());
            r.ToTheWin();
            r.Session.Send(Command.Undo); // takes the win back mid-presentation (Snap)
            Assert.IsFalse(r.Director.FinaleHeld, "Undo drops it");
        }

        [Test]
        public void QuietRunsShowNothing()
        {
            var r = new Rig(quiet: true) { Finale = "진실" };
            r.Session.Load(Short());
            Assert.IsFalse(r.Line.LineShown);
            r.ToTheWin();
            Assert.AreEqual(0f, r.Director.FinaleStarting());
            CollectionAssert.IsEmpty(r.Line.Log);
        }

        [Test]
        public void ReplayShowsTheStartLineAgainAfterTheMenuCloses()
        {
            var r = new Rig();
            Assert.IsFalse(r.Director.ReplayStartLine(), "no stage yet");
            r.Session.Load(Short());
            r.Move(Dir.Right);
            Assert.IsFalse(r.Line.LineShown);
            r.Covered = true; // asked from the menu
            Assert.IsTrue(r.Director.ReplayStartLine());
            Assert.IsFalse(r.Line.LineShown, "waits under the menu");
            r.Covered = false;
            r.Director.CoverChanged();
            Assert.IsTrue(r.Line.LineShown);
            Assert.AreEqual("첫째가 먼저 간다", r.Line.Text);
            r.Move(Dir.Right);
            Assert.IsFalse(r.Line.LineShown, "gone at the next move");

            r.Start = "";
            Assert.IsFalse(r.Director.ReplayStartLine(), "a stage without a start line");
            Assert.IsFalse(r.Line.LineShown);

            var quiet = new Rig(quiet: true);
            quiet.Session.Load(Short());
            Assert.IsTrue(quiet.Director.ReplayStartLine(), "the player asked, also in a quiet run");
            Assert.IsTrue(quiet.Line.LineShown);
        }

        [Test]
        public void DisposeStopsListening()
        {
            var r = new Rig();
            r.Director.Dispose();
            r.Session.Load(Short());
            Assert.IsFalse(r.Line.LineShown);
        }
    }
}
