using System;
namespace NightSupermarket.Core
{
    public enum DetectionState { Green, Orange, Red, Discovered }
    public sealed class DetectionSystem
    {
        public DetectionState State { get; private set; }
        public SuspicionSystem Suspicion { get; }
        public event Action<DetectionState> Changed;
        private readonly GameRules rules;
        private double observedTime;
        public DetectionSystem(GameRules rules) { this.rules = rules; Suspicion = new SuspicionSystem(rules); }
        public void Tick(bool observed, double actualSpeed, double delta)
        {
            GameRules.RequireDelta(delta);
            if (State == DetectionState.Discovered || delta == 0) return;
            if (!observed)
            {
                observedTime = 0; SetState(DetectionState.Green); Suspicion.Tick(false, delta); return;
            }
            if (State == DetectionState.Green) SetState(DetectionState.Orange);
            double redDelta = delta;
            if (State == DetectionState.Orange)
            {
                double needed = rules.OrangeDuration - observedTime;
                observedTime += delta;
                if (observedTime + 1e-9 < rules.OrangeDuration) return;
                SetState(DetectionState.Red); redDelta = Math.Max(0, delta - needed);
            }
            if (Suspicion.Tick(actualSpeed > rules.MovementThreshold, redDelta)) SetState(DetectionState.Discovered);
        }
        private void SetState(DetectionState state) { if (State == state) return; State = state; Changed?.Invoke(state); }
        public void Reset() { observedTime = 0; Suspicion.Reset(); SetState(DetectionState.Green); }
    }
}
