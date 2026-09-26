using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using NUnit.Framework;
namespace NightSupermarket.Tests
{
    public sealed class AwarenessTests
    {
        private static readonly MapPoint Here = new MapPoint(1, 0, 2);
        private static AwarenessSettings Settings() => new AwarenessSettings
        { NoticeTime = 0.3, SuspicionGain = 1, SuspicionDecay = 0.5, StillDecay = 0.1, Threshold = 1, ReactionTime = 1, ReportDelay = 2, ForgetTime = 1 };

        [Test] public void StillMannequinIsJustAMannequin()
        {
            var tracker = new AwarenessTracker(Settings());
            for (int i = 0; i < 300; i++) Assert.That(tracker.Tick(true, 0, Here, 0.1), Is.False);
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Observing));
            Assert.That(tracker.Suspicion, Is.EqualTo(0));
        }

        [Test] public void MovingWhileWatchedBuildsSuspicionAfterNoticeTime()
        {
            var tracker = new AwarenessTracker(Settings());
            tracker.Tick(true, 1.5, Here, 0.2);
            Assert.That(tracker.Suspicion, Is.EqualTo(0), "a glance is too short to register movement");
            tracker.Tick(true, 1.5, Here, 0.2);
            Assert.That(tracker.Suspicion, Is.GreaterThan(0));
            for (int i = 0; i < 6; i++) tracker.Tick(true, 1.5, Here, 0.2);
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Suspicious));
        }

        [Test] public void MovementOutOfSightIsNeverNoticed()
        {
            var tracker = new AwarenessTracker(Settings());
            for (int i = 0; i < 100; i++) tracker.Tick(false, 3, Here, 0.1);
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Unaware));
            Assert.That(tracker.HasLastKnown, Is.False);
        }

        [Test] public void SuspicionDecaysAndFreezingEarlyCalmsTheObserver()
        {
            var tracker = new AwarenessTracker(Settings());
            for (int i = 0; i < 4; i++) tracker.Tick(true, 1.5, Here, 0.2);
            double partial = tracker.Suspicion;
            Assert.That(partial, Is.GreaterThan(0).And.LessThan(1));
            for (int i = 0; i < 10; i++) tracker.Tick(false, 0, Here, 0.2);
            Assert.That(tracker.Suspicion, Is.LessThan(partial));
            for (int i = 0; i < 30; i++) tracker.Tick(false, 0, Here, 0.2);
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Unaware));
        }

        [Test] public void ReportFiresOnceAfterReactionAndDelayWithLastSeenPosition()
        {
            var tracker = new AwarenessTracker(Settings());
            for (int i = 0; i < 8; i++) tracker.Tick(true, 1.5, Here, 0.2);
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Suspicious));
            var hidden = new MapPoint(9, 0, 9);
            int reports = 0;
            for (int i = 0; i < 40; i++) if (tracker.Tick(false, 1.5, hidden, 0.1)) reports++;
            Assert.That(tracker.State, Is.EqualTo(AwarenessState.Reporting));
            Assert.That(reports, Is.EqualTo(1));
            Assert.That(tracker.LastKnown.X, Is.EqualTo(Here.X), "the report keeps where it was seen, not where it went");
            Assert.That(tracker.LastKnown.Z, Is.EqualTo(Here.Z));
        }

        [Test] public void ThresholdAndSensitivityAreConfigurable()
        {
            var calm = Settings(); calm.Sensitivity = 0.25;
            var jumpy = Settings(); jumpy.Sensitivity = 2;
            var slow = new AwarenessTracker(calm); var fast = new AwarenessTracker(jumpy);
            for (int i = 0; i < 5; i++) { slow.Tick(true, 1.5, Here, 0.2); fast.Tick(true, 1.5, Here, 0.2); }
            Assert.That(fast.State, Is.EqualTo(AwarenessState.Suspicious));
            Assert.That(slow.State, Is.EqualTo(AwarenessState.Observing));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AwarenessTracker(new AwarenessSettings { Threshold = 0 }));
        }
    }

    public sealed class CustomerBrainTests
    {
        private static CustomerSettings Quick() => new CustomerSettings
        { BrowseMin = 1, BrowseMax = 1, PickTime = 0.5, WaitChance = 0, CheckoutMin = 1, CheckoutMax = 1, CheckoutChance = 1, ReportingProbability = 1, LeaveAfterReport = 1, StareTime = 1 };

        [Test] public void FullVisitEntersShopsPaysAndLeaves()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Clothing, "Shirt", 1), new ShoppingItem(ZoneType.Electronics, "TV", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(1));
            var seen = new List<CustomerState> { brain.State };
            brain.Changed += s => seen.Add(s);
            for (int i = 0; i < 200 && brain.State != CustomerState.Gone; i++) brain.Tick(!brain.Stationary, 0.25);
            CollectionAssert.AreEqual(new[]
            {
                CustomerState.Entering, CustomerState.Walking, CustomerState.Browsing, CustomerState.Shopping,
                CustomerState.Walking, CustomerState.Browsing, CustomerState.Shopping,
                CustomerState.MovingToCheckout, CustomerState.Checkout, CustomerState.Leaving, CustomerState.Gone
            }, seen);
            Assert.That(list.Done, Is.True);
        }

        [Test] public void DestinationsFollowTheShoppingList()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(2));
            Assert.That(brain.Destination, Is.EqualTo(DestinationKind.Entrance));
            brain.Tick(true, 0.1);
            Assert.That(brain.Destination, Is.EqualTo(DestinationKind.Department));
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Home));
        }

        [Test] public void UnreachableDepartmentIsSkippedNotRetriedForever()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Clothing, "Shirt", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(3));
            brain.Tick(true, 0.1);
            brain.Unreachable();
            Assert.That(list.Done, Is.True);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Leaving), "nothing bought, so no checkout");
        }

        [Test] public void AlertStopsShoppingAndReportingLeavesAfterwards()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Clothing, "Shirt", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(4));
            brain.Tick(true, 0.1);
            Assert.That(brain.Alert(), Is.True);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Reporting));
            Assert.That(brain.Stationary, Is.True, "a reporting customer stands and stares; it never chases");
            Assert.That(brain.Alert(), Is.False);
            brain.ReportFiled();
            Assert.That(brain.State, Is.EqualTo(CustomerState.Leaving));
        }

        [Test] public void ShruggingItOffResumesShopping()
        {
            var settings = Quick(); settings.ReportingProbability = 0;
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Clothing, "Shirt", 1) });
            var brain = new CustomerBrain(list, settings, new Random(5));
            brain.Tick(true, 0.1);
            Assert.That(brain.Alert(), Is.False);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Alerted));
            brain.Tick(false, 1.1);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Walking));
        }
    }

    public sealed class ShoppingAndZoneTests
    {
        [Test] public void ListsVaryBetweenCustomersAndRespectPreferences()
        {
            var routes = new HashSet<string>();
            var random = new Random(11);
            var clothing = new Dictionary<ZoneType, float> { { ZoneType.Clothing, 20f } };
            int clothingItems = 0, total = 0;
            for (int c = 0; c < 20; c++)
            {
                var list = ShoppingListGenerator.Create(ShoppingCatalog.Default, clothing, 1, 3, random);
                Assert.That(list.Items.Count, Is.InRange(1, 3));
                var route = new List<string>();
                foreach (var item in list.Items)
                {
                    if (route.Count == 0 || route[route.Count - 1] != item.Department.ToString()) route.Add(item.Department.ToString());
                    if (item.Department == ZoneType.Clothing) clothingItems++;
                    total++;
                }
                routes.Add(string.Join(">", route));
            }
            Assert.That(routes.Count, Is.GreaterThan(1), "customers must not all walk the same route");
            Assert.That(clothingItems, Is.GreaterThan(total / 2), "a fashion lover mostly shops clothing");
        }

        [Test] public void ItemsFromOneDepartmentAreGroupedIntoOneVisit()
        {
            var list = ShoppingListGenerator.Create(ShoppingCatalog.Default, null, 6, 6, new Random(3));
            var visited = new List<ZoneType>();
            foreach (var item in list.Items)
                if (visited.Count == 0 || visited[visited.Count - 1] != item.Department)
                {
                    Assert.That(visited.Contains(item.Department), Is.False);
                    visited.Add(item.Department);
                }
        }

        [Test] public void CustomersCannotEnterStaffZones()
        {
            foreach (var zone in new[] { ZoneType.Warehouse, ZoneType.Employee, ZoneType.Security })
            {
                Assert.That(ZoneAccess.Customer.Allows(zone), Is.False);
                Assert.That(ZoneAccess.Employee.Allows(zone), Is.True);
                Assert.That(ZoneAccess.IsRestricted(zone), Is.True);
            }
            Assert.That(ZoneAccess.Customer.Allows(ZoneType.Clothing), Is.True);
        }
    }

    public sealed class ReportAuthorityTests
    {
        private static LocalMatchAuthority Night()
        {
            var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
            authority.TryBeginNight();
            return authority;
        }

        [Test] public void AcceptedReportsReachSecurityAndTheCameras()
        {
            var authority = Night();
            var received = new List<SuspiciousActivityEvent>();
            authority.Reports.Subscribe(received.Add);
            var report = new SuspiciousActivityEvent("Customer 3", "p1", new MapPoint(14, 0, 32), 12.5, 1.1, ReportKind.MovingMannequin);
            Assert.That(authority.TryReport(report), Is.True);
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].LastKnownPosition.X, Is.EqualTo(14));
            Assert.That(authority.Stats.Reports, Is.EqualTo(1));
            var reader = new PlayerRecord("reader"); reader.SetState(PlayerState.Captured);
            Assert.That(authority.Surveillance.TryRead(reader, out var view), Is.True);
            Assert.That(view.Alerts.Count, Is.EqualTo(1), "security room hears the report");
            Assert.That(view.Alerts[0].LastKnownPosition.Z, Is.EqualTo(32));
        }

        [Test] public void ReportsOutsideTheNightAreIgnored()
        {
            var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
            Assert.That(authority.TryReport(new SuspiciousActivityEvent("c", "p", new MapPoint(), 0, 1, ReportKind.MovingMannequin)), Is.False);
            Assert.That(authority.Stats.Reports, Is.EqualTo(0));
        }

        [Test] public void CamerasListEveryoneWithoutAwarenessDetails()
        {
            var board = new SurveillanceBoard();
            board.ReportEntities(new[] { new EntitySighting("c1", EntityKind.Customer, new MapPoint(1, 0, 1)), new EntitySighting("g", EntityKind.Guard, new MapPoint()) });
            var reader = new PlayerRecord("p"); reader.SetState(PlayerState.Captured);
            Assert.That(board.TryRead(reader, out var view), Is.True);
            Assert.That(view.Entities.Count, Is.EqualTo(2));
            Assert.That(view.Entities[0].Kind, Is.EqualTo(EntityKind.Customer));
        }

        [Test] public void NightGradeRewardsUnnoticedEscapes()
        {
            var stats = new NightStats();
            Assert.That(stats.Grade(true, 120), Is.EqualTo("S"));
            stats.RecordReport();
            Assert.That(stats.Grade(true, 120), Is.EqualTo("A"));
            stats.RecordCapture(); stats.RecordReport();
            Assert.That(stats.Grade(true, 120), Is.EqualTo("C"));
            Assert.That(stats.Grade(false, 0), Is.EqualTo("D"));
        }
    }
}

