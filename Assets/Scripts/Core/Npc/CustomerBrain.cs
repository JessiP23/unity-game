using System;
namespace NightSupermarket.Core
{
    public enum CustomerState { Entering, Walking, Browsing, Shopping, Waiting, MovingToCheckout, Checkout, Leaving, Alerted, Reporting, Gone }

    /// <summary>Where the customer wants to be next; the Unity side turns this into a navigation point.</summary>
    public enum DestinationKind { None, Entrance, Department, Checkout, Exit }

    /// <summary>Per-customer tuning, drawn from a profile with controlled randomness.</summary>
    public sealed class CustomerSettings
    {
        public double BrowseMin { get; set; } = 2;
        public double BrowseMax { get; set; } = 8;
        public double PickTime { get; set; } = 1.2;
        public double WaitMin { get; set; } = 1;
        public double WaitMax { get; set; } = 3;
        /// <summary>Chance of pausing to look around between destinations.</summary>
        public double WaitChance { get; set; } = 0.35;
        public double CheckoutMin { get; set; } = 3;
        public double CheckoutMax { get; set; } = 6;
        /// <summary>Chance a customer with purchases actually pays before leaving.</summary>
        public double CheckoutChance { get; set; } = 0.9;
        /// <summary>Chance the customer reports what they saw instead of shrugging it off.</summary>
        public double ReportingProbability { get; set; } = 0.85;
        /// <summary>Chance a customer who reported leaves the store instead of carrying on.</summary>
        public double LeaveAfterReport { get; set; } = 0.5;
        public double StareTime { get; set; } = 1.2;
        /// <summary>Seconds after which a customer gives up on the rest of the list and heads for the exit.</summary>
        public double MaxVisitSeconds { get; set; } = 420;
        /// <summary>Seconds a leaving customer may spend failing to reach an exit before being removed at the door.</summary>
        public double ExitTimeoutSeconds { get; set; } = 90;
        /// <summary>Walking pace multiplier when leaving shaken after a report.</summary>
        public double HurriedPace { get; set; } = 1.3;
        /// <summary>Chance, each few seconds while standing, of glancing sideways instead of at the shelf.</summary>
        public double GlanceChance { get; set; } = 0.35;
    }

    /// <summary>
    /// Customer state machine. Pure logic: the Unity behaviour reports arrival and perception events,
    /// the brain decides what to do and where to go. States are table-driven so new ones slot in.
    /// </summary>
    public sealed class CustomerBrain
    {
        private readonly ShoppingList list;
        private readonly CustomerSettings settings;
        private readonly Random random;
        private double timer, duration;
        public CustomerState State { get; private set; }
        public DestinationKind Destination { get; private set; }
        public ZoneType? DestinationZone { get; private set; }
        /// <summary>Bumped whenever the destination changes, so movement knows to re-path.</summary>
        public int DestinationVersion { get; private set; }
        public int Purchases { get; private set; }
        public ShoppingList List => list;
        public CustomerSettings Settings => settings;
        public event Action<CustomerState> Changed;
        public event Action ItemPicked;

        public CustomerBrain(ShoppingList list, CustomerSettings settings, Random random)
        {
            this.list = list ?? throw new ArgumentNullException(nameof(list));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Enter(CustomerState.Entering, DestinationKind.Entrance, null);
        }

        /// <summary>Starts partway through shopping, used to populate the store at the start of the night.</summary>
        public void StartShopping() => NextDestination();

        /// <summary>Seconds since the customer entered.</summary>
        public double VisitSeconds { get; private set; }

