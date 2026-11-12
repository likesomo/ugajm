using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game.Flow
{
    /// <summary>The board as a tutorial run sees it (BoardController; a fake on a headless GameSession in tests).</summary>
    public interface ITutorialBoard
    {
        /// Loads tutorial board <paramref name="index"/> (LevelCatalog.Tutorials): a new attempt, Rebuild.
        void LoadTutorial(int index);
        State State { get; }
        void Restart();
        /// The cue's cell ring (docs/tutorial.md 2.3); -1 = none.
        void Highlight(int cell);
        /// Every session update (TutorialCues.Match reads the applied command and its events).
        event Action<SessionUpdate> Updated;
        /// The win presentation (walk to the exit) finished.
        event Action<State> WinPresented;
        /// The board became Lose.
        event Action<State> Lost;
    }

    /// <summary>The tutorial guide as the run sees it: one cue line, the board's place in the run, a skip icon.</summary>
    public interface ITutorialCueView
    {
        /// <paramref name="line"/>: the cue's words in the current language, "" = none (the plate hides).
        void ShowCue(string line, int board, int boards);
        void Show();
        void Hide();
        /// The skip icon (Esc reaches the run through the scene root).
        event Action SkipRequested;
    }

    /// <summary>
    /// Plays the tutorial boards T1..T3 (docs/tutorial.md 2.4) on the Game scene's board. Pure C#: the scene root
    /// (GameController) calls <see cref="Begin"/> when AppFlow enters a board, and the callbacks move the flow.
    /// - Cues: only Core TutorialCues decides which line shows (start, each applied command, restart); -1 keeps the line.
    ///   The cue's cell gets the gold ring.
    /// - Lose: the lose cue shows, then after <see cref="LoseSeconds"/> the board restarts by itself (the restart cue).
    /// - Win: the board is saved as done (TutorialBoards.SaveId through <c>markDone</c>), the win cue stays up for
    ///   <see cref="WinSeconds"/>, then the next board (<c>enterBoard</c>) or the end.
    /// - Skip (icon or Esc): the end at once; boards skipped are not saved as done.
    /// - The end: <c>finished</c> (the scene root goes to stage 1 on a new game, else to the map).
    /// No progress, stars or level_* telemetry here or on the board (docs/tutorial.md 2.5, 2.6).
    /// </summary>
    public sealed class TutorialRunController : IDisposable
    {
        public const float LoseSeconds = 1.2f, WinSeconds = 1.8f;

        readonly IReadOnlyList<TutorialDto> boards;
        readonly ITutorialBoard board;
        readonly ITutorialCueView view;
        readonly Func<Language> language;
        readonly Action<string> markDone;
        readonly Action<float, Action> after;
        readonly Action<int> enterBoard;
        readonly Action<bool> finished;
        int generation; // delayed actions from an earlier board or run do nothing

        /// <param name="markDone">Saves a finished board (SaveSystem.MarkTutorialSeen of TutorialBoards.SaveId).</param>
        /// <param name="after">Runs an action after some seconds (the scene root's coroutine; tests run it at once or hold it).</param>
        /// <param name="enterBoard">The next board: the scene root moves AppFlow there and calls Begin.</param>
        /// <param name="finished">The run is over (true = skipped).</param>
        public TutorialRunController(IReadOnlyList<TutorialDto> boards, ITutorialBoard board, ITutorialCueView view, Func<Language> language,
            Action<string> markDone, Action<float, Action> after, Action<int> enterBoard, Action<bool> finished)
        {
            this.boards = boards ?? throw new ArgumentNullException(nameof(boards));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.language = language ?? throw new ArgumentNullException(nameof(language));
            this.markDone = markDone ?? throw new ArgumentNullException(nameof(markDone));
            this.after = after ?? throw new ArgumentNullException(nameof(after));
            this.enterBoard = enterBoard ?? throw new ArgumentNullException(nameof(enterBoard));
            this.finished = finished ?? throw new ArgumentNullException(nameof(finished));
            board.Updated += OnUpdated;
            board.WinPresented += OnWinPresented;
            board.Lost += OnLost;
            view.SkipRequested += Skip;
        }

        public void Dispose()
        {
            board.Updated -= OnUpdated;
            board.WinPresented -= OnWinPresented;
            board.Lost -= OnLost;
            view.SkipRequested -= Skip;
        }

        /// 0-based board being played, -1 when no run is on.
        public int Index { get; private set; } = -1;
        public bool Active => Index >= 0;
        /// The cue on screen (index into the board's cues), -1 = none.
        public int Cue { get; private set; } = -1;
        /// The board just won and waits for the next one.
        public bool Won { get; private set; }

        /// Plays board <paramref name="index"/> from its start cue (AppFlow is already on it).
        public void Begin(int index)
        {
            if (index < 0 || index >= boards.Count) throw new ArgumentOutOfRangeException(nameof(index), index, $"tutorial board must be 0..{boards.Count - 1}");
            generation++;
            Index = index;
            Won = false;
            board.LoadTutorial(index);
            view.Show();
            ShowCue(TutorialCues.Start(boards[index]));
        }

        /// Skip icon / Esc: the run ends here; the board on screen is not saved as done.
        public void Skip()
        {
            if (Active) End(true);
        }

        /// The language changed: the same cue in the new words.
        public void Redraw()
        {
            if (Active) ShowCue(Cue);
        }

        /// The cue's line in the current language ("" for none).
        public string Line => Active && Cue >= 0 ? boards[Index].cues[Cue].Line(Languages.Code(language())) : "";

        void OnUpdated(SessionUpdate u)
        {
            if (!Active || Won) return;
            if (u.Kind == SessionUpdateKind.Rebuild || u.Result == null) return; // a load, or a replay snap
            int c = TutorialCues.Match(boards[Index], u.Command, u.Prev, u.State, u.Result.Events);
            if (c >= 0) ShowCue(c);
        }

        void OnLost(State s)
        {
            if (!Active || Won) return;
            int g = generation;
            after(LoseSeconds, () =>
            {
                if (g == generation && Active && board.State != null && board.State.Status == Status.Lose) board.Restart();
            });
        }

        void OnWinPresented(State s)
        {
            if (!Active || Won) return;
            Won = true;
            markDone(TutorialBoards.SaveId(boards[Index]));
            int g = generation;
            after(WinSeconds, () =>
            {
                if (g != generation || !Active) return;
                if (Index + 1 < boards.Count) enterBoard(Index + 1);
                else End(false);
            });
        }

        void ShowCue(int cue)
        {
            var t = boards[Index];
            Cue = cue;
            var c = cue >= 0 ? t.cues[cue] : null;
            board.Highlight(c != null && c.HasCell ? c.Y * board.State.Level.W + c.X : -1);
            view.ShowCue(c != null ? c.Line(Languages.Code(language())) : "", Index, boards.Count);
        }

        void End(bool skipped)
        {
            generation++;
            Index = -1;
            Cue = -1;
            Won = false;
            board.Highlight(-1);
            view.Hide();
            finished(skipped);
        }

        /// Where the run leads (docs/tutorial.md 2.4): a new game (no stage cleared) goes on to stage 1, a replay goes back
        /// to the map.
        public static bool ToFirstStage(int stagesCleared) => stagesCleared == 0;

        /// The new game takes the tutorial (docs/tutorial.md 2.5): there are boards, one is not done yet and no stage is cleared.
        public static bool OnNewGame(IReadOnlyList<TutorialDto> boards, Func<string, bool> seen, int stagesCleared) =>
            boards != null && boards.Count > 0 && stagesCleared == 0 && !TutorialBoards.AllDone(boards, seen);
    }
}
