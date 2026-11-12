using UnityEngine;

namespace RealityDefender
{
    /// <summary>How a level's bubbles move.</summary>
    public enum TargetMotion
    {
        /// <summary>Fixed in place above the base; only the film surface lives.</summary>
        Static,
        /// <summary>Carried around the base on a tilting, precessing carousel.</summary>
        Orbit,
        /// <summary>Free flight that always stays inside the phone's visible screen area.</summary>
        Wander
    }

    /// <summary>The signature destruction animation of a level's bubbles.</summary>
    public enum DeathStyle
    {
        /// <summary>Jelly shock, then the film tears open from the impact point while the core implodes.</summary>
        Rupture,
        /// <summary>The bubble is crushed into a spinning accretion disc and collapses into a singularity flash.</summary>
        Singularity,
        /// <summary>Hit-stop, then the bubble splits into a swarm of mini bubbles that pop one after another.</summary>
        Fission
    }

    public readonly struct LevelInfo
    {
        public readonly int number;
        public readonly int targets;
        public readonly string name;
        public readonly TargetMotion motion;
        public readonly DeathStyle death;
        public readonly Color primary;
        public readonly Color secondary;

        public LevelInfo(int number, int targets, string name, TargetMotion motion, DeathStyle death, Color primary, Color secondary)
        {
            this.number = number;
            this.targets = targets;
            this.name = name;
            this.motion = motion;
            this.death = death;
            this.primary = primary;
            this.secondary = secondary;
        }

        public int Points => targets * 10;
    }

    /// <summary>Summary handed to the HUD when a level is cleared.</summary>
    public readonly struct LevelResult
    {
        public readonly LevelInfo level;
        public readonly bool final;
        public readonly int totalScore;
        public readonly int shots;
        public readonly int hits;
        public readonly float seconds;

        public LevelResult(LevelInfo level, bool final, int totalScore, int shots, int hits, float seconds)
        {
            this.level = level;
            this.final = final;
            this.totalScore = totalScore;
            this.shots = shots;
            this.hits = hits;
            this.seconds = seconds;
        }

        public int Accuracy => shots > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * hits / shots), 0, 100) : 100;
    }

    public static class Levels
    {
        public static readonly LevelInfo[] All =
        {
            new LevelInfo(1, 2, "STILL WATERS", TargetMotion.Static, DeathStyle.Rupture,
                new Color(0.25f, 0.92f, 1f), new Color(0.35f, 1f, 0.75f)),
            new LevelInfo(2, 3, "ORBIT STORM", TargetMotion.Orbit, DeathStyle.Singularity,
                new Color(0.72f, 0.38f, 1f), new Color(1f, 0.3f, 0.82f)),
            new LevelInfo(3, 4, "CHAOS DRIFT", TargetMotion.Wander, DeathStyle.Fission,
                new Color(1f, 0.78f, 0.22f), new Color(1f, 0.42f, 0.18f)),
        };

        public static int Count => All.Length;
        public static int TotalPoints
        {
            get
            {
                int sum = 0;
                foreach (var level in All) sum += level.Points;
                return sum;
            }
        }

        /// <summary>1-based lookup; clamps out-of-range numbers.</summary>
        public static LevelInfo Get(int number) => All[Mathf.Clamp(number - 1, 0, All.Length - 1)];
    }
}