        public void Tick(bool arrived, double delta)
        {
            GameRules.RequireDelta(delta);
            timer += delta;
            VisitSeconds += delta;
            if (VisitSeconds > settings.MaxVisitSeconds && Leavable) { Enter(CustomerState.Leaving, DestinationKind.Exit, null); return; }
            switch (State)
            {
                case CustomerState.Entering: if (arrived) NextDestination(); break;
                case CustomerState.Walking: if (arrived) Begin(CustomerState.Browsing, Range(settings.BrowseMin, settings.BrowseMax)); break;
                case CustomerState.Browsing: if (timer >= duration) Begin(CustomerState.Shopping, settings.PickTime); break;
                case CustomerState.Shopping:
                    if (timer < duration) break;
                    list.MarkPicked(); Purchases++; ItemPicked?.Invoke();
                    if (random.NextDouble() < settings.WaitChance) Begin(CustomerState.Waiting, Range(settings.WaitMin, settings.WaitMax));
                    else NextDestination();
                    break;
                case CustomerState.Waiting: if (timer >= duration) NextDestination(); break;
                case CustomerState.MovingToCheckout: if (arrived) Begin(CustomerState.Checkout, Range(settings.CheckoutMin, settings.CheckoutMax)); break;
                case CustomerState.Checkout: if (timer >= duration) { Purchases = 0; Enter(CustomerState.Leaving, DestinationKind.Exit, null); } break;
                case CustomerState.Leaving: if (arrived) Enter(CustomerState.Gone, DestinationKind.None, null); break;
                case CustomerState.Alerted: if (timer >= duration) NextDestination(); break;
            }
        }

        /// <summary>Something looked wrong: stop and stare. Returns true if this person will report it.</summary>
        public bool Alert()
        {
            if (State == CustomerState.Alerted || State == CustomerState.Reporting || State == CustomerState.Gone) return false;
            bool willReport = random.NextDouble() < settings.ReportingProbability;
            Begin(willReport ? CustomerState.Reporting : CustomerState.Alerted, settings.StareTime);
            return willReport;
        }

        /// <summary>The report went out; either leave shaken or carry on shopping.</summary>
        public void ReportFiled()
        {
            if (State != CustomerState.Reporting) return;
            if (random.NextDouble() < settings.LeaveAfterReport) Enter(CustomerState.Leaving, DestinationKind.Exit, null);
            else NextDestination();
        }

        /// <summary>Perception calmed down before a report; go back to what they were doing.</summary>
        public void CalmDown() { if (State == CustomerState.Alerted || State == CustomerState.Reporting) NextDestination(); }

        private bool Leavable => State != CustomerState.Leaving && State != CustomerState.Gone && State != CustomerState.Reporting && State != CustomerState.Checkout;

        /// <summary>Gives up on the visit and walks out.</summary>
        public void Leave() { if (State != CustomerState.Gone) Enter(CustomerState.Leaving, DestinationKind.Exit, null); }

        /// <summary>True while the customer should stand still rather than walk.</summary>
        public bool Stationary => State == CustomerState.Browsing || State == CustomerState.Shopping || State == CustomerState.Waiting ||
                                  State == CustomerState.Checkout || State == CustomerState.Alerted || State == CustomerState.Reporting || State == CustomerState.Gone;

        /// <summary>The current destination could not be reached; drop it and move on.</summary>
        public void Unreachable()
        {
            if (State == CustomerState.Walking) { list.Skip(); NextDestination(); }
            else if (State == CustomerState.MovingToCheckout) Enter(CustomerState.Leaving, DestinationKind.Exit, null);
            else if (State == CustomerState.Entering) NextDestination();
            else if (State == CustomerState.Leaving) Enter(CustomerState.Gone, DestinationKind.None, null);
        }

        private void NextDestination()
        {
            var item = list.Current;
            if (item.HasValue) Enter(CustomerState.Walking, DestinationKind.Department, item.Value.Department);
            else if (Purchases > 0 && random.NextDouble() < settings.CheckoutChance) Enter(CustomerState.MovingToCheckout, DestinationKind.Checkout, ZoneType.Checkout);
            else Enter(CustomerState.Leaving, DestinationKind.Exit, null);
        }

        /// <summary>Timed state that keeps the current destination.</summary>
        private void Begin(CustomerState state, double seconds)
        {
            timer = 0; duration = seconds;
            SetState(state);
        }

        private void Enter(CustomerState state, DestinationKind destination, ZoneType? zone)
        {
            timer = 0;
            Destination = destination; DestinationZone = zone; DestinationVersion++;
            SetState(state);
        }

        private void SetState(CustomerState state)
        {
            if (State == state) return;
            State = state;
            Changed?.Invoke(state);
        }

        private double Range(double min, double max) => min + random.NextDouble() * Math.Max(0, max - min);
    }
}
