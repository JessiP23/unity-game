using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Keeps the store believably busy. The state authority spawns, recycles, and simulates NPCs; remote
    /// clients only mirror snapshots. Customers leaving go back to a pool instead of being destroyed.
    /// </summary>
    public sealed class CustomerPopulationManager : MonoBehaviour
    {
        private static readonly Color[] Tints = { new Color(0.35f, 0.55f, 0.85f), new Color(0.85f, 0.45f, 0.35f), new Color(0.5f, 0.75f, 0.45f), new Color(0.8f, 0.7f, 0.3f), new Color(0.6f, 0.45f, 0.75f) };
        private readonly List<NpcController> active = new List<NpcController>();
        private readonly List<NpcController> pool = new List<NpcController>();
        private readonly Dictionary<int, NpcController> byId = new Dictionary<int, NpcController>();
        private NpcPopulationSettings settings;
        private StoreDirectory directory;
        private Func<SuspiciousActivityEvent, bool> reportSink;
        private Func<double> clock;
        private Random random;
        private float spawnTimer, elapsed;
        private int nextId = 1;
        public bool Authority { get; private set; }
        public IReadOnlyList<NpcController> Active => active;
        public int Customers { get { int n = 0; foreach (var npc in active) if (npc.Role == NpcRole.Customer) n++; return n; } }
        public int Pooled => pool.Count;
        public NpcPopulationSettings Settings => settings;
        /// <summary>Night-schedule share of the normal crowd. 1 = the settings asset; 0 = nobody new comes in.</summary>
        public float CrowdScale { get; set; } = 1f;

        public void Configure(StoreDirectory store, NpcPopulationSettings population, Func<SuspiciousActivityEvent, bool> sink, Func<double> time, bool authority, int seed)
        {
            directory = store; settings = population; reportSink = sink; clock = time; Authority = authority;
            random = new Random(seed);
        }

        /// <summary>Opening state of the night: staff at work and shoppers already browsing.</summary>
        public void Populate()
        {
            if (!Authority) return;
            for (int i = 0; i < settings.employees; i++)
            {
                var spot = directory.Pick(PointKind.Work, null, ZoneAccess.Employee, random, -1);
                if (spot == null) break;
                spot.Release(-1);
                Spawn(NpcRole.Employee, spot.Position, spot.transform.eulerAngles.y, true);
            }
            for (int i = 0; i < Mathf.Min(settings.initialCustomers, settings.maxActiveCustomers); i++)
            {
                var spot = directory.Pick(PointKind.Browse, null, ZoneAccess.Customer, random, -1);
                if (spot == null) break;
                spot.Release(-1);
                Vector2 jitter = new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 1.5f;
                Spawn(NpcRole.Customer, spot.Position + new Vector3(jitter.x, 0, jitter.y), (float)random.NextDouble() * 360, true);
            }
            spawnTimer = settings.spawnInterval * 0.5f;
        }

        /// <summary>Target crowd drifts slowly between minimum and maximum so the store ebbs and flows.</summary>
        public int TargetCustomers
        {
            get
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(elapsed / settings.crowdCycleSeconds * Mathf.PI * 2f);
                return Mathf.Min(settings.maxActiveCustomers, Mathf.RoundToInt(Mathf.Lerp(settings.minimumCustomers, settings.maximumCustomers, wave) * Mathf.Clamp01(CrowdScale)));
            }
        }

        private void Update()
        {
            if (!Authority || settings == null) return;
            elapsed += Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0 || Customers >= TargetCustomers) return;
            spawnTimer = settings.spawnInterval * (0.6f + (float)random.NextDouble() * 0.8f);
            var door = directory.Pick(PointKind.Spawn, null, ZoneAccess.Customer, random, -1);
            if (door != null) Spawn(NpcRole.Customer, door.Position, door.transform.eulerAngles.y, false);
        }

        /// <summary>Spawns one NPC. <paramref name="profile"/> overrides the random personality (scripted events, tests).</summary>
        public NpcController Spawn(NpcRole role, Vector3 position, float yaw, bool alreadyInside, CustomerProfile profile = null)
        {
            var npc = TakeFromPool(role) ?? Build(role, nextId);
            int id = nextId++;
            npc.gameObject.name = $"{role} {id}";
            npc.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            npc.gameObject.SetActive(true);
            npc.Configure(id, role, Authority);
            var rng = new Random(random.Next());
            if (Authority)
            {
                npc.Movement.Warp(position);
                if (role == NpcRole.Customer) BeginCustomer(npc, rng, alreadyInside, profile);
                else BeginEmployee(npc, rng);
            }
            active.Add(npc); byId[id] = npc;
            return npc;
        }

        private void BeginCustomer(NpcController npc, Random rng, bool alreadyInside, CustomerProfile chosen)
        {
            var profile = chosen != null ? chosen : settings.profiles[rng.Next(settings.profiles.Length)];
            npc.Movement.Configure(profile.RollWalkSpeed(rng), NavigationAreas.CivilianMask, 40 + rng.Next(20));
            npc.Navigation.Configure(directory, ZoneAccess.Customer, rng, npc.Id);
            npc.Perception.Configure(profile.visionRange, profile.fieldOfView, profile.perceptionInterval, profile.CreateAwareness(rng), (float)rng.NextDouble());
            var list = ShoppingListGenerator.Create(ShoppingCatalog.Default, profile.Preferences(), profile.items.x, profile.items.y, rng);
            var behavior = (CustomerBehavior)npc.Behavior;
            behavior.Attach(npc, reportSink, clock);
            npc.GetComponent<NpcShopping>().Clear();
            behavior.Begin(list, profile.CreateSettings(rng), rng, alreadyInside);
        }

        private void BeginEmployee(NpcController npc, Random rng)
        {
            var speed = settings.employeeWalkSpeed;
            npc.Movement.Configure(speed.x + (float)rng.NextDouble() * Mathf.Max(0, speed.y - speed.x), NavMesh.AllAreas, 30);
            npc.Navigation.Configure(directory, ZoneAccess.Employee, rng, npc.Id);
            var awareness = new AwarenessSettings
            {
                Sensitivity = settings.employeeSensitivity, ReactionTime = settings.employeeReactionSeconds,
                ReportDelay = settings.employeeReportDelaySeconds, NoticeTime = settings.employeeNoticeSeconds
            };
            npc.Perception.Configure(settings.employeeVisionRange, settings.employeeFieldOfView, 0.2f, awareness, (float)rng.NextDouble());
            var behavior = (EmployeeBehavior)npc.Behavior;
            behavior.workSeconds = settings.employeeWorkSeconds;
            behavior.Attach(npc, reportSink, clock);
            behavior.Begin(rng);
        }

        private NpcController TakeFromPool(NpcRole role)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].Role == role) { var npc = pool[i]; pool.RemoveAt(i); return npc; }
            return null;
        }

        /// <summary>Assembles an NPC from generic parts; the role decides the behaviour component.</summary>
        private NpcController Build(NpcRole role, int seed)
        {
            var body = new GameObject(role.ToString());
            body.transform.SetParent(transform, false);
            body.SetActive(false);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = 0.28f; capsule.height = 1.8f; capsule.center = new Vector3(0, 0.9f, 0);
            var rigidbody = body.AddComponent<Rigidbody>(); rigidbody.isKinematic = true;
            body.AddComponent<NavMeshAgent>();
            body.AddComponent<NpcMovement>();
            body.AddComponent<NpcNavigation>();
            body.AddComponent<NpcPerception>();
            if (role == NpcRole.Customer) { body.AddComponent<CustomerBehavior>(); body.AddComponent<NpcShopping>(); }
            else body.AddComponent<EmployeeBehavior>();
            var look = body.AddComponent<NpcAnimationHooks>();
            var profile = role == NpcRole.Customer ? CharacterProfile.Customers[seed % CharacterProfile.Customers.Length] : CharacterProfile.Employee;
            look.Dress(profile, role == NpcRole.Customer ? Tints[seed % Tints.Length] : new Color(0.95f, 0.55f, 0.1f));
            var npc = body.AddComponent<NpcController>();
            npc.Departed += Despawn;
            return npc;
        }

        private void Despawn(NpcController npc)
        {
            if (!active.Remove(npc)) return;
            byId.Remove(npc.Id);
            if (npc.Navigation != null) npc.Navigation.Release();
            if (npc.Perception != null) npc.Perception.ResetAll();
            npc.gameObject.SetActive(false);
            pool.Add(npc);
        }

        /// <summary>Strongest visible reaction any NPC currently has toward a mannequin (for HUD hints, not numbers).</summary>
        public AwarenessState StrongestAwarenessOf(PerceptionTarget target)
        {
            var strongest = AwarenessState.Unaware;
            foreach (var npc in active)
            {
                if (npc.Perception == null) continue;
                var state = npc.Perception.StateFor(target);
                if (state > strongest) strongest = state;
            }
            return strongest;
        }

        /// <summary>Current sight, separate from suspicion retained in an NPC's memory.</summary>
        public int WatchingCount(PerceptionTarget target)
        {
            if (target == null || !target.Noticeable) return 0;
            int count = 0;
            foreach (var npc in active)
                if (npc.Perception != null && npc.Perception.Trackers.TryGetValue(target, out var tracker) && tracker.Visible) count++;
            return count;
        }

        public void CaptureSnapshots(List<NpcSnapshot> into)
        {
            into.Clear();
            foreach (var npc in active) into.Add(npc.Capture());
        }

        /// <summary>Remote client: create, move, and remove proxies to match the authority. Runs no AI.</summary>
        public void ApplySnapshots(IReadOnlyList<NpcSnapshot> snapshots)
        {
            if (Authority) return;
            var seen = new HashSet<int>();
            foreach (var snapshot in snapshots)
            {
                seen.Add(snapshot.Id);
                if (!byId.TryGetValue(snapshot.Id, out var npc))
                {
                    npc = TakeFromPool(snapshot.Role) ?? Build(snapshot.Role, snapshot.Id);
                    npc.gameObject.name = $"{snapshot.Role} {snapshot.Id} (proxy)";
                    npc.gameObject.SetActive(true);
                    npc.Configure(snapshot.Id, snapshot.Role, false);
                    active.Add(npc); byId[snapshot.Id] = npc;
                }
                npc.Apply(snapshot);
            }
            for (int i = active.Count - 1; i >= 0; i--)
                if (!seen.Contains(active[i].Id)) Despawn(active[i]);
        }
    }
}
