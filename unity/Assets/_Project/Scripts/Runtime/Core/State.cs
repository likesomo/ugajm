using System.Collections.Generic;

namespace SevenSteps.Core
{
    public enum Dir { Up, Down, Left, Right }

    /// Solver/replay action. Order matters: the BFS walks moves in this order, same as core.js.
    public enum Act { Up, Down, Left, Right, Keep, Switch }

    public enum Status { Play, Choose, Win, Lose }

    public enum GameEventType { Bump, Step, Crumble, Bridge, Fall, Win, Seal, Vanish, Choose, Stone, Switch, Respawn, Lose, Undo, Restart }

    public readonly struct GameEvent
    {
        public readonly GameEventType Type;
        public readonly int At;
        public readonly int StepIdx;
        public GameEvent(GameEventType type, int at = -1, int stepIdx = 0) { Type = type; At = at; StepIdx = stepIdx; }
        public override string ToString() => $"{Type}@{At}";
    }

    public readonly struct Footprint
    {
        public readonly int Cell;
        public readonly int Step;
        public Footprint(int cell, int step) { Cell = cell; Step = step; }
    }

    public enum CommandType { Move, Keep, Switch, Undo, Restart }

    public readonly struct Command
    {
        public readonly CommandType Type;
        public readonly Dir Dir;
        Command(CommandType type, Dir dir) { Type = type; Dir = dir; }
        public static Command Move(Dir d) => new Command(CommandType.Move, d);
        public static readonly Command Keep = new Command(CommandType.Keep, Dir.Up);
        public static readonly Command Switch = new Command(CommandType.Switch, Dir.Up);
        public static readonly Command Undo = new Command(CommandType.Undo, Dir.Up);
        public static readonly Command Restart = new Command(CommandType.Restart, Dir.Up);

        public static Command FromAct(Act a) => a switch
        {
            Act.Keep => Keep,
            Act.Switch => Switch,
            _ => Move((Dir)(int)a),
        };
    }

    /// <summary>
    /// Game state. Treated as immutable: rules clone it before changing anything, so old states stay valid for undo.
    /// </summary>
    public sealed class State
    {
        public Level Level { get; internal set; }
        /// Where the next body wakes up (moved by SWITCH).
        public int Start { get; internal set; }
        public int Pos { get; internal set; }
        /// Steps this body has taken (0..7).
        public int Used { get; internal set; }
        /// Bodies left, including the current one.
        public int Bodies { get; internal set; }
        public int[] Statues { get; internal set; }
        public int[] Bridges { get; internal set; }
        public int[] Broken { get; internal set; }
        public Footprint[] Trail { get; internal set; }
        public Status Status { get; internal set; }
        /// Undo chain (set by Rules.Dispatch only).
        public State Prev { get; internal set; }

        internal State Clone() => (State)MemberwiseClone();
    }

    public sealed class StepResult
    {
        public State State;
        public List<GameEvent> Events;
        /// Cells walked through by this command, in order (ice slides included). Empty for non-moves.
        public int[] Path;

        public StepResult(State state, List<GameEvent> events, int[] path = null)
        {
            State = state; Events = events; Path = path ?? new int[0];
        }

        public bool Has(GameEventType t)
        {
            foreach (var e in Events) if (e.Type == t) return true;
            return false;
        }
    }
}
