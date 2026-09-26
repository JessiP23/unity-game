using System;
namespace NightSupermarket.Core
{
    public sealed class SuspicionSystem
    {
        public int Value { get; private set; }
        private readonly GameRules rules;
        private double cooldown;
        private bool moving;
        public SuspicionSystem(GameRules rules) { this.rules = rules; }
        /// <summary>One increment at movement onset, then at fixed intervals while movement continues.</summary>
        public bool Tick(bool validMovement, double delta)
        {
            GameRules.RequireDelta(delta);
            if (!validMovement) { moving = false; cooldown = 0; return false; }
            if (delta == 0) return false;
            if (!moving) { moving = true; cooldown = rules.SuspicionInterval; if (Increment()) return true; }
            cooldown -= delta;
            while (cooldown <= 0) { cooldown += rules.SuspicionInterval; if (Increment()) return true; }
            return false;
        }
        private bool Increment()
        {
            if (Value >= rules.DiscoveryThreshold) return true;
            Value++;
            return !rules.ContinuedMovementToDiscover && Value >= rules.DiscoveryThreshold;
        }
        public void Reset() { Value = 0; moving = false; cooldown = 0; }
    }
}
