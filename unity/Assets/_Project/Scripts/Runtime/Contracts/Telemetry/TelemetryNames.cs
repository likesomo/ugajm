namespace SevenSteps.Contracts
{
    /// <summary>String constants of docs/telemetry.md v1. Use these instead of literals so a schema change is one edit.</summary>
    public static class TelemetryNames
    {
        public static class Types
        {
            public const string SessionStart = "session_start";
            public const string LevelStart = "level_start";
            public const string Move = "move";
            public const string SeventhStep = "seventh_step";
            public const string Keep = "keep";
            public const string Switch = "switch";
            public const string Undo = "undo";
            public const string Restart = "restart";
            public const string Hint = "hint";
            public const string BodyLost = "body_lost";
            public const string LevelFail = "level_fail";
            public const string LevelClear = "level_clear";
            public const string LevelLeave = "level_leave";
            public const string Idle = "idle";
            public const string SessionEnd = "session_end";
        }

        /// level_start.via
        public static class Via
        {
            public const string Boot = "boot", Url = "url", Next = "next", Select = "select", Again = "again";
        }

        /// move.dir
        public static class Dir
        {
            public const string Up = "up", Down = "down", Left = "left", Right = "right";
        }

        /// move.result
        public static class MoveResult
        {
            public const string Moved = "moved", Blocked = "blocked", Slid = "slid", Fell = "fell", Bridged = "bridged", Door = "door", Gate = "gate";
        }

        /// seventh_step.effect
        public static class Effect
        {
            public const string Win = "win", Bridge = "bridge", Seal = "seal", Vanish = "vanish", Choose = "choose", Statue = "statue";
        }

        /// body_lost.reason
        public static class LostReason
        {
            public const string Fell = "fell", Vanish = "vanish", Statue = "statue", Switch = "switch", Bridge = "bridge", Seal = "seal";
        }

        /// session_end.reason
        public static class EndReason
        {
            public const string Hidden = "hidden", Unload = "unload";
        }

        /// undo.status / idle.status
        public static class Status
        {
            public const string Play = "play", Choose = "choose", Lose = "lose", Win = "win";
        }

        /// tile kinds (section 2) plus blockedBy extras
        public static class Tile
        {
            public const string Floor = "floor", Exit = "exit", Pit = "pit", Ice = "ice", Cracked = "cracked", Bridge = "bridge",
                Plate = "plate", Door = "door", Gate = "gate", Wall = "wall", Start = "start", Statue = "statue", Edge = "edge";
        }

        /// Idle threshold (telemetry.js IDLE_MS).
        public const int IdleMs = 30000;
    }
}
