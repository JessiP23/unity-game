using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Runs tonight's special night on top of the ordinary one (see <see cref="NightKinds"/>): builds its set
    /// pieces once the player has clocked in, keeps its distraction on schedule and pays out its prize.
    /// Black Friday and the blackout are mostly rules (crowd, sales, lights, sight); the heist and the birthday
    /// party add a place, a prize and a once-a-minute distraction you time your move to.
    /// </summary>
    public sealed class ScenarioDirector : MonoBehaviour
    {
        public const int ShowGroup = 200, CakeGroup = 300;
        public NightKind Kind { get; private set; } = NightKind.Regular;
        private CrowdDirector crowd;
        private CustomerPopulationManager population;
        private StoreDirectory directory;
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private Func<double> elapsed;
        private double duration = 360;
        private Action<string, float> toast;
        private Action<string, string> banner;
        private Func<StoreAudio> sound;
        private readonly System.Random random = new System.Random(4242);
        private SaleStage show = SaleStage.None;
        private double showLeft;
        private Vector3 stage, prizeAt;
        private HoldSpot prize;
        private Transform diamond;
        private readonly HashSet<PlayerMotor> carrying = new HashSet<PlayerMotor>();
        private readonly HashSet<int> partyKids = new HashSet<int>();
        private readonly HashSet<int> torchIds = new HashSet<int>();
        private bool LiveId(int id)
        {
            foreach (var npc in population.Active) if (npc != null && npc.Id == id) return true;
            return false;
        }
        private Light[] stageLights = new Light[0];
        private float clock;

        /// <summary>The prize was taken tonight (the present) or is being carried out (the diamond).</summary>
        public bool PrizeTaken { get; private set; }
        /// <summary>Pays a special objective's points (set by the game).</summary>
        public Action<ScoreEvent> Award;
        public bool Carrying(PlayerMotor motor) => carrying.Contains(motor);
        public int Torches { get; private set; }
        public int PartyKids => partyKids.Count;
        public Vector3? Stage => Kind == NightKind.Heist || Kind == NightKind.Birthday ? stage : (Vector3?)null;
        public SaleStage Show => show;

        public void Configure(CrowdDirector crowdDirector, CustomerPopulationManager people, StoreDirectory store, LocalMatchAuthority match, WorldSignals world,
            Func<double> nightElapsed, double nightSeconds, Action<string, float> showToast, Action<string, string> showBanner, Func<StoreAudio> audio)
        {
            duration = nightSeconds;
            crowd = crowdDirector; population = people; directory = store; authority = match; signals = world;
            elapsed = nightElapsed; toast = showToast; banner = showBanner; sound = audio;
        }

        /// <summary>Clocked in: set the night up. Safe to call once per night.</summary>
        public void Begin(NightKind kind)
        {
            Kind = kind;
            population.Spawned -= OnSpawned;
            if (kind == NightKind.Regular) return;
            population.Spawned += OnSpawned;
            population.DifficultyFactor *= (float)NightKinds.CrowdFactor(kind);
            switch (kind)
            {
                case NightKind.BlackFriday:
                    population.Pull.Lure = 0.85;
                    for (int i = 0; i < 8; i++) SpawnAtDoor(null, ShopperKind.Regular);
                    banner?.Invoke("BLACK FRIDAY", "Doors open early. Four doorbusters tonight — hide in the stampede.");
                    break;
                case NightKind.Blackout:
                    foreach (var npc in population.Active) OnSpawned(npc);
                    banner?.Invoke("BLACKOUT", "The power's out. Shoppers see only down their phone torches. The guard's flashlight never goes off.");
                    break;
                case NightKind.Heist:
                    BuildHeist();
                    banner?.Invoke("THE HEIST", "A diamond in the atrium. Lift it while everyone watches the fashion show, then get out with it.");
                    break;
                case NightKind.Birthday:
                    BuildParty();
                    banner?.Invoke("BIRTHDAY PARTY", "Kids have taken over the toy store. When the cake comes out, grab the present.");
                    break;
            }
        }

        private NpcController SpawnAtDoor(CustomerProfile profile, ShopperKind kind)
        {
            var door = directory.Pick(PointKind.Spawn, null, ZoneAccess.Customer, random, -1);
            if (door == null) return null;
            Vector3 at = door.Position + door.transform.right * ((float)random.NextDouble() - 0.5f) * 2f;
            if (NavMesh.SamplePosition(at, out var hit, 0.8f, NavMesh.AllAreas)) at = hit.position;
            return population.Spawn(NpcRole.Customer, at, door.transform.eulerAngles.y, false, profile, kind);
        }

        /// <summary>A blackout changes how everyone sees; some carry a phone torch.</summary>
        private void OnSpawned(NpcController npc)
        {
            if (Kind != NightKind.Blackout || npc == null || npc.Perception == null) return;
            if (npc.Role != NpcRole.Customer) { npc.Perception.SetSight(5f, 60f); return; }
            torchIds.RemoveWhere(id => !LiveId(id));
            bool torch = random.NextDouble() < NightKinds.TorchShare && torchIds.Count < 7;
            var (range, fov) = NightKinds.BlackoutSight(torch);
            npc.Perception.SetSight((float)range, (float)fov);
            var look = npc.GetComponent<ShopperLook>();
            if (torch && look != null) { look.AddTorch(); torchIds.Add(npc.Id); Torches = torchIds.Count; }
        }

        // ---------------- the heist ----------------

        private void BuildHeist()
        {
            // The runway and the case share the atrium, a few metres apart, so the crowd at one has its back to the other.
            // East or west of the fountain, between the benches (z -20 and -28) and clear of the planters and wall pots.
            stage = Clear(new[] { new Vector3(7.6f, 0f, -24.2f), new Vector3(-7.6f, 0f, -24.2f) }, new Vector3(6.4f, 1f, 1.6f));
            var runway = Box("Fashion runway", stage + Vector3.up * 0.12f, new Vector3(6f, 0.24f, 1.3f), ArtLibrary.Lit(new Color(0.08f, 0.08f, 0.09f), 0.85f));
            Carve(runway, new Vector3(6f, 1f, 1.3f));
            Box("Runway light strip", stage + new Vector3(0f, 0.245f, 0.6f), new Vector3(6f, 0.01f, 0.06f), ArtLibrary.Emissive(new Color(1f, 0.85f, 0.6f), 2f));
            Box("Runway light strip", stage + new Vector3(0f, 0.245f, -0.6f), new Vector3(6f, 0.01f, 0.06f), ArtLibrary.Emissive(new Color(1f, 0.85f, 0.6f), 2f));
            Sign("FASHION SHOW", stage + new Vector3(0f, 2.6f, 0f), new Color(0.12f, 0.1f, 0.14f));
            stageLights = new[] { Spot(stage + new Vector3(-2f, 4f, -2.5f), stage + new Vector3(-1f, 0.3f, 0f)), Spot(stage + new Vector3(2f, 4f, -2.5f), stage + new Vector3(1f, 0.3f, 0f)) };
            foreach (var l in stageLights) l.intensity = 0f;
            // Two mannequins "model" on the runway, striking a pose. They never move — and a third one would fit right in.
            for (int i = -1; i <= 1; i += 2)
            {
                var host = new GameObject("Runway model");
                host.transform.SetParent(transform, false);
                host.transform.SetPositionAndRotation(stage + new Vector3(i * 1.9f, 0.24f, 0f), Quaternion.Euler(0f, 180f + i * 20f, 0f));
                var visual = CharacterVisual.Attach(host.transform, i < 0 ? CharacterProfile.MannequinFemale : CharacterProfile.MannequinMale);
                if (visual != null) { visual.HoldPose(1.5 + i); visual.SetStance(Stance.Display); }
            }
            crowd.AddAttraction(ShowGroup, stage, new[] { 2.0f, 2.8f, 3.6f }, 12);

            var caseSpots = new List<Vector3>();
            foreach (var c in new[] { new Vector3(8.4f, 0f, -19.6f), new Vector3(-8.4f, 0f, -19.6f), new Vector3(3.6f, 0f, -30.8f), new Vector3(-3.6f, 0f, -30.8f) })
                if ((c - stage).magnitude > 4f && (c - stage).magnitude < 13f) caseSpots.Add(c);
            prizeAt = Clear(caseSpots.ToArray(), new Vector3(1.6f, 1.2f, 1.6f));
            var plinth = Box("Jewel plinth", prizeAt + Vector3.up * 0.45f, new Vector3(0.8f, 0.9f, 0.8f), ArtLibrary.Lit(new Color(0.12f, 0.09f, 0.07f), 0.7f));
            Carve(plinth, new Vector3(0.9f, 1f, 0.9f));
            Box("Jewel case", prizeAt + Vector3.up * 1.15f, new Vector3(0.7f, 0.5f, 0.7f), ArtLibrary.Glass(new Color(0.8f, 0.9f, 1f, 0.25f)), keepCollider: false);
            diamond = MeshKit.Spin(transform, "Diamond", prizeAt + Vector3.up * 1.05f,
                new[] { new Vector2(0f, 0f), new Vector2(0.11f, 0.13f), new Vector2(0.08f, 0.17f), new Vector2(0f, 0.18f) }, 8,
                ArtLibrary.Emissive(new Color(0.75f, 0.95f, 1f), 2.4f)).transform;
            Spot(prizeAt + new Vector3(0f, 3.5f, 0f), prizeAt + Vector3.up * 1.1f).intensity = 3f;
            Sign("DIAMOND · DO NOT TOUCH", prizeAt + new Vector3(0f, 1.75f, 0f), new Color(0.1f, 0.12f, 0.16f), 0.08f);
            MakePrize(3.5f, "E — lift the diamond (hold still 3.5 s; it sets off the alarm)", "Lifting the diamond", OnDiamond);
        }

        private void OnDiamond(PlayerMotor thief)
        {
            carrying.Add(thief);
            if (diamond != null) diamond.gameObject.SetActive(false);
            signals?.Noise.Publish(new NoiseEvent(prizeAt, 2.4f, "the jewel case alarm"));
            sound?.Invoke()?.Announce();
            toast?.Invoke("ALARM! You have the diamond. The guard heard that — get out before dawn to keep it (+" + NightKinds.DiamondPoints + ").", 6f);
        }

        /// <summary>Caught with the diamond: it goes back in its case, ready to be stolen again.</summary>
        public void OnCaptured(PlayerMotor motor)
        {
            if (!carrying.Remove(motor) || Kind != NightKind.Heist) return;
            if (diamond != null) diamond.gameObject.SetActive(true);
            if (prize != null) Destroy(prize.gameObject);
            MakePrize(3.5f, "E — lift the diamond (hold still 3.5 s; it sets off the alarm)", "Lifting the diamond", OnDiamond);
            toast?.Invoke("They took the diamond back. It's in its case again.", 4f);
        }

        /// <summary>Points the night's prize pays at the end (the diamond, if someone is carrying it out).</summary>
        public bool PaysOnEscape => Kind == NightKind.Heist && carrying.Count > 0;

        // ---------------- the birthday party ----------------

        private void BuildParty()
        {
            stage = Clear(new[] { new Vector3(-15.6f, 0f, -30.4f), new Vector3(-14.6f, 0f, -20.4f), new Vector3(-25.5f, 0f, -20.2f), new Vector3(-25.5f, 0f, -31f) }, new Vector3(2.2f, 1.2f, 1.4f));
            var table = Box("Party table", stage + Vector3.up * 0.38f, new Vector3(1.8f, 0.76f, 1f), ArtLibrary.Stripes(new Color(1f, 0.85f, 0.3f), new Color(0.98f, 0.98f, 0.95f), 8));
            Carve(table, new Vector3(1.9f, 1f, 1.1f));
            var cake = new GameObject("Birthday cake").transform;
            cake.SetParent(transform, false);
            MeshKit.Spin(cake, "Cake", stage + Vector3.up * 0.76f, new[] { new Vector2(0f, 0f), new Vector2(0.26f, 0f), new Vector2(0.26f, 0.12f), new Vector2(0.18f, 0.12f), new Vector2(0.18f, 0.22f), new Vector2(0f, 0.22f) }, 20, ArtLibrary.Lit(new Color(0.98f, 0.8f, 0.86f), 0.4f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 at = stage + new Vector3(Mathf.Cos(a) * 0.1f, 0.98f + 0.04f, Mathf.Sin(a) * 0.1f);
                Box("Candle", at, new Vector3(0.015f, 0.08f, 0.015f), ArtLibrary.Lit(new Color(0.5f, 0.75f, 1f), 0.3f), keepCollider: false, parent: cake);
                Box("Flame", at + Vector3.up * 0.055f, new Vector3(0.012f, 0.025f, 0.012f), ArtLibrary.Emissive(new Color(1f, 0.7f, 0.25f), 3f), keepCollider: false, parent: cake);
            }
            // Balloons on strings over the table.
            Color[] colors = { new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.85f, 0.25f), new Color(0.35f, 0.85f, 0.45f) };
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = stage + new Vector3(-0.8f + i * 0.32f, 2.0f + (i % 2) * 0.25f, 0.45f);
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Balloon"; Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false); b.transform.position = at; b.transform.localScale = new Vector3(0.28f, 0.34f, 0.28f);
                b.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(colors[i % colors.Length], 0.85f);
                Box("Balloon string", at - Vector3.up * 0.65f, new Vector3(0.005f, 1.1f, 0.005f), ArtLibrary.Lit(Color.white, 0.2f), keepCollider: false);
            }
            Sign("HAPPY BIRTHDAY!", stage + new Vector3(0f, 2.7f, 0f), new Color(0.85f, 0.25f, 0.45f));
            crowd.AddAttraction(CakeGroup, stage, new[] { 1.3f, 1.9f, 2.5f }, 12);
            // The party: a host and eight kids who stay in the toy store and see everything.
            var party = ShopperArchetypes.PartyKid();
            var catalog = population.Catalog;
            population.Catalog = ShoppingCatalog.Mall; // the party is in the toy store, even on a test night's classic catalog
            var spots = new List<NavigationPoint>();
            foreach (var p in directory.Points) if (p.zone == ZoneType.ToyStore && (p.kind == PointKind.Browse || p.kind == PointKind.Linger)) spots.Add(p);
            for (int i = 0; i < 8; i++)
            {
                Vector3 at = spots.Count > 0 ? spots[i % spots.Count].Position + new Vector3(((float)random.NextDouble() - 0.5f), 0f, ((float)random.NextDouble() - 0.5f)) : stage + Vector3.right * i * 0.4f;
                if (NavMesh.SamplePosition(at, out var hit, 1f, NavigationAreas.CivilianMask)) at = hit.position;
                var kid = population.Spawn(NpcRole.Customer, at, (float)random.NextDouble() * 360f, true, party, ShopperKind.Kid);
                if (kid != null) partyKids.Add(kid.Id);
            }
            population.Catalog = catalog;
            // The present: as far from the cake as the toy store allows.
            var pile = new List<Vector3> { new Vector3(-28.3f, 0f, -23.5f), new Vector3(-28.3f, 0f, -27.5f), new Vector3(-13.5f, 0f, -31.5f), new Vector3(-13.5f, 0f, -19.5f) };
            pile.Sort((a, c) => (c - stage).sqrMagnitude.CompareTo((a - stage).sqrMagnitude));
            prizeAt = Clear(pile.ToArray(), new Vector3(1f, 1f, 1f));
            var gift = Box("Birthday present", prizeAt + Vector3.up * 0.2f, new Vector3(0.5f, 0.4f, 0.5f), ArtLibrary.Stripes(new Color(0.3f, 0.55f, 1f), new Color(1f, 0.85f, 0.3f), 3));
            Box("Ribbon", prizeAt + Vector3.up * 0.41f, new Vector3(0.52f, 0.03f, 0.08f), ArtLibrary.Lit(new Color(0.95f, 0.25f, 0.3f), 0.6f), keepCollider: false);
            Box("Ribbon", prizeAt + Vector3.up * 0.41f, new Vector3(0.08f, 0.03f, 0.52f), ArtLibrary.Lit(new Color(0.95f, 0.25f, 0.3f), 0.6f), keepCollider: false);
            Carve(gift, new Vector3(0.6f, 0.6f, 0.6f));
            MakePrize(2.5f, "E — take the birthday present (hold still 2.5 s)", "Taking the present", thief =>
            {
                PrizeTaken = true;
                Award?.Invoke(ScoreEvent.Present);
                if (gift != null) gift.SetActive(false);
                foreach (Transform t in transform) if (t.name == "Ribbon") t.gameObject.SetActive(false);
            });
        }

        // ---------------- the distraction ----------------

        private void Update()
        {
            if (Kind != NightKind.Heist && Kind != NightKind.Birthday || authority == null) return;
            clock += Time.deltaTime;
            if (diamond != null && diamond.gameObject.activeSelf) diamond.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            var before = show;
            show = authority.Clock.Paused ? show : NightKinds.ShowAt(elapsed(), out showLeft);
            if (NightSchedule.At(authority.Clock.Remaining, duration).Crowd <= 0) show = SaleStage.None;
            if (show == before) { if (show == SaleStage.Open) PulseLights(); return; }
            bool heist = Kind == NightKind.Heist;
            if (show == SaleStage.Announced) toast?.Invoke(heist ? "The fashion show starts in a few seconds — everyone will turn to the runway." : "Cake time in a few seconds — the kids will rush to the table.", 4f);
            if (show == SaleStage.Open)
            {
                sound?.Invoke()?.Announce();
                if (heist) crowd.Gather(ShowGroup, stage, 16f, 12, NightKinds.ShowSeconds);
                else crowd.Gather(CakeGroup, stage, 20f, 12, NightKinds.ShowSeconds, c => partyKids.Contains(c.NpcId));
            }
            if (show == SaleStage.None && before == SaleStage.Open)
            {
                crowd.Release(heist ? ShowGroup : CakeGroup);
                foreach (var l in stageLights) l.intensity = 0f;
            }
        }

        private void PulseLights()
        {
            foreach (var l in stageLights) l.intensity = 3.5f + Mathf.Sin(clock * 3f) * 0.8f;
        }

        /// <summary>One line under the clock while the show or the cake is on or about to be.</summary>
        public string EventLine(out bool hot)
        {
            hot = show == SaleStage.Open;
            if (show == SaleStage.None) return "";
            bool heist = Kind == NightKind.Heist;
            if (show == SaleStage.Announced) return (heist ? "FASHION SHOW IN " : "CAKE TIME IN ") + Mathf.CeilToInt((float)showLeft);
            return heist ? "FASHION SHOW · " + HudText.Clock(showLeft) + " · every eye on the runway" : "CAKE TIME · " + HudText.Clock(showLeft) + " · the kids are at the cake";
        }

        /// <summary>The night's objective for the HUD (empty on ordinary nights).</summary>
        public string Objective(PlayerMotor me)
        {
            switch (Kind)
            {
                case NightKind.Heist: return me != null && carrying.Contains(me) ? "YOU HAVE THE DIAMOND — get out before dawn" : "Lift the diamond (atrium) during the fashion show · +" + NightKinds.DiamondPoints;
                case NightKind.Birthday: return PrizeTaken ? "Present taken!" : "Take the present (toy store) at cake time · +" + NightKinds.PresentPoints;
                case NightKind.BlackFriday: return "Black Friday · score ×" + NightKinds.ScoreFactor(Kind).ToString("0.0#") + " · " + population.Customers + " shoppers";
                case NightKind.Blackout: return "Blackout · stay out of the torch beams · score ×" + NightKinds.ScoreFactor(Kind).ToString("0.0#");
                default: return "";
            }
        }

        public bool TryPrizeMark(out Vector3 at, out string label)
        {
            at = prizeAt; label = Kind == NightKind.Heist ? "DIAMOND" : "PRESENT";
            return (Kind == NightKind.Heist && carrying.Count == 0) || (Kind == NightKind.Birthday && !PrizeTaken);
        }

        // ---------------- building helpers ----------------

        private void MakePrize(float seconds, string start, string label, Action<PlayerMotor> done)
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.name = Kind == NightKind.Heist ? "Diamond case" : "Present pile";
            host.transform.SetParent(transform, false);
            host.transform.position = prizeAt + Vector3.up * (Kind == NightKind.Heist ? 1.1f : 0.25f);
            host.transform.localScale = Kind == NightKind.Heist ? new Vector3(0.75f, 0.55f, 0.75f) : new Vector3(0.55f, 0.5f, 0.55f);
            host.GetComponent<Renderer>().enabled = false;
            host.layer = 3;
            prize = host.AddComponent<HoldSpot>();
            prize.Configure(authority, signals, ActionKind.Steal, Kind == NightKind.Heist ? "diamond" : "present");
            prize.Seconds = seconds; prize.KeepOnCancel = 0.6f;
            prize.StartHint = start; prize.Label = label; prize.DoneHint = "Taken";
            prize.BlockedHint = "Wait until nobody is looking";
            prize.Completed = done;
        }

        private GameObject Box(string name, Vector3 center, Vector3 size, Material material, bool keepCollider = true, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent != null ? parent : transform, false);
            box.transform.position = center; box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Destroy(box.GetComponent<Collider>());
            return box;
        }

        private static void Carve(GameObject thing, Vector3 size)
        {
            var obstacle = thing.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(size.x / thing.transform.lossyScale.x, size.y / thing.transform.lossyScale.y, size.z / thing.transform.lossyScale.z);
            obstacle.carving = true;
        }

        private void Sign(string text, Vector3 at, Color board, float letter = 0.18f)
        {
            var host = new GameObject(text + " sign").transform;
            host.SetParent(transform, false);
            host.position = at;
            for (int side = 0; side < 2; side++)
            {
                var label = SignFactory.Label(host, text, letter, Color.white);
                if (label == null) continue;
                label.transform.localPosition = new Vector3(0f, 0f, side == 0 ? -0.03f : 0.03f);
                label.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
            }
            var plate = DressingKit.Panel(host, "Plate", Vector3.zero, new Vector3(text.Length * letter * 0.62f + 0.3f, letter * 1.8f, 0.04f));
            plate.sharedMaterial = ArtLibrary.Lit(board, 0.5f);
            plate.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var c in host.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        private Light Spot(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Stage light");
            go.transform.SetParent(transform, false);
            go.transform.position = from; go.transform.LookAt(to);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot; light.spotAngle = 40f; light.range = 9f; light.intensity = 2.5f;
            light.color = new Color(1f, 0.92f, 0.8f); light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>The first candidate with open floor for a footprint of this size (else the first candidate).</summary>
        private static Vector3 Clear(Vector3[] candidates, Vector3 size)
        {
            foreach (var c in candidates)
            {
                if (!NavMesh.SamplePosition(c, out var hit, 0.6f, NavigationAreas.CivilianMask)) continue;
                Vector3 at = hit.position;
                bool blocked = false;
                foreach (var col in Physics.OverlapBox(at + Vector3.up * (size.y * 0.5f + 0.05f), size * 0.5f, Quaternion.identity, 1 | 1 << 3, QueryTriggerInteraction.Ignore))
                    if (col.GetComponentInParent<NpcController>() == null && col.GetComponentInParent<PlayerMotor>() == null) { blocked = true; break; }
                if (!blocked) return at;
            }
            Debug.LogWarning("[NIGHT] no clear spot among " + candidates.Length + " candidates for a " + size + " set piece; using the least crowded one.");
            Vector3 best = candidates.Length > 0 ? candidates[0] : new Vector3(0f, 0f, -30f);
            int fewest = int.MaxValue;
            foreach (var c in candidates)
            {
                int n = Physics.OverlapBox(c + Vector3.up * (size.y * 0.5f + 0.05f), size * 0.5f, Quaternion.identity, 1, QueryTriggerInteraction.Ignore).Length;
                if (n < fewest) { fewest = n; best = c; }
            }
            return best;
        }
    }
}
