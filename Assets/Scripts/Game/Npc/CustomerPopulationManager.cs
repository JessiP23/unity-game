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
        /// <summary>What shoppers can put on their lists. Test arenas keep the four classic departments; the mall adds its own.</summary>
        public ShoppingCatalog Catalog { get; set; } = ShoppingCatalog.Default;
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
        /// <summary>How far into their career the player is; each level brings more people (see <see cref="CrowdPlan"/>).</summary>
        public int Level { get; set; }
        /// <summary>Live frame-rate budget: never more shoppers than this, whatever the night wants. Starts at the hard cap.</summary>
        public int Budget { get; set; } = int.MaxValue;
        /// <summary>Where a flash sale is pulling the crowd. Every shopper's brain reads it.</summary>
        public CrowdPull Pull { get; } = new CrowdPull();
        public int Cap => Mathf.Min(settings != null ? settings.maxActiveCustomers : 0, Budget);
        /// <summary>The shift's difficulty share of the crowd (see <see cref="DifficultyRules.CrowdFactor"/>).</summary>
        public float DifficultyFactor { get; set; } = 1f;
        /// <summary>Chance a group arriving together is a grown-up with kids. Zero keeps every arrival an ordinary shopper.</summary>
        public float FamilyChance { get; set; }
        /// <summary>Chance any one arrival is glued to their phone.</summary>
        public float PhoneChance { get; set; }

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

        /// <summary>Target crowd drifts slowly between minimum and maximum so the store ebbs and flows; see <see cref="CrowdPlan.Target"/>.</summary>
        public int TargetCustomers => CrowdPlan.Target(settings.minimumCustomers, settings.maximumCustomers, elapsed, settings.crowdCycleSeconds, Mathf.Clamp01(CrowdScale), Level, Pull.Active, Cap, DifficultyFactor);

        private void Update()
        {
            if (!Authority || settings == null) return;
            elapsed += Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            int shortfall = TargetCustomers - Customers;
            if (spawnTimer > 0 || shortfall <= 0) return;
            spawnTimer = (float)CrowdPlan.ArrivalGap(settings.spawnInterval, shortfall, random.NextDouble());
            // People come in ones, couples and little groups: a group shares a door and walks in side by side.
            int group = CrowdPlan.Arrivals(shortfall, random.NextDouble());
            var door = directory.Pick(PointKind.Spawn, null, ZoneAccess.Customer, random, -1);
            if (door == null) return;
            Vector3 side = door.transform.right;
            // A group of two or more may be a family: one grown-up and the rest kids at their side.
            bool family = group >= 2 && random.NextDouble() < FamilyChance;
            CustomerBehavior leader = null;
            for (int i = 0; i < group; i++)
            {
                Vector3 at = door.Position + side * ((i - (group - 1) * 0.5f) * 0.65f) - door.transform.forward * (0.5f * (i / 2));
                if (NavMesh.SamplePosition(at, out var hit, 0.6f, NavMesh.AllAreas)) at = hit.position; else at = door.Position;
                if (family && i > 0 && leader != null)
                {
                    Spawn(NpcRole.Customer, at, door.transform.eulerAngles.y, false, ShopperArchetypes.Kid(), ShopperKind.Kid, leader);
                    continue;
                }
                bool phone = !family && random.NextDouble() < PhoneChance;
                var npc = Spawn(NpcRole.Customer, at, door.transform.eulerAngles.y, false, phone ? ShopperArchetypes.PhoneZombie() : null, phone ? ShopperKind.PhoneZombie : ShopperKind.Regular);
                if (family && i == 0) { leader = npc.Behavior as CustomerBehavior; npc.Movement.SetPace(0.85f); } // families dawdle
            }
        }

        /// <summary>A flash sale opens: the pull points at the zone, and some of the people already shopping head straight there.</summary>
        public int CallSale(ZoneType zone)
        {
            Pull.Open(zone);
            int going = 0;
            if (!Authority) return 0;
            foreach (var npc in active)
                if (npc.Role == NpcRole.Customer && npc.Behavior is CustomerBehavior shopper && shopper.Brain != null && shopper.Brain.HearSale()) going++;
            return going;
        }

        public void EndSale() => Pull.Close();

        /// <summary>Customers currently heading to or shopping at the sale.</summary>
        public int AtSale
        {
            get
            {
                int n = 0;
                foreach (var npc in active) if (npc.Behavior is CustomerBehavior c && c.Brain != null && c.Brain.OnSale) n++;
                return n;
            }
        }

        /// <summary>Spawns one NPC. <paramref name="profile"/> overrides the random personality (scripted events, tests).</summary>
        public NpcController Spawn(NpcRole role, Vector3 position, float yaw, bool alreadyInside, CustomerProfile profile = null, ShopperKind kind = ShopperKind.Regular, CustomerBehavior leader = null)
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
                if (role == NpcRole.Customer) BeginCustomer(npc, rng, alreadyInside, profile, kind, leader);
                else BeginEmployee(npc, rng);
            }
            active.Add(npc); byId[id] = npc;
            if (Authority) Spawned?.Invoke(npc);
            return npc;
        }

        /// <summary>Someone new walked in (after they are dressed and configured): special nights adjust them here.</summary>
        public event Action<NpcController> Spawned;

        private void BeginCustomer(NpcController npc, Random rng, bool alreadyInside, CustomerProfile chosen, ShopperKind kind, CustomerBehavior leader)
        {
            var profile = chosen != null ? chosen : settings.profiles[rng.Next(settings.profiles.Length)];
            npc.Movement.Configure(profile.RollWalkSpeed(rng), NavigationAreas.CivilianMask, 40 + rng.Next(20));
            npc.Navigation.Configure(directory, ZoneAccess.Customer, rng, npc.Id);
            npc.Perception.Configure(profile.visionRange, profile.fieldOfView, profile.perceptionInterval, profile.CreateAwareness(rng), (float)rng.NextDouble());
            var list = ShoppingListGenerator.Create(Catalog, profile.Preferences(), profile.items.x, profile.items.y, rng);
            var behavior = (CustomerBehavior)npc.Behavior;
            behavior.Attach(npc, reportSink, clock);
            npc.GetComponent<NpcShopping>().Clear();
            behavior.Begin(list, profile.CreateSettings(rng), rng, alreadyInside, Pull, kind, leader);
            Dress(npc, kind);
            // An influencer's camera sees right through a crowd; a kid's eyes nearly do.
            if (npc.Perception != null) npc.Perception.CrowdBlind = kind == ShopperKind.Influencer ? 0f : kind == ShopperKind.Kid ? 0.5f : 1f;
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
            behavior.Errand = null;
            Dress(npc, ShopperKind.Regular);
            behavior.Attach(npc, reportSink, clock);
            behavior.Begin(rng);
        }

        /// <summary>Pose, size and props for a kind of person. Also used to turn a staff member into a cleaner or restocker.</summary>
        public static void Dress(NpcController npc, ShopperKind kind)
        {
            var look = npc.GetComponent<ShopperLook>();
            if (look != null) look.Apply(kind, npc.Animation);
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
            body.AddComponent<ShopperLook>();
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
