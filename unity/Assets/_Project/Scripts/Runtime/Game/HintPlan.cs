using System;
using System.Collections.Generic;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// <summary>
    /// Switches for the staged hint (QA-1 (A), contract review 2026-11-11-staged-hint-contract.md). One place, so the user
    /// decision on the star rule and the backend's telemetry fields each flip one line.
    /// </summary>
    public static class HintRules
    {
        /// Star rule (docs 6.5, user decision pending): false = every hint tier opened counts as a hint (★2 lost, as before
        /// the staged hint); true = tier 1 (the principle, the level's hint text) is free, tiers 2+ count.
        public static readonly bool FirstTierFree = false;

        /// The telemetry hint event's optional tier / tiers fields (docs/telemetry.md, backend schema and goldens). Off
        /// until the backend has them: the event stays as it is now.
        public static readonly bool TelemetryTier = false;

        /// Opening <paramref name="tier"/> costs the ★2 (AttemptCounts.WithHint, telemetry hints).
        public static bool Costs(int tier) => tier >= 2 || !FirstTierFree;

        /// Blind QA-2: a hint capped the attempt at 1 star without a word. The first H that would cost in an attempt only
        /// says so ("hint.confirm"); the next H opens the tier. True = this press shows the warning and opens nothing.
        /// <param name="costs">The tier this press would open costs (HintSteps.NextCosts; always for a stage without a route).</param>
        /// <param name="hintsSoFar">Hints already counted in this attempt (AttemptCounts.Hints).</param>
        /// <param name="warned">The warning was shown in this attempt.</param>
        public static bool AsksFirst(bool costs, int hintsSoFar, bool warned) => costs && hintsSoFar == 0 && !warned;
    }

    /// <summary>How a body's run ends in the stored solution.</summary>
    /// <summary>
    /// Hints unlock by tries (user request 2026-11-14): the hint stays hidden until the player has failed or restarted
    /// the stage <see cref="Step"/> times, then each further <see cref="Step"/> tries opens the next of <see cref="Tiers"/>
    /// tiers: 1 = a direction nudge in words (how many brothers, KEEP / SWITCH, the mechanic; no squares), 2 = where the
    /// walking brother's 7th step lands (the gold ring), 3 = the full route. Opening any tier caps the try at 1 star.
    /// </summary>
    public static class HintGate
    {
        public const int Step = 3, Tiers = 3;

        /// Tiers open after <paramref name="tries"/> failed or restarted tries: 0 (hidden) .. 3.
        public static int Unlocked(int tries) => tries < 0 ? 0 : Math.Min(Tiers, tries / Step);

        /// Tries still needed before the next tier, 0 when every tier is open.
        public static int TriesToNext(int tries) => Unlocked(tries) >= Tiers ? 0 : Step * (Unlocked(tries) + 1) - Math.Max(0, tries);
    }

    public enum HintGoalKind { Keep, Switch, Bridge, Seal, Stone, Vanish, Fall }

    /// <summary>One body's goal in the stored solution: where its run ends and how.</summary>
    public readonly struct HintGoal
    {
        /// 0-based, in the order the bodies walk.
        public int Body { get; }
        public int Cell { get; }
        public HintGoalKind Kind { get; }
        /// The 7th step there pulls the lever (flips the walls).
        public bool Lever { get; }

        public HintGoal(int body, int cell, HintGoalKind kind, bool lever)
        {
            Body = body; Cell = cell; Kind = kind; Lever = lever;
        }

        public override string ToString() => $"body {Body} cell {Cell} {Kind}{(Lever ? " lever" : "")}";
    }

    /// <summary>
    /// The staged hint of one stage, from its stored solution (levels.json expected.path, the ★3 route; contract review
    /// 2026-11-11 (3)): tier 1 the principle (the level's hint text), then one tier per body whose run ends before the
    /// exit (where and how: KEEP, SWITCH, bridge, seal ...), then the whole route. Tiers are absolute: 1 + goals + 1.
    /// Built by following the solution with Rules.Dispatch (the rules are not re-implemented, docs 4 "Dispatch 하나"), no
    /// search at runtime. Also maps each state on the route to the body walking there, so a player still on the route
    /// gets the next body's goal and one off it gets them from the start. Pure C#.
    /// </summary>
    public sealed class HintPlan
    {
        public IReadOnlyList<HintGoal> Goals { get; }
        /// The route as replay letters (U D L R K X).
        public string Path { get; }
        /// 1 (principle) + goals + 1 (route).
        public int Tiers => Goals.Count + 2;
        public int RouteTier => Tiers;

        readonly Dictionary<string, int> bodyAt; // Rules.Key of a state on the route → goals already reached there

        HintPlan(List<HintGoal> goals, string path, Dictionary<string, int> bodyAt)
        {
            Goals = goals;
            Path = path;
            this.bodyAt = bodyAt;
        }

        /// <summary>The tier that shows goal <paramref name="goal"/> (0-based).</summary>
        public static int GoalTier(int goal) => goal + 2;

        /// Goals already reached when the game is in <paramref name="s"/>, if <paramref name="s"/> is a state on the route
        /// (Rules.Key); false when the player is off the route.
        public bool TryOnRoute(State s, out int reached)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return bodyAt.TryGetValue(Rules.Key(s), out reached);
        }

        /// <summary>Follows <paramref name="path"/> (replay letters) from the stage start. Throws when a letter is not a
        /// command or the route does not win (bad data: verify catches it before a build).</summary>
        public static HintPlan From(Level level, string path)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException($"stage \"{level.Name}\": no solution path", nameof(path));
            var goals = new List<HintGoal>();
            var bodyAt = new Dictionary<string, int>();
            var s = Rules.NewState(level);
            bodyAt[Rules.Key(s)] = 0;
            int choosing = -1; // index in goals of a Choose waiting for its K / X
            for (int i = 0; i < path.Length; i++)
            {
                if (!Rules.TryParseLetter(path[i], out var act))
                    throw new ArgumentException($"stage \"{level.Name}\": '{path[i]}' at {i} in \"{path}\" is not a command", nameof(path));
                var cmd = Command.FromAct(act);
                var r = Rules.Dispatch(s, cmd);
                if (ReferenceEquals(r.State, s))
                    throw new ArgumentException($"stage \"{level.Name}\": '{path[i]}' at {i} in \"{path}\" does nothing", nameof(path));
                if (choosing >= 0)
                {
                    if (cmd.Type != CommandType.Keep && cmd.Type != CommandType.Switch)
                        throw new ArgumentException($"stage \"{level.Name}\": a move at {i} while choosing", nameof(path));
                    var g = goals[choosing];
                    goals[choosing] = new HintGoal(g.Body, g.Cell, cmd.Type == CommandType.Keep ? HintGoalKind.Keep : HintGoalKind.Switch, g.Lever);
                    choosing = -1;
                }
                bool lever = r.Has(GameEventType.Flip);
                foreach (var e in r.Events)
                {
                    HintGoalKind? kind = e.Type switch
                    {
                        GameEventType.Choose => HintGoalKind.Keep, // K or X decided by the next letter
                        GameEventType.Bridge => HintGoalKind.Bridge,
                        GameEventType.Seal => HintGoalKind.Seal,
                        GameEventType.Vanish => HintGoalKind.Vanish,
                        GameEventType.Fall => HintGoalKind.Fall,
                        GameEventType.Stone when cmd.Type == CommandType.Move => HintGoalKind.Stone, // a lone body's automatic statue
                        _ => (HintGoalKind?)null,
                    };
                    if (kind == null) continue;
                    if (e.Type == GameEventType.Choose) choosing = goals.Count;
                    goals.Add(new HintGoal(goals.Count, e.At, kind.Value, lever));
                }
                s = r.State;
                if (choosing < 0 && !bodyAt.ContainsKey(Rules.Key(s))) bodyAt[Rules.Key(s)] = goals.Count;
                if (s.Status == Status.Win)
                {
                    if (i != path.Length - 1) throw new ArgumentException($"stage \"{level.Name}\": won at {i}, before the end of \"{path}\"", nameof(path));
                    return new HintPlan(goals, path, bodyAt);
                }
            }
            throw new ArgumentException($"stage \"{level.Name}\": \"{path}\" does not reach the exit", nameof(path));
        }
    }

    /// <summary>What one press of H opened.</summary>
    public readonly struct HintTier
    {
        public enum Shows { Principle, Goal, Route }

        /// Absolute tier, 1..plan.Tiers.
        public int Tier { get; }
        public int Tiers { get; }
        public Shows What { get; }
        /// Valid when <see cref="What"/> is Goal.
        public HintGoal Goal { get; }
        /// The player is off the stored route: the goal / route is the one from the stage start.
        public bool FromStart { get; }
        /// This opening costs the ★2 (HintRules.Costs).
        public bool Costs { get; }

        public HintTier(int tier, int tiers, Shows what, HintGoal goal, bool fromStart, bool costs)
        {
            Tier = tier; Tiers = tiers; What = what; Goal = goal; FromStart = fromStart; Costs = costs;
        }
    }

    /// <summary>
    /// The staged hint through one attempt (contract review (3)): each H opens the next tier, opened tiers stay through
    /// Undo, a new attempt (LoadStage) starts again. Tier 1 is skipped when the stage has no hint text or already showed
    /// it as its start line (startLine "hint"). A player on the route gets the goal of the body walking now (goals
    /// behind are skipped); off the route the goals go on in order, marked from the start. After the route, H shows the
    /// route again. Pure C#.
    /// </summary>
    public sealed class HintSteps
    {
        readonly HintPlan plan;
        readonly bool principle;
        int opened; // highest tier opened, 0 = none
        int nextGoal;

        /// <param name="principle">Tier 1 exists: the stage has a hint text and did not start with it.</param>
        public HintSteps(HintPlan plan, bool principle)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.principle = principle;
        }

        public HintPlan Plan => plan;
        /// Tier 1 (the principle) is part of this stage's hint.
        public bool HasPrinciple => principle;
        /// Tiers as the player counts them: from the first one this stage opens (a stage that started with its hint
        /// text has no tier 1, so its first H reads 1 / N - 1; review M2 on 4024c25). Telemetry keeps the absolute tier.
        public int Shown(int tier) => principle ? tier : tier - 1;
        public int ShownTiers => principle ? plan.Tiers : plan.Tiers - 1;
        public int Opened => opened;

        /// The tier the next H opens (no state change).
        public int NextTier(State now)
        {
            if (opened == 0 && principle) return 1;
            if (opened >= plan.RouteTier) return plan.RouteTier;
            int goal = nextGoal;
            if (now != null && plan.TryOnRoute(now, out int reached)) goal = Math.Max(goal, reached);
            return goal < plan.Goals.Count ? HintPlan.GoalTier(goal) : plan.RouteTier;
        }

        /// The next H costs the ★2 (the HUD marks it before the press, contract review (1)).
        public bool NextCosts(State now) => HintRules.Costs(NextTier(now));

        /// Opens the next tier.
        public HintTier Open(State now)
        {
            int tier = NextTier(now);
            bool fromStart = now != null && now.Prev != null && !plan.TryOnRoute(now, out _);
            opened = Math.Max(opened, tier);
            if (tier == 1) return new HintTier(1, plan.Tiers, HintTier.Shows.Principle, default, false, HintRules.Costs(1));
            if (tier == plan.RouteTier) return new HintTier(tier, plan.Tiers, HintTier.Shows.Route, default, fromStart, HintRules.Costs(tier));
            int goal = tier - 2;
            nextGoal = goal + 1;
            return new HintTier(tier, plan.Tiers, HintTier.Shows.Goal, plan.Goals[goal], fromStart, HintRules.Costs(tier));
        }
    }
}
