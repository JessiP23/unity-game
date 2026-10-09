using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Builds the night's objectives in the store: the three drawn jobs, the optional stands, the bell,
    /// the model stand and the exit, each dressed with a real prop. Also answers "where should the gold
    /// marker point for this job right now". Rules stay in <see cref="JobCatalog"/> and the mission trackers.
    /// </summary>
    public sealed class JobBuilder
    {
        public IReadOnlyList<JobTemplate> Jobs { get; private set; }
        public MissionDefinition[] Definitions { get; private set; }
        public Transform Escape { get; private set; }
        /// <summary>The upstairs fire exit: the only way out once the front shutter is down.</summary>
        public Transform FireExit { get; private set; }
        public FrontShutter Shutter { get; private set; }
        public Transform Rescue { get; private set; }
        public Transform ClothingRug { get; private set; }
        public Transform HomeRug { get; private set; }
        public DistractionBell Bell { get; private set; }
        public readonly List<BonusDisplay> Bonuses = new List<BonusDisplay>();
        /// <summary>Set by the root: true when no guard or shopper can see this player.</summary>
        public Func<PlayerMotor, bool> Unwatched = _ => true;
        public Action<string> Toast = _ => { };
        /// <summary>The pose a player is holding right now (null when not posing); gems behind displays need it.</summary>
        public Func<PlayerMotor, Stance?> HeldPose = _ => null;
        public Action<Gem, PlayerMotor> GemCollected = (_, __) => { };
        public readonly List<Gem> Gems = new List<Gem>();
        /// <summary>The window job's zone, which starts by itself; the HUD shows its progress since no E is involved.</summary>
        public HoldSpot Window => window;

        private readonly Transform root;
        private readonly LocalMatchAuthority authority;
        private readonly WorldSignals signals;
        private readonly GameRulesAsset rules;
        private readonly int shift;
        private readonly System.Random layout;
        private readonly bool solo;
        private bool cratesBuilt;
        private readonly List<PhysicalItem> crates = new List<PhysicalItem>();
        private readonly List<HoldSpot> tags = new List<HoldSpot>();
        private HoldSpot window, swap, cameraPole;
        private Transform swapMarker;
        private int swapIndex;
        private CrossZone lane;
        private bool cameraTurned;
        private Transform cameraHead;
        private ReturnPoint lostFound;
        private PhysicalItem toy;
        private readonly List<PhysicalItem> vases = new List<PhysicalItem>();
        private static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f), Plinth = new Color(0.92f, 0.92f, 0.9f), DarkPanel = new Color(0.12f, 0.1f, 0.08f);

        public JobBuilder(Transform parent, LocalMatchAuthority match, WorldSignals world, GameRulesAsset config, int shiftNumber, System.Random rng, bool soloNight)
        {
            root = parent; authority = match; signals = world; rules = config; shift = shiftNumber; layout = rng; solo = soloNight;
        }

        private Vector3 Pick(Vector3[] pool, List<int> used)
        {
            int index;
            do index = layout.Next(pool.Length); while (used.Contains(index) && used.Count < pool.Length);
            used.Add(index);
            return pool[index];
        }

        /// <summary>Everything that stands on the floor before the NavMesh bakes.</summary>
        public void Build()
        {
            Jobs = JobCatalog.Draw(shift);
            var definitions = new List<MissionDefinition>();
            foreach (var job in Jobs)
            {
                var definition = ScriptableObject.CreateInstance<MissionDefinition>();
                definition.id = job.Id; definition.title = job.Title; definition.kind = job.Kind; definition.targetTag = job.Tag;
                definition.quantity = job.Quantity; definition.destination = job.Destination; definition.requiredItem = job.RequiredItem; definition.required = true;
                definitions.Add(definition);
            }
            Definitions = definitions.ToArray();

            BuildClothingRug();
            BuildKeyAndDoor();
            BuildStands();
            BuildBell();
            BuildModelStand();
            if (!solo) BuildRescue();
            BuildExit();
            BuildGems();
            foreach (var job in Jobs)
                switch (job.Id)
                {
                    case "collect-crates": case "place-crate": EnsureCrates(); break;
                    case "steal-shirt": BuildShirts(); break;
                    case "window-display": BuildWindow(); break;
                    case "price-tags": BuildPriceTags(); break;
                    case "camera-blind": BuildCamera(); break;
                    case "lost-toy": BuildLostToy(); break;
                    case "fragile-vase": BuildVases(); break;
                }
        }

        /// <summary>Jobs that hang off the dressed store (display mannequins exist only after StoreDressing.Apply).</summary>
        public void AfterDressing()
        {
            foreach (var job in Jobs) if (job.Id == "swap-places") BuildSwap();
        }

        /// <summary>
        /// Safety net after the NavMesh bakes: any loose item whose spot is not on walkable floor (inside a
        /// shelf, say) slides to the nearest walkable point, so the gold marker never points into furniture.
        /// </summary>
        private static readonly Collider[] overlaps = new Collider[8];
        /// <summary>
        /// Moves any loose item whose body overlaps store furniture (a crate spawned inside a shelf) to the
        /// nearest open floor. Items resting ON a table or shelf top are left alone: the test is a physics
        /// overlap of the item's own box shrunk a little, not distance to the NavMesh, because the NavMesh is
        /// eroded by the agent radius and would count every table-top item as "inside".
        /// </summary>
        public int SnapLooseItemsToFloor()
        {
            int moved = 0;
            foreach (var item in PhysicalItem.Active)
            {
                if (item == null || item.Holder != null) continue;
                var box = item.GetComponent<Collider>();
                if (box == null) continue;
                var bounds = box.bounds;
                Vector3 half = Vector3.Max(bounds.extents - Vector3.one * 0.04f, Vector3.one * 0.01f);
                int count = Physics.OverlapBoxNonAlloc(bounds.center, half, overlaps, Quaternion.identity, 1, QueryTriggerInteraction.Ignore);
                bool buried = false;
                for (int i = 0; i < count && !buried; i++)
                    buried = overlaps[i] != box && !overlaps[i].transform.IsChildOf(item.transform) && overlaps[i].GetComponentInParent<PhysicalItem>() == null;
                if (!buried) continue;
                if (!UnityEngine.AI.NavMesh.SamplePosition(bounds.center, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                Vector3 flat = new Vector3(hit.position.x - bounds.center.x, 0, hit.position.z - bounds.center.z);
                Vector3 away = flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
                Vector3 target = new Vector3(hit.position.x, hit.position.y + bounds.extents.y + 0.02f, hit.position.z) + away * 0.35f;
                item.transform.position = target;
                if (item.Body != null) { item.Body.position = target; item.Body.linearVelocity = Vector3.zero; }
                moved++;
            }
            return moved;
        }

        public string How(string missionId) => JobCatalog.Find(missionId)?.How ?? "";

        // ---------------- shared fixtures ----------------

        private void BuildClothingRug()
        {
            var zone = PrimitiveWorld.Box(root, "Clothing placement zone", new Vector3(-4.2f, 0.15f, 7.0f), new Vector3(3, 0.3f, 3), Color.green);
            zone.AddComponent<PlacementZone>();
            ObjectiveDressing.Surface(zone, "wool_boucle", new Vector2(2, 2), new Color(0.45f, 0.75f, 0.5f), new Color(0.16f, 0.42f, 0.28f));
            ClothingRug = zone.transform;
        }

        private void BuildKeyAndDoor()
        {
            var keyData = ScriptableObject.CreateInstance<ItemDefinition>(); keyData.id = "employee-key";
            keyData.displayName = "Employee key card"; keyData.inventoryOnly = true; keyData.slotCost = 0;
            Vector3[] keyPool = { new Vector3(9, 0.5f, -10), new Vector3(-6.8f, 1.12f, -13.1f), new Vector3(12.6f, 0.78f, 2.1f) };
            var key = PrimitiveWorld.Box(root, "Employee key", shift == 0 ? keyPool[0] : keyPool[layout.Next(keyPool.Length)], new Vector3(0.09f, 0.012f, 0.06f), new Color(0.12f, 0.16f, 0.3f));
            key.layer = 3;
            key.AddComponent<PhysicalItem>().Configure(keyData, signals);
            var stripe = PrimitiveWorld.Box(key.transform, "Stripe", key.transform.position + new Vector3(0, 0.007f, 0.015f), Vector3.one, Color.white);
            stripe.transform.localScale = new Vector3(0.8f, 0.2f, 0.18f);
            stripe.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(0.9f, 0.95f, 1f), 0.6f);
            UnityEngine.Object.Destroy(stripe.GetComponent<Collider>());
            var door = PrimitiveWorld.Box(root, "Employee door", new Vector3(10, 1.3f, 5), new Vector3(2, 2.6f, 0.3f), Color.blue);
            var interactable = door.AddComponent<DoorInteractable>();
            interactable.Configure("employee-key", signals);
            door.AddComponent<UnityEngine.AI.NavMeshObstacle>().carving = true;
            ObjectiveDressing.Recolor(door, new Color(0.36f, 0.37f, 0.4f), 0.5f);
            ObjectiveDressing.Sign(root, "STAFF ONLY", new Vector3(10f, 2.78f, 4.8f), 0f, DarkPanel, new Color(1f, 0.85f, 0.35f), 0.5f, 0.1f);
            Door = interactable;
        }
        public DoorInteractable Door { get; private set; }

        private void BuildStands()
        {
            Vector3[] standPool = { new Vector3(3, 0.6f, -5.7f), new Vector3(6.5f, 0.6f, 7), new Vector3(-11f, 0.6f, -4f), new Vector3(11.3f, 0.6f, -1.5f) };
            string[] labels = { "Aisle promotion", "Electronics promotion", "West aisle promotion", "Home promotion" };
            var used = new List<int>();
            for (int i = 0; i < 2; i++)
            {
                int index = shift == 0 ? i : -1;
                if (index < 0) { Pick(standPool, used); index = used[used.Count - 1]; }
                var stand = PrimitiveWorld.Box(root, labels[index], standPool[index], new Vector3(0.65f, 1.2f, 0.35f), new Color(0.7f, 0.3f, 0.8f));
                var bonus = stand.AddComponent<BonusDisplay>(); bonus.Configure(authority, signals, labels[index]);
                Bonuses.Add(bonus);
                if (!ObjectiveDressing.Dress(stand, "wooden_crate_02", 1.0f, 0f, Wood))
                    ObjectiveDressing.Recolor(stand, Wood);
                else
                {
                    Vector3 top = standPool[index] + new Vector3(0, 0.42f, 0);
                    ArtLibrary.Spawn("food_apple_01", root, top + new Vector3(-0.12f, 0, 0.05f), 20);
                    ArtLibrary.Spawn("food_lime_01", root, top + new Vector3(0.1f, 0, -0.06f), 70);
                    ArtLibrary.Spawn("lemon", root, top + new Vector3(0.02f, 0, 0.1f), 0);
                }
                ObjectiveDressing.Sign(root, "PROMO SWAP · OPTIONAL", standPool[index] + new Vector3(0, 1.55f, 0), 0f, new Color(0.45f, 0.2f, 0.55f), Color.white, 0.5f, 0.09f);
            }
        }

        private void BuildBell()
        {
            var bell = PrimitiveWorld.Box(root, "Delayed service bell", new Vector3(-8.8f, 0.45f, 1.8f), new Vector3(0.55f, 0.9f, 0.55f), new Color(0.95f, 0.65f, 0.15f));
            Bell = bell.AddComponent<DistractionBell>();
            Bell.Configure(authority, signals);
            ObjectiveDressing.Recolor(bell, new Color(0.3f, 0.22f, 0.16f), 0.5f);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Bell"; dome.transform.SetParent(root, false);
            dome.transform.position = new Vector3(-8.8f, 0.98f, 1.8f); dome.transform.localScale = new Vector3(0.22f, 0.16f, 0.22f);
            dome.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(new Color(0.85f, 0.68f, 0.3f), 0.9f, 1f);
            UnityEngine.Object.Destroy(dome.GetComponent<Collider>());
            ObjectiveDressing.Sign(root, "SERVICE BELL · RINGS IN 3 S", new Vector3(-8.8f, 1.55f, 1.8f), 0f, new Color(0.6f, 0.4f, 0.1f), Color.white, 0.5f, 0.09f);
        }

        private void BuildModelStand()
        {
            var pose = PrimitiveWorld.Box(root, "Model display", new Vector3(-4.2f, 0.6f, 12.9f), new Vector3(0.5f, 1.2f, 0.3f), new Color(0.2f, 0.5f, 0.9f));
            pose.AddComponent<DisplayPose>().Configure(authority);
            ObjectiveDressing.Recolor(pose, Plinth, 0.8f);
            ObjectiveDressing.Sign(root, "MODEL STAND · SHIRT + EMPTY HANDS", new Vector3(-4.2f, 1.6f, 12.75f), 0f, new Color(0.15f, 0.3f, 0.55f), Color.white, 0.5f, 0.09f);
        }

        private void BuildRescue()
        {
            var rescue = PrimitiveWorld.Box(root, "Rescue console", new Vector3(-11.1f, 0.7f, 6.6f), new Vector3(0.8f, 1.2f, 0.5f), new Color(0.1f, 0.8f, 0.4f));
            rescue.AddComponent<RescueInteractable>().Configure(authority);
            Rescue = rescue.transform;
            ObjectiveDressing.Recolor(rescue, new Color(0.2f, 0.22f, 0.25f), 0.6f);
            var screen = PrimitiveWorld.Box(root, "Console screen", new Vector3(-11.1f, 1.1f, 6.34f), new Vector3(0.5f, 0.3f, 0.02f), Color.black);
            screen.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(0.2f, 0.9f, 0.5f), 1.2f);
            UnityEngine.Object.Destroy(screen.GetComponent<Collider>());
            var terminal = PrimitiveWorld.Box(root, "Surveillance terminal", new Vector3(-12, 1f, 12.4f), new Vector3(0.8f, 0.6f, 0.4f), Color.black);
            ObjectiveDressing.Dress(terminal, "classic_laptop", 0.3f, 180f, new Color(0.15f, 0.15f, 0.17f));
        }

        private void BuildExit()
        {
            var exit = PrimitiveWorld.Box(root, "Escape door", new Vector3(5, 1.3f, -14.6f), new Vector3(1.4f, 2.4f, 0.3f), new Color(0.8f, 0.2f, 0.7f));
            var front = exit.AddComponent<EscapeInteractable>();
            front.Configure(authority, "");
            front.BlockedPrompt = "Shutter down. The FIRE EXIT is upstairs — take the escalator.";
            Escape = exit.transform;
            Shutter = FrontShutter.Create(root, front, new Vector3(5f, 0f, -14.3f));
            // The fire exit door is store geometry (PrimitiveWorld.FireExit); it only needs the interaction.
            var fire = GameObject.Find("Fire exit");
            if (fire != null)
            {
                fire.AddComponent<EscapeInteractable>().Configure(authority, "");
                FireExit = fire.transform;
                ObjectiveDressing.Sign(root, "FIRE EXIT · E", PrimitiveWorld.FireExit + new Vector3(0, 1.1f, -0.35f), 0f, new Color(0.1f, 0.5f, 0.25f), Color.white, 1.4f, 0.12f);
            }
            if (!ObjectiveDressing.Dress(exit, "rollershutter_door", 2.4f, 0f, new Color(0.2f, 0.42f, 0.3f)))
                ObjectiveDressing.Recolor(exit, new Color(0.2f, 0.42f, 0.3f), 0.5f);
            ObjectiveDressing.Sign(root, "EXIT", new Vector3(5f, 2.72f, -14.3f), 0f, new Color(0.1f, 0.5f, 0.25f), Color.white, 1.4f, 0.16f);
        }

        // ---------------- gems ----------------

        /// <summary>Every hiding spot in the store. The shift picks GemPlan.PerNight of them, at least one pose-gated.</summary>
        public static readonly (Vector3 at, Stance pose)[] GemSpots =
        {
            (new Vector3(-13.6f, 0.25f, -12.6f), Stance.Neutral),    // 0 corner behind the produce tables
            (new Vector3(6.5f, 0.25f, -13.0f), Stance.Neutral),      // 1 between the escape door and checkout 1
            (new Vector3(-4.2f, 0.4f, 13.55f), Stance.Display),      // 2 on the mannequin platform, among the displays
            (new Vector3(13.5f, 0.3f, -2.7f), Stance.Lounging),      // 3 by the sofa in Home
            (new Vector3(-1.2f, 0.25f, 4.1f), Stance.Neutral),       // 4 at the end of the middle shelf
            (new Vector3(-14.2f, 0.3f, 3.4f), Stance.Neutral),       // 5 past the bakery shelf
            (new Vector3(10.0f, 0.3f, -7.4f), Stance.Neutral),       // 6 between Home and the checkouts
            (new Vector3(4.2f, 0.3f, 13.75f), Stance.Browsing),      // 7 under the TV ledge in Electronics
            (new Vector3(2.2f, PrimitiveWorld.UpstairsY + 0.25f, 13.8f), Stance.Neutral),   // 8 upstairs, under the gallery TV ledge
            (new Vector3(14.45f, PrimitiveWorld.UpstairsY + 0.25f, 8.0f), Stance.Neutral),  // 9 upstairs, by the east wall past the sofa
            (new Vector3(10.4f, PrimitiveWorld.UpstairsY + 0.3f, 9.4f), Stance.Lounging),   // 10 upstairs, on the lounge rug
            (new Vector3(-0.6f, PrimitiveWorld.UpstairsY + 0.25f, 14.6f), Stance.Neutral),  // 11 upstairs, landing corner
        };
        public static readonly int[] GatedSpots = { 2, 3, 7, 10 };

        private void BuildGems()
        {
            foreach (int index in GemPlan.Pick(shift, GemSpots.Length, GemPlan.PerNight, GatedSpots))
                Gems.Add(Gem.Create(root, index, GemSpots[index].at, GemSpots[index].pose, HeldPose, GemCollected));
        }

        // ---------------- jobs ----------------

        private void EnsureCrates()
        {
            if (cratesBuilt) return;
            cratesBuilt = true;
            var itemData = ScriptableObject.CreateInstance<ItemDefinition>(); itemData.canBreak = true;
            // Shelf units stand at x = -10, -5, 0, 5 (z -4.4..4.4); the aisles between them are at x = -7.5, -2.5, 2.5, 7.5.
            Vector3[] cratePool =
            {
                new Vector3(-2, 0.5f, -8), new Vector3(0, 0.5f, -8), new Vector3(2, 0.5f, -8), new Vector3(-7.5f, 0.5f, -3f), new Vector3(-2.5f, 0.5f, 0f),
                new Vector3(2.5f, 0.5f, 3f), new Vector3(7.5f, 0.5f, -3f), new Vector3(-11f, 0.5f, -8f), new Vector3(-13.2f, 0.5f, -2.5f), new Vector3(5.2f, 0.5f, -7f)
            };
            var used = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = shift == 0 ? cratePool[i] : Pick(cratePool, used);
                var box = PrimitiveWorld.Box(root, "Collectible crate", at, Vector3.one * 0.6f, Color.yellow);
                box.layer = 3;
                var item = box.AddComponent<PhysicalItem>(); item.Configure(itemData, signals);
                crates.Add(item);
                ObjectiveDressing.Dress(box, "cardboard_box_01", 0.6f, layout.Next(-12, 12), new Color(0.72f, 0.55f, 0.32f));
            }
        }

        private void BuildShirts()
        {
            var shirtData = ScriptableObject.CreateInstance<ItemDefinition>(); shirtData.id = "shirt";
            shirtData.displayName = "Shirt"; shirtData.missionTag = "shirt"; shirtData.inventoryOnly = true; shirtData.slotCost = 1;
            Vector3[] shirtPool = { new Vector3(-6.2f, 0.94f, 9.0f), new Vector3(-2.4f, 0.94f, 9.0f), new Vector3(-6.7f, 0.94f, 9.0f), new Vector3(-1.9f, 0.94f, 9.0f) };
            var used = new List<int>();
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = shift == 0 ? shirtPool[i] : Pick(shirtPool, used);
                var shirt = PrimitiveWorld.Box(root, "Shirt", at, new Vector3(0.4f, 0.08f, 0.32f), new Color(0.2f, 0.35f, 0.7f));
                shirt.layer = 3;
                shirt.AddComponent<PhysicalItem>().Configure(shirtData, signals);
                ObjectiveDressing.Surface(shirt, "cotton_jersey", new Vector2(1, 1), new Color(0.35f, 0.5f, 0.9f), new Color(0.2f, 0.35f, 0.7f));
                ObjectiveDressing.Sign(root, "SHIRT · E", at + new Vector3(0, 0.55f, 0), 0f, new Color(0.15f, 0.3f, 0.55f), Color.white, 0.4f, 0.07f);
            }
        }

        private void BuildWindow()
        {
            Vector3 at = new Vector3(10.3f, 0f, -13.6f);
            var platform = PrimitiveWorld.Box(root, "Window platform", at + new Vector3(0, 0.06f, 0), new Vector3(2.8f, 0.12f, 1.4f), Plinth);
            ObjectiveDressing.Recolor(platform, Plinth, 0.8f);
            var pane = PrimitiveWorld.Box(root, "Shop window", new Vector3(10.3f, 1.4f, -14.72f), new Vector3(3.0f, 2.3f, 0.04f), Color.white);
            pane.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Glass(new Color(0.6f, 0.8f, 1f, 0.28f));
            UnityEngine.Object.Destroy(pane.GetComponent<Collider>());
            var zone = new GameObject("Window display");
            zone.transform.SetParent(root, false);
            zone.transform.position = at + new Vector3(0, 1.1f, 0);
            var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(2.6f, 2.2f, 1.3f);
            window = zone.AddComponent<HoldSpot>();
            window.Configure(authority, signals, ActionKind.Place, "window", "window");
            window.Label = "In the window"; window.Seconds = 20f; window.KeepOnCancel = 0.5f; window.AutoStart = true;
            window.StartHint = "Step onto the window platform and hold still"; window.DoneHint = "Window display done";
            window.MovedHint = "You moved — half the time is lost";
            ObjectiveDressing.Sign(root, "WINDOW DISPLAY · HOLD STILL 20 S", at + new Vector3(0, 2.75f, 0.4f), 0f, new Color(0.15f, 0.3f, 0.55f), Color.white, 0.6f, 0.1f);
        }

        private void BuildSwap()
        {
            swapIndex = shift == 0 ? 1 : layout.Next(4);
            var host = GameObject.Find("Display mannequin " + (swapIndex + 1));
            Vector3 feet;
            if (host == null)
            {
                // No art pack: a plinth stands in for the display mannequin.
                feet = new Vector3(-4.2f + (-1.2f + 0.8f * swapIndex), 0.15f, 13.9f);
                host = PrimitiveWorld.Box(root, "Display mannequin stand-in", feet + new Vector3(0, 0.9f, 0), new Vector3(0.5f, 1.8f, 0.3f), Plinth);
            }
            else feet = host.transform.position;
            swap = host.AddComponent<HoldSpot>();
            swap.Configure(authority, signals, ActionKind.Place, "display", "plinth");
            swap.Label = "Taking its place"; swap.Seconds = 6f; swap.KeepOnCancel = 0f;
            swap.CanStart = Unwatched;
            swap.StartHint = "E — swap places with this mannequin (nobody looking)";
            swap.BlockedHint = "Someone is looking. Wait, then E.";
            swap.DoneHint = "You are the display now";
            var target = host;
            swap.Completed += _ =>
            {
                var visual = target.GetComponentInChildren<CharacterVisual>();
                if (visual != null) visual.gameObject.SetActive(false);
                var stand = target.GetComponent<Renderer>(); if (stand != null) stand.enabled = false;
                if (swapMarker != null) swapMarker.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(0.3f, 0.9f, 0.5f), 1.5f);
            };
            var marker = PrimitiveWorld.Box(root, "Swap marker", feet + new Vector3(0, 0.17f, 0), new Vector3(0.7f, 0.03f, 0.7f), Color.yellow);
            marker.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.78f, 0.2f), 1.8f);
            UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
            swapMarker = marker.transform;
        }

        private void BuildPriceTags()
        {
            Vector3[] pool = { new Vector3(-5f, 1.25f, -4.5f), new Vector3(5f, 1.25f, 4.5f), new Vector3(0f, 1.25f, -4.5f), new Vector3(-10f, 1.25f, 4.5f) };
            var used = new List<int>();
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = shift == 0 ? pool[i] : Pick(pool, used);
                var tag = PrimitiveWorld.Box(root, "Price tag", at, new Vector3(0.36f, 0.24f, 0.05f), new Color(0.98f, 0.9f, 0.3f));
                tag.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(0.98f, 0.9f, 0.3f), 0.5f);
                var spot = tag.AddComponent<HoldSpot>();
                spot.Configure(authority, signals, ActionKind.Steal, "pricetag");
                spot.Label = "Swapping the tag"; spot.Seconds = 3f; spot.KeepOnCancel = 0f;
                spot.StartHint = "E — swap this price tag (3 s still)"; spot.DoneHint = "Tag swapped";
                var face = tag;
                spot.Completed += _ => face.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(0.3f, 0.9f, 0.5f), 0.8f);
                tags.Add(spot);
                ObjectiveDressing.Sign(root, "PRICE TAG · E", at + new Vector3(0, 0.45f, 0), 0f, DarkPanel, new Color(1f, 0.85f, 0.35f), 0.4f, 0.07f);
            }
        }

        private void BuildCamera()
        {
            Vector3 foot = new Vector3(6.4f, 0f, -8.0f);
            var pole = PrimitiveWorld.Box(root, "Camera pole", foot + new Vector3(0, 1.3f, 0), new Vector3(0.12f, 2.6f, 0.12f), new Color(0.2f, 0.2f, 0.22f));
            ObjectiveDressing.Recolor(pole, new Color(0.2f, 0.2f, 0.22f), 0.6f);
            cameraHead = ObjectiveDressing.FitProp("security_camera_01", root, foot + new Vector3(0, 2.6f, 0), 135f, 0.32f)?.transform;
            if (cameraHead == null)
            {
                var head = PrimitiveWorld.Box(root, "Camera head", foot + new Vector3(0, 2.72f, 0), new Vector3(0.18f, 0.16f, 0.36f), new Color(0.1f, 0.1f, 0.12f));
                head.transform.rotation = Quaternion.Euler(0, 135f, 0);
                UnityEngine.Object.Destroy(head.GetComponent<Collider>());
                cameraHead = head.transform;
            }
            var lamp = PrimitiveWorld.Box(root, "Camera lamp", foot + new Vector3(0, 2.5f, 0), new Vector3(0.08f, 0.08f, 0.08f), Color.red);
            var lampRenderer = lamp.GetComponent<Renderer>(); lampRenderer.sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.15f, 0.1f), 2f);
            UnityEngine.Object.Destroy(lamp.GetComponent<Collider>());
            cameraPole = pole.AddComponent<HoldSpot>();
            cameraPole.Configure(authority, signals, ActionKind.Collect, "camera-turned");
            cameraPole.Label = "Turning the camera"; cameraPole.Seconds = 2f; cameraPole.KeepOnCancel = 0f;
            cameraPole.StartHint = "E — turn the camera away from the lane (2 s)"; cameraPole.DoneHint = "Camera turned · now cross the checkout lane";
            cameraPole.Completed += _ =>
            {
                cameraTurned = true;
                if (cameraHead != null) cameraHead.rotation = Quaternion.Euler(0, 135f + 120f, 0);
                lampRenderer.sharedMaterial = ArtLibrary.Emissive(new Color(0.3f, 0.9f, 0.5f), 1.5f);
                Toast("Camera turned. Cross the checkout lane now.");
            };
            var zone = new GameObject("Checkout lane");
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(9.5f, 1f, -10.5f);
            var box = zone.AddComponent<BoxCollider>(); box.size = new Vector3(2.2f, 2f, 2.6f);
            lane = zone.AddComponent<CrossZone>();
            lane.Configure(authority, signals, ActionKind.Move, "camera", "checkout");
            lane.Gate = () => cameraTurned;
            lane.BlockedToast = "The camera sees the lane. Turn it first.";
            lane.Say = s => Toast(s);
            ObjectiveDressing.Sign(root, "CAMERA · E TO TURN", foot + new Vector3(0, 1.75f, -0.1f), 0f, DarkPanel, new Color(1f, 0.85f, 0.35f), 0.4f, 0.07f);
            var stripe = PrimitiveWorld.Box(root, "Lane stripe", new Vector3(9.5f, 0.012f, -10.5f), new Vector3(2.2f, 0.02f, 0.12f), new Color(1f, 0.85f, 0.2f));
            UnityEngine.Object.Destroy(stripe.GetComponent<Collider>());
        }

        private void BuildLostToy()
        {
            var toyData = ScriptableObject.CreateInstance<ItemDefinition>(); toyData.id = "toy";
            toyData.displayName = "Lost toy"; toyData.missionTag = "toy"; toyData.inventoryOnly = true; toyData.slotCost = 1;
            Vector3[] pool = { new Vector3(11.2f, 0.82f, -5.2f), new Vector3(12.6f, 0.5f, 2.1f), new Vector3(13.5f, 0.3f, -3.4f) };
            Vector3 at = shift == 0 ? pool[0] : pool[layout.Next(pool.Length)];
            var box = PrimitiveWorld.Box(root, "Lost toy", at, new Vector3(0.22f, 0.12f, 0.16f), new Color(0.9f, 0.3f, 0.3f));
            box.layer = 3;
            toy = box.AddComponent<PhysicalItem>(); toy.Configure(toyData, signals);
            ObjectiveDressing.Dress(box, "gamepad", 0.12f, layout.Next(0, 360), new Color(0.9f, 0.3f, 0.3f));
            ObjectiveDressing.Sign(root, "LOST TOY · E", at + new Vector3(0, 0.5f, 0), 0f, new Color(0.55f, 0.15f, 0.15f), Color.white, 0.4f, 0.07f);
            var desk = PrimitiveWorld.Box(root, "Lost and found", new Vector3(-6.0f, 1.2f, -13.3f), new Vector3(0.6f, 0.3f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
            ObjectiveDressing.Dress(desk, "wicker_basket_02", 0.3f, 0f, new Color(0.3f, 0.3f, 0.32f));
            lostFound = desk.AddComponent<ReturnPoint>();
            lostFound.Configure(authority, signals, ActionKind.Place, "toy", "lostfound");
            lostFound.ItemId = "toy"; lostFound.ItemName = "the lost toy"; lostFound.Where = "Lost & Found";
            ObjectiveDressing.Sign(root, "LOST & FOUND", new Vector3(-6.0f, 2.0f, -13.3f), 0f, DarkPanel, new Color(1f, 0.85f, 0.35f), 0.5f, 0.1f);
        }

        private void BuildVases()
        {
            var vaseData = ScriptableObject.CreateInstance<ItemDefinition>(); vaseData.id = "vase";
            vaseData.displayName = "Vase"; vaseData.missionTag = "vase"; vaseData.canBreak = true; vaseData.breakSpeed = 3.2f; vaseData.mass = 1.2f;
            Vector3[] pool = { new Vector3(-12.3f, 0.95f, -10f), new Vector3(-12.3f, 0.95f, -6.2f), new Vector3(-8.6f, 0.15f, -4.2f) };
            for (int i = 0; i < 2; i++)
            {
                var box = PrimitiveWorld.Box(root, "Vase", pool[i], new Vector3(0.3f, 0.3f, 0.3f), new Color(0.8f, 0.8f, 0.85f));
                box.layer = 3;
                var item = box.AddComponent<PhysicalItem>(); item.Configure(vaseData, signals);
                box.AddComponent<Rattle>().Configure(signals);
                vases.Add(item);
                ObjectiveDressing.Dress(box, i == 0 ? "ceramic_vase_01" : "ceramic_vase_02", 0.4f, 0f, new Color(0.8f, 0.8f, 0.85f));
            }
            ObjectiveDressing.Sign(root, "VASES · CARRY GENTLY", pool[0] + new Vector3(0, 0.7f, 1.9f), 0f, DarkPanel, new Color(1f, 0.85f, 0.35f), 0.4f, 0.07f);
            var rug = PrimitiveWorld.Box(root, "Home drop rug", new Vector3(10.5f, 0.15f, -1.5f), new Vector3(2.2f, 0.3f, 2.2f), new Color(0.5f, 0.3f, 0.2f));
            rug.AddComponent<PlacementZone>().destinationId = "home";
            ObjectiveDressing.Surface(rug, "ribbed_corduroy", new Vector2(2, 2), new Color(0.75f, 0.5f, 0.35f), new Color(0.45f, 0.28f, 0.18f));
            HomeRug = rug.transform;
            ObjectiveDressing.Sign(root, "HOME RUG · G TO SET DOWN", new Vector3(10.5f, 1.6f, -1.5f), 0f, new Color(0.45f, 0.28f, 0.12f), Color.white, 0.5f, 0.09f);
        }

        // ---------------- guidance ----------------

        /// <summary>Where the gold marker points for an unfinished job, or false when nothing useful can be shown.</summary>
        public bool Guide(MissionTracker job, PlayerMotor motor, out Vector3 at, out string label)
        {
            at = Vector3.zero; label = "";
            var carry = motor.GetComponent<CarrySystem>();
            var held = carry != null ? carry.Held : null;
            var inventory = motor.GetComponent<PlayerInventory>();
            switch (job.Rule.Id)
            {
                case "collect-crates":
                    if (held != null) return false;
                    return Nearest(crates, motor, c => !job.HasCounted(c.Id), out at, "CRATE", ref label);
                case "place-crate":
                    if (held != null && held.Definition.missionTag == "object") { at = ClothingRug.position; label = "GREEN RUG"; return true; }
                    if (held != null) return false;
                    return Nearest(crates, motor, _ => true, out at, "CRATE", ref label);
                case "steal-shirt":
                    if (held != null) return false;
                    return NearestItem(motor, "shirt", out at, "SHIRT", ref label);
                case "window-display":
                    at = window.transform.position; label = "WINDOW"; return true;
                case "swap-places":
                    if (swapMarker == null) return false;
                    at = swapMarker.position; label = "PLINTH"; return true;
                case "price-tags":
                    return Nearest(tags, motor, t => !t.Complete, out at, "PRICE TAG", ref label);
                case "camera-blind":
                    if (cameraTurned) { at = lane.transform.position; label = "CROSS HERE"; return true; }
                    at = cameraPole.transform.position; label = "CAMERA"; return true;
                case "lost-toy":
                    if (inventory != null && inventory.Items.Count("toy") > 0) { at = lostFound.transform.position; label = "LOST & FOUND"; return true; }
                    if (toy != null && toy.gameObject.activeInHierarchy) { at = toy.transform.position; label = "TOY"; return true; }
                    return false;
                case "fragile-vase":
                    if (held != null && held.Definition.missionTag == "vase") { at = HomeRug.position; label = "HOME RUG"; return true; }
                    if (held != null) return false;
                    return Nearest(vases, motor, v => !v.Broken && v.Holder == null && v.gameObject.activeInHierarchy, out at, "VASE", ref label);
            }
            return false;
        }

        /// <summary>What the bottom prompt says while carrying something for a job.</summary>
        public string CarryPrompt(PhysicalItem held, IReadOnlyList<MissionTracker> missions)
        {
            if (held == null) return "";
            foreach (var job in missions)
            {
                if (job.Complete) continue;
                if (job.Rule.Id == "place-crate" && held.Definition.missionTag == "object") return "Follow the gold marker. G sets the crate down on the green rug.";
                if (job.Rule.Id == "fragile-vase" && held.Definition.missionTag == "vase") return "Carry it gently to the Home rug. It rattles, and a hard drop breaks it. G sets it down.";
            }
            return "Press G to put it down.";
        }

        private static bool Nearest<T>(List<T> items, PlayerMotor motor, Func<T, bool> ok, out Vector3 at, string tag, ref string label) where T : Component
        {
            at = Vector3.zero;
            float best = float.MaxValue; bool found = false;
            foreach (var item in items)
            {
                if (item == null || !item.gameObject.activeInHierarchy || !ok(item)) continue;
                if (item is PhysicalItem physical && (physical.Holder != null || physical.Broken)) continue;
                float d = (item.transform.position - motor.transform.position).sqrMagnitude;
                if (d < best) { best = d; at = item.transform.position; found = true; }
            }
            if (found) label = tag;
            return found;
        }

        private static bool NearestItem(PlayerMotor motor, string missionTag, out Vector3 at, string tag, ref string label)
        {
            at = Vector3.zero;
            float best = float.MaxValue; bool found = false;
            foreach (var item in PhysicalItem.Active)
            {
                if (item.Definition == null || item.Definition.missionTag != missionTag || item.Holder != null || item.Broken || !item.gameObject.activeInHierarchy) continue;
                float d = (item.transform.position - motor.transform.position).sqrMagnitude;
                if (d < best) { best = d; at = item.transform.position; found = true; }
            }
            if (found) label = tag;
            return found;
        }
    }
}
