using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public enum ScoreEvent { RequiredJob, OptionalJob, CloseCall, PoseMatch, Comeback, EscapeTime, Report, Capture }

    /// <summary>One line of the end-of-night card.</summary>
    public readonly struct ScoreLine
    {
        public readonly ScoreEvent Event;
        public readonly string Label;
        public readonly int Points;
        public readonly double Multiplier;
        public ScoreLine(ScoreEvent evt, string label, int points, double multiplier) { Event = evt; Label = label; Points = points; Multiplier = multiplier; }
    }

    /// <summary>
    /// Points for a night. Style pays: close calls, matching poses and long unseen streaks score;
    /// reports and captures cost. Pure arithmetic, no match state. The grade comes from the total,
    /// and a lost night is always D so bonuses can never buy a win.
    /// </summary>
    public sealed class NightScore
    {
        public const int RequiredJobPoints = 500, OptionalJobPoints = 300, CloseCallPoints = 100, PoseMatchPoints = 150, ComebackPoints = 200;
        public const int ReportPenalty = 150, CapturePenalty = 400, PointsPerSecondLeft = 10;
        /// <summary>Unseen seconds that lift the multiplier to 1.5, then to 2.</summary>
        public const double StreakTierOne = 60, StreakTierTwo = 120;

        private readonly List<ScoreLine> lines = new List<ScoreLine>();
        public IReadOnlyList<ScoreLine> Lines => lines;
        public int Total { get; private set; }
        public double Streak { get; private set; }
        public int CloseCalls { get; private set; }
        public int BestStreakSeconds { get; private set; }
        public double Multiplier => Streak >= StreakTierTwo ? 2 : Streak >= StreakTierOne ? 1.5 : 1;

        /// <summary>Unseen time grows the streak; a watcher reaching red ends it.</summary>
        public void TickUnseen(double delta)
        {
            GameRules.RequireDelta(delta);
            Streak += delta;
            BestStreakSeconds = Math.Max(BestStreakSeconds, (int)Streak);
        }
        public void BreakStreak() => Streak = 0;

        public int Add(ScoreEvent evt, string label = null)
        {
            int basePoints = evt switch
            {
                ScoreEvent.RequiredJob => RequiredJobPoints,
                ScoreEvent.OptionalJob => OptionalJobPoints,
                ScoreEvent.CloseCall => CloseCallPoints,
                ScoreEvent.PoseMatch => PoseMatchPoints,
                ScoreEvent.Comeback => ComebackPoints,
                ScoreEvent.Report => -ReportPenalty,
                ScoreEvent.Capture => -CapturePenalty,
                _ => 0
            };
            double factor = basePoints > 0 ? Multiplier : 1;
            int points = (int)Math.Round(basePoints * factor);
            if (evt == ScoreEvent.CloseCall) CloseCalls++;
            if (evt == ScoreEvent.Capture) BreakStreak();
            lines.Add(new ScoreLine(evt, label ?? Describe(evt), points, factor));
            Total += points;
            return points;
        }

        /// <summary>Seconds left on the clock when the team escaped.</summary>
        public int AddEscapeTime(double secondsLeft)
        {
            int points = (int)Math.Max(0, secondsLeft) * PointsPerSecondLeft;
            lines.Add(new ScoreLine(ScoreEvent.EscapeTime, (int)secondsLeft + " s before dawn", points, 1));
            Total += points;
            return points;
        }

        public string Grade(bool victory)
        {
            if (!victory) return "D";
            if (Total >= 2500) return "S";
            if (Total >= 1800) return "A";
            if (Total >= 1200) return "B";
            if (Total >= 600) return "C";
            return "D";
        }

        public static string Describe(ScoreEvent evt) => evt switch
        {
            ScoreEvent.RequiredJob => "Job done",
            ScoreEvent.OptionalJob => "Optional swap",
            ScoreEvent.CloseCall => "Close call",
            ScoreEvent.PoseMatch => "Perfect pose",
            ScoreEvent.Comeback => "Comeback from the back hall",
            ScoreEvent.EscapeTime => "Time left",
            ScoreEvent.Report => "Reported by a shopper",
            ScoreEvent.Capture => "Caught",
            _ => evt.ToString()
        };
    }
}
