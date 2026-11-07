using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>Visual category of a board cell. Append-only: TileSet assets and saved data depend on the order.</summary>
    public enum TileKind { Floor, Wall, Pit, Ice, Cracked, Bridge, Exit, Gate, Plate, Door }

    /// <summary>Things drawn on top of tiles. Append-only.</summary>
    public enum ActorKind { Player, Statue, StartMarker, OldStartMarker, Footprint, LuckyRing }

    /// <summary>
    /// Which visual a cell needs, decided from <see cref="Rules.TileAt"/> (the current tile, not the original one).
    /// Variant: Floor 0/1 checker, Gate 1-7 (the step number), Plate/Door 0-2 (a/A=0, b/B=1, c/C=2), otherwise 0.
    /// This is the only place that turns legend characters into visuals. Views do not switch on chars themselves.
    /// </summary>
    public readonly struct TileId
    {
        public readonly TileKind Kind;
        public readonly int Variant;

        public TileId(TileKind kind, int variant = 0) { Kind = kind; Variant = variant; }

        public static TileId FromChar(char t, int x, int y)
        {
            if (t >= '1' && t <= '7') return new TileId(TileKind.Gate, t - '0');
            if (t >= 'a' && t <= 'c') return new TileId(TileKind.Plate, t - 'a');
            if (t >= 'A' && t <= 'C') return new TileId(TileKind.Door, t - 'A');
            switch (t)
            {
                case '#': return new TileId(TileKind.Wall);
                case 'o': return new TileId(TileKind.Pit);
                case '~': return new TileId(TileKind.Ice);
                case 'x': return new TileId(TileKind.Cracked);
                case '=': return new TileId(TileKind.Bridge);
                case 'E': return new TileId(TileKind.Exit);
                default: return new TileId(TileKind.Floor, (x + y) & 1);
            }
        }

        /// Visual of cell i in state s (bridges and broken floor included).
        public static TileId At(State s, int i) => FromChar(Rules.TileAt(s, i), s.Level.X(i), s.Level.Y(i));

        public bool Equals(TileId o) => Kind == o.Kind && Variant == o.Variant;
        public override string ToString() => $"{Kind}{Variant}";
    }
}
