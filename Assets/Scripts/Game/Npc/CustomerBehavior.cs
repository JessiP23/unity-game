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
        /// <summary>Who this is in the crowd (see <see cref="ShopperKind"/>).</summary>
        public ShopperKind Kind { get; private set; }
        /// <summary>A kid's grown-up. Followers have no shopping list of their own: they stay close and look around.</summary>
        public CustomerBehavior Leader { get; private set; }
        public bool Following => Leader != null;
        /// <summary>An influencer posted a clip of a mannequin moving. The crowd director turns it into a moment.</summary>
        public static event System.Action<CustomerBehavior, SuspiciousActivityEvent> Filmed;
        private float followTimer, sweepClock;
        private int leaderId;
        private Vector3 followSide;
        public override byte StateCode => brain != null ? (byte)brain.State : (byte)0;
        public override string StateLabel(byte code) => ((CustomerState)code).ToString();

        public void Begin(ShoppingList list, CustomerSettings settings, Random rng, bool alreadyInside, CrowdPull pull = null, ShopperKind kind = ShopperKind.Regular, CustomerBehavior leader = null)
        {
            random = rng;
            Kind = kind; Leader = leader; followTimer = 0; leaderId = leader != null && leader.Controller != null ? leader.Controller.Id : 0; sweepClock = (float)rng.NextDouble() * 10f;
            followSide = new Vector3(rng.NextDouble() < 0.5 ? -0.7f : 0.7f, 0f, -0.5f);
            shopping = GetComponent<NpcShopping>();
            Movement.SetPace(1f);
            pathVersion = -1; attempts = 0; leaving = 0; Recoveries = 0; lastBrowsed = null;
            if (leader != null) { brain = null; return; }
            brain = new CustomerBrain(list, settings, rng, pull);
            brain.ItemPicked += () =>
            {
                if (shopping == null) return;
                if (brain.LastPickWasSale) shopping.Pick(new ShoppingItem(brain.DestinationZone ?? ZoneType.Supermarket, "Flash-sale bargain", 1));
                else if (brain.LastPickWasLinger) shopping.Pick(new ShoppingItem(brain.DestinationZone ?? ZoneType.Supermarket, "Impulse buy", 1));
                else shopping.Pick(list.Items[Mathf.Max(0, list.Picked - 1)]);
            };
            brain.Changed += OnStateChanged;
            if (alreadyInside) brain.StartShopping();
        }

        public override void Tick(float delta)
        {
            if (Leader != null) { Follow(delta); return; }
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
            var point = Navigation.Choose(brain.Destination, brain.DestinationZone, brain.Destination == DestinationKind.Department ? lastBrowsed : null, brain.OnSale, brain.LingerGroup)
                        ?? Navigation.Choose(brain.Destination, brain.DestinationZone, null, brain.OnSale, brain.LingerGroup);
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
                var alternative = Navigation.Choose(brain.Destination, brain.DestinationZone, current, brain.OnSale, brain.LingerGroup);
                if (alternative != null && Movement.GoTo(alternative.Position)) return;
            }
            attempts = 0;
            brain.Unreachable();
        }

        /// <summary>
        /// A kid with their grown-up: keep to one side and a step behind, catch up when left behind, and look
        /// all around when the grown-up stops. Goes home when the grown-up does.
        /// </summary>
        private void Follow(float delta)
        {
            // Pooled bodies are reused: a grown-up whose body now belongs to someone else has gone home.
            if (Leader == null || !Leader.isActiveAndEnabled || Leader.Controller == null || Leader.Controller.Id != leaderId || Leader.Brain == null || Leader.Brain.State == CustomerState.Gone) { Controller.Depart(); return; }
            var me = Controller;
            bool watching = Perception != null && Perception.Strongest >= AwarenessState.Suspicious;
            if (watching) return; // pointing at something; base behaviour already stopped and turned us
            followTimer -= delta;
            Transform lead = Leader.transform;
            Vector3 spot = lead.position + lead.rotation * followSide;
            float gap = Vector3.Distance(Flat(me.transform.position), Flat(spot));
            if (followTimer <= 0)
            {
                followTimer = 0.4f;
                if (gap > 0.8f) { Movement.SetPace(gap > 3f ? 1.5f : 1.15f); Movement.GoTo(spot); }
                else if (Leader.Brain.Stationary) Movement.Stop();
            }
            if (Leader.Brain.Stationary && gap <= 0.8f)
            {
                glanceTimer -= delta;
                if (glanceTimer <= 0) { glanceTimer = 1.2f + (float)random.NextDouble() * 1.6f; glanceYaw = (float)(random.NextDouble() * 300 - 150); }
                Movement.Face(me.transform.position + Quaternion.Euler(0, glanceYaw, 0) * lead.forward);
            }
            if (Vector3.Distance(me.transform.position, lead.position) > 25f) Controller.Depart(); // lost: no kid wanders the mall alone
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Standing still: face the shelf, and now and then glance around like a real shopper.</summary>
        private void Idle(float delta)
        {
            if (brain.State == CustomerState.Alerted || brain.State == CustomerState.Reporting) return;
            var passer = Kind == ShopperKind.PhoneZombie ? null : Perception != null ? Perception.NearbyMover(2.4f, 100f) : null;
            if (passer != null) { Movement.Face(passer.transform.position); return; }
            var target = Navigation.Target;
            if (target == null) return;
            if (Kind == ShopperKind.Influencer)
            {
                // Filming: a slow pan across the room and back, the red light sweeping the floor ahead.
                sweepClock += delta;
                float pan = Mathf.Sin(sweepClock * 0.55f) * 70f;
                Movement.Face(transform.position + Quaternion.Euler(0, pan + 180f, 0) * target.transform.forward);
                return;
            }
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
            if (Leader != null)
            {
                // "Mum! That mannequin moved!" — the kid points and tugs; the grown-up turns to look.
                base.OnSuspicious(target, tracker);
                var look = GetComponent<ShopperLook>();
                if (look != null) look.Point(true);
                if (tracker.HasLastKnown && Leader.Controller != null) Leader.Controller.Movement.Pause(2.5f, ToVector(tracker.LastKnown));
                return;
            }
            if (brain == null) return;
            base.OnSuspicious(target, tracker);
            if (!brain.Alert()) tracker.Reset();
        }

        protected override void OnCalmed(PerceptionTarget target, AwarenessTracker tracker)
        {
            if (Leader != null) { var look = GetComponent<ShopperLook>(); if (look != null) look.Point(false); return; }
            if (brain == null || Perception.Strongest >= AwarenessState.Suspicious) return;
            if (brain.State == CustomerState.Alerted || brain.State == CustomerState.Reporting) brain.CalmDown();
        }

        protected override bool WillReport(PerceptionTarget target, AwarenessTracker tracker) => Leader != null || (brain != null && brain.State == CustomerState.Reporting);

        protected override void OnReported(SuspiciousActivityEvent report, bool accepted)
        {
            if (Leader != null) { var look = GetComponent<ShopperLook>(); if (look != null) look.Point(false); return; }
            if (accepted && Kind == ShopperKind.Influencer) Filmed?.Invoke(this, report);
            if (brain != null) brain.ReportFiled();
        }
    }
}
