using NightSupermarket.Core;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    public enum EmployeeState : byte { Walking, Working, Alerted }

    /// <summary>
    /// Minimal staff member proving the shared NPC stack: walks between work spots (including staff-only
    /// zones), works for a while, and reports suspicious movement like any civilian. Stocking, cleaning, and
    /// register work can grow from here without touching customers.
    /// </summary>
    public sealed class EmployeeBehavior : NpcBehavior
    {
        public Vector2 workSeconds = new Vector2(6, 14);
        private EmployeeState state;
        private float timer;
        private Random random;
        public override byte StateCode => (byte)state;
        public override string StateLabel(byte code) => ((EmployeeState)code).ToString();

        public void Begin(Random rng) { random = rng; NextSpot(); }

        public override void Tick(float delta)
        {
            if (random == null) return;
            timer -= delta;
            switch (state)
            {
                case EmployeeState.Walking:
                    if (Movement.Arrived) { state = EmployeeState.Working; Movement.Stop(); timer = Range(workSeconds); }
                    else if (Movement.Stuck || Movement.Unreachable) NextSpot();
                    break;
                case EmployeeState.Working:
                    if (Navigation.Target != null) Movement.Face(transform.position + Navigation.Target.transform.forward);
                    if (timer <= 0) NextSpot();
                    break;
                case EmployeeState.Alerted:
                    if (timer <= 0) NextSpot();
                    break;
            }
        }

        private void NextSpot()
        {
            state = EmployeeState.Walking;
            var spot = Navigation.ChooseWork();
            if (spot == null || !Movement.GoTo(spot.Position)) { state = EmployeeState.Working; timer = 3; }
        }

        protected override void OnSuspicious(PerceptionTarget target, AwarenessTracker tracker)
        {
            base.OnSuspicious(target, tracker);
            state = EmployeeState.Alerted; timer = 6f;
        }

        protected override void OnReported(SuspiciousActivityEvent report, bool accepted) { state = EmployeeState.Alerted; timer = 2f; }

        private float Range(Vector2 range) => range.x + (float)random.NextDouble() * (range.y - range.x);
    }
}
