using System;
namespace NightSupermarket.Core
{
    /// <summary>
    /// Hiding in the crowd. Walk with two or more shoppers, going their way at their pace, and people's eyes
    /// slide over you; run, walk against the flow or walk alone and you are a mannequin on the move again.
    /// </summary>
    public static class CoverRules
    {
        /// <summary>A companion walks within this many metres of you.</summary>
        public const double Radius = 2.0;
        /// <summary>Largest angle between your heading and a companion's that still counts as "their way".</summary>
        public const double MaxAngle = 55;
        /// <summary>A shopper slower than this is standing, not walking; standing groups hide a standing mannequin.</summary>
        public const double MinWalkerSpeed = 0.3, StillSpeed = 0.15;
        /// <summary>Guard doubt (seconds of extra grace) while covered: the same as a fitting display stand.</summary>
        public const double GuardDoubtFrom = 0.5;

        /// <summary>How much of a watcher's suspicion the crowd absorbs: 0 none, 0.55 with two companions, 0.75 with three or more.</summary>
        public static double Strength(int companions, bool running) => running ? 0 : companions >= 3 ? 0.75 : companions == 2 ? 0.55 : 0;

        /// <summary>
        /// Does this walker count as company? (dx, dz) is the walker relative to you, (wx, wz) its velocity and
        /// (px, pz) yours, all on the floor plane.
        /// </summary>
        public static bool Companion(double dx, double dz, double wx, double wz, double px, double pz)
        {
            if (dx * dx + dz * dz > Radius * Radius) return false;
            double walker = Math.Sqrt(wx * wx + wz * wz), you = Math.Sqrt(px * px + pz * pz);
            if (you < StillSpeed) return walker < MinWalkerSpeed; // standing among people who are standing
            if (walker < MinWalkerSpeed) return false;            // walking past people who are standing
            double cos = (wx * px + wz * pz) / (walker * you);
            return cos >= Math.Cos(MaxAngle * Math.PI / 180);
        }

        /// <summary>Suspicion gain after the crowd's share: what a watcher still makes of the movement.</summary>
        public static double Gain(double gain, double strength) => gain * (1 - Math.Max(0, Math.Min(0.9, strength)));
    }

    public enum PressureStage { Busy, QuietSoon, Quiet }

    /// <summary>A job's crowd rhythm: busy most of the time, quiet for <see cref="JobPressure.QuietSeconds"/> once per period.</summary>
    public readonly struct PressureCycle
    {
        public readonly double Period, Offset;
        public PressureCycle(double period, double offset) { Period = period; Offset = offset; }
    }

    /// <summary>
    /// People gather where your jobs are: window shoppers, a little queue, someone browsing right beside the
    /// thing you need. Never more than four, never on the spot itself, and every minute or so they drift off for
    /// fifteen seconds. Waiting for that moment, or making your own with the crowd, is the skill.
    /// </summary>
    public static class JobPressure
    {
        public const double QuietSeconds = 15, WarnSeconds = 6, MinPeriod = 60, MaxPeriod = 90;
        public const int MaxLingerers = 4;
        /// <summary>Lingerers keep this far from the job's own spot.</summary>
        public const double KeepClear = 1.1;

        public static int Lingerers(int level, Difficulty difficulty)
        {
            int n = 2 + (level >= 3 ? 1 : 0) + (level >= 6 ? 1 : 0) + (difficulty == Difficulty.RushHour ? 1 : difficulty == Difficulty.Relaxed ? -1 : 0);
            return Math.Max(1, Math.Min(MaxLingerers, n));
        }

        /// <summary>Seeded per shift and job, so jobs breathe out of step and everyone on a shift gets the same rhythm.</summary>
        public static PressureCycle For(int shift, int job)
        {
            var random = new Random(shift * 4099 + job * 131 + 7);
            double period = MinPeriod + random.NextDouble() * (MaxPeriod - MinPeriod);
            return new PressureCycle(period, random.NextDouble() * period);
        }

        /// <summary>Where the job is in its rhythm; <paramref name="untilChange"/> counts down to the next stage boundary that matters.</summary>
        public static PressureStage StageAt(PressureCycle cycle, double elapsed, out double untilChange)
        {
            double t = ((elapsed + cycle.Offset) % cycle.Period + cycle.Period) % cycle.Period;
            double busy = cycle.Period - QuietSeconds;
            if (t < busy - WarnSeconds) { untilChange = busy - t; return PressureStage.Busy; }
            if (t < busy) { untilChange = busy - t; return PressureStage.QuietSoon; }
            untilChange = cycle.Period - t;
            return PressureStage.Quiet;
        }
    }