namespace NightSupermarket.Tests
{
    using NightSupermarket.Game;
    using UnityEditor;
    using UnityEngine;

    public sealed class ConfigurationTests
    {
        private static NpcPopulationSettings Population() => Resources.Load<NpcPopulationSettings>("Npc/NpcPopulation");
        private static CustomerProfile Profile(string name)
        {
            foreach (var p in Population().profiles) if (p.name.Contains(name)) return p;
            throw new AssertionException("missing profile " + name);
        }

        [Test] public void PopulationBoundsComeFromTheAsset()
        {
            var settings = Population();
            Assert.That(settings, Is.Not.Null, "Assets/Resources/Npc/NpcPopulation.asset is the source of truth");
            Assert.That(settings.minimumCustomers, Is.EqualTo(6));
            Assert.That(settings.maximumCustomers, Is.EqualTo(12));
            Assert.That(settings.maxActiveCustomers, Is.EqualTo(16));
            Assert.That(settings.profiles.Length, Is.EqualTo(5));
        }

        [Test] public void PersonalitiesChangeSpeedBrowsingPreferencesReactionAndSensitivity()
        {
            var hurried = Profile("hurry"); var fashion = Profile("Fashion"); var gadget = Profile("Gadget");
            var nosy = Profile("Nosy"); var casual = Profile("Casual");
            Assert.That(hurried.walkSpeed.x, Is.GreaterThan(fashion.walkSpeed.y), "someone in a hurry always outwalks a browser");
            Assert.That(hurried.browseSeconds.y, Is.LessThan(fashion.browseSeconds.x), "and never lingers as long");
            Assert.That(nosy.reactionSeconds.y, Is.LessThan(hurried.reactionSeconds.x), "the nosy neighbour reacts first");
            Assert.That(nosy.suspicionSensitivity.x, Is.GreaterThan(gadget.suspicionSensitivity.y), "and is more easily unsettled");
            Assert.That(Favourite(fashion), Is.EqualTo(ZoneType.Clothing));
            Assert.That(Favourite(gadget), Is.EqualTo(ZoneType.Electronics));
            Assert.That(Favourite(nosy), Is.EqualTo(ZoneType.Home));
            Assert.That(Favourite(casual), Is.EqualTo(ZoneType.Supermarket));
            var random = new System.Random(4);
            var rolled = hurried.CreateAwareness(random);
            Assert.That(rolled.ReactionTime, Is.InRange(hurried.reactionSeconds.x, hurried.reactionSeconds.y));
            Assert.That(rolled.Sensitivity, Is.InRange(hurried.suspicionSensitivity.x, hurried.suspicionSensitivity.y));
        }

