using System;
namespace NightSupermarket.Core
{
    public enum NightPhase { Open, Closing, Dark, Lockdown, Dawn }

    /// <summary>What the clock does to the store. Read-only facts from remaining time; adapters apply them.</summary>
    public readonly struct NightMoment
    {
        public readonly NightPhase Phase;
        public readonly LightingMode Lighting;
        /// <summary>Multiplier on the guard's patrol and chase speed.</summary>
        public readonly double GuardPace;
        /// <summary>Share of the shopper cap that should be on the floor.</summary>
        public readonly double Crowd;
        public readonly bool Flashlight;
        public readonly string Headline;
        public NightMoment(NightPhase phase, LightingMode lighting, double guardPace, double crowd, bool flashlight, string headline)
        { Phase = phase; Lighting = lighting; GuardPace = guardPace; Crowd = crowd; Flashlight = flashlight; Headline = headline; }
    }

    /// <summary>
    /// The night escalates on a fixed curve: open store, closing announcement, lights out, lockdown.
    /// Thresholds are fractions of the night so a 6- or 10-minute rule set keeps the same shape.
    /// </summary>
    public static class NightSchedule
    {
        public const double ClosingAt = 2.0 / 3, DarkAt = 1.0 / 3, LockdownAt = 1.0 / 6;

        public static NightMoment At(double remaining, double duration)
        {
            GameRules.RequirePositive(duration, nameof(duration));
            double share = Math.Max(0, remaining) / duration;
            if (remaining <= 0) return new NightMoment(NightPhase.Dawn, LightingMode.Dawn, 1, 0, false, "Dawn. The store opens.");
            if (share <= LockdownAt) return new NightMoment(NightPhase.Lockdown, LightingMode.Emergency, 1.4, 0, true, "Lockdown. Front shutter down — the fire exit is upstairs.");
            if (share <= DarkAt) return new NightMoment(NightPhase.Dark, LightingMode.Dark, 1.25, 0.15, true, "Lights out. The guard has a torch and a quicker step.");
            if (share <= ClosingAt) return new NightMoment(NightPhase.Closing, LightingMode.Partial, 1.1, 0.5, false, "Closing time. Shoppers are leaving; fewer eyes, longer patrols.");
            return new NightMoment(NightPhase.Open, LightingMode.Normal, 1, 1, false, "Store open. Crowds hide you and watch you.");
        }

        /// <summary>Seconds until the next phase begins, for a countdown. Zero at lockdown and after.</summary>
        public static double UntilNextPhase(double remaining, double duration)
        {
            double share = remaining / duration;
            if (share > ClosingAt) return remaining - duration * ClosingAt;
            if (share > DarkAt) return remaining - duration * DarkAt;
            if (share > LockdownAt) return remaining - duration * LockdownAt;
            return 0;
        }
    }
}
