using NightSupermarket.Core;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A shopper: follows a <see cref="CustomerBrain"/> through departments, checkout, and out the door.
    /// Reacts to a moving mannequin like a person, not a guard: freezes, stares at where it was, and may report.
    /// </summary>
    public sealed class CustomerBehavior : NpcBehavior
    {
        private CustomerBrain brain;
        private Random random;
        private NpcShopping shopping;
        private int pathVersion = -1;
        private int attempts;
        private NavigationPoint lastBrowsed;
        private float leaving;
        /// <summary>Times the fallback had to re-path or re-target (debug and tests).</summary>
        public int Recoveries { get; private set; }
        private float glanceTimer;
        private float glanceYaw;
        public CustomerBrain Brain => brain;
        public override byte StateCode => brain != null ? (byte)brain.State : (byte)0;
        public override string StateLabel(byte code) => ((CustomerState)code).ToString();

        public void Begin(ShoppingList list, CustomerSettings settings, Random rng, bool alreadyInside)
        {
            random = rng;
            shopping = GetComponent<NpcShopping>();
            brain = new CustomerBrain(list, settings, rng);
            brain.ItemPicked += () => { if (shopping != null) shopping.Pick(list.Items[Mathf.Max(0, list.Picked - 1)]); };
            brain.Changed += OnStateChanged;
            if (alreadyInside) brain.StartShopping();
            pathVersion = -1; attempts = 0; leaving = 0; Recoveries = 0; lastBrowsed = null;
            Movement.SetPace(1f);
        }

        public override void Tick(float delta)
        {
            if (brain == null) return;
            if (brain.DestinationVersion != pathVersion) { attempts = 0; Route(); }
            if (!brain.Stationary && (Movement.Stuck || Movement.Unreachable)) { Recover(); return; }
            bool arrived = !brain.Stationary && Movement.Arrived;
            brain.Tick(arrived, delta);
            if (brain.State == CustomerState.Leaving && (leaving += delta) > brain.Settings.ExitTimeoutSeconds) { Controller.Depart(); return; }
            if (brain.Stationary) Idle(delta);
            if (brain.State == CustomerState.Gone) Controller.Depart();
        }

        private void Route()
        {
            pathVersion = brain.DestinationVersion;
            if (brain.Destination == DestinationKind.None) return;
            var point = Navigation.Choose(brain.Destination, brain.DestinationZone, brain.Destination == DestinationKind.Department ? lastBrowsed : null)
                        ?? Navigation.Choose(brain.Destination, brain.DestinationZone);
            if (point == null) { brain.Unreachable(); return; }
            if (!Movement.GoTo(point.Position)) Recover();
        }

        /// <summary>Never stay stuck: retry the same spot, then another valid spot, then give up on it.</summary>
        private void Recover()
        {
            attempts++; Recoveries++;
            var current = Navigation.Target;
            if (attempts == 1 && current != null && Movement.GoTo(current.Position)) return;
            if (attempts == 2)
            {
                var alternative = Navigation.Choose(brain.Destination, brain.DestinationZone, current);
                if (alternative != null && Movement.GoTo(alternative.Position)) return;
            }
            attempts = 0;
            brain.Unreachable();
        }

        /// <summary>Standing still: face the shelf, and now and then glance around like a real shopper.</summary>
        private void Idle(float delta)
        {
            if (brain.State == CustomerState.Alerted || brain.State == CustomerState.Reporting) return;
            var target = Navigation.Target;
            if (target == null) return;
            glanceTimer -= delta;
            if (glanceTimer <= 0)
            {
                glanceTimer = 2.5f + (float)random.NextDouble() * 4f;
                glanceYaw = random.NextDouble() < brain.Settings.GlanceChance ? (float)(random.NextDouble() * 160 - 80) : 0f;
            }
            Vector3 facing = Quaternion.Euler(0, glanceYaw, 0) * target.transform.forward;
            Movement.Face(transform.position + facing);
        }

        private void OnStateChanged(CustomerState state)
        {
            if (brain.Stationary) Movement.Stop();
            if (state == CustomerState.Browsing) lastBrowsed = Navigation.Target;
            if (state == CustomerState.Checkout && shopping != null) shopping.PayAndBag();
            if (state == CustomerState.Leaving) { leaving = 0; Movement.SetPace(brain.Purchases == 0 && ReportsSent > 0 ? (float)brain.Settings.HurriedPace : 1f); }
            if (state == CustomerState.Gone) Navigation.Release();
        }

        protected override void OnSuspicious(PerceptionTarget target, AwarenessTracker tracker)
        {
            if (brain == null) return;
            base.OnSuspicious(target, tracker);
            if (!brain.Alert()) tracker.Reset();
        }

        protected override void OnCalmed(PerceptionTarget target, AwarenessTracker tracker)
        {
            if (brain == null || Perception.Strongest >= AwarenessState.Suspicious) return;
            if (brain.State == CustomerState.Alerted || brain.State == CustomerState.Reporting) brain.CalmDown();
        }

        protected override bool WillReport(PerceptionTarget target, AwarenessTracker tracker) => brain != null && brain.State == CustomerState.Reporting;

        protected override void OnReported(SuspiciousActivityEvent report, bool accepted) => brain.ReportFiled();
    }
}
