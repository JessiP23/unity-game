using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Behavioural checks for customer stability, perception, reports, and guard investigation.</summary>
    public sealed class CustomerStabilityTests
    {
        private static CustomerProfile Quick(ZoneType department)
        {
            var profile = Arena.Watcher();
            profile.browseSeconds = new Vector2(0.5f, 0.5f); profile.checkoutSeconds = new Vector2(0.5f, 0.5f); profile.checkoutChance = 1;
            profile.preferredDepartments = new[]
            {
                new CustomerProfile.Preference { department = department, weight = 1 },
                new CustomerProfile.Preference { department = department == ZoneType.Clothing ? ZoneType.Home : ZoneType.Clothing, weight = 0 },
                new CustomerProfile.Preference { department = ZoneType.Supermarket, weight = 0 },
                new CustomerProfile.Preference { department = ZoneType.Electronics, weight = 0 },
            };
            return profile;
        }

        [UnityTest] public IEnumerator UnreachableDepartmentIsRetriedThenSkippedAndTheCustomerLeaves()
        {
            var arena = new Arena(island: true);
            float scale = Time.timeScale;
            try
            {
                arena.Directory.AddPoint(ZoneType.Home, PointKind.Browse, Arena.Island, 0);
                var population = arena.Population(Quick(ZoneType.Home), _ => true);
                var npc = population.Spawn(NpcRole.Customer, Arena.Origin + new Vector3(0, 0, -9), 0, false);
                var customer = (CustomerBehavior)npc.Behavior;
                var states = new List<CustomerState>();
                customer.Brain.Changed += states.Add;
                Time.timeScale = 4;
                float end = Time.time + 60;
                while (Time.time < end && population.Customers > 0) yield return null;
                Assert.That(customer.Recoveries, Is.GreaterThanOrEqualTo(2), "retried and looked for another spot first");
                Assert.That(customer.Brain.List.Done, Is.True, "the unreachable item was dropped");
                Assert.That(states, Has.No.Member(CustomerState.Browsing), "it never pretended to arrive");
                Assert.That(states, Has.Member(CustomerState.Leaving));
                Assert.That(population.Customers, Is.EqualTo(0), "and it left instead of standing stuck");
            }
            finally { Time.timeScale = scale; arena.Dispose(); }
        }

        [UnityTest] public IEnumerator CustomerWithNoReachableExitStillLeavesTheSimulation()
        {
            var arena = new Arena(island: true);
            float scale = Time.timeScale;
            try
            {
                foreach (var point in arena.Directory.Points)
                    if (point.kind == PointKind.Exit) point.transform.position = Arena.Island;
                var population = arena.Population(Quick(ZoneType.Clothing), _ => true);
                population.Spawn(NpcRole.Customer, Arena.Origin, 0, true);
                Time.timeScale = 4;
                float end = Time.time + 80;
                while (Time.time < end && population.Customers > 0) yield return null;
                Assert.That(population.Customers, Is.EqualTo(0), "no customer is ever permanently stuck");
                Assert.That(population.Pooled, Is.EqualTo(1));
            }
            finally { Time.timeScale = scale; arena.Dispose(); }
        }

        [UnityTest] public IEnumerator PopulationRespectsItsCapRecyclesAndLeaksNothing()
        {
            var arena = new Arena();
            float scale = Time.timeScale;
            try
            {
                for (int i = 0; i < 6; i++) arena.Directory.AddPoint(ZoneType.Clothing, PointKind.Browse, Arena.Origin + new Vector3(-5 + i * 2, 0, 4), 0);
                var population = arena.Population(Quick(ZoneType.Clothing), _ => true);
                population.Settings.minimumCustomers = 2; population.Settings.maximumCustomers = 3; population.Settings.maxActiveCustomers = 3;
                population.Settings.spawnInterval = 0.2f; population.Settings.crowdCycleSeconds = 4;
                Time.timeScale = 6;
                int peak = 0, reached = 0;
                var everyone = new HashSet<int>();
                float end = Time.time + 150;
                while (Time.time < end)
                {
                    yield return null;
                    int count = population.Customers;
                    peak = Mathf.Max(peak, count);
                    if (count >= population.Settings.minimumCustomers) reached++;
                    var ids = new HashSet<int>();
                    foreach (var npc in population.Active)
                    {
                        Assert.That(npc != null && npc.isActiveAndEnabled, Is.True, "no destroyed or inactive NPC in the active list");
                        Assert.That(ids.Add(npc.Id), Is.True, "no duplicate NPC references");
                        everyone.Add(npc.Id);
                    }
                }
                Assert.That(peak, Is.LessThanOrEqualTo(3), "hard maximum respected");
                Assert.That(reached, Is.GreaterThan(0), "the minimum population is reached");
                int bodies = population.GetComponentsInChildren<NpcController>(true).Length;
                Assert.That(everyone.Count, Is.GreaterThan(bodies), "more customers visited than bodies exist: leavers were recycled");
                Assert.That(bodies, Is.EqualTo(population.Active.Count + population.Pooled), "every body is either active or pooled");
                Assert.That(bodies, Is.LessThanOrEqualTo(4), "recycling, not endless instantiation");
            }
            finally { Time.timeScale = scale; arena.Dispose(); }
        }

        [UnityTest] public IEnumerator PersonalitySetsTheWalkingPace()
        {
            var arena = new Arena();
            try
            {
                var fast = Arena.Watcher(); fast.walkSpeed = new Vector2(1.5f, 1.6f);
                var slow = Arena.Watcher(); slow.walkSpeed = new Vector2(0.9f, 1.0f);
                var hurried = arena.Population(fast, _ => true).Spawn(NpcRole.Customer, Arena.Origin + new Vector3(-3, 0, -6), 0, false);
                var browsing = arena.Population(slow, _ => true).Spawn(NpcRole.Customer, Arena.Origin + new Vector3(3, 0, -6), 0, false);
                yield return null;
                Assert.That(hurried.Movement.Agent.speed, Is.InRange(1.5f, 1.6f));
                Assert.That(browsing.Movement.Agent.speed, Is.InRange(0.9f, 1.0f));
            }
            finally { arena.Dispose(); }
        }

        [UnityTest] public IEnumerator CustomersWalkAroundMissionCratesWithoutMovingThem()
        {
            var arena = new Arena();
            try
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.transform.SetParent(arena.Root.transform);
                crate.transform.position = Arena.Origin + new Vector3(0, 0.3f, -3); crate.transform.localScale = Vector3.one * 0.6f;
                var definition = ScriptableObject.CreateInstance<ItemDefinition>(); arena.Owned.Add(definition);
                crate.AddComponent<PhysicalItem>().Configure(definition, new WorldSignals());
                for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
                Vector3 rest = crate.transform.position;
                var population = arena.Population(Arena.Watcher(), _ => true);
                var npc = population.Spawn(NpcRole.Customer, Arena.Origin + new Vector3(0, 0, -9), 0, false);
                var brain = ((CustomerBehavior)npc.Behavior).Brain;
                float end = Time.time + 20;
                while (Time.time < end && brain.State != CustomerState.Browsing) yield return null;
                Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing), "reached the shelf past the crate");
                Assert.That(Vector3.Distance(crate.transform.position, rest), Is.LessThan(0.05f), "mission crate untouched");
            }
            finally { arena.Dispose(); }
        }
    }

    public sealed class SharedVisionTests
    {
        [UnityTest] public IEnumerator ShelvesBlockSightAndCapturedMannequinsAreIgnored()
        {
            var arena = new Arena();
            try
            {
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = "Shelf"; shelf.transform.SetParent(arena.Root.transform);
                shelf.transform.position = Arena.Origin + new Vector3(0, 1.07f, 2.5f); shelf.transform.localScale = new Vector3(8f, 2.14f, 1.05f);
                var observer = new GameObject("Observer").AddComponent<NpcPerception>();
                observer.transform.SetParent(arena.Root.transform);
                observer.transform.SetPositionAndRotation(Arena.Origin, Quaternion.identity);
                observer.Configure(8, 70, 0.1f, new AwarenessSettings { NoticeTime = 0.1 }, 0);
                var hidden = arena.Mannequin(Arena.Origin + new Vector3(0, 0.1f, 4.5f));
                var captured = arena.Mannequin(Arena.Origin + new Vector3(-0.6f, 0.1f, 1.8f));
                captured.Record.SetState(PlayerState.Captured);
                Physics.SyncTransforms();
                for (int i = 0; i < 60; i++)
                {
                    yield return new WaitForFixedUpdate();
                    hidden.Simulate(new PlayerCommand(i % 30 < 15 ? Vector2.right : Vector2.left, false, false), Time.fixedDeltaTime);
                    observer.Tick(Time.fixedDeltaTime);
                }
                Assert.That(observer.StateFor(hidden.GetComponent<PerceptionTarget>()), Is.EqualTo(AwarenessState.Unaware), "a shelf hides a moving mannequin");
                Assert.That(observer.StateFor(captured.GetComponent<PerceptionTarget>()), Is.EqualTo(AwarenessState.Unaware), "captured mannequins are not targets");
                shelf.SetActive(false); captured.Record.SetState(PlayerState.Normal);
                for (int i = 0; i < 20; i++) { yield return new WaitForFixedUpdate(); observer.Tick(Time.fixedDeltaTime); }
                Assert.That(observer.StateFor(hidden.GetComponent<PerceptionTarget>()), Is.Not.EqualTo(AwarenessState.Unaware), "control: visible without the shelf");
                Assert.That(observer.StateFor(captured.GetComponent<PerceptionTarget>()), Is.Not.EqualTo(AwarenessState.Unaware), "control: noticeable once free");
            }
            finally { arena.Dispose(); }
        }

        [Test] public void GuardEyesReachFurtherThanAnyShopper()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var guardRules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                floor.transform.position = new Vector3(500, -0.5f, 500); floor.transform.localScale = new Vector3(30, 1, 30);
                Physics.SyncTransforms();
                var guard = new GuardVisionSystem(guardRules.visionDistance, guardRules.fieldOfView, ~(1 << 2));
                Vector3 eye = new Vector3(500, 1.6f, 500);
                Vector3 far = eye + Vector3.forward * 10f, wide = eye + Quaternion.Euler(0, 44, 0) * Vector3.forward * 4f;
                Assert.That(guard.CanSee(eye, Vector3.forward, far).Visible, Is.True);
                Assert.That(guard.CanSee(eye, Vector3.forward, wide).Visible, Is.True);
                foreach (var profile in CustomerProfile.Defaults())
                {
                    var shopper = new VisionSensor(profile.visionRange, profile.fieldOfView, ~(1 << 2));
                    Assert.That(shopper.CanSee(eye, Vector3.forward, far).Visible, Is.False, profile.name + " cannot see 10 m");
                    Assert.That(shopper.CanSee(eye, Vector3.forward, wide).Visible, Is.False, profile.name + " cannot see 44 degrees aside");
                    Object.DestroyImmediate(profile);
                }
            }
            finally { Object.Destroy(floor); Object.Destroy(guardRules); }
        }
    }

    public sealed class ReportAndInvestigationTests
    {
        private static IEnumerator Drive(PlayerMotor motor, float seconds, Vector2 direction)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                motor.Simulate(new PlayerCommand(direction, false, false), Time.fixedDeltaTime);
            }
        }
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        [UnityTest] public IEnumerator ReportPointsToWhereTheMannequinWasLastSeenNotWhereItWent()
        {
            var arena = new Arena(wall: true);
            try
            {
                var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
                authority.TryBeginNight();
                var reports = new List<SuspiciousActivityEvent>();
                authority.Reports.Subscribe(reports.Add);
                var population = arena.Population(Arena.Watcher(), authority.TryReport);
                var customer = population.Spawn(NpcRole.Customer, Arena.Origin, 0, true);
                var brain = ((CustomerBehavior)customer.Behavior).Brain;
                float wait = Time.time + 6;
                while (Time.time < wait && brain.State != CustomerState.Browsing) yield return null;
                var mannequin = arena.Mannequin(Arena.Origin + new Vector3(0, 0.1f, 3));
                var target = mannequin.GetComponent<PerceptionTarget>();
                Physics.SyncTransforms();
                yield return Drive(mannequin, 1.0f, Vector2.zero);
                double start = Time.timeAsDouble;
                yield return Drive(mannequin, 1.4f, Vector2.up);
                Assert.That(customer.Perception.StateFor(target), Is.GreaterThanOrEqualTo(AwarenessState.Suspicious));
                Vector3 a = mannequin.transform.position;
                mannequin.Teleport(Arena.Origin + new Vector3(6, 0.1f, 1));
                Physics.SyncTransforms();
                Vector3 b = mannequin.transform.position;
                wait = Time.time + 6;
                while (Time.time < wait && reports.Count == 0) yield return Drive(mannequin, 0.1f, Vector2.up);
                Vector3 c = mannequin.transform.position;
                Assert.That(reports.Count, Is.EqualTo(1));
                var report = reports[0];
                var at = new Vector3(report.LastKnownPosition.X, 0, report.LastKnownPosition.Z);
                Assert.That(Vector3.Distance(at, Flat(a)), Is.LessThan(0.6f), "A: last place the customer saw it");
                Assert.That(Vector3.Distance(at, Flat(b)), Is.GreaterThan(3f), "not B");
                Assert.That(Vector3.Distance(Flat(b), Flat(c)), Is.GreaterThan(0.5f), "it kept moving out of sight");
                Assert.That(Vector3.Distance(at, Flat(c)), Is.GreaterThan(3f), "not C");
                Assert.That(report.SourceId, Does.StartWith("Customer"));
                Assert.That(report.TargetId, Is.EqualTo(mannequin.Record.Id));
                Assert.That(report.Zone, Is.EqualTo(ZoneType.Clothing), "report names the area");
                Assert.That(report.Kind, Is.EqualTo(ReportKind.MovingMannequin));
                Assert.That(report.Timestamp, Is.GreaterThan(start));
                Assert.That(report.Confidence, Is.GreaterThan(0));
            }
            finally { arena.Dispose(); }
        }

        [UnityTest] public IEnumerator AfterReportingTheCustomerForgetsAndGoesBackToShopping()
        {
            var arena = new Arena();
            try
            {
                var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
                authority.TryBeginNight();
                var profile = Arena.Watcher(); profile.items = new Vector2Int(2, 2);
                arena.Directory.AddPoint(ZoneType.Clothing, PointKind.Browse, Arena.Origin + new Vector3(-6, 0, -6), 180);
                var population = arena.Population(profile, authority.TryReport);
                var customer = population.Spawn(NpcRole.Customer, Arena.Origin, 0, true);
                var brain = ((CustomerBehavior)customer.Behavior).Brain;
                var states = new List<CustomerState>();
                brain.Changed += states.Add;
                float wait = Time.time + 8;
                while (Time.time < wait && brain.State != CustomerState.Browsing) yield return null;
                var mannequin = arena.Mannequin(customer.transform.position + customer.transform.forward * 3 + Vector3.up * 0.1f);
                mannequin.transform.rotation = customer.transform.rotation;
                var target = mannequin.GetComponent<PerceptionTarget>();
                Physics.SyncTransforms();
                yield return Drive(mannequin, 1.0f, Vector2.zero);
                Assert.That(customer.Perception.StateFor(target), Is.EqualTo(AwarenessState.Observing), "OBSERVING");
                yield return Drive(mannequin, 1.4f, Vector2.up);
                Assert.That(customer.Perception.StateFor(target), Is.GreaterThanOrEqualTo(AwarenessState.Suspicious), "SUSPICIOUS");
                float closest = Vector3.Distance(Flat(customer.transform.position), Flat(mannequin.transform.position));
                wait = Time.time + 6;
                while (Time.time < wait && authority.Stats.Reports == 0)
                {
                    yield return Drive(mannequin, 0.1f, Vector2.zero);
                    closest = Mathf.Min(closest, Vector3.Distance(Flat(customer.transform.position), Flat(mannequin.transform.position)));
                }
                Assert.That(authority.Stats.Reports, Is.EqualTo(1), "REPORTING");
                Assert.That(states, Has.Member(CustomerState.Reporting));
                Assert.That(customer.Perception.StateFor(target), Is.EqualTo(AwarenessState.Unaware), "back to NORMAL: stops tracking");
                Vector3 before = customer.transform.position;
                yield return Drive(mannequin, 3f, Vector2.zero);
                Assert.That(brain.State, Is.Not.EqualTo(CustomerState.Reporting).And.Not.EqualTo(CustomerState.Alerted), "carries on with the day");
                Assert.That(customer.Navigation.Target != null && customer.Navigation.Target.kind == PointKind.Browse, Is.True, "heads for a shelf, not the mannequin");
                Assert.That(Vector3.Distance(Flat(customer.transform.position), Flat(mannequin.transform.position)), Is.GreaterThan(closest - 0.3f), "never follows it");
                Assert.That(Vector3.Distance(customer.transform.position, before), Is.GreaterThan(0.5f), "actually walks on");
                Assert.That(authority.Warehouse.Count, Is.EqualTo(0), "customers never capture");
            }
            finally { arena.Dispose(); }
        }

        [UnityTest] public IEnumerator GuardWalksToTheReportedSpotAndSearchesWithoutKnowingWhereTheMannequinIs()
        {
            var arena = new Arena();
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
                authority.TryBeginNight();
                var guardObject = new GameObject("Guard");
                guardObject.transform.SetParent(arena.Root.transform);
                guardObject.transform.position = Arena.Origin + new Vector3(-12, 0, -12);
                var guard = guardObject.AddComponent<GuardController>();
                guard.Configure(rules, new WorldSignals(), new[] { Arena.Origin + new Vector3(-12, 0, -12) });
                guard.InvestigateReports(authority.Reports);
                var mannequin = arena.Mannequin(Arena.Origin + new Vector3(12, 0.1f, 12));
                for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); guard.Tick(null, null, Time.fixedDeltaTime); }
                var spot = Arena.Origin + new Vector3(6, 0, 4);
                authority.TryReport(new SuspiciousActivityEvent("Customer 1", mannequin.Record.Id, new MapPoint(spot.x, spot.y, spot.z), Time.timeAsDouble, 1.2, ReportKind.MovingMannequin, ZoneType.Clothing));
                Assert.That(guard.Brain.State, Is.EqualTo(GuardState.Investigate));
                Vector3 last = guardObject.transform.position;
                float maxStep = 0, speed = Mathf.Max(rules.guardSpeed, rules.guardChaseSpeed);
                bool searched = false;
                float end = Time.time + 20;
                while (Time.time < end && !searched)
                {
                    yield return new WaitForFixedUpdate();
                    guard.Tick(null, null, Time.fixedDeltaTime);
                    maxStep = Mathf.Max(maxStep, Vector3.Distance(Flat(guardObject.transform.position), Flat(last)) / Time.fixedDeltaTime);
                    last = guardObject.transform.position;
                    searched = guard.Brain.State == GuardState.Search;
                }
                Assert.That(searched, Is.True, "guard arrived and searched");
                Assert.That(Vector3.Distance(Flat(guardObject.transform.position), Flat(spot)), Is.LessThan(1.5f), "at the reported spot");
                Assert.That(maxStep, Is.LessThan(speed * 2f), "walked there; never teleported");
                Assert.That(Vector3.Distance(Flat(guard.LastKnownPosition), Flat(mannequin.transform.position)), Is.GreaterThan(5f), "never learned the mannequin's real position");
            }
            finally { Object.Destroy(rules); arena.Dispose(); }
        }

        [UnityTest] public IEnumerator IdleGuardWalksToANoiseInsteadOfSearchingWhereItStands()
        {
            var arena = new Arena();
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                var signals = new WorldSignals();
                var guardObject = new GameObject("Guard");
                guardObject.transform.SetParent(arena.Root.transform);
                guardObject.transform.position = Arena.Origin + new Vector3(-6, 0, 0);
                var guard = guardObject.AddComponent<GuardController>();
                var post = Arena.Origin + new Vector3(-6, 0, 0);
                guard.Configure(rules, signals, new[] { post });
                for (int i = 0; i < 60; i++) { yield return new WaitForFixedUpdate(); guard.Tick(null, null, Time.fixedDeltaTime); }
                Assert.That(guard.Brain.State, Is.EqualTo(GuardState.Patrol), "standing at its post");
                var noise = Arena.Origin + new Vector3(3, 0, 0);
                signals.Noise.Publish(new NoiseEvent(noise, 1, "test"));
                Vector3 start = guardObject.transform.position;
                bool searchedEarly = false;
                float end = Time.time + 2;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    guard.Tick(null, null, Time.fixedDeltaTime);
                    if (guard.Brain.State == GuardState.Search && Vector3.Distance(Flat(guardObject.transform.position), Flat(noise)) > 2f) searchedEarly = true;
                }
                Assert.That(searchedEarly, Is.False, "does not search on the spot before getting there");
                Assert.That(Vector3.Distance(Flat(guardObject.transform.position), Flat(start)), Is.GreaterThan(2f), "walks toward the noise");
            }
            finally { Object.Destroy(rules); arena.Dispose(); }
        }
    }

    public sealed class EmployeeArchitectureTests
    {
        [UnityTest] public IEnumerator EmployeeUsesTheSharedStackWithoutCustomerLogicAndMayEnterStaffAreas()
        {
            var arena = new Arena(warehouse: true);
            try
            {
                var inside = Arena.Origin + new Vector3(15.5f, 0, 0);
                arena.Directory.AddPoint(ZoneType.Warehouse, PointKind.Work, inside, 90);
                var population = arena.Population(Arena.Watcher(), _ => true);
                var employee = population.Spawn(NpcRole.Employee, Arena.Origin, 0, true);
                Assert.That(employee.Behavior, Is.InstanceOf<EmployeeBehavior>());
                Assert.That(employee.GetComponent<CustomerBehavior>(), Is.Null);
                Assert.That(employee.GetComponent<NpcShopping>(), Is.Null, "no shopping list or basket");
                Assert.That(employee.Movement, Is.Not.Null); Assert.That(employee.Perception, Is.Not.Null); Assert.That(employee.Navigation, Is.Not.Null);
                float end = Time.time + 20;
                while (Time.time < end && employee.StateLabel != EmployeeState.Working.ToString()) yield return null;
                Assert.That(employee.StateLabel, Is.EqualTo(EmployeeState.Working.ToString()));
                Assert.That(Vector3.Distance(new Vector3(employee.transform.position.x, 0, employee.transform.position.z), new Vector3(inside.x, 0, inside.z)), Is.LessThan(0.8f), "staff can work in the warehouse");
            }
            finally { arena.Dispose(); }
        }

        [UnityTest] public IEnumerator ProxyNpcNeverMovesOnItsOwn()
        {
            var arena = new Arena();
            try
            {
                var authority = arena.Population(Arena.Watcher(), _ => true);
                var proxies = arena.Population(Arena.Watcher(), _ => false, authority: false);
                authority.Spawn(NpcRole.Customer, Arena.Origin + new Vector3(0, 0, -9), 0, false);
                var snapshots = new List<NpcSnapshot>();
                authority.CaptureSnapshots(snapshots);
                proxies.ApplySnapshots(snapshots);
                var proxy = proxies.Active[0];
                Vector3 frozen = proxy.transform.position;
                for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(proxy.transform.position, frozen), Is.LessThan(0.001f), "without new snapshots a client NPC stays put");
                Assert.That(Vector3.Distance(authority.Active[0].transform.position, frozen), Is.GreaterThan(0.5f), "while the authority's copy walks on");
            }
            finally { arena.Dispose(); }
        }
    }
}
