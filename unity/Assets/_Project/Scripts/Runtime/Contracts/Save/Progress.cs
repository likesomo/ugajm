using System;
using System.Collections.Generic;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Stage progress, v2 (docs/architecture.md 11.3.1): one record per stage, keyed by the stage name (Level.Name, unique
    /// in levels.json), so records follow a stage when stages are inserted or reordered. levelKey (TelemetryJson.LevelKey,
    /// map + bodies) tells a changed map under the same name: the clear stays, the best-bodies record is dropped.
    /// Read through the methods; the public fields exist for JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        public const int Version = 2;
        /// Most stars a clear can earn (docs/architecture.md 6.5, Core Scoring.MaxStars).
        public const int MaxStars = 3;

        public int version = Version;
        public List<StageRecord> stages = new List<StageRecord>();
        /// Name of the stage played last ("" = none).
        public string lastStage = "";

        public bool IsCleared(Level level) => Find(NameOf(level))?.cleared == true;

        /// Fewest bodies this player used to clear <paramref name="level"/>, 0 = no record. A record made on another map
        /// of the same name (levelKey differs) or outside the stage's body budget does not count.
        public int BestBodies(Level level)
        {
            var r = Find(NameOf(level));
            if (r == null || r.bestBodies <= 0 || r.bestBodies > level.Bodies) return 0;
            if (!string.IsNullOrEmpty(r.levelKey) && r.levelKey != TelemetryJson.LevelKey(level)) return 0;
            return r.bestBodies;
        }

        /// A clear with <paramref name="bodiesUsed"/> bodies (1..level.Bodies). Keeps the fewest.
        public void MarkCleared(Level level, int bodiesUsed)
        {
            string name = NameOf(level);
            if (bodiesUsed < 1 || bodiesUsed > level.Bodies)
                throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, $"1..{level.Bodies} for {name}");
            var r = Get(name);
            string key = TelemetryJson.LevelKey(level);
            if (r.levelKey != key)
            {
                // Same name, other map (or a migrated record with no key): an old best only stands if it fits this map.
                if (!string.IsNullOrEmpty(r.levelKey) || r.bestBodies > level.Bodies) r.bestBodies = 0;
                r.bestStars = 0; // stars were earned on another map (or never: v0/v1 had none)
                r.levelKey = key;
            }
            r.cleared = true;
            if (r.bestBodies == 0 || bodiesUsed < r.bestBodies) r.bestBodies = bodiesUsed;
        }

        /// A clear that earned <paramref name="stars"/> (1..MaxStars, Core Scoring.Stars; 6.5). Keeps the fewest bodies and the most stars.
        public void MarkCleared(Level level, int bodiesUsed, int stars)
        {
            if (stars < 1 || stars > MaxStars) throw new ArgumentOutOfRangeException(nameof(stars), stars, $"1..{MaxStars}");
            MarkCleared(level, bodiesUsed);
            var r = Get(NameOf(level));
            if (stars > r.bestStars) r.bestStars = stars;
        }

        /// Most stars earned on this map, 0 = none (no record, another map of the same name, or out of range).
        public int BestStars(Level level)
        {
            var r = Find(NameOf(level));
            if (r == null || r.bestStars <= 0 || r.bestStars > MaxStars) return 0;
            if (r.levelKey != TelemetryJson.LevelKey(level)) return 0;
            return r.bestStars;
        }

        /// Count one more start of <paramref name="level"/> and return the attempt number (1 = first; level_start.attempt).
        public int CountStart(Level level) => ++Get(NameOf(level)).starts;

        /// Cleared stages, including names not in the current levels.json (session_start.cleared).
        public int ClearedCount
        {
            get
            {
                int n = 0;
                foreach (var r in stages) if (r.cleared) n++;
                return n;
            }
        }

        public void SetLastStage(Level level) => lastStage = NameOf(level);

        /// Index of the last played stage in <paramref name="levels"/>; 0 when none or no longer in the list.
        public int LastStageIndex(IReadOnlyList<Level> levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            for (int k = 0; k < levels.Count; k++) if (levels[k].Name == lastStage) return k;
            return 0;
        }

        StageRecord Find(string name)
        {
            foreach (var r in stages) if (r.name == name) return r;
            return null;
        }

        StageRecord Get(string name)
        {
            var r = Find(name);
            if (r == null) stages.Add(r = new StageRecord { name = name });
            return r;
        }

        static string NameOf(Level level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (string.IsNullOrEmpty(level.Name)) throw new ArgumentException("stage has no name (the save key)", nameof(level));
            return level.Name;
        }
    }

    /// <summary>One stage's progress (ProgressData v2).</summary>
    [Serializable]
    public sealed class StageRecord
    {
        public string name = "";
        /// TelemetryJson.LevelKey of the map the record was made on; "" = unknown (migrated from v0/v1).
        public string levelKey = "";
        public bool cleared;
        /// Fewest bodies used, 0 = none.
        public int bestBodies;
        /// Starts on this device (level_start.attempt).
        public int starts;
        /// Most stars earned (0..3, docs/architecture.md 6.5). 0 = none. Written by MarkCleared(level, bodies, stars).
        public int bestStars;
    }

    /// <summary>
    /// Old progress formats → v2 (docs/architecture.md 11.3.1). v1 ("version":1, stage numbers) and v0 (key
    /// "seven-steps-cleared", comma list) stored 0-based stage numbers of the 17-stage set; they are mapped to names with
    /// the frozen table below. Numbers outside it are dropped.
    /// </summary>
    public static class ProgressMigration
    {
        /// 7lucky levels.json stage names on 2026-11-09 (the set every v0/v1 save was made with). Never edit.
        public static readonly string[] V1StageNames =
        {
            "일곱 걸음", "늦게 도착하기", "돌이 된 몸", "새 출발점", "몸으로 놓는 다리", "숫자 문", "홀수의 저주", "내리는 벽",
            "멈춰 세우는 석상", "올려서 멈추기", "한 번만 버티는 바닥", "두 개의 문", "하나만 남겨라", "열쇠가 되는 몸",
            "남기기 전에 지나가라", "칠칠의 방", "떠나온 자리",
        };

        /// v1 JSON shape (read only for migration).
        [Serializable]
        public sealed class V1
        {
            public int version;
            public List<int> cleared = new List<int>();
            public List<int> bestStage = new List<int>();
            public List<int> bestBodies = new List<int>();
            public int lastStage;
            public List<int> starts = new List<int>();
        }

        public static ProgressData FromV1(V1 old)
        {
            if (old == null) throw new ArgumentNullException(nameof(old));
            var p = new ProgressData();
            foreach (int k in old.cleared) if (Name(k) is string n) Record(p, n).cleared = true;
            for (int i = 0; i < old.bestStage.Count && i < old.bestBodies.Count; i++)
                if (Name(old.bestStage[i]) is string n && old.bestBodies[i] > 0) Record(p, n).bestBodies = old.bestBodies[i];
            for (int k = 0; k < old.starts.Count; k++)
                if (Name(k) is string n && old.starts[k] > 0) Record(p, n).starts = old.starts[k];
            p.lastStage = Name(old.lastStage) ?? "";
            return p;
        }

        /// v0 "seven-steps-cleared" value ("0,2,5").
        public static ProgressData FromV0(string clearedList)
        {
            var old = new V1 { version = 0 };
            foreach (var part in (clearedList ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out int k) && !old.cleared.Contains(k)) old.cleared.Add(k);
            return FromV1(old);
        }

        static string Name(int stage) => stage >= 0 && stage < V1StageNames.Length ? V1StageNames[stage] : null;

        static StageRecord Record(ProgressData p, string name)
        {
            foreach (var r in p.stages) if (r.name == name) return r;
            var rec = new StageRecord { name = name };
            p.stages.Add(rec);
            return rec;
        }
    }
}
