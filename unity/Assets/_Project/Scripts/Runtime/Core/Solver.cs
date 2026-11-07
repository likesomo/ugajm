using System.Collections.Generic;
using System.Text;

namespace SevenSteps.Core
{
    public sealed class Solution
    {
        public List<Act> Path;
        public int BodiesUsed;

        public string Letters()
        {
            var sb = new StringBuilder(Path.Count);
            foreach (var a in Path) sb.Append(Rules.Letter(a));
            return sb.ToString();
        }
    }

    /// BFS over actions (moves and KEEP/SWITCH), same order and pruning as core.js solve().
    public static class Solver
    {
        sealed class Node
        {
            public readonly State State;
            public readonly Node Parent;
            public readonly Act Act;
            public Node(State state, Node parent, Act act) { State = state; Parent = parent; Act = act; }
        }

        public static Solution Solve(Level level, int maxBodies = 0, bool noSwitch = false, int limit = 400000)
        {
            if (maxBodies <= 0) maxBodies = level.Bodies;
            var start = Rules.NewState(level, maxBodies);
            var seen = new HashSet<string> { Rules.Key(start) };
            var frontier = new List<Node> { new Node(start, null, Act.Up) };
            while (frontier.Count > 0 && seen.Count < limit)
            {
                var nextFrontier = new List<Node>();
                foreach (var node in frontier)
                {
                    foreach (var a in Rules.Actions(node.State, noSwitch))
                    {
                        var st = Rules.Apply(node.State, a).State;
                        if (ReferenceEquals(st, node.State) || st.Status == Status.Lose) continue;
                        var child = new Node(st, node, a);
                        if (st.Status == Status.Win)
                            return new Solution { Path = PathOf(child), BodiesUsed = maxBodies - st.Bodies + 1 };
                        if (!seen.Add(Rules.Key(st))) continue;
                        nextFrontier.Add(child);
                    }
                }
                frontier = nextFrontier;
            }
            return null;
        }

        static List<Act> PathOf(Node n)
        {
            var path = new List<Act>();
            for (; n.Parent != null; n = n.Parent) path.Add(n.Act);
            path.Reverse();
            return path;
        }

        /// Fewest bodies that can still clear the level, with the shortest route for that count.
        public static Solution MinBodies(Level level)
        {
            for (int b = 1; b <= level.Bodies; b++)
            {
                var r = Solve(level, b);
                if (r != null) { r.BodiesUsed = b; return r; }
            }
            return null;
        }
    }
}
