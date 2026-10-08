using System;
namespace NightSupermarket.Core
{
    public sealed class SuspicionSystem
    {
        public int Value { get; private set; }
        private readonly GameRules rules;
        private double cooldown, unseen;
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
        /// <summary>
        /// Time spent out of every watcher's sight slowly forgives old slips: one point per
        /// <see cref="GameRules.SuspicionDecay"/> seconds. Being seen again pauses the forgetting
        /// but does not undo it. A decay of zero keeps the old permanent memory.
        /// </summary>
        public void Forget(double unseenDelta)
        {
            GameRules.RequireDelta(unseenDelta);
            if (rules.SuspicionDecay <= 0 || Value == 0) { unseen = 0; return; }
            unseen += unseenDelta;
            while (unseen >= rules.SuspicionDecay && Value > 0) { unseen -= rules.SuspicionDecay; Value--; }
            if (Value == 0) unseen = 0;
        }
        /// <summary>Seconds of unseen time still needed before the next point is forgiven; 0 when nothing to forgive.</summary>
        public double NextForgiveness => Value == 0 || rules.SuspicionDecay <= 0 ? 0 : Math.Max(0, rules.SuspicionDecay - unseen);
        private bool Increment()
        {
            if (Value >= rules.DiscoveryThreshold) return true;
            Value++;
            unseen = 0;
            return !rules.ContinuedMovementToDiscover && Value >= rules.DiscoveryThreshold;
        }
        public void Reset() { Value = 0; moving = false; cooldown = 0; unseen = 0; }
        /// <summary>Debug override. Gameplay suspicion only changes through <see cref="Tick"/>.</summary>
        public void DebugSet(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value; moving = false; cooldown = 0; unseen = 0;
        }
    }
}
