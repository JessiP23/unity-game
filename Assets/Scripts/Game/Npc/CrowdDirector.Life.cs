using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>One of tonight's jobs as the crowd sees it: where the player has to be, and whether it still matters.</summary>
    public readonly struct JobSite
    {
        public readonly int Index;
        public readonly bool Done, Known;
        public readonly Vector3 Spot;
        public JobSite(int index, bool done, bool known, Vector3 spot) { Index = index; Done = done; Known = known; Spot = spot; }
    }

    /// <summary>Department heat for the map: how many shoppers, how crowded that reads, and which way it is going.</summary>
    public readonly struct ZoneHeat
    {
        public readonly ZoneType Zone;
        public readonly int People, Level, Trend;
        public ZoneHeat(ZoneType zone, int people, int level, int trend) { Zone = zone; People = people; Level = level; Trend = trend; }
    }

    public sealed partial class CrowdDirector
    {
        private int shift;
        private float lifeTimer, heatTimer, kindsTimer;
        private float nextInfluencerAt = 45f, nightClock;
        private readonly List<JobSite> sites = new List<JobSite>();
        private readonly Dictionary<int, JobCrowd> jobCrowds = new Dictionary<int, JobCrowd>();
        private readonly Dictionary<ZoneType, int> counts = new Dictionary<ZoneType, int>();
        private readonly Queue<Dictionary<ZoneType, int>> history = new Queue<Dictionary<ZoneType, int>>();
        private readonly List<ZoneHeat> heat = new List<ZoneHeat>();
        private readonly List<WetFloor> wetFloors = new List<WetFloor>();
        private NpcController influencer;
        private int influencerId;
        private readonly List<NpcController> crew = new List<NpcController>();
        private readonly System.Random lifeRandom = new System.Random(911);

        private sealed class JobCrowd
        {
            public int Index;
            public Vector3 Spot;
            public ZoneType? Zone;
            public bool Built;
            public readonly List<NavigationPoint> Points = new List<NavigationPoint>();
            public PressureCycle Cycle;
            public PressureStage Stage;
            public double Until;
            public bool Live;
            public float NextAssign, BuiltAt;
        }

        /// <summary>Fills the list with tonight's jobs (index, done, where the player has to be). Set by the game.</summary>
        public System.Action<List<JobSite>> JobSites { get; set; }
        /// <summary>The shift the player signed up for. Changes the crowd, the sales, the job crowds and who turns up.</summary>
        public Difficulty Difficulty { get; private set; } = Difficulty.Normal;
        /// <summary>People hang around tonight's jobs, with quiet windows. Scripted tests run without it unless they turn it on.</summary>
        public bool PressureEnabled { get; set; } = true;
        /// <summary>Families, phone zombies, the influencer, cleaners and restockers. Scripted tests run without them unless they turn them on.</summary>
        public bool KindsEnabled { get; set; } = true;
        public IReadOnlyList<ZoneHeat> Heat => heat;
        /// <summary>The influencer, if still on the floor (a pooled body reused for someone else no longer counts).</summary>
        public NpcController InfluencerOnFloor => influencer != null && influencer.isActiveAndEnabled && influencer.Id == influencerId ? influencer : null;
        public int Crew => crew.Count;
        /// <summary>Seconds the player has spent near each unfinished job (night metrics: how long the crowd made them wait).</summary>
        public readonly Dictionary<int, double> WaitSeconds = new Dictionary<int, double>();
        public int InfluencerClips { get; private set; }
        /// <summary>A clip went up: the game turns it into a toast and a photo.</summary>
        public System.Action<CustomerBehavior> Viral { get; set; }

        public void SetDifficulty(Difficulty difficulty)
        {
            Difficulty = difficulty;
            if (population == null) return;
            population.DifficultyFactor = (float)DifficultyRules.CrowdFactor(difficulty);
            sales = CrowdPlan.SalesFor(shift, DifficultyRules.Sales(difficulty));
            if (KindsEnabled) ApplyKindChances();
        }

        private void ApplyKindChances()
        {
            population.FamilyChance = KindsEnabled ? 0.4f : 0f;
            population.PhoneChance = KindsEnabled ? 0.18f : 0f;
        }

        public void EnableKinds(bool on)
        {
            KindsEnabled = on;
            ApplyKindChances();
            if (on) CustomerBehavior.Filmed -= OnFilmed;
            if (on) CustomerBehavior.Filmed += OnFilmed;
            else CustomerBehavior.Filmed -= OnFilmed;
        }

        private void OnDestroy() => CustomerBehavior.Filmed -= OnFilmed;

        private void OnFilmed(CustomerBehavior who, SuspiciousActivityEvent report)
        {
            InfluencerClips++;
            Viral?.Invoke(who);
        }

        private double Elapsed => duration - remaining();

        private void UpdateCrowdLife()
        {
            float dt = Time.deltaTime;
            nightClock += dt;
            lifeTimer -= dt; heatTimer -= dt; kindsTimer -= dt;
            if (lifeTimer <= 0) { lifeTimer = 0.5f; UpdatePressure(); }
            if (heatTimer <= 0) { heatTimer = 1f; UpdateHeat(); }
            if (kindsTimer <= 0) { kindsTimer = 1f; UpdateKinds(); }
            for (int i = wetFloors.Count - 1; i >= 0; i--) if (wetFloors[i] == null) wetFloors.RemoveAt(i);
        }

        // ---------------- people at the jobs ----------------

        private void UpdatePressure()
        {
            if (JobSites == null || !Ready) return;
            sites.Clear();
            JobSites(sites);
            double elapsed = Elapsed;
            double crowd = NightSchedule.At(remaining(), duration).Crowd;
            foreach (var site in sites)
            {
                if (!jobCrowds.TryGetValue(site.Index, out var job))
                    jobCrowds[site.Index] = job = new JobCrowd { Index = site.Index, Cycle = JobPressure.For(shift, site.Index) };
                job.Stage = JobPressure.StageAt(job.Cycle, elapsed, out job.Until);
                job.Live = !site.Done && site.Known;
                if (!job.Live) { Disperse(job); if (site.Done) Clear(job); continue; }
                if (!PressureEnabled) { if (job.Built) Disperse(job); continue; }
                // The crowd stays put at a job: it only follows a moved target (the next crate, say) after a while,
                // so it never trails the player round the store.
                bool moved = (Flat(site.Spot) - Flat(job.Spot)).sqrMagnitude > 4f || Mathf.Abs(site.Spot.y - job.Spot.y) > 1.5f;
                if (!job.Built || (moved && Time.time - job.BuiltAt > 20f)) Rebuild(job, site.Spot);
                if (crowd <= 0 || job.Stage == PressureStage.Quiet) { Disperse(job); continue; }
                if (!population.Authority || Time.time < job.NextAssign || !job.Zone.HasValue || !ZoneAccess.Customer.Allows(job.Zone.Value)) continue;
                int want = Mathf.Min(JobPressure.Lingerers(Level, Difficulty), job.Points.Count);
                if (Lingerers(job.Index) >= want) continue;
                var who = Nearest(job.Spot);
                if (who != null && who.Brain.Linger(job.Zone.Value, job.Index, 22 + lifeRandom.NextDouble() * 20)) job.NextAssign = Time.time + 1.5f;
                else job.NextAssign = Time.time + 3f;
            }
        }

        private int Lingerers(int group)
        {
            int n = 0;
            foreach (var npc in population.Active)
                if (npc.Behavior is CustomerBehavior c && c.Brain != null && c.Brain.LingerGroup == group) n++;
            return n;
        }

        /// <summary>The closest shopper free to wander over: on the same floor, not at a sale, not leaving, not a kid.</summary>
        private CustomerBehavior Nearest(Vector3 spot)
        {
            CustomerBehavior best = null; float bestD = 35f * 35f;
            foreach (var npc in population.Active)
            {
                if (!(npc.Behavior is CustomerBehavior c) || c.Brain == null || c.Following || c.Kind == ShopperKind.Influencer) continue;
                var b = c.Brain;
                if (b.OnSale || b.Lingering || b.VisitSeconds > b.Settings.MaxVisitSeconds * 0.7) continue;
                if (b.State != CustomerState.Walking && b.State != CustomerState.Waiting && b.State != CustomerState.Browsing) continue;
                Vector3 d = npc.transform.position - spot;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                float d2 = d.x * d.x + d.z * d.z;
                if (d2 < bestD) { bestD = d2; best = c; }
            }
            return best;
        }

        private void Disperse(JobCrowd job)
        {
            foreach (var npc in population.Active)
                if (npc.Behavior is CustomerBehavior c && c.Brain != null && c.Brain.LingerGroup == job.Index) c.Brain.MoveOn();
        }

        private void Clear(JobCrowd job)
        {
            foreach (var p in job.Points) directory.Remove(p);
            job.Points.Clear(); job.Built = false;
        }

        /// <summary>
        /// Standing room round the job, facing it: rings 1.5–3 m out, on open floor of the job's own department,
        /// never inside furniture and never on the spot itself. Restricted rooms get spots for staff only.
        /// </summary>
        private void Rebuild(JobCrowd job, Vector3 spot)
        {
            Disperse(job);
            Clear(job);
            job.Spot = spot; job.Built = true; job.BuiltAt = Time.time;
            if (NavMesh.SamplePosition(spot, out var floor, 1.6f, NavMesh.AllAreas)) spot = new Vector3(spot.x, floor.position.y, spot.z);
            job.Zone = directory.ZoneAt(spot);
            if (!job.Zone.HasValue) return;
            bool staffOnly = !ZoneAccess.Customer.Allows(job.Zone.Value);
            int mask = staffOnly ? NavMesh.AllAreas : NavigationAreas.CivilianMask;
            var taken = new List<Vector3>();
            foreach (var p in directory.Points) if ((p.Position - spot).sqrMagnitude < 16f) taken.Add(p.Position);
            foreach (float radius in new[] { 1.6f, 2.3f, 3.0f })
                for (int a = 0; a < 10 && job.Points.Count < 6; a++)
                {
                    float angle = (a + radius) * 36f * Mathf.Deg2Rad;
                    Vector3 at = spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!ClearFor(at, job.Zone.Value, taken, mask, out var stand)) continue;
                    if ((Flat(stand) - Flat(spot)).sqrMagnitude < JobPressure.KeepClear * JobPressure.KeepClear) continue;
                    Vector3 look = spot - stand; look.y = 0;
                    float yaw = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
                    var point = directory.AddPoint(job.Zone.Value, PointKind.Linger, stand, yaw);
                    point.group = job.Index;
                    job.Points.Add(point); taken.Add(stand);
                }
        }

        /// <summary>Like a sale spot's check, on the given NavMesh areas.</summary>
        private bool ClearFor(Vector3 at, ZoneType zone, List<Vector3> taken, int mask, out Vector3 spot)
        {
            spot = at;
            if (!NavMesh.SamplePosition(at, out var hit, 0.4f, mask)) return false;
            if (Mathf.Abs(hit.position.y - at.y) > 0.4f) return false;
            spot = hit.position;
            if (directory.ZoneAt(spot) != zone) return false;
            foreach (var other in taken) if ((other - spot).sqrMagnitude < 0.75f * 0.75f) return false;
            foreach (var c in Physics.OverlapCapsule(spot + Vector3.up * 0.36f, spot + Vector3.up * 1.6f, 0.3f, 1 | 1 << 3, QueryTriggerInteraction.Collide))
            {
                if (c.GetComponentInParent<PlacementZone>() != null) return false;
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<NpcController>() == null && c.GetComponentInParent<PlayerMotor>() == null) return false;
            }
            return true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Linger spots for a job (tests and the debug overlay).</summary>
        public int LingerSpots(int job) => jobCrowds.TryGetValue(job, out var j) ? j.Points.Count : 0;
        public int LingerersAt(int job) => population != null ? Lingerers(job) : 0;
        public Vector3? JobSpot(int job) => jobCrowds.TryGetValue(job, out var j) && j.Built ? j.Spot : (Vector3?)null;
        public ZoneType? JobZone(int job) => jobCrowds.TryGetValue(job, out var j) ? j.Zone : null;
        public PressureStage StageOf(int job) => jobCrowds.TryGetValue(job, out var j) ? j.Stage : PressureStage.Busy;

        /// <summary>
        /// The job card line: how crowded the player's next job is and when it goes quiet.
        /// Empty when the job has no place to point at.
        /// </summary>
        public string JobLine(int job, out string tint)
        {
            tint = "#9FB3C8";
            if (!jobCrowds.TryGetValue(job, out var j) || !j.Live || !j.Built) return "";
            int people = 0;
            foreach (var npc in population.Active)
            {
                if (npc == null) continue;
                Vector3 d = npc.transform.position - j.Spot;
                if (Mathf.Abs(d.y) < 1.5f && d.x * d.x + d.z * d.z < 16f) people++;
            }
            int level = CrowdHeat.Level(people);
            string crowd = "CROWD HERE: " + CrowdHeat.Name(level).ToUpperInvariant() + (people > 0 ? " (" + people + ")" : "");
            if (!PressureEnabled) { tint = CrowdHeat.Tint(level); return crowd; }
            switch (j.Stage)
            {
                case PressureStage.Quiet: tint = "#40DF80"; return "QUIET NOW · " + Mathf.CeilToInt((float)j.Until) + "s — go!";
                case PressureStage.QuietSoon: tint = "#F4C15D"; return crowd + " · quiet in " + Mathf.CeilToInt((float)j.Until) + "s — get ready";
                default: tint = CrowdHeat.Tint(level); return crowd + " · quiet in " + HudText.Clock(j.Until);
            }
        }

        /// <summary>A job that is about to go quiet, or is quiet: the map marks it green.</summary>
        public bool TryQuietMark(int job, out Vector3 at)
        {
            at = default;
            if (!PressureEnabled || !jobCrowds.TryGetValue(job, out var j) || !j.Live || j.Stage == PressureStage.Busy) return false;
            at = j.Spot; return true;
        }

        /// <summary>Night metrics: count time spent near unfinished jobs.</summary>
        public void TrackWait(Vector3 player, float dt)
        {
            foreach (var j in jobCrowds.Values)
            {
                if (!j.Live || !j.Built) continue;
                Vector3 d = player - j.Spot;
                if (Mathf.Abs(d.y) < 1.5f && d.x * d.x + d.z * d.z < 36f)
                    WaitSeconds[j.Index] = (WaitSeconds.TryGetValue(j.Index, out var w) ? w : 0) + dt;
            }
        }

        // ---------------- the heat map ----------------

        private void UpdateHeat()
        {
            counts.Clear();
            foreach (var npc in population.Active)
            {
                if (npc == null || npc.Role != NpcRole.Customer) continue;
                var zone = directory.ZoneAt(npc.transform.position);
                if (zone.HasValue) counts[zone.Value] = (counts.TryGetValue(zone.Value, out var n) ? n : 0) + 1;
            }
            history.Enqueue(new Dictionary<ZoneType, int>(counts));
            while (history.Count > 6) history.Dequeue();
            var before = history.Peek();
            heat.Clear();
            foreach (ZoneType zone in System.Enum.GetValues(typeof(ZoneType)))
            {
                if (!ZoneAccess.Customer.Allows(zone) || zone == ZoneType.EntranceExit) continue;
                counts.TryGetValue(zone, out int now);
                before.TryGetValue(zone, out int then);
                heat.Add(new ZoneHeat(zone, now, CrowdHeat.Level(now), CrowdHeat.Trend(then, now)));
            }
        }

        // ---------------- who turns up ----------------

        private void UpdateKinds()
        {
            for (int i = crew.Count - 1; i >= 0; i--) if (crew[i] == null || !crew[i].isActiveAndEnabled) crew.RemoveAt(i);
            if (influencer != null && InfluencerOnFloor == null) { influencer = null; nextInfluencerAt = nightClock + 70f + (float)lifeRandom.NextDouble() * 50f; }
            if (!KindsEnabled || !population.Authority || !Ready) return;
            double crowd = NightSchedule.At(remaining(), duration).Crowd;
            // Staff errands: a cleaner always, a restocker once the mall gets busy or on rush hour.
            int wantCrew = 1 + (Level >= 3 || Difficulty == Difficulty.RushHour ? 1 : 0);
            if (crew.Count < wantCrew && crowd > 0) SpawnCrew(crew.Count == 0 ? ShopperKind.Cleaner : ShopperKind.Restocker);
            // One influencer at a time, never on Relaxed, only while there are shoppers to film.
            if (influencer == null && DifficultyRules.Influencers(Difficulty) && crowd >= 0.5 && nightClock >= nextInfluencerAt && population.Customers < population.Cap)
            {
                var door = directory.Pick(PointKind.Spawn, null, ZoneAccess.Customer, lifeRandom, -1);
                if (door != null)
                {
                    influencer = population.Spawn(NpcRole.Customer, door.Position, door.transform.eulerAngles.y, false, ShopperArchetypes.Influencer(), ShopperKind.Influencer);
                    influencerId = influencer.Id;
                    toast?.Invoke("An influencer just walked in, filming everything. Their red light sees through any crowd: freeze when it sweeps over you.", 5f);
                }
            }
        }

        private void SpawnCrew(ShopperKind kind)
        {
            var spot = directory.Pick(PointKind.Work, null, ZoneAccess.Employee, lifeRandom, -1);
            if (spot == null) return;
            spot.Release(-1);
            var npc = population.Spawn(NpcRole.Employee, spot.Position, spot.transform.eulerAngles.y, true);
            if (!(npc.Behavior is EmployeeBehavior staff)) return;
            CustomerPopulationManager.Dress(npc, kind);
            // Busy with a mop or a box: they notice less than staff on the floor, but they work right where you need to be.
            if (npc.Perception != null)
                npc.Perception.Configure(6.5f, 75f, 0.25f, new AwarenessSettings { Sensitivity = 1.0, NoticeTime = 0.4, ReactionTime = 1.1, ReportDelay = 1.6 }, (float)lifeRandom.NextDouble());
            staff.Errand = NextErrand;
            staff.StartedWork -= OnStaffWorking;
            staff.StartedWork += OnStaffWorking;
            crew.Add(npc);
        }

        /// <summary>Cleaners and restockers walk the job spots in turn: exactly where the player needs to be.</summary>
        private int NextErrand()
        {
            var live = new List<int>();
            foreach (var j in jobCrowds.Values) if (j.Live && j.Points.Count > 0) live.Add(j.Index);
            return live.Count == 0 ? -1 : live[lifeRandom.Next(live.Count)];
        }

        private void OnStaffWorking(EmployeeBehavior staff)
        {
            var look = staff.GetComponent<ShopperLook>();
            if (look == null || look.Kind != ShopperKind.Cleaner) return;
            if (wetFloors.Count >= 3) { if (wetFloors[0] != null) Destroy(wetFloors[0].gameObject); wetFloors.RemoveAt(0); }
            Vector3 at = staff.transform.position + staff.transform.forward * 0.7f;
            if (NavMesh.SamplePosition(at, out var hit, 0.6f, NavMesh.AllAreas)) at = hit.position;
            wetFloors.Add(WetFloor.Create(transform, at, staff.transform.eulerAngles.y));
        }
    }

    /// <summary>
    /// A freshly mopped patch with a yellow sign. Walk across and nothing happens; run across and your feet
    /// squeak loud enough for the guard to come and look. Dries after forty seconds.
    /// </summary>
    public sealed class WetFloor : MonoBehaviour
    {
        public const float Radius = 1.0f, DrySeconds = 40f;
        public static WorldSignals Signals;
        public static System.Func<IReadOnlyList<PlayerMotor>> Players;
        public static System.Action<string> Said;
        private float age, quiet;
        private Renderer sheen;

        public static WetFloor Create(Transform parent, Vector3 at, float yaw)
        {
            var host = new GameObject("Wet floor");
            host.transform.SetParent(parent, false);
            host.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var wet = host.AddComponent<WetFloor>();
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Wet sheen";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(host.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            disc.transform.localScale = new Vector3(Radius * 2f, 0.002f, Radius * 1.6f);
            wet.sheen = disc.GetComponent<Renderer>();
            wet.sheen.sharedMaterial = ArtLibrary.Particle(new Color(0.7f, 0.85f, 1f, 0.35f)) ?? ArtLibrary.Glass(new Color(0.7f, 0.85f, 1f, 0.3f));
            wet.sheen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The yellow A-frame sign: the real model when the art pack has it, a built one otherwise.
            var sign = ObjectiveDressing.FitProp("WetFloorSign_01", host.transform, at + host.transform.right * 0.55f, yaw + 90f, 0.62f);
            if (sign != null)
            {
                foreach (var c in sign.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in host.GetComponentsInChildren<Renderer>()) r.gameObject.layer = 2;
                return wet;
            }
            var yellow = ArtLibrary.Lit(new Color(1f, 0.82f, 0.1f), 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                var leaf = DressingKit.Panel(host.transform, "Sign leaf", new Vector3(0.55f, 0.3f, side * 0.08f), new Vector3(0.3f, 0.6f, 0.012f));
                leaf.transform.localRotation = Quaternion.Euler(side * 12f, 0f, 0f);
                leaf.sharedMaterial = yellow;
                var label = SignFactory.Label(host.transform, "WET\nFLOOR", 0.06f, new Color(0.1f, 0.1f, 0.1f));
                if (label != null)
                {
                    label.transform.localPosition = new Vector3(0.55f, 0.36f, side * 0.098f);
                    label.transform.localRotation = Quaternion.Euler(side * 12f, side > 0 ? 180f : 0f, 0f);
                }
            }
            foreach (var c in host.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in host.GetComponentsInChildren<Renderer>()) r.gameObject.layer = 2;
            return wet;
        }

        private void Update()
        {
            age += Time.deltaTime; quiet -= Time.deltaTime;
            if (age > DrySeconds) { Destroy(gameObject); return; }
            if (sheen != null && age > DrySeconds - 8f) sheen.enabled = Mathf.Repeat(age, 0.6f) < 0.45f || age < DrySeconds - 3f;
            if (Players == null || quiet > 0) return;
            foreach (var motor in Players())
            {
                if (motor == null || motor.Record == null || motor.Record.State != PlayerState.Running) continue;
                Vector3 d = motor.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 1f || d.x * d.x + d.z * d.z > Radius * Radius) continue;
                quiet = 2f;
                Signals?.Noise.Publish(new NoiseEvent(motor.transform.position, 1.3f, "squeaky wet floor"));
                Said?.Invoke("SQUEAK! Running on a wet floor — the guard heard that.");
                break;
            }
        }
    }
}
