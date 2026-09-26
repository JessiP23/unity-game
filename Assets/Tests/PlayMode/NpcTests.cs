using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Small NavMesh arena with a store directory, far from the prototype store.</summary>
    internal sealed class Arena : System.IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(300, 0, 300);
        public readonly GameObject Root;
        public readonly StoreDirectory Directory;
        public readonly List<Object> Owned = new List<Object>();
        /// <summary>Walkable floor with no connection to the arena, for unreachable destinations.</summary>
        public static readonly Vector3 Island = Origin + new Vector3(60, 0, 0);
        public Arena(bool warehouse = false, bool wall = false, bool island = false)
        {
            Root = new GameObject("NPC arena");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(Root.transform);
            floor.transform.position = Origin + new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
            if (wall)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Wall"; block.transform.SetParent(Root.transform);
                block.transform.position = Origin + new Vector3(4, 1.5f, 3); block.transform.localScale = new Vector3(0.4f, 3, 6);
            }
            if (island)
            {
                var detached = GameObject.CreatePrimitive(PrimitiveType.Cube);
                detached.name = "Island"; detached.transform.SetParent(Root.transform);
                detached.transform.position = Island + new Vector3(0, -0.5f, 0); detached.transform.localScale = new Vector3(6, 1, 6);
            }
            Directory = new GameObject("Directory").AddComponent<StoreDirectory>();
            Directory.transform.SetParent(Root.transform);
            Directory.AddZone(ZoneType.Clothing, Origin + new Vector3(-10, 0, -10), Origin + new Vector3(10, 3, 10));
            if (warehouse) Directory.AddZone(ZoneType.Warehouse, Origin + new Vector3(12, 0, -4), Origin + new Vector3(19, 3, 4));
            Directory.AddPoint(ZoneType.Clothing, PointKind.Browse, Origin, 0);
            Directory.AddPoint(ZoneType.Clothing, PointKind.Entrance, Origin + new Vector3(0, 0, -6), 0);
            Directory.AddPoint(ZoneType.Checkout, PointKind.Checkout, Origin + new Vector3(-6, 0, 0), 180);
            Directory.AddPoint<NpcExitPoint>(ZoneType.Clothing, PointKind.Exit, Origin + new Vector3(0, 0, -9), 180);
            Directory.AddPoint<NpcSpawnPoint>(ZoneType.Clothing, PointKind.Spawn, Origin + new Vector3(0, 0, -9), 0);
            var surface = Root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        public static CustomerProfile Watcher()
        {
            var profile = ScriptableObject.CreateInstance<CustomerProfile>();
            profile.walkSpeed = new Vector2(1.4f, 1.4f); profile.browseSeconds = new Vector2(60, 60); profile.items = new Vector2Int(1, 1);
            profile.glanceChance = 0; profile.waitChance = 0; profile.fieldOfView = 90; profile.visionRange = 8;
            profile.noticeSeconds = new Vector2(0.2f, 0.2f); profile.reactionSeconds = new Vector2(0.5f, 0.5f);
            profile.reportDelaySeconds = new Vector2(1f, 1f); profile.reportingProbability = 1; profile.leaveAfterReport = 0;
            profile.suspicionSensitivity = new Vector2(1, 1);
            profile.preferredDepartments = new[]
            {
                new CustomerProfile.Preference { department = ZoneType.Clothing, weight = 1 },
                new CustomerProfile.Preference { department = ZoneType.Supermarket, weight = 0 },
                new CustomerProfile.Preference { department = ZoneType.Electronics, weight = 0 },
                new CustomerProfile.Preference { department = ZoneType.Home, weight = 0 },
            };
            return profile;
        }

        public CustomerPopulationManager Population(CustomerProfile profile, System.Func<SuspiciousActivityEvent, bool> sink, bool authority = true)
        {
            var settings = ScriptableObject.CreateInstance<NpcPopulationSettings>();
            settings.minimumCustomers = 0; settings.maximumCustomers = 0; settings.initialCustomers = 0; settings.employees = 0;
            settings.profiles = new[] { profile };
            Owned.Add(settings); Owned.Add(profile);
            var manager = new GameObject(authority ? "Population" : "Proxy population").AddComponent<CustomerPopulationManager>();
            manager.transform.SetParent(Root.transform);
            manager.Configure(Directory, settings, sink, () => Time.timeAsDouble, authority, 7);
            return manager;
        }

        public PlayerMotor Mannequin(Vector3 position)
        {
            var actor = new GameObject("Test mannequin");
            actor.transform.SetParent(Root.transform);
            actor.layer = 2; actor.transform.position = position;
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>(); Owned.Add(rules);
            var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, new PlayerRecord("mannequin-" + Owned.Count));
            actor.AddComponent<PerceptionTarget>();
            return motor;
        }

        public void Dispose()
        {
            Object.Destroy(Root);
            foreach (var o in Owned) Object.Destroy(o);
        }
    }

    public sealed class NpcNavigationTests
    {
        [UnityTest] public IEnumerator CustomerSpawnsShopsChecksOutLeavesAndIsRecycled()
        {
            var arena = new Arena();
            float scale = Time.timeScale;
            try
            {
                var profile = Arena.Watcher(); profile.browseSeconds = new Vector2(1, 1); profile.checkoutSeconds = new Vector2(1, 1); profile.checkoutChance = 1;
                var population = arena.Population(profile, _ => true);
                var npc = population.Spawn(NpcRole.Customer, Arena.Origin + new Vector3(0, 0, -9), 0, false);
                var brain = ((CustomerBehavior)npc.Behavior).Brain;
                var seen = new HashSet<CustomerState>();
                brain.Changed += s => seen.Add(s);
                Time.timeScale = 4;
                float end = Time.time + 80;
                while (Time.time < end && population.Customers > 0) yield return null;
                Assert.That(seen, Is.SupersetOf(new[] { CustomerState.Walking, CustomerState.Browsing, CustomerState.Shopping, CustomerState.Checkout, CustomerState.Leaving }));
                Assert.That(population.Customers, Is.EqualTo(0), "customer left the store");
                Assert.That(population.Pooled, Is.EqualTo(1), "and went back to the pool");
                var again = population.Spawn(NpcRole.Customer, Arena.Origin + new Vector3(0, 0, -9), 0, false);
                Assert.That(again, Is.SameAs(npc), "pooled bodies are reused instead of instantiated");
            }
            finally { Time.timeScale = scale; arena.Dispose(); }
        }

        [UnityTest] public IEnumerator CustomersCannotPathIntoRestrictedZones()
        {
            var arena = new Arena(warehouse: true);
            try
            {
                yield return null;
                var inside = Arena.Origin + new Vector3(15.5f, 0, 0);
                var from = Arena.Origin;
                var path = new NavMeshPath();
                NavMesh.CalculatePath(from, inside, NavMesh.AllAreas, path);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "staff and the guard can walk in");
                var civilian = new NavMeshQueryFilter { areaMask = NavigationAreas.CivilianMask, agentTypeID = 0 };
                NavMesh.CalculatePath(from, inside, civilian, path);
                Assert.That(path.status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete), "customers cannot");
                arena.Directory.AddPoint(ZoneType.Warehouse, PointKind.Browse, inside, 0);
                Assert.That(arena.Directory.Pick(PointKind.Browse, ZoneType.Warehouse, ZoneAccess.Customer, new System.Random(1), 5), Is.Null);
                Assert.That(arena.Directory.ZoneAt(inside), Is.EqualTo(ZoneType.Warehouse));
            }
            finally { arena.Dispose(); }
        }
    }

    public sealed class NpcPerceptionTests
    {
        private static NpcPerception Observer(Arena arena, float yaw)
        {
            var body = new GameObject("Observer");
            body.transform.SetParent(arena.Root.transform);
            body.transform.SetPositionAndRotation(Arena.Origin, Quaternion.Euler(0, yaw, 0));
            var perception = body.AddComponent<NpcPerception>();
            perception.Configure(9, 70, 0.1f, new AwarenessSettings { NoticeTime = 0.2, ReactionTime = 0.5, ReportDelay = 1 }, 0);
            return perception;
        }

        private static IEnumerator Walk(PlayerMotor motor, NpcPerception observer, float seconds, Vector2 direction)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                motor.Simulate(new PlayerCommand(direction, false, false), Time.fixedDeltaTime);
                observer.Tick(Time.fixedDeltaTime);
            }
        }

        private static IEnumerator Stand(PlayerMotor motor, NpcPerception observer, float seconds) => Walk(motor, observer, seconds, Vector2.zero);

        [UnityTest] public IEnumerator CannotSeeThroughWallsOutsideFovOrBeyondRange()
        {
            var arena = new Arena(wall: true);
            try
            {
                var observer = Observer(arena, 90);
                var target = arena.Mannequin(Arena.Origin + new Vector3(7, 0.1f, 3));
                var perceived = target.GetComponent<PerceptionTarget>();
                Physics.SyncTransforms();
                yield return Walk(target, observer, 1.5f, Vector2.up);
                Assert.That(observer.StateFor(perceived), Is.EqualTo(AwarenessState.Unaware), "in range and in view, but behind the wall");
                target.Teleport(Arena.Origin + new Vector3(-3, 0.1f, 0));
                yield return Walk(target, observer, 1.5f, Vector2.up);
                Assert.That(observer.StateFor(perceived), Is.EqualTo(AwarenessState.Unaware), "behind the observer's back");
                target.Teleport(Arena.Origin + new Vector3(12, 0.1f, -1));
                yield return Walk(target, observer, 1.5f, Vector2.up);
                Assert.That(observer.StateFor(perceived), Is.EqualTo(AwarenessState.Unaware), "straight ahead but too far away");
                target.Teleport(Arena.Origin + new Vector3(3, 0.1f, -2));
                yield return Walk(target, observer, 1.5f, Vector2.up);
                Assert.That(observer.StateFor(perceived), Is.Not.EqualTo(AwarenessState.Unaware), "control: the same movement in the open is seen");
            }
            finally { arena.Dispose(); }
        }

        [UnityTest] public IEnumerator StillMannequinIsIgnoredMovingOneRaisesSuspicion()
        {
            var arena = new Arena();
            try
            {
                var observer = Observer(arena, 0);
                var motor = arena.Mannequin(Arena.Origin + new Vector3(0, 0.1f, 3));
                var target = motor.GetComponent<PerceptionTarget>();
                Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); motor.Simulate(default, Time.fixedDeltaTime); }
                yield return new WaitForSeconds(0.35f);
                yield return Stand(motor, observer, 2f);
                Assert.That(observer.StateFor(target), Is.EqualTo(AwarenessState.Observing));
                Assert.That(observer.Trackers[target].Suspicion, Is.EqualTo(0), "a still mannequin looks like a mannequin");
                yield return Walk(motor, observer, 0.5f, Vector2.up);
                Assert.That(observer.Trackers[target].Suspicion, Is.GreaterThan(0), "that mannequin moved");
                yield return Walk(motor, observer, 1.2f, Vector2.up);
                Assert.That(observer.StateFor(target), Is.GreaterThanOrEqualTo(AwarenessState.Suspicious));
            }
            finally { arena.Dispose(); }
        }
    }

    public sealed class ClothingScenarioTests
    {
        /// <summary>
        /// The design's clothing-aisle scene, unscripted: only the mannequin is driven; the customer and
        /// guard act through their own systems.
        /// </summary>
        [UnityTest] public IEnumerator CustomerSeesMannequinMoveReportsLastKnownSpotAndGuardInvestigates()
        {
            var arena = new Arena(wall: true);
            try
            {
                var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
                authority.TryBeginNight();
                var population = arena.Population(Arena.Watcher(), authority.TryReport);
                var guardObject = new GameObject("Guard");
                guardObject.transform.SetParent(arena.Root.transform);
                guardObject.transform.position = Arena.Origin + new Vector3(-14, 0, -14);
                var rules = ScriptableObject.CreateInstance<GameRulesAsset>(); arena.Owned.Add(rules);
                var guard = guardObject.AddComponent<GuardController>();
                guard.Configure(rules, new WorldSignals(), new[] { Arena.Origin + new Vector3(-14, 0, -14) });
                guard.InvestigateReports(authority.Reports);

                var customer = population.Spawn(NpcRole.Customer, Arena.Origin, 0, true);
                var brain = ((CustomerBehavior)customer.Behavior).Brain;
                float wait = Time.time + 6;
                while (Time.time < wait && brain.State != CustomerState.Browsing) yield return null;
                Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing), "the customer is browsing the clothing");

                var mannequin = arena.Mannequin(Arena.Origin + new Vector3(0, 0.1f, -3));
                var target = mannequin.GetComponent<PerceptionTarget>();
                Physics.SyncTransforms();
                yield return Drive(mannequin, 1.0f, Vector2.right);
                Assert.That(customer.Perception.StateFor(target), Is.EqualTo(AwarenessState.Unaware), "moving behind the customer's back is safe");

                mannequin.Teleport(Arena.Origin + new Vector3(0, 0.1f, 3));
                yield return Drive(mannequin, 2.5f, Vector2.zero);
                Assert.That(customer.Perception.StateFor(target), Is.EqualTo(AwarenessState.Observing), "customer looks, mannequin freezes");
                Assert.That(authority.Stats.Reports, Is.EqualTo(0));

                yield return Drive(mannequin, 1.4f, Vector2.up);
                Assert.That(brain.State, Is.EqualTo(CustomerState.Reporting), "moved while watched: the customer reacts");
                Vector3 lastSeen = mannequin.transform.position;

                mannequin.Teleport(Arena.Origin + new Vector3(6, 0.1f, 3));
                Physics.SyncTransforms();
                wait = Time.time + 6;
                while (Time.time < wait && authority.Stats.Reports == 0) yield return Drive(mannequin, 0.1f, Vector2.zero);
                Assert.That(authority.Stats.Reports, Is.EqualTo(1), "the report reaches security");
                Assert.That(guard.LastReport.HasValue, Is.True);
                var reported = guard.LastReport.Value.LastKnownPosition;
                var reportedPoint = new Vector3(reported.X, reported.Y, reported.Z);
                Assert.That(Vector3.Distance(Flat(reportedPoint), Flat(lastSeen)), Is.LessThan(0.6f), "report holds the last-seen spot");
                Assert.That(Vector3.Distance(Flat(reportedPoint), Flat(mannequin.transform.position)), Is.GreaterThan(3f), "not where the mannequin hid");
                Assert.That(guard.Brain.State, Is.EqualTo(GuardState.Investigate));

                float before = Vector3.Distance(Flat(guardObject.transform.position), Flat(reportedPoint));
                wait = Time.time + 4;
                while (Time.time < wait)
                {
                    yield return new WaitForFixedUpdate();
                    guard.Tick(null, null, Time.fixedDeltaTime);
                }
                Assert.That(Vector3.Distance(Flat(guardObject.transform.position), Flat(reportedPoint)), Is.LessThan(before - 2f), "guard heads for the reported area");
                Assert.That(brain.State, Is.Not.EqualTo(CustomerState.Reporting), "customer goes back to normal life; it never becomes a guard");
            }
            finally { arena.Dispose(); }
        }

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
    }

    public sealed class NpcReplicationTests
    {
        [UnityTest] public IEnumerator ProxiesMirrorAuthorityStateAndNeverSimulate()
        {
            var arena = new Arena();
            try
            {
                var authority = arena.Population(Arena.Watcher(), _ => true);
                var proxies = arena.Population(Arena.Watcher(), _ => { Assert.Fail("proxies must never report"); return false; }, authority: false);
                var npc = authority.Spawn(NpcRole.Customer, Arena.Origin, 0, true);
                var motor = arena.Mannequin(Arena.Origin + new Vector3(0, 0.1f, 3));
                var snapshots = new List<NpcSnapshot>();
                for (int i = 0; i < 90; i++)
                {
                    yield return new WaitForFixedUpdate();
                    motor.Simulate(new PlayerCommand(i % 20 < 10 ? Vector2.right : Vector2.left, false, false), Time.fixedDeltaTime);
                    authority.CaptureSnapshots(snapshots);
                    proxies.ApplySnapshots(snapshots);
                }
                Assert.That(proxies.Active.Count, Is.EqualTo(1));
                var proxy = proxies.Active[0];
                Assert.That(proxy.Authority, Is.False);
                Assert.That(proxy.Id, Is.EqualTo(npc.Id));
                Assert.That(Vector3.Distance(proxy.transform.position, npc.transform.position), Is.LessThan(0.01f), "movement synchronised");
                Assert.That(proxy.BehaviorCode, Is.EqualTo(npc.BehaviorCode), "behaviour state synchronised");
                Assert.That(proxy.Awareness, Is.EqualTo(npc.Awareness), "alert state synchronised");
                Assert.That(npc.Awareness, Is.Not.EqualTo(AwarenessState.Unaware), "the authority customer did notice the mannequin");
                Assert.That(proxy.Movement.Agent.enabled, Is.False, "no pathfinding on clients");
                Assert.That(proxy.Perception.Trackers.Count, Is.EqualTo(0), "no perception on clients");
                authority.CaptureSnapshots(snapshots); snapshots.Clear();
                proxies.ApplySnapshots(snapshots);
                Assert.That(proxies.Active.Count, Is.EqualTo(0), "despawns replicate too");
            }
            finally { arena.Dispose(); }
        }
    }
}
