using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// What an NPC does with its day. Subclasses (customer, employee, ...) own a state machine; this base
    /// owns the shared civilian reaction to perception: stop, stare, and pass a last-known report to security.
    /// Civilians never chase or capture; that stays with <see cref="GuardController"/>.
    /// </summary>
    public abstract class NpcBehavior : MonoBehaviour
    {
        protected NpcController Controller { get; private set; }
        protected NpcMovement Movement => Controller.Movement;
        protected NpcNavigation Navigation => Controller.Navigation;
        protected NpcPerception Perception => Controller.Perception;
        private Func<SuspiciousActivityEvent, bool> reportSink;
        private Func<double> clock;
        public int ReportsSent { get; private set; }
        public abstract byte StateCode { get; }
        public abstract string StateLabel(byte code);
        public virtual Vector3? Destination => Navigation != null && Navigation.Target != null ? Navigation.Target.Position : (Vector3?)null;
        public abstract void Tick(float delta);

        /// <summary><paramref name="sink"/> is the match authority's report entry point.</summary>
        public void Attach(NpcController controller, Func<SuspiciousActivityEvent, bool> sink, Func<double> time)
        {
            Controller = controller; reportSink = sink; clock = time;
            if (Perception == null) return;
            Perception.BecameSuspicious -= HandleSuspicious; Perception.BecameSuspicious += HandleSuspicious;
            Perception.CalmedDown -= HandleCalm; Perception.CalmedDown += HandleCalm;
            Perception.ReportReady -= HandleReport; Perception.ReportReady += HandleReport;
        }

        private void HandleSuspicious(PerceptionTarget target, AwarenessTracker tracker) => OnSuspicious(target, tracker);
        private void HandleCalm(PerceptionTarget target, AwarenessTracker tracker) => OnCalmed(target, tracker);
        private void HandleReport(PerceptionTarget target, AwarenessTracker tracker)
        {
            if (!WillReport(target, tracker)) { tracker.Reset(); return; }
            var zone = Navigation != null ? Navigation.ZoneAt(ToVector(tracker.LastKnown)) : null;
            var report = new SuspiciousActivityEvent(Controller.Role + " " + Controller.Id, target.Id, tracker.LastKnown,
                clock != null ? clock() : Time.timeAsDouble, tracker.Suspicion, ReportKind.MovingMannequin, zone);
            bool accepted = reportSink != null && reportSink(report);
            if (accepted) ReportsSent++;
            tracker.Reset();
            OnReported(report, accepted);
        }

        protected virtual void OnSuspicious(PerceptionTarget target, AwarenessTracker tracker)
        {
            Movement.Stop();
            if (tracker.HasLastKnown) Movement.Face(ToVector(tracker.LastKnown));
        }
        protected virtual void OnCalmed(PerceptionTarget target, AwarenessTracker tracker) { }
        protected virtual bool WillReport(PerceptionTarget target, AwarenessTracker tracker) => true;
        protected virtual void OnReported(SuspiciousActivityEvent report, bool accepted) { }

        protected static Vector3 ToVector(MapPoint point) => new Vector3(point.X, point.Y, point.Z);
    }
}
