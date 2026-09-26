using System;

namespace NightSupermarket.Core
{
    /// <summary>Validated immutable match rules, independent of assets and transport.</summary>
    public sealed class GameRules
    {
        public double Duration { get; }
        public double OrangeDuration { get; }
        public double SuspicionInterval { get; }
        public int DiscoveryThreshold { get; }
        public double MovementThreshold { get; }
        public bool ContinuedMovementToDiscover { get; }
        public int RescuesPerAction { get; }
        public int RequiredEscapes { get; }
        public bool AllowSelfRescue { get; }
        public bool RequireMissionsToEscape { get; }
        public bool DefeatWhenNoRescueRemains { get; }
        public GameRules(double duration = 600, double orangeDuration = 1.5,
            double suspicionInterval = 1, int discoveryThreshold = 3,
            double movementThreshold = 0.08, bool continuedMovementToDiscover = true,
            int rescuesPerAction = 1, int requiredEscapes = 1, bool allowSelfRescue = false,
            bool requireMissionsToEscape = true, bool defeatWhenNoRescueRemains = true)
        {
            RequirePositive(duration, nameof(duration));
            RequirePositive(orangeDuration, nameof(orangeDuration));
            RequirePositive(suspicionInterval, nameof(suspicionInterval));
            RequirePositive(movementThreshold, nameof(movementThreshold));
            if (discoveryThreshold < 1) throw new ArgumentOutOfRangeException(nameof(discoveryThreshold));
            if (rescuesPerAction < 1) throw new ArgumentOutOfRangeException(nameof(rescuesPerAction));
            if (requiredEscapes < 1) throw new ArgumentOutOfRangeException(nameof(requiredEscapes));
            Duration = duration; OrangeDuration = orangeDuration; SuspicionInterval = suspicionInterval;
            DiscoveryThreshold = discoveryThreshold; MovementThreshold = movementThreshold;
            ContinuedMovementToDiscover = continuedMovementToDiscover;
            RescuesPerAction = rescuesPerAction; RequiredEscapes = requiredEscapes;
            AllowSelfRescue = allowSelfRescue; RequireMissionsToEscape = requireMissionsToEscape;
            DefeatWhenNoRescueRemains = defeatWhenNoRescueRemains;
        }
        public static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }
        public static void RequireDelta(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
