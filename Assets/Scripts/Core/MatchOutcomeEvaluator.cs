namespace NightSupermarket.Core
{
    /// <summary>Facts the outcome evaluator is allowed to see. Callers assemble these from authority state.</summary>
    public readonly struct RosterFacts
    {
        public readonly int Mannequins, Free, Captured, Escaped;
        public readonly bool MissionsComplete;
        public readonly double RemainingTime;
        public readonly MatchPhase Phase;
        public RosterFacts(int mannequins, int free, int captured, int escaped, bool missionsComplete, double remainingTime, MatchPhase phase)
        {
            Mannequins = mannequins; Free = free; Captured = captured; Escaped = escaped;
            MissionsComplete = missionsComplete; RemainingTime = remainingTime; Phase = phase;
        }
    }
    /// <summary>Single place that decides victory or defeat. Other systems report facts; they do not end the match.</summary>
    public static class MatchOutcomeEvaluator
    {
        public static MatchPhase? Evaluate(GameRules rules, RosterFacts facts)
        {
            if (rules == null || facts.Phase != MatchPhase.Night) return null;
            bool missionsReady = !rules.RequireMissionsToEscape || facts.MissionsComplete;
            if (facts.Escaped >= rules.RequiredEscapes && missionsReady) return MatchPhase.Victory;
            if (facts.RemainingTime <= 0) return MatchPhase.Defeat;
            // A captive can still walk the back hall alone, so a full warehouse is not the end of the night.
            bool escapeStillPossible = facts.Free > 0 || facts.Captured > 0 || facts.Escaped >= rules.RequiredEscapes;
            if (rules.DefeatWhenNoRescueRemains && facts.Mannequins > 0 && !escapeStillPossible) return MatchPhase.Defeat;
            return null;
        }
    }
}
