using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Runs the mall's crowd on top of <see cref="CustomerPopulationManager"/>: tonight's level, the two flash
    /// sales that flood one department, the floating sale sign, crowd level of detail, and the live frame-rate
    /// budget that keeps a laptop smooth. Presentation and pacing only; the rules live in <see cref="CrowdPlan"/>.
    /// </summary>
    public sealed partial class CrowdDirector : MonoBehaviour
    {
        /// <summary>Frame time the budget aims for: about 48 fps. Slower than this and the mall sheds shoppers.</summary>
        public const double SmoothFrameMs = 1000.0 / 48.0;
        public const int MaxSaleSpotsPerZone = 10;

        private CustomerPopulationManager population;
        private StoreDirectory directory;
        private Func<double> remaining;
        private double duration;
        private IReadOnlyList<FlashSale> sales;
        private readonly List<ZoneType> saleZones = new List<ZoneType>();
        private readonly Dictionary<ZoneType, Vector3> signSpots = new Dictionary<ZoneType, Vector3>();
        private Action<string, float> toast;
        private Action<string, string> banner;
        /// <summary>The mall PA; set once the sound system exists.</summary>
        public StoreAudio Audio { get; set; }
        private bool governed;
        private int announced = -1, opened = -1, closed = -1;
        private SaleSign sign;
        private float lodTimer, budgetTimer, budgetFrames, budgetMs, budgetWorst, cooldown, warmup = 6f;
        private readonly Dictionary<NpcController, int> detail = new Dictionary<NpcController, int>();
        /// <summary>Test runs keep the night calm unless a test turns sales on; real nights always have them.</summary>
        public bool SalesEnabled { get; set; } = true;
        /// <summary>True once the sale spots exist (they are placed after the store's physics and NavMesh carving settle).</summary>
        public bool Ready { get; private set; }

        public int Level { get; private set; }
        public SaleStage Stage { get; private set; }
        public ZoneType? SaleZone { get; private set; }
        public double SaleSecondsLeft { get; private set; }
        public int SaleIndex { get; private set; } = -1;
        public IReadOnlyList<FlashSale> Sales => sales;
        public IReadOnlyList<ZoneType> SaleZones => saleZones;
        /// <summary>Sale spots added round the shelves of each department, for tests and the debug overlay.</summary>
        public int SaleSpots { get; private set; }

        /// <param name="jobSpots">Where tonight's jobs are: sales are called in those departments first, so they get in the way.</param>
        /// <param name="governBudget">False in tests: batch-mode frame times mean nothing.</param>
        /// <param name="withSales">False keeps the night free of flash sales (scripted test scenarios).</param>
        public void Configure(CustomerPopulationManager crowd, StoreDirectory store, ShoppingCatalog catalog, int shiftNumber, int level,
            Func<double> clockRemaining, double nightSeconds, IEnumerable<Vector3> jobSpots,
            Action<string, float> showToast, Action<string, string> showBanner, bool governBudget, bool withSales = true)
        {
            SalesEnabled = withSales;
            population = crowd; directory = store; remaining = clockRemaining; duration = nightSeconds;
            toast = showToast; banner = showBanner; governed = governBudget;
            Level = level; shift = shiftNumber;
            population.Level = level;
            var settings = population.Settings;
            population.Budget = governBudget ? Mathf.Clamp(settings.startingBudget, settings.budgetFloor, settings.maxActiveCustomers) : settings.maxActiveCustomers;
            // Forty agents re-pathing after a sale call should all have routes within a frame or two.
            NavMesh.pathfindingIterationsPerFrame = Mathf.Max(NavMesh.pathfindingIterationsPerFrame, 400);
            this.sales = CrowdPlan.SalesFor(shiftNumber, DifficultyRules.Sales(Difficulty));
            ChooseSaleZones(catalog, jobSpots);
            sign = SaleSign.Create(transform);
            StartCoroutine(BuildWhenSettled());
        }

        /// <summary>
        /// Everything placed after the NavMesh bake (furniture, job crates, the carousel) carves its hole and
        /// reaches the physics scene a frame or two later; sale spots wait for that so none lands inside a crate.
        /// </summary>
        private System.Collections.IEnumerator BuildWhenSettled()
        {
            yield return null; yield return null;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            BuildSaleSpots();
            Ready = true;
        }

        // ---------------- where sales happen ----------------

        /// <summary>Departments shoppers can reach that hold one of tonight's jobs, else every department on sale.</summary>
        private void ChooseSaleZones(ShoppingCatalog catalog, IEnumerable<Vector3> jobSpots)
        {
            saleZones.Clear();
            if (jobSpots != null)
                foreach (var spot in jobSpots)
                {
                    var zone = directory.ZoneAt(spot);
                    if (zone.HasValue && catalog.Sells(zone.Value) && ZoneAccess.Customer.Allows(zone.Value) && !saleZones.Contains(zone.Value)) saleZones.Add(zone.Value);
                }
            if (saleZones.Count == 0)
                foreach (var zone in catalog.Departments) if (ZoneAccess.Customer.Allows(zone)) saleZones.Add(zone);
            saleZones.Sort();
        }

        public ZoneType? ZoneForSale(int index)
        {
            if (sales == null || index < 0 || index >= sales.Count || saleZones.Count == 0) return null;
            // Back-to-back sales never use the same department when there is a choice.
            int pick = sales[0].Pick % saleZones.Count;
            for (int i = 1; i <= index; i++)
            {
                int next = sales[i].Pick % saleZones.Count;
                if (saleZones.Count > 1 && next == pick) next = (next + 1) % saleZones.Count;
                pick = next;
            }
            return saleZones[pick];
        }

        /// <summary>
        /// Extra standing room round each department's shelves, so a sale draws a real crowd instead of
        /// queueing for the zone's handful of browse spots. Every spot is checked against the NavMesh and furniture.
        /// </summary>
        private void BuildSaleSpots()
        {
            SaleSpots = 0;
            var zones = new HashSet<ZoneType>(saleZones);
            var taken = new List<Vector3>();
            foreach (var p in directory.Points) taken.Add(p.Position);
            var browse = new List<NavigationPoint>();
            foreach (var p in directory.Points) if (p.kind == PointKind.Browse && zones.Contains(p.zone)) browse.Add(p);
            var count = new Dictionary<ZoneType, int>();
            var sum = new Dictionary<ZoneType, Vector3>();
            Vector2[] offsets = { new Vector2(0.85f, -0.9f), new Vector2(-0.85f, -0.9f), new Vector2(0f, -1.3f), new Vector2(0.9f, 0f), new Vector2(-0.9f, 0f) };
            foreach (var point in browse)
            {
                count.TryGetValue(point.zone, out int made);
                sum[point.zone] = (sum.TryGetValue(point.zone, out var s) ? s : Vector3.zero) + point.Position;
                foreach (var offset in offsets)
                {
                    if (made >= MaxSaleSpotsPerZone) break;
                    Vector3 at = point.Position + point.transform.right * offset.x + point.transform.forward * offset.y;
                    if (!Clear(at, point.zone, taken, out var spot)) continue;
                    directory.AddPoint(point.zone, PointKind.Sale, spot, point.transform.eulerAngles.y);
                    taken.Add(spot); made++; SaleSpots++;
                }
                count[point.zone] = made;
            }
            signSpots.Clear();
            foreach (var zone in zones)
            {
                int n = 0; Vector3 total = Vector3.zero;
                foreach (var p in directory.Points) if (p.zone == zone && (p.kind == PointKind.Browse || p.kind == PointKind.Sale)) { total += p.Position; n++; }
                if (n > 0) signSpots[zone] = total / n;
            }
        }

        private bool Clear(Vector3 at, ZoneType zone, List<Vector3> taken, out Vector3 spot)
        {
            spot = at;
            if (!NavMesh.SamplePosition(at, out var hit, 0.35f, NavigationAreas.CivilianMask)) return false;
            if (Mathf.Abs(hit.position.y - at.y) > 0.4f) return false;
            spot = hit.position;
            if (directory.ZoneAt(spot) != zone) return false;
            foreach (var other in taken) if ((other - spot).sqrMagnitude < 0.7f * 0.7f) return false;
            // Furniture (layer 0) and job props (layer 3: crates, vases, shirts, the carousel); people do not count.
            // Drop rugs count too, even as triggers: a crowd standing on one would knock the player's crate around.
            foreach (var c in Physics.OverlapCapsule(spot + Vector3.up * 0.36f, spot + Vector3.up * 1.6f, 0.3f, 1 | 1 << 3, QueryTriggerInteraction.Collide))
            {
                if (c.GetComponentInParent<PlacementZone>() != null) return false;
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<NpcController>() == null && c.GetComponentInParent<PlayerMotor>() == null) return false;
            }
            return true;
        }

        // ---------------- the night ----------------

        private void Update()
        {
            if (population == null) return;
            UpdateSale();
            UpdateCrowdLife();
            lodTimer -= Time.unscaledDeltaTime;
            if (lodTimer <= 0) { lodTimer = 0.25f; UpdateDetail(); }
            if (governed) Govern();
        }

        private void UpdateSale()
        {
            double left = remaining();
            int index = -1; double secondsLeft = 0;
            var stage = SalesEnabled && Ready ? CrowdPlan.StageAt(sales, left, duration, out index, out secondsLeft) : SaleStage.None;
            Stage = stage; SaleIndex = index; SaleSecondsLeft = secondsLeft;
            SaleZone = index >= 0 ? ZoneForSale(index) : null;
            // Lockdown has nobody to sell to.
            if (NightSchedule.At(left, duration).Crowd <= 0 && stage != SaleStage.None) { Stage = SaleStage.None; SaleZone = null; }
            if (Stage == SaleStage.Announced && announced != index && SaleZone.HasValue)
            {
                announced = index;
                toast?.Invoke("Attention shoppers: FLASH SALE in " + HudText.ZoneLabel(SaleZone) + " in " + Mathf.CeilToInt((float)secondsLeft) + " seconds!\nThe crowd will pour in there for " + (int)CrowdPlan.SaleSeconds + " s — and thin out everywhere else.", 6f);
                if (Audio != null) Audio.Announce();
            }
            if (Stage == SaleStage.Open && opened != index && SaleZone.HasValue)
            {
                opened = index;
                int going = population.Authority ? population.CallSale(SaleZone.Value) : 0;
                banner?.Invoke("FLASH SALE", HudText.ZoneLabel(SaleZone) + " is mobbed for " + (int)CrowdPlan.SaleSeconds + " s" + (going > 0 ? " — " + going + " shoppers drop everything" : "") + ". Elsewhere goes quiet.");
                if (Audio != null) Audio.Announce();
            }
            if (Stage != SaleStage.Open && opened >= 0 && closed != opened)
            {
                closed = opened;
                population.EndSale();
                toast?.Invoke("The flash sale is over. The crowd drifts back to the shelves.", 3.5f);
            }
            if (sign != null)
            {
                if (Stage == SaleStage.None || !SaleZone.HasValue || !signSpots.TryGetValue(SaleZone.Value, out var at)) sign.Hide();
                else sign.Show(at, Stage == SaleStage.Open, SaleSecondsLeft);
            }
        }

        /// <summary>One line for the top of the screen while a sale is announced or on; empty otherwise.</summary>
        public string EventLine
        {
            get
            {
                if (!SaleZone.HasValue) return "";
                string where = HudText.ZoneLabel(SaleZone).ToUpperInvariant();
                return Stage == SaleStage.Announced ? "FLASH SALE IN " + where + " IN " + Mathf.CeilToInt((float)SaleSecondsLeft)
                     : Stage == SaleStage.Open ? "FLASH SALE · " + where + " · " + HudText.Clock(SaleSecondsLeft) + " · " + population.AtSale + " shoppers there" : "";
            }
        }

        /// <summary>Night-page copy: tonight's crowd and the next sale.</summary>
        public string NightLine
        {
            get
            {
                string line = "Crowd tonight: " + CrowdPlan.LevelName(Level) + (Level > 0 ? " (level " + Level + " of " + CrowdPlan.MaxLevel + ")" : "")
                    + " · " + population.Customers + " shoppers now, up to " + population.Cap;
                double left = remaining();
                for (int i = 0; sales != null && i < sales.Count; i++)
                {
                    double until = left - sales[i].OpensAt * duration;
                    if (until > CrowdPlan.AnnounceSeconds && ZoneForSale(i).HasValue)
                        return line + "\nNext flash sale: " + HudText.ZoneLabel(ZoneForSale(i)) + " in " + HudText.Clock(until) + ". Shoppers flood it for " + (int)CrowdPlan.SaleSeconds + " s: get your job there done first, or do it while the rest of the mall is empty.";
                }
                return line;
            }
        }

        public bool TryMark(out Vector3 at, out string label)
        {
            at = default; label = "";
            if (Stage == SaleStage.None || !SaleZone.HasValue || !signSpots.TryGetValue(SaleZone.Value, out at)) return false;
            label = Stage == SaleStage.Open ? "SALE" : "sale soon";
            return true;
        }

        // ---------------- keeping it smooth ----------------

        /// <summary>
        /// Crowd level of detail: shoppers near the camera skin with four bones and cast shadows; distant ones use
        /// cheaper skinning and no shadow, and nobody's skeleton updates while off screen. Avoidance quality follows.
        /// </summary>
        private void UpdateDetail()
        {
            var eyes = Camera.main;
            if (eyes == null) return;
            Vector3 from = eyes.transform.position;
            foreach (var npc in population.Active)
            {
                if (npc == null) continue;
                float d = Vector3.Distance(npc.transform.position, from);
                detail.TryGetValue(npc, out int was);
                // Hysteresis: a shopper pacing along a boundary does not flicker its shadow on and off.
                int now = was == 0 ? (d > 15f ? 1 : 0) : was == 1 ? (d < 12f ? 0 : d > 27f ? 2 : 1) : (d < 23f ? 1 : 2);
                if (!detail.ContainsKey(npc)) now = d < 13.5f ? 0 : d < 25f ? 1 : 2;
                detail[npc] = now;
                var look = npc.Animation;
                if (look != null && look.Visual != null) look.Visual.SetDetail(now);
                if (npc.Movement != null) npc.Movement.SetAvoidance(now == 0 ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance);
            }
        }

        /// <summary>
        /// Every two seconds: slow frames lower the budget (never far below the people already here, then a
        /// pause to see if it helped), fast frames earn it back one at a time. A one-off hitch (a shader compile,
        /// the window losing focus) is ignored. If the mall is well over budget, one distant shopper heads home.
        /// </summary>
        private void Govern()
        {
            if (Time.timeScale <= 0) return;
            if ((warmup -= Time.unscaledDeltaTime) > 0) return;
            float frame = Time.unscaledDeltaTime;
            budgetTimer += frame; budgetFrames++; budgetMs += frame * 1000f; budgetWorst = Mathf.Max(budgetWorst, frame);
            if (budgetTimer < 2f) return;
            double mean = budgetMs / Mathf.Max(1, budgetFrames);
            bool hitch = budgetWorst > 0.25f;
            budgetTimer = 0; budgetFrames = 0; budgetMs = 0; budgetWorst = 0;
            if (hitch) return;
            if ((cooldown -= 2f) > 0) return;
            var settings = population.Settings;
            int customers = population.Customers;
            int basis = Mathf.Min(population.Budget, customers + 2);
            int next = CrowdBudget.Next(basis, mean, SmoothFrameMs, settings.budgetFloor, settings.maxActiveCustomers);
            if (next < basis) { cooldown = 6f; next = Mathf.Max(next, Mathf.Min(basis, customers - 4)); next = Mathf.Max(next, settings.budgetFloor); }
            else if (next == basis) next = population.Budget; // holding steady: keep the headroom already earned
            population.Budget = next;
            if (customers > next + 3) SendOneHome();
        }

        private void SendOneHome()
        {
            var eyes = Camera.main;
            if (eyes == null) return;
            CustomerBehavior farthest = null; float best = 0;
            foreach (var npc in population.Active)
            {
                if (!(npc.Behavior is CustomerBehavior c) || c.Brain == null || c.Brain.OnSale) continue;
                if (c.Brain.State == CustomerState.Leaving || c.Brain.State == CustomerState.Reporting || c.Brain.State == CustomerState.Alerted) continue;
                float d = (npc.transform.position - eyes.transform.position).sqrMagnitude;
                if (d > best) { best = d; farthest = c; }
            }
            if (farthest != null) farthest.Brain.Leave();
        }
    }

    /// <summary>The big orange sign that hangs over a flash sale: counts down, then flashes. One light, no collider.</summary>
    public sealed class SaleSign : MonoBehaviour
    {
        private TextMesh front, back;
        private Light glow;
        private Renderer board;
        private Material lit, dim;
        private float clock;

        public static SaleSign Create(Transform parent)
        {
            var host = new GameObject("Flash sale sign");
            host.transform.SetParent(parent, false);
            var sign = host.AddComponent<SaleSign>();
            sign.lit = ArtLibrary.Emissive(new Color(1f, 0.45f, 0.12f), 2.2f);
            sign.dim = ArtLibrary.Emissive(new Color(0.8f, 0.3f, 0.08f), 0.9f);
            sign.board = DressingKit.Panel(host.transform, "Sale board", Vector3.zero, new Vector3(2.3f, 0.62f, 0.05f));
            sign.board.sharedMaterial = sign.dim;
            sign.board.shadowCastingMode = ShadowCastingMode.Off;
            var trim = DressingKit.Panel(host.transform, "Sale trim", new Vector3(0f, 0f, 0f), new Vector3(2.42f, 0.74f, 0.03f));
            trim.sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.85f, 0.35f), 1.6f);
            trim.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var c in host.GetComponentsInChildren<Collider>()) Destroy(c);
            sign.front = SignFactory.Label(host.transform, "FLASH SALE", 0.3f, Color.white);
            if (sign.front != null) sign.front.transform.localPosition = new Vector3(0f, 0f, -0.035f);
            sign.back = SignFactory.Label(host.transform, "FLASH SALE", 0.3f, Color.white);
            if (sign.back != null) { sign.back.transform.localPosition = new Vector3(0f, 0f, 0.035f); sign.back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); }
            var lamp = new GameObject("Sale glow");
            lamp.transform.SetParent(host.transform, false);
            lamp.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            sign.glow = lamp.AddComponent<Light>();
            sign.glow.type = LightType.Point; sign.glow.range = 7f; sign.glow.color = new Color(1f, 0.55f, 0.25f);
            sign.glow.shadows = LightShadows.None; sign.glow.intensity = 0f;
            foreach (var r in host.GetComponentsInChildren<Renderer>()) r.gameObject.layer = 2;
            host.SetActive(false);
            return sign;
        }

        public void Show(Vector3 floorSpot, bool open, double secondsLeft)
        {
            if (!gameObject.activeSelf) { gameObject.SetActive(true); clock = 0; }
            clock += Time.deltaTime;
            transform.position = floorSpot + Vector3.up * (2.55f + Mathf.Sin(clock * 1.6f) * 0.05f);
            transform.rotation = Quaternion.Euler(0f, clock * 25f, Mathf.Sin(clock * 1.1f) * 2f);
            string text = open ? (Mathf.Repeat(clock, 1.6f) < 0.8f ? "FLASH SALE" : "-50% NOW") : "SALE IN " + Mathf.CeilToInt((float)secondsLeft);
            if (front != null && front.text != text) { front.text = text; back.text = text; }
            bool flash = open ? Mathf.Repeat(clock, 0.5f) < 0.3f : Mathf.Repeat(clock, 1f) < 0.5f;
            board.sharedMaterial = flash ? lit : dim;
            glow.intensity = open ? (flash ? 2.6f : 1.4f) : 0.8f;
        }

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
    }
}
