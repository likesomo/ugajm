using System;
using System.Collections.Generic;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>What a <see cref="SessionUpdate"/> asks listeners to do.</summary>
    public enum SessionUpdateKind
    {
        /// New stage loaded. Rebuild everything. Result is null.
        Rebuild,
        /// Undo, Restart or replay: draw the snapshot State immediately, no animation. Result is the Undo/Restart result, or null for a replay.
        Snap,
        /// A Move/Keep/Switch changed the state. The board animates it; input is locked until it finishes.
        Present,
        /// A command the rules rejected (Bump, or a move while not in Play). State is unchanged; play a short cue, do not lock.
        Feedback,
    }

    public enum SendOutcome { Applied, Queued, Rejected, Ignored }

    public readonly struct SessionUpdate
    {
        public readonly SessionUpdateKind Kind;
        public readonly Command Command;
        public readonly State Prev;
        public readonly State State;
        public readonly StepResult Result;

        public SessionUpdate(SessionUpdateKind kind, Command command, State prev, State state, StepResult result)
        {
            Kind = kind; Command = command; Prev = prev; State = state; Result = result;
        }
    }

    /// <summary>
    /// The one place that owns the current State and the input lock (docs/architecture.md, "입력 락").
    /// - Every state change goes through Rules.Dispatch here. Views and HUD read <see cref="State"/> and <see cref="Updated"/>.
    /// - Move/Keep/Switch while a presentation runs are queued (FIFO, up to <see cref="QueueCapacity"/>; extra presses are dropped)
    ///   and dispatched one by one after each presentation ends. A command that ends the brother (fall, bridge, KEEP,
    ///   SWITCH, the last step: the body count changes) drops what is still queued when its presentation ends: those
    ///   presses were meant for him, not for the next brother (blind QA-2).
    /// - Undo first removes the newest queued command, if any. Otherwise Undo/Restart/Load are never queued: they clear
    ///   the queue, cancel the presentation and Snap to the new snapshot.
    /// Pure C#: no Unity time or coroutines, so it is testable in EditMode with a fake presenter.
    /// </summary>
    public sealed class GameSession
    {
        public const int QueueCapacity = 2;

        readonly IBoardPresenter board;
        readonly List<Command> queue = new List<Command>(QueueCapacity);
        int presentToken;
        /// The presentation running now ends a brother: drop the queue when it finishes.
        bool bodyEnding;

        /// Board may be null (headless: tests, solver tools). Then nothing locks.
        public GameSession(IBoardPresenter board = null) { this.board = board; }

        public State State { get; private set; }
        public Level Level => State?.Level;
        /// True while a Present is running. Move/Keep/Switch sent now are queued.
        public bool IsLocked { get; private set; }
        /// Commands waiting for the current presentation. A presenter may play faster while this is above 0.
        public int QueuedCount => queue.Count;

        /// Raised after State changes (and for rejected commands). Raised before the board starts animating.
        public event Action<SessionUpdate> Updated;

        /// Raised when a Present animation has finished (before queued commands run), with the state now on screen.
        /// Use it for things that must wait for the animation, e.g. opening the result panel after the winning step.
        /// Not raised for cancelled presentations, Snap or Rebuild. Headless sessions (no board) raise it right after Updated.
        public event Action<State> Presented;

        public void Load(Level level)
        {
            Cancel();
            var prev = State;
            State = Rules.NewState(level);
            board?.Rebuild(State);
            Raise(new SessionUpdate(SessionUpdateKind.Rebuild, default, prev, State, null));
        }

        public SendOutcome Send(Command cmd)
        {
            if (State == null) return SendOutcome.Ignored;
            if (cmd.Type == CommandType.Undo && queue.Count > 0)
            {
                // Undo takes back the newest input first: a queued move that has not happened yet.
                queue.RemoveAt(queue.Count - 1);
                return SendOutcome.Applied;
            }
            if (cmd.Type == CommandType.Undo || cmd.Type == CommandType.Restart) return Interrupt(cmd);
            if (IsLocked)
            {
                if (queue.Count >= QueueCapacity) return SendOutcome.Ignored;
                queue.Add(cmd);
                return SendOutcome.Queued;
            }
            return Apply(cmd);
        }

        /// Apply replay letters (U D L R K X) at once and Snap to the result. Unknown letters are skipped.
        public void Replay(string letters)
        {
            if (State == null || string.IsNullOrEmpty(letters)) return;
            Cancel();
            var prev = State;
            foreach (char c in letters)
                if (Rules.TryParseLetter(c, out var act)) State = Rules.Dispatch(State, Command.FromAct(act)).State;
            board?.Snap(State);
            Raise(new SessionUpdate(SessionUpdateKind.Snap, default, prev, State, null));
        }

        SendOutcome Interrupt(Command cmd)
        {
            var r = Rules.Dispatch(State, cmd);
            if (ReferenceEquals(r.State, State)) return SendOutcome.Ignored; // Undo with nothing to undo
            Cancel();
            var prev = State;
            State = r.State;
            board?.Snap(State);
            Raise(new SessionUpdate(SessionUpdateKind.Snap, cmd, prev, State, r));
            return SendOutcome.Applied;
        }

        SendOutcome Apply(Command cmd)
        {
            var prev = State;
            var r = Rules.Dispatch(prev, cmd);
            if (ReferenceEquals(r.State, prev))
            {
                Raise(new SessionUpdate(SessionUpdateKind.Feedback, cmd, prev, prev, r));
                return SendOutcome.Rejected;
            }
            State = r.State;
            bool endsBody = State.Bodies != prev.Bodies;
            if (endsBody) queue.Clear(); // pressed for this brother before his last step
            Raise(new SessionUpdate(SessionUpdateKind.Present, cmd, prev, State, r));
            if (board != null)
            {
                bodyEnding = endsBody;
                IsLocked = true;
                int token = ++presentToken;
                board.Present(prev, r, () => OnPresented(token));
            }
            else Presented?.Invoke(State);
            return SendOutcome.Applied;
        }

        void OnPresented(int token)
        {
            if (token != presentToken || !IsLocked) return; // stale callback of a cancelled presentation
            IsLocked = false;
            if (bodyEnding) queue.Clear(); // pressed while he fell / turned to stone: not the next brother's moves
            bodyEnding = false;
            Presented?.Invoke(State);
            while (!IsLocked && queue.Count > 0)
            {
                var next = queue[0];
                queue.RemoveAt(0);
                Apply(next);
            }
        }

        void Cancel()
        {
            queue.Clear();
            IsLocked = false;
            bodyEnding = false;
            presentToken++;
        }

        void Raise(SessionUpdate u) => Updated?.Invoke(u);
    }
}
