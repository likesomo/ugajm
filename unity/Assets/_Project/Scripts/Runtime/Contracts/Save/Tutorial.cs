using System;
using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// One-time guides of the minimal UI (docs/architecture.md 11.3.2): which control guides this player has finished and
    /// how many KEEP / SWITCH choices they have confirmed. Saved under <see cref="SaveSystem.TutorialKey"/>; reset with the
    /// progress by SaveSystem.StartNewPlayer. Never part of Rules.Key, a replay or a QA attempt line.
    /// Read and change it through SaveSystem (TutorialSeen, MarkTutorialSeen, CountChoiceConfirm); fields are for JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class TutorialData
    {
        public const int Version = 1;
        /// The KEEP / SWITCH cards explain both outcomes until this many choices were confirmed.
        public const int ChoiceExplainCount = 3;

        public int version = Version;
        /// Finished guides, "id@version" (<see cref="TutorialIds"/>). A new guide version is a new id: it shows again.
        public List<string> seen = new List<string>();
        /// Confirmed KEEP / SWITCH choices, 0..ChoiceExplainCount (stops counting there).
        public int choiceConfirms;

        public bool IsSeen(string id) => seen != null && seen.Contains(id);

        /// Returns true when this changed the data (the caller saves).
        public bool MarkSeen(string id)
        {
            if (!TutorialIds.IsValid(id)) throw new ArgumentException("tutorial id is name@version, name a-z 0-9 -, version 1..99", nameof(id));
            if (seen == null) seen = new List<string>();
            if (seen.Contains(id)) return false;
            seen.Add(id);
            return true;
        }

        /// Both outcomes are explained on the cards (the first ChoiceExplainCount confirmed choices).
        public bool ExplainChoice => choiceConfirms < ChoiceExplainCount;

        /// One more confirmed choice. Returns true when this changed the data (false once the count is at its cap).
        public bool CountChoice()
        {
            if (choiceConfirms >= ChoiceExplainCount) return false;
            choiceConfirms = Math.Max(0, choiceConfirms) + 1;
            return true;
        }
    }

    /// <summary>Tutorial ids, "name@version". Bump the version when a guide's content changes so players see it once more.</summary>
    public static class TutorialIds
    {
        /// First move guide (first stage: direction keys / buttons, until the first applied move).
        public const string Move = "move@1";
        /// First KEEP / SWITCH choice screen guide ("walking is locked, pick one"), until the first confirmed choice.
        public const string Choose = "choose@1";
        /// First ice slide (where a slide stops: the first floor cell past the ice, or before a wall or statue), shown once.
        public const string Slide = "slide@1";

        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32) return false;
            int at = id.IndexOf('@');
            if (at <= 0 || at != id.LastIndexOf('@')) return false;
            for (int i = 0; i < at; i++)
            {
                char c = id[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')) return false;
            }
            return int.TryParse(id.Substring(at + 1), System.Globalization.NumberStyles.None, null, out int v) && v >= 1 && v <= 99
                && id.Substring(at + 1) == v.ToString();
        }
    }
}
