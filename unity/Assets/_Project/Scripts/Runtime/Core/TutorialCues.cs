using System;
using System.Collections.Generic;

namespace SevenSteps.Core
{
    /// <summary>
    /// Which tutorial guide line a command shows (docs/tutorial.md 2.3). The screen only shows the returned cue's line
    /// and cell; it never matches triggers itself. -1 = keep the current line.
    /// Match rule: walk the command's events in order and, for each event, the cues in list order; the last cue that
    /// matches wins. A cue may match again later (the same guide for each brother).
    /// </summary>
    public static class TutorialCues
    {
        public const string OnStart = "start", OnRestart = "restart", OnStep = "step", OnSeventh = "seventh",
            OnChoose = "choose", OnKeep = "keep", OnSwitch = "switch", OnDoorOpen = "doorOpen",
            OnRespawn = "respawn", OnWin = "win", OnLose = "lose", OnDoorShut = "doorShut";

        public static readonly string[] Ons =
            { OnStart, OnRestart, OnStep, OnSeventh, OnChoose, OnKeep, OnSwitch, OnDoorOpen, OnRespawn, OnWin, OnLose, OnDoorShut };

        public static bool IsValidOn(string on) => Array.IndexOf(Ons, on) >= 0;

        /// The board was opened: the last "start" cue, -1 when there is none.
        public static int Start(TutorialDto t) => Last(t, OnStart);

        /// After a Restart (also the automatic one after Lose): the last "restart" cue, else Start(t).
        public static int Restart(TutorialDto t)
        {
            int i = Last(t, OnRestart);
            return i >= 0 ? i : Start(t);
        }

        /// The cue for one dispatched command: before = the state it was applied to, after = the resulting state,
        /// events = what Rules.Dispatch returned. Undo, a command that changed nothing and a Bump return -1, except a Bump
        /// into a closed door (doorShut).
        public static int Match(TutorialDto t, Command cmd, State before, State after, IReadOnlyList<GameEvent> events)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (cmd.Type == CommandType.Restart) return Restart(t);
            if (cmd.Type == CommandType.Undo || events == null || t.cues == null) return -1;

            int body = Stars.BodiesUsed(before);
            int woke = Stars.BodiesUsed(after);
            bool move = cmd.Type == CommandType.Move;
            int found = -1;
            foreach (var e in events)
            {
                for (int i = 0; i < t.cues.Length; i++)
                {
                    var c = t.cues[i];
                    if (c == null) continue;
                    int who = e.Type == GameEventType.Respawn ? woke : body;
                    if (c.body != 0 && c.body != who) continue;
                    if (Fires(c, e, cmd, move, before)) found = i;
                }
            }
            return found;
        }

        static bool Fires(TutorialCueDto c, GameEvent e, Command cmd, bool move, State before)
        {
            switch (c.on)
            {
                case OnStep: return e.Type == GameEventType.Step && e.StepIdx == c.n;
                // Only a 7th step makes these events from a move (Stone from a move = the last body turning to stone).
                case OnSeventh:
                    return move && (e.Type == GameEventType.Win || e.Type == GameEventType.Choose || e.Type == GameEventType.Stone
                        || e.Type == GameEventType.Vanish || e.Type == GameEventType.Bridge || e.Type == GameEventType.Seal);
                case OnChoose: return e.Type == GameEventType.Choose;
                case OnKeep: return cmd.Type == CommandType.Keep && e.Type == GameEventType.Stone;
                case OnSwitch: return cmd.Type == CommandType.Switch && e.Type == GameEventType.Switch;
                case OnDoorOpen: return e.Type == GameEventType.DoorOpen;
                case OnRespawn: return e.Type == GameEventType.Respawn;
                case OnWin: return e.Type == GameEventType.Win;
                case OnLose: return e.Type == GameEventType.Lose;
                // A move refused by a door that is shut now (the plate is not held). Other bumps keep the line.
                case OnDoorShut: return move && e.Type == GameEventType.Bump && ClosedDoor(before, e.At);
                default: return false; // start / restart are not event triggers
            }
        }

        static bool ClosedDoor(State s, int i)
        {
            if (i < 0 || i >= s.Level.Count) return false;
            char t = Rules.TileAt(s, i);
            return Rules.IsDoor(t) && !Rules.DoorOpen(s, t);
        }

        static int Last(TutorialDto t, string on)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int found = -1;
            if (t.cues != null)
                for (int i = 0; i < t.cues.Length; i++)
                    if (t.cues[i] != null && t.cues[i].on == on) found = i;
            return found;
        }
    }
}