    /// <summary>How crowded a department looks on the map, and whether it is filling up or emptying.</summary>
    public static class CrowdHeat
    {
        public static int Level(int people) => people <= 1 ? 0 : people <= 3 ? 1 : people <= 6 ? 2 : 3;
        public static string Name(int level) => level <= 0 ? "Quiet" : level == 1 ? "Steady" : level == 2 ? "Busy" : "Packed";
        /// <summary>+1 filling up, -1 emptying, 0 steady. Two people either way is a trend; one is noise.</summary>
        public static int Trend(int before, int now) => now - before >= 2 ? 1 : before - now >= 2 ? -1 : 0;
        public static string Arrow(int trend) => trend > 0 ? " ↑" : trend < 0 ? " ↓" : "";
        public static string Tint(int level) => level <= 0 ? "#40DF80" : level == 1 ? "#C8D86A" : level == 2 ? "#FFA43D" : "#FF5A4E";
    }

    public enum Difficulty { Relaxed, Normal, RushHour }

    /// <summary>The shift you sign up for. Busier nights pay more; the quiet one pays less.</summary>
    public static class DifficultyRules
    {
        public const int RushHourUnlocksAfterNights = 3;
        public static double CrowdFactor(Difficulty d) => d == Difficulty.Relaxed ? 0.7 : d == Difficulty.RushHour ? 1.3 : 1;
        public static double ScoreFactor(Difficulty d) => d == Difficulty.Relaxed ? 0.8 : d == Difficulty.RushHour ? 1.3 : 1;
        public static int Sales(Difficulty d) => d == Difficulty.RushHour ? 3 : 2;
        public static bool Influencers(Difficulty d) => d != Difficulty.Relaxed;
        public static bool Unlocked(Difficulty d, int nightsPlayed) => d != Difficulty.RushHour || nightsPlayed >= RushHourUnlocksAfterNights;
        public static string Name(Difficulty d) => d == Difficulty.Relaxed ? "Relaxed" : d == Difficulty.RushHour ? "Rush hour" : "Normal";
        public static string Describe(Difficulty d) => d switch
        {
            Difficulty.Relaxed => "Fewer shoppers, no influencers, gentler jobs. Score ×0.8.",
            Difficulty.RushHour => "30% more people, three flash sales, crowds round every job. Score ×1.3.",
            _ => "The mall as it is tonight. Score ×1.",
        };
        public static Difficulty Parse(int stored) => stored == 0 ? Difficulty.Relaxed : stored == 2 ? Difficulty.RushHour : Difficulty.Normal;
    }

    /// <summary>One night's crowd numbers, kept so the crowd can be tuned against real play.</summary>
    public sealed class CrowdMetrics
    {
        public int Reports, PeakCrowd, JobsDone, JobsTotal, InfluencerReports, Level;
        public double CoveredSeconds, NightSeconds, BudgetSum, FrameMsSum;
        public int Samples;
        public Difficulty Difficulty;
        public double[] WaitSeconds = new double[0];

        public void Sample(int crowd, int budget, double frameMs)
        {
            PeakCrowd = Math.Max(PeakCrowd, crowd);
            BudgetSum += budget; FrameMsSum += frameMs; Samples++;
        }

        public double ReportsPerMinute => NightSeconds > 0 ? Reports / (NightSeconds / 60) : 0;

        public static string Header => "date,shift,difficulty,level,victory,jobs_done,jobs_total,reports,reports_per_min,influencer_reports,peak_crowd,mean_budget,mean_frame_ms,covered_s,night_s,wait_s_per_job";

        public string Row(string date, int shift, bool victory)
        {
            string waits = string.Join(" ", Array.ConvertAll(WaitSeconds, w => w.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(",", date, shift.ToString(c), DifficultyRules.Name(Difficulty), Level.ToString(c), victory ? "1" : "0",
                JobsDone.ToString(c), JobsTotal.ToString(c), Reports.ToString(c), ReportsPerMinute.ToString("0.00", c), InfluencerReports.ToString(c),
                PeakCrowd.ToString(c), (Samples > 0 ? BudgetSum / Samples : 0).ToString("0.0", c), (Samples > 0 ? FrameMsSum / Samples : 0).ToString("0.0", c),
                CoveredSeconds.ToString("0", c), NightSeconds.ToString("0", c), waits);
        }
    }
}
