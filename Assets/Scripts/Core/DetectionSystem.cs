using System;
namespace NightSupermarket.Core
{
    public enum DetectionState { Green, Orange, Red, Discovered }
    public sealed class DetectionSystem
    {
        public DetectionState State { get; private set; }
        public SuspicionSystem Suspicion { get; }
        public double GraceRemaining => State == DetectionState.Orange ? Math.Max(0, rules.OrangeDuration + encounterBonus - observedTime) : 0;
        public bool DisplayDoubt => State == DetectionState.Orange && encounterBonus > 0;
        /// <summary>
        /// True while the guard's attention from the last sighting has not fully faded. A new sighting
        /// in this window resumes the old grace instead of granting a fresh one, so slipping behind a
        /// shelf for a few frames no longer resets the guard.
        /// </summary>
        public bool AttentionLingering => observedTime > 0 && State == DetectionState.Green;
        /// <summary>Set by the tick that started a sighting from a fully cold guard; false for a resumed one.</summary>
        public bool FreshSighting { get; private set; }
        public event Action<DetectionState> Changed;
        private readonly GameRules rules;
        private double observedTime, encounterBonus;
        /// <summary>When set, observation ticks do not overwrite a debug state.</summary>
        public bool Hold { get; private set; }
        public DetectionSystem(GameRules rules) { this.rules = rules; Suspicion = new SuspicionSystem(rules); }
        public void Tick(bool observed, double actualSpeed, double delta, bool displayCamouflage = false)
        {
            GameRules.RequireDelta(delta);
            FreshSighting = false;
            if (Hold || State == DetectionState.Discovered || delta == 0) return;
            if (!observed)
            {
                // Live sight is gone at once (the HUD says UNSEEN), but the guard's attention
                // drains at twice the rate it built rather than vanishing.
                observedTime = Math.Max(0, observedTime - delta * rules.AttentionDrain);
                if (observedTime == 0) encounterBonus = 0;
                SetState(DetectionState.Green);
                Suspicion.Tick(false, delta);
                Suspicion.Forget(delta);
                return;
            }
            if (State == DetectionState.Green)
            {
                FreshSighting = observedTime == 0;
                if (FreshSighting) encounterBonus = displayCamouflage ? 2 : 0;
                SetState(observedTime + 1e-9 < rules.OrangeDuration + encounterBonus ? DetectionState.Orange : DetectionState.Red);
            }
            double redDelta = delta;
            if (State == DetectionState.Orange)
            {
                double needed = Math.Max(0, rules.OrangeDuration + encounterBonus - observedTime);
                observedTime += delta;
                if (observedTime + 1e-9 < rules.OrangeDuration + encounterBonus) return;
                SetState(DetectionState.Red); redDelta = Math.Max(0, delta - needed);
            }
            else observedTime = Math.Min(observedTime + delta, rules.OrangeDuration + encounterBonus + 2);
            if (Suspicion.Tick(actualSpeed > rules.MovementThreshold, redDelta)) SetState(DetectionState.Discovered);
        }
        private void SetState(DetectionState state) { if (State == state) return; State = state; Changed?.Invoke(state); }
        public void Reset() { Hold = false; observedTime = 0; encounterBonus = 0; Suspicion.Reset(); SetState(DetectionState.Green); }
        /// <summary>Debug override. Observation gameplay still goes through <see cref="Tick"/>.</summary>
        public void DebugOverride(DetectionState state, int suspicion)
        {
            Suspicion.DebugSet(suspicion);
            Hold = state != DetectionState.Green;
            encounterBonus = 0;
            observedTime = state == DetectionState.Orange ? 0 : rules.OrangeDuration;
            SetState(state);
        }
    }
}