        [Test] public void CustomersPerceiveLessThanTheGuardAndForget()
        {
            var guard = AssetDatabase.LoadAssetAtPath<GameRulesAsset>("Assets/Settings/GameRules.asset");
            foreach (var profile in Population().profiles)
            {
                Assert.That(profile.visionRange, Is.LessThan(guard.visionDistance), profile.name);
                Assert.That(profile.fieldOfView, Is.LessThan(guard.fieldOfView), profile.name);
                Assert.That(profile.suspicionDecay, Is.GreaterThan(0), profile.name + " suspicion must fade");
            }
            var security = new DetectionSystem(guard.CreateRules());
            for (int i = 0; i < 40; i++) security.Tick(true, 2, 0.1);
            int guardSuspicion = security.Suspicion.Value;
            for (int i = 0; i < 100; i++) security.Tick(false, 0, 0.1);
            Assert.That(guardSuspicion, Is.GreaterThan(0));
            Assert.That(security.Suspicion.Value, Is.EqualTo(guardSuspicion), "guard suspicion never fades; that is what separates security from shoppers");
        }

        [Test] public void ReportsNeverDecideTheNight()
        {
            var session = new LocalSession();
            var authority = new LocalMatchAuthority(new GameRules(), session);
            for (int i = 0; i < 4; i++) authority.Register(new PlayerRecord(session.Join()));
            authority.TryBeginNight();
            for (int i = 0; i < 50; i++)
                authority.TryReport(new SuspiciousActivityEvent("c" + i, "p", new MapPoint(i, 0, 0), i, 1, ReportKind.MovingMannequin));
            authority.Tick(1);
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.Night));
            Assert.That(authority.Warehouse.Count, Is.EqualTo(0), "customers never capture");
        }

        [Test] public void PeopleLineNeverShowsNumbers()
        {
            foreach (AwarenessState state in System.Enum.GetValues(typeof(AwarenessState)))
                Assert.That(System.Text.RegularExpressions.Regex.Replace(HudText.CrowdLabel(state), "<[^>]+>", ""), Does.Not.Match("[0-9]"));
        }

        [Test] public void LongVisitsEndWithTheCustomerLeaving()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1) });
            var brain = new CustomerBrain(list, new CustomerSettings { BrowseMin = 600, BrowseMax = 600, MaxVisitSeconds = 5 }, new System.Random(1));
            brain.Tick(true, 0.1); brain.Tick(true, 0.1);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing));
            for (int i = 0; i < 60; i++) brain.Tick(false, 0.1);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Leaving));
        }

        private static ZoneType Favourite(CustomerProfile profile)
        {
            var best = ZoneType.Supermarket; float weight = -1;
            foreach (var p in profile.preferredDepartments) if (p.weight > weight) { weight = p.weight; best = p.department; }
            return best;
        }
    }
}
