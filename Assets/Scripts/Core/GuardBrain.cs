using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public enum GuardState { Patrol, Investigate, Search, Chase, Capture, ReturnToPatrol }
    public sealed class GuardBrain
    {
        public GuardState State { get; private set; }
        public bool AdvancePatrol { get; private set; }
        private readonly double patrolWait, searchDuration;
        private double timer;
        private bool arrived, threat, canCapture;
        private double delta;
        private readonly Dictionary<GuardState, Action> states;
        public GuardBrain(double patrolWait, double searchDuration)
        {
            GameRules.RequirePositive(patrolWait, nameof(patrolWait)); GameRules.RequirePositive(searchDuration, nameof(searchDuration));
            this.patrolWait = patrolWait; this.searchDuration = searchDuration;
            states = new Dictionary<GuardState, Action>
            {
                {GuardState.Patrol, Patrol}, {GuardState.Investigate, Investigate}, {GuardState.Search, Search},
                {GuardState.Chase, Chase}, {GuardState.Capture, Capture}, {GuardState.ReturnToPatrol, Return}
            };
        }
        public void Tick(bool atDestination, bool visibleThreat, bool captureAllowed, double dt)
        {
            GameRules.RequireDelta(dt); delta = dt; arrived = atDestination; threat = visibleThreat; canCapture = captureAllowed;
            AdvancePatrol = false;
            if (threat && State != GuardState.Chase && State != GuardState.Capture) Transition(GuardState.Chase);
            states[State]();
        }
        public void Hear() { if (State != GuardState.Chase && State != GuardState.Capture) Transition(GuardState.Investigate); }
        public void Transition(GuardState next) { State = next; timer = 0; }
        private void Patrol() { if (!arrived) { timer = 0; return; } timer += delta; if (timer >= patrolWait) { AdvancePatrol = true; timer = 0; } }
        private void Investigate() { if (arrived) Transition(GuardState.Search); }
        private void Search() { timer += delta; if (timer >= searchDuration) Transition(GuardState.ReturnToPatrol); }
        private void Chase() { if (canCapture) Transition(GuardState.Capture); else if (!threat) Transition(GuardState.Search); }
        private void Capture() => Transition(GuardState.ReturnToPatrol);
        private void Return() { if (arrived) Transition(GuardState.Patrol); }
    }
}
