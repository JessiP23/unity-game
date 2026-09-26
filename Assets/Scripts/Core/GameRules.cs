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
        public GameRules(double duration = 600, double orangeDuration = 1.5,
            double suspicionInterval = 1, int discoveryThreshold = 3,
            double movementThreshold = 0.08, bool continuedMovementToDiscover = true)
        {
            RequirePositive(duration, nameof(duration));
            RequirePositive(orangeDuration, nameof(orangeDuration));
            RequirePositive(suspicionInterval, nameof(suspicionInterval));
            RequirePositive(movementThreshold, nameof(movementThreshold));
            if (discoveryThreshold < 1) throw new ArgumentOutOfRangeException(nameof(discoveryThreshold));
            Duration = duration; OrangeDuration = orangeDuration; SuspicionInterval = suspicionInterval;
            DiscoveryThreshold = discoveryThreshold; MovementThreshold = movementThreshold;
            ContinuedMovementToDiscover = continuedMovementToDiscover;
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
