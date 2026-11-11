using System;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game.Story
{
    /// <summary>The one passing line in the game (story-integration.md 0.2 "잠깐 줄"): a View that shows a line for a time and hides it.</summary>
    public interface ITransientLine
    {
        /// Shows <paramref name="text"/> (not empty) for <paramref name="seconds"/>, replacing a line already up.
        void ShowLine(string text, float seconds);
        void HideLine();
        /// A line is up (false once its time ran out).
        bool LineShown { get; }
    }

    /// <summary>
    /// The board's story lines (docs/architecture.md 16.7 / 16.8, story-integration.md 0.1 - 0.3, 4.3, 5.2). The minimal UI
    /// keeps two, on one passing line (<see cref="ITransientLine"/>):
    /// <list type="bullet">
    /// <item>The start line: on entering a stage (Rebuild), the stage's story or hint as its <c>startLine</c> picks (one of
    /// them, never both), gone at the first Present (a move, KEEP or SWITCH), an Undo / Restart (Snap), the next stage or
    /// after its time. Not again on a restart. While an overlay covers the board (the planet entry card, the menu) it waits
    /// and starts its time when uncovered (<see cref="CoverChanged"/>).</item>
    /// <item>The finale line: a finale stage won (Updated Present Win) holds the planet's truth, which shows when the board's
    /// constellation finale starts (<see cref="FinaleStarting"/>, the board keeps the finale up for the line's time, skippable)
    /// and goes when the result opens (Presented Win).</item>
    /// </list>
    /// Moment lines (KEEP, SWITCH, bridges ...) and the first-seventh line are out of the jam (0.1): the star trail says them.
    /// Quiet runs (replay, smoke, flow / title checks, art perf) show nothing. Pure C#: the scene root builds it and hands
    /// the texts in as functions (UI language at the time of showing).
    /// </summary>
    public sealed class StoryDirector : IDisposable
    {
        /// Shortest time a line stays (story-integration.md 0.2).
        public const float MinSeconds = 2.5f;
        /// Reading time per character (0.2: max(2.5 s, characters x 0.12 s)).
        public const float SecondsPerChar = 0.12f;

        readonly GameSession session;
        readonly ITransientLine view;
        readonly bool quiet;
        readonly Func<string> startLine, finaleLine;
        readonly Func<bool> covered;
        readonly Action finaleShown;
        string held, waitingStart;
        bool startUp;

        /// <param name="startLine">The current stage's start line ("" = none, <see cref="StartText"/>).</param>
        /// <param name="finaleLine">The current stage's finale line, null or "" when it is not a finale stage or the story is off.</param>
        /// <param name="finaleShown">Called once the finale line is on screen (count it as shown, StoryProgress.MarkShown).</param>
        /// <param name="covered">An overlay covers the board now (the start line waits).</param>
        public StoryDirector(GameSession session, ITransientLine view, bool quiet, Func<string> startLine, Func<string> finaleLine, Action finaleShown,
            Func<bool> covered)
        {
            this.covered = covered ?? throw new ArgumentNullException(nameof(covered));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.quiet = quiet;
            this.startLine = startLine ?? throw new ArgumentNullException(nameof(startLine));
            this.finaleLine = finaleLine ?? throw new ArgumentNullException(nameof(finaleLine));
            this.finaleShown = finaleShown ?? throw new ArgumentNullException(nameof(finaleShown));
            session.Updated += OnUpdated;
            session.Presented += OnPresented;
        }

        public void Dispose()
        {
            session.Updated -= OnUpdated;
            session.Presented -= OnPresented;
        }

        /// How long a line stays: max(2.5 s, characters x 0.12 s).
        public static float Seconds(string text) => Math.Max(MinSeconds, (text ?? "").Length * SecondsPerChar);

        /// The start line a stage picked (LevelDto.StartLine, 16.7): its story, its hint, or "" for none.
        public static string StartText(LevelTextDto text, string startLine)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            switch (startLine)
            {
                case "story": return text.story ?? "";
                case "hint": return text.hint ?? "";
                case "": return "";
                default: throw new ArgumentException($"startLine \"{startLine}\" is not story, hint or empty", nameof(startLine));
            }
        }

        /// A finale line is waiting for the board's finale (a finale stage just won).
        public bool FinaleHeld => held != null;

        /// The start line waits for the board to be uncovered.
        public bool StartWaiting => waitingStart != null;

        /// Menu "다시 보기" (story-integration.md 0.2): the stage's start line again, with its full time; it waits while the
        /// menu still covers the board and goes at the next move like the first time. False when the stage has none
        /// (startLine "" or an empty text) or no stage is loaded; then nothing changes. Also in quiet runs: the player asked.
        public bool ReplayStartLine()
        {
            if (session.State == null) return false;
            string start = startLine() ?? "";
            if (start.Length == 0) return false;
            StartGone();
            waitingStart = start;
            CoverChanged();
            return true;
        }

        /// Overlays opened or closed: a start line on screen goes back to waiting under a cover, a waiting one shows when uncovered.
        public void CoverChanged()
        {
            if (covered())
            {
                if (!startUp) return;
                startUp = false;
                if (view.LineShown) view.HideLine(); // shows again, with its full time, once uncovered
                else waitingStart = null; // its time ran out: done
            }
            else if (waitingStart != null)
            {
                string line = waitingStart;
                view.ShowLine(line, Seconds(line));
                startUp = true;
            }
        }

        /// The board's constellation finale starts: the held finale line shows. Returns how long the board should keep the
        /// finale up for it (0 = no line, the finale as it is).
        public float FinaleStarting()
        {
            if (held == null) return 0f;
            string line = held;
            held = null;
            StartGone();
            view.ShowLine(line, Seconds(line));
            finaleShown();
            return Seconds(line);
        }

        void OnUpdated(SessionUpdate u)
        {
            switch (u.Kind)
            {
                case SessionUpdateKind.Rebuild:
                    held = null;
                    StartGone();
                    if (quiet) break;
                    string start = startLine() ?? "";
                    if (start.Length == 0) break;
                    waitingStart = start;
                    CoverChanged();
                    break;
                case SessionUpdateKind.Snap:
                    held = null;
                    StartGone();
                    break;
                case SessionUpdateKind.Present:
                    StartGone();
                    held = null;
                    if (!quiet && u.State.Status == Status.Win)
                    {
                        string f = finaleLine();
                        if (!string.IsNullOrEmpty(f)) held = f;
                    }
                    break;
            }
        }

        void OnPresented(State s)
        {
            if (s.Status != Status.Win) return;
            held = null; // the finale did not take it (no finale ran): the star map keeps the truth
            StartGone(); // the result opens now
        }

        void StartGone()
        {
            waitingStart = null;
            startUp = false;
            view.HideLine();
        }
    }
}
