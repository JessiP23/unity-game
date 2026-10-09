using System;
using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Scene composition and simulation scheduling. Match rules stay in <see cref="LocalMatchAuthority"/>;
    /// this class wires the seeded layout, the night schedule, scoring, poses, the capture flow,
    /// the tutorial and the HUD model together.
    /// </summary>
    public sealed class PrototypeRoot : MonoBehaviour
    {
        public GameRulesAsset rules;
        /// <summary>Shift to play on the next scene load. Set by the end card; null = tonight's shift.</summary>
        public static int? NextShift;
        /// <summary>Opening the menu when the window loses focus. Off under the test runner.</summary>
        public static bool PauseOnFocusLoss = true;
        public PlayerMotor Player => active >= 0 && active < pawns.Count ? pawns[active].Motor : null;
        private PlayerMotor[] motors = Array.Empty<PlayerMotor>();
        private DetectionSystem[] vision = Array.Empty<DetectionSystem>();
        private bool[] observations = Array.Empty<bool>();
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly List<DoorInteractable> doors = new List<DoorInteractable>();
        private readonly WorldSignals signals = new WorldSignals();
        private readonly GameLog log = new GameLog(Application.isEditor);
        private LocalMatchAuthority authority;
        private MissionSystem missions;
        private GuardController guardController;
        private PlayerInputReader guardInput;
        private PlayerView guardView;
        private CharacterVisual guardVisual;
        private PrototypeHud hud;
        private StoreAudio storeAudio;
        private GuideBeacon guideBeacon;
        private JobBuilder jobs;
        private Transform escapeGuide, rescueGuide;
        private List<BonusDisplay> bonusDisplays = new List<BonusDisplay>();
        private readonly HashSet<BonusDisplay> scoredBonuses = new HashSet<BonusDisplay>();
        private readonly HashSet<MissionTracker> scoredMissions = new HashSet<MissionTracker>();
        private DistractionBell distractionBell;
        private float nextHudRefresh;
        private int hudPawn = -1;
        private readonly List<PlayerSighting> surveillancePlayers = new List<PlayerSighting>();
        private readonly List<DoorSighting> surveillanceDoors = new List<DoorSighting>();
        private readonly List<ObjectiveSighting> surveillanceObjectives = new List<ObjectiveSighting>();
        private KeyCommandMap controls, testing;
        private bool testingKeys;
        private StoreDirectory directory;
        private CustomerPopulationManager population;
        private NpcDebugOverlay npcDebug;
        private INetworkService network = new LocalNetworkService();
        private FusionSession fusion;
        public CustomerPopulationManager Population => population;
        public StoreDirectory Directory => directory;
        public LocalMatchAuthority Authority => authority;
        public GuardController Guard => guardController;
        public NightScore Score => score;
        public int Shift => shift;
        public int MannequinCount => pawns.Count;
        public bool Solo => pawns.Count == 1;
        private bool restarting;
        public PlayerMotor MannequinAt(int index) => pawns[index].Motor;
        public PlayerInputReader InputAt(int index) => pawns[index].Input;
        public MissionSystem Missions => missions;
        private int active;
        private bool help;
        private MenuPage menuPage = MenuPage.Night;
        private float timeScaleBeforeMenu = 1f;
        private bool clockPausedBeforeMenu;
        private string reportToast, toastText;
        private float reportToastUntil, toastUntil;
        private readonly List<SuspiciousActivityEvent> reportLog = new List<SuspiciousActivityEvent>();
        private readonly HudModel hudModel = new HudModel();
        private bool logAudio = true;
        private bool missionsAnnounced;
        private BackroomPocket backroom;
        private IDisposable audioSubscription, actionSubscription, noiseSubscription, reportSubscription;

        // Night
        private int shift;
        private System.Random layout;
        private readonly NightScore score = new NightScore();
        private NightPhase phase = NightPhase.Open;
        private bool phaseApplied;
        private int bestBefore;
        private bool ended, newBest;
        private string banner, bannerDetail;
        private float bannerUntil;
        private bool bannerDanger;
        private float danger;
        private readonly Dictionary<PlayerMotor, float> coverUntil = new Dictionary<PlayerMotor, float>();
        private static readonly Vector3[] ReleaseSpots =
        {
            new Vector3(-11f, 0.1f, -10f), new Vector3(12.5f, 0.1f, -2.5f), new Vector3(-13.5f, 0.1f, 0.5f),
            new Vector3(1f, 0.1f, -10.5f), new Vector3(-4.8f, 0.1f, 11.4f), new Vector3(7f, 0.1f, 12.2f)
        };

        // Guard on duty, gadgets, shopper reactions, camera
        private GuardProfile guardProfile;
        private GadgetKit gadgets;
        private ShopperReactions reactions;
        private LeaderboardStore board;
        private bool editingName, testRun;
        public const string WardrobeKey = "ns-wardrobe";
        private WardrobeState wardrobe = new WardrobeState();
        private Outfit menuOutfit;
        private int gemsTonight;
        private readonly HashSet<ZoneType> outfitNoted = new HashSet<ZoneType>();
        public IReadOnlyList<Gem> Gems => jobs != null ? jobs.Gems : System.Array.Empty<Gem>();
        public WardrobeState Closet => wardrobe;
        private int careerBefore;
        private readonly Dictionary<PlayerMotor, float> stillSince = new Dictionary<PlayerMotor, float>();
        private bool autoPoseCam = true;

        // Tutorial
        private bool tutorial;
        private readonly HashSet<string> tipsShown = new HashSet<string>();
        private string tipText;
        private float tipUntil, tipCooldown, zoneDwell;

        private void Start()
        {
            Application.targetFrameRate = 60;
            PrimitiveWorld.Build(transform);
            directory = StoreLayout.Build(transform);
            BuildKeyMaps();
            fusion = gameObject.AddComponent<FusionSession>();
            network = fusion;
            if (!Application.isBatchMode && rules.mannequinPlayers > 1) fusion.ConnectHost();
            // Shift 0 is the classic layout every test knows. Players get today's shift.
            bool testRun = Application.isBatchMode || GameObject.Find("Code-based tests runner") != null;
            this.testRun = testRun;
            shift = NextShift ?? (testRun ? 0 : DailyShift());
            NextShift = null;
            if (testRun) PauseOnFocusLoss = false;
            // The guard on duty scales the asset's guard numbers; a private copy keeps the asset untouched.
            guardProfile = GuardRoster.For(shift);
            rules = Instantiate(rules);
            rules.visionDistance *= (float)guardProfile.Vision;
            rules.fieldOfView = Mathf.Clamp(rules.fieldOfView * (float)guardProfile.FieldOfView, 20f, 170f);
            rules.guardSpeed *= (float)guardProfile.Pace;
            rules.guardChaseSpeed *= (float)guardProfile.Pace;
            rules.hearingRadius *= (float)guardProfile.Hearing;
            rules.flashlightCone = Mathf.Clamp(rules.flashlightCone * (float)guardProfile.Torch, 10f, 150f);
            careerBefore = PlayerPrefs.GetInt("ns-career", 0);
            var session = new LocalSession();
            authority = new LocalMatchAuthority(rules.CreateRules(), session);
            authority.TryBeginNight();
            authority.Flow.Changed += OnPhaseChanged;
            layout = new System.Random(shift * 7919 + 17);
            bestBefore = PlayerPrefs.GetInt("ns-best-" + shift, 0);
            tutorial = PlayerPrefs.GetInt("ns-nights", 0) < 2 && !testRun;
            backroom = gameObject.AddComponent<BackroomPocket>();
            backroom.Configure(authority, rules.mannequinPlayers > 1);
            backroom.Cleared += OnBackroomCleared;
            authority.Lighting.Changed += LightingPresenter.Apply;
            LightingPresenter.Apply(authority.Lighting.Mode);
            audioSubscription = authority.Audio.Subscribe(cue => { if (logAudio) log.Write("AUDIO", cue.ToString()); });
            reportSubscription = authority.Reports.Subscribe(OnReport);
            for (int i = 0; i < rules.mannequinPlayers; i++)
                pawns.Add(CreatePawn(session, new Vector3(-5.4f + (i % 4) * 0.9f, 0.1f, 12.4f - (i / 4) * 0.8f)));
            wardrobe = testRun ? new WardrobeState() : WardrobeState.Parse(PlayerPrefs.GetString(WardrobeKey, ""));
            menuOutfit = wardrobe.Worn;
            foreach (var pawn in pawns) pawn.Visual?.SetOutfit(wardrobe.Worn);
            jobs = new JobBuilder(transform, authority, signals, rules, shift, layout, Solo)
            {
                Unwatched = Unwatched,
                Toast = text => Toast(text, 3f),
                HeldPose = m => { var p = PawnOf(m); return p != null && p.Mannequin.Holding ? p.Mannequin.Strain.Current : (Stance?)null; },
                GemCollected = OnGem
            };
            jobs.Build();
            escapeGuide = jobs.Escape; rescueGuide = jobs.Rescue; distractionBell = jobs.Bell; bonusDisplays = jobs.Bonuses;
            doors.Add(jobs.Door);
            missions = new MissionSystem(jobs.Definitions, signals, InventoryFor);
            authority.MissionsComplete = () => missions != null && missions.Complete;
            foreach (var tracker in missions.Missions)
            {
                var job = tracker;
                job.Changed += () => OnMissionChanged(job);
            }
            actionSubscription = signals.Actions.Subscribe(action =>
            {
                if (action.Kind == ActionKind.Unlock) authority.Audio.Publish(AudioCue.Door);
                if (action.Kind == ActionKind.Break) authority.Audio.Publish(AudioCue.Break);
            });
            noiseSubscription = signals.Noise.Subscribe(noise => authority.Surveillance.ReportNoise(
                new NoiseSighting(new MapPoint(noise.Position.x, noise.Position.y, noise.Position.z), noise.Loudness, noise.Source, noise.Timestamp)));
            var surface = gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            int snapped = jobs.SnapLooseItemsToFloor();
            if (snapped > 0) Debug.LogWarning("[JOBS] " + snapped + " item(s) spawned inside furniture on shift " + shift + " and were moved to open floor. Check the position pools in JobBuilder.");
            BuildGuard();
            foreach (var pawn in pawns)
            {
                pawn.Detection = new DetectionCoordinator(pawn.Motor, guardController.transform, rules) { Flashlight = guardController.Flashlight };
                pawn.Pose.Configure(pawn.Motor, pawn.Detection.Detection);
                pawn.Mannequin.Configure(pawn.Motor, directory);
            }
            StoreDressing.Apply(transform, authority.Lighting.Mode);
            jobs.AfterDressing();
            population = gameObject.AddComponent<CustomerPopulationManager>();
            population.Configure(directory, NpcPopulationSettings.Load(), authority.TryReport, () => Time.timeAsDouble, network.IsAuthority, shift == 0 ? Environment.TickCount : shift);
            population.Populate();
            var debugHost = new GameObject("NPC debug");
            debugHost.transform.SetParent(transform, false);
            npcDebug = debugHost.AddComponent<NpcDebugOverlay>();
            npcDebug.Configure(population);
            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Configure();
            hud.PagePicked = page => menuPage = page;
            storeAudio = StoreAudio.Create(transform);
            storeAudio.Bind(pawns[0].Motor, signals);
            storeAudio.BindGuard(guardController);
            storeAudio.Watch(missions);
            storeAudio.Listen(authority.Audio);
            if (StoreLighting.Active != null) StoreLighting.Active.BankOff = _ => storeAudio.LightsClunk();
            guideBeacon = GuideBeacon.Create(transform);
            reactions = gameObject.AddComponent<ShopperReactions>();
            reactions.Configure(population, () => motors, StillFor, m => PawnOf(m)?.Mannequin.Holding ?? false, OnAdmired, storeAudio, shift * 13 + 5);
            gadgets = gameObject.AddComponent<GadgetKit>();
            gadgets.Configure(transform, signals, authority, careerBefore, text => Toast(text, 3.5f));
            board = gameObject.AddComponent<LeaderboardStore>();
            board.Refresh(shift);
            SetActive(0);
            Cursor.lockState = CursorLockMode.Locked;
            if (tutorial) Tip("start", "Move with WASD. When the guard or a shopper looks your way, STOP.", 7f);
            else
            {
                var top = board.Local(shift).Entries;
                string target = top.Count > 0 && !string.Equals(top[0].Name, board.PlayerName, StringComparison.OrdinalIgnoreCase)
                    ? "  ·  " + top[0].Name + " holds " + top[0].Points.ToString("N0")
                    : bestBefore > 0 ? "  ·  best " + bestBefore.ToString("N0") : "";
                Toast("Shift #" + shift + "  ·  Guard: " + guardProfile.Name + ", " + guardProfile.Trait + target, 5f);
            }
        }

        /// <summary>Days since the game's epoch: everyone who plays today gets the same store.</summary>
        private static int DailyShift() => (int)(DateTime.UtcNow.Date - new DateTime(2026, 1, 1)).TotalDays + 1;

        private Vector3 Pick(Vector3[] pool, List<int> used)
        {
            int index;
            do index = layout.Next(pool.Length); while (used.Contains(index) && used.Count < pool.Length);
            used.Add(index);
            return pool[index];
        }

        private void BuildGuard()
        {
            Vector3[][] routes =
            {
                new[] { new Vector3(3, 0, -8), new Vector3(10, 0, 0), new Vector3(3, 0, 8), new Vector3(-10, 0, 0) },
                new[] { new Vector3(-10, 0, -8), new Vector3(3, 0, -8), new Vector3(6, 0, 3), new Vector3(3, 0, 8), new Vector3(-6, 0, 3) },
                new[] { new Vector3(10, 0, -5), new Vector3(3, 0, 8), new Vector3(-10, 0, 0), new Vector3(-3, 0, -3) },
                // One guard takes the escalator: from upstairs the whole floor is in view, and so is he.
                new[] { new Vector3(3, 0, -8), new Vector3(10, 0, 0), new Vector3(9, PrimitiveWorld.UpstairsY, 10), new Vector3(3, PrimitiveWorld.UpstairsY, 12), new Vector3(-6, 0, 3) }
            };
            var route = routes[shift == 0 ? 0 : layout.Next(routes.Length)];
            var guard = GameObject.CreatePrimitive(PrimitiveType.Capsule); guard.name = "Guard";
            guard.transform.SetParent(transform); guard.transform.position = new Vector3(3, 1, -5);
            guard.layer = 2; guard.transform.rotation = Quaternion.Euler(0, 180, 0);
            var flashlight = guard.AddComponent<GuardFlashlight>();
            flashlight.Configure(rules.flashlightRange, rules.flashlightCone);
            guardController = guard.AddComponent<GuardController>();
            guardController.Flashlight = flashlight;
            guardController.Configure(rules, signals, route);
            guardController.InvestigateReports(authority.Reports);
            guardInput = guard.AddComponent<PlayerInputReader>(); guardInput.Active = false;
            guardView = guard.AddComponent<PlayerView>(); guardView.ConfigureStandalone(rules.lookSensitivity, 0.6f);
            guardView.Active = false; guardView.View.gameObject.SetActive(false);
            guardVisual = CharacterVisual.Attach(guard.transform, CharacterProfile.Guard);
            if (guardVisual != null) guard.GetComponent<MeshRenderer>().enabled = false;
        }

        private Pawn CreatePawn(LocalSession session, Vector3 position)
        {
            var actor = new GameObject("Mannequin"); actor.transform.SetParent(transform);
            actor.layer = 2; actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
            var record = new PlayerRecord(session.Join());
            var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, record);
            actor.AddComponent<CarrySystem>().Configure(motor);
            var inventory = actor.AddComponent<PlayerInventory>(); inventory.Configure(rules.inventoryCapacity);
            var input = actor.AddComponent<PlayerInputReader>();
            var view = actor.AddComponent<PlayerView>(); view.Configure(motor);
            var probe = actor.AddComponent<InteractionProbe>(); probe.Configure(motor, view);
            var pose = actor.AddComponent<PoseDriver>();
            actor.AddComponent<PerceptionTarget>();
            actor.AddComponent<DisplayCamouflage>();
            var mannequin = actor.AddComponent<MannequinPose>();
            var bodyObstacle = actor.AddComponent<NavMeshObstacle>();
            bodyObstacle.shape = NavMeshObstacleShape.Capsule; bodyObstacle.radius = 0.3f; bodyObstacle.height = 1.8f;
            bodyObstacle.center = new Vector3(0, 0.9f, 0); bodyObstacle.carving = false;
            authority.Register(record);
            int index = pawns.Count;
            var visual = CharacterVisual.Attach(actor.transform, CharacterProfile.MannequinFor(index));
            var pawn = new Pawn { Motor = motor, Input = input, View = view, Probe = probe, Inventory = inventory, Pose = pose, Visual = visual, Mannequin = mannequin };
            bool warehoused = false;
            record.Changed += state =>
            {
                if (state == PlayerState.Captured || state == PlayerState.Surveillance) warehoused = true;
                if (state == PlayerState.Captured && !record.InBackroom) OnCaptured(pawn, index);
                else if (state == PlayerState.Normal && warehoused)
                {
                    warehoused = false;
                    if (pawn.Detection != null) pawn.Detection.Detection.Reset();
                    Release(pawn);
                }
            };
            return pawn;
        }

        private PlayerInventory InventoryFor(string id)
        {
            for (int i = 0; i < pawns.Count; i++) if (pawns[i].Motor.Record.Id == id) return pawns[i].Inventory;
            return null;
        }

        // ---------------- capture flow ----------------

        private void OnCaptured(Pawn pawn, int index)
        {
            int moves = pawn.Detection != null ? pawn.Detection.Detection.Suspicion.Value : rules.discoveryThreshold;
            int penalty = score.Add(ScoreEvent.Capture);
            score.BreakStreak();
            // The hall is entered at once (rules and tests see the state change now); the player sees a card first.
            backroom.Admit(pawn.Motor, authority.Stats.Captures * 31 + shift * 7 + index * 17, authority.Stats.Captures);
            StartCoroutine(CaughtBeat(pawn, index, moves, penalty));
        }

        private IEnumerator CaughtBeat(Pawn pawn, int index, int moves, int penalty)
        {
            pawn.Input.Active = false;
            ShowBanner("CAUGHT", "The guard saw you move " + moves + (moves == 1 ? " time" : " times") + " while he was watching.   " + penalty.ToString("+#;-#;0") + " pts", 2.0f, true);
            yield return new WaitForSeconds(2.0f);
            ShowBanner("THE BACK HALL", "Clear 3 rooms to get back on the floor. Read the sign on each gate. The clock keeps running.", 2.6f, false);
            yield return new WaitForSeconds(1.0f);
            pawn.Input.Active = index == active;
        }

        private void OnBackroomCleared(PlayerMotor motor, float seconds, int attempts)
        {
            if (seconds <= 45f && attempts == 0)
            {
                int points = score.Add(ScoreEvent.Comeback);
                Toast("Comeback — clean hall in " + Mathf.RoundToInt(seconds) + " s   +" + points, 3.5f);
            }
            else Toast("Back on the floor after " + Mathf.RoundToInt(seconds) + " s", 3f);
        }

        /// <summary>The walkable release spot farthest from the guard; spots that are not on the NavMesh are skipped.</summary>
        private Vector3 SafestRelease()
        {
            Vector3 guard = guardController != null ? guardController.transform.position : Vector3.zero;
            Vector3 best = ReleaseSpots[0];
            float far = -1f;
            foreach (var spot in ReleaseSpots)
            {
                if (!NavMesh.SamplePosition(spot, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                float d = (hit.position - guard).sqrMagnitude;
                if (d > far) { far = d; best = hit.position + Vector3.up * 0.1f; }
            }
            return best;
        }

        private void Release(Pawn pawn)
        {
            pawn.Motor.Teleport(SafestRelease());
            coverUntil[pawn.Motor] = Time.time + 3f;
            if (pawns[active] == pawn) ShowBanner("BACK ON THE FLOOR", "3 seconds of cover. Get your bearings, then move.", 2.2f, false);
        }

        private Pawn PawnOf(PlayerMotor motor)
        {
            foreach (var pawn in pawns) if (pawn.Motor == motor) return pawn;
            return null;
        }

        /// <summary>Seconds this mannequin has been standing still; shoppers only react to a body that holds.</summary>
        private float StillFor(PlayerMotor motor)
        {
            var pawn = PawnOf(motor);
            return pawn == null ? 0f : Mathf.Max(0f, Time.time - pawn.LastMoved);
        }

        private void OnAdmired(PlayerMotor motor, string what)
        {
            int pts = score.Add(ScoreEvent.Admired, what);
            if (pawns[active].Motor == motor) Toast(what + "   +" + pts, 2.5f);
        }

        private void CycleCamera()
        {
            if (active >= pawns.Count) return;
            var view = pawns[active].View;
            view.Mode = view.Mode switch { ViewMode.FirstPerson => ViewMode.Shoulder, ViewMode.Shoulder => ViewMode.Front, _ => ViewMode.FirstPerson };
            Toast(view.Mode switch { ViewMode.Shoulder => "Camera: over the shoulder", ViewMode.Front => "Camera: front — you see your own pose", _ => "Camera: first person" }, 2.2f);
        }

        /// <summary>True when no guard and no shopper can currently see this mannequin.</summary>
        private bool Unwatched(PlayerMotor motor)
        {
            foreach (var pawn in pawns)
            {
                if (pawn.Motor != motor) continue;
                if (pawn.Detection != null && pawn.Detection.Observed) return false;
                var target = motor.GetComponent<PerceptionTarget>();
                return population == null || target == null || population.WatchingCount(target) == 0;
            }
            return true;
        }

        private bool Covered(PlayerMotor motor) => coverUntil.TryGetValue(motor, out float until) && Time.time < until;

        // ---------------- night schedule ----------------

        private void ApplySchedule()
        {
            var moment = NightSchedule.At(authority.Clock.Remaining, rules.matchDuration);
            if (phaseApplied && moment.Phase == phase) return;
            bool first = !phaseApplied;
            phase = moment.Phase; phaseApplied = true;
            authority.Lighting.Set(moment.Lighting);
            authority.Surveillance.ReportLighting(moment.Lighting);
            guardController.Pace = (float)moment.GuardPace;
            if (population != null) population.CrowdScale = (float)moment.Crowd;
            if (guardController.Flashlight != null && !guardController.PlayerDriven) guardController.Flashlight.SetEnabled(moment.Flashlight);
            if (moment.Phase == NightPhase.Lockdown || moment.Phase == NightPhase.Dawn) CloseFront(!first);
            bool gemsOut = moment.Phase == NightPhase.Dark || moment.Phase == NightPhase.Lockdown;
            if (jobs != null) foreach (var gem in jobs.Gems) gem.Lit = gemsOut;
            if (first || moment.Phase == NightPhase.Dawn) return;
            if (moment.Phase == NightPhase.Dark && jobs != null && jobs.Gems.Count > 0)
                Toast("Lights out: the hidden gems glint now. Tab shows them on the map. Gems buy outfits.", 6f);
            string title = moment.Phase switch { NightPhase.Closing => "CLOSING TIME", NightPhase.Dark => "LIGHTS OUT", _ => "LOCKDOWN" };
            ShowBanner(title, moment.Headline, 2.6f, moment.Phase == NightPhase.Lockdown);
            authority.Audio.Publish(AudioCue.Door);
        }

        private void OnGem(Gem gem, PlayerMotor by)
        {
            gemsTonight++;
            int pts = score.Add(ScoreEvent.Gem, "Hidden gem " + gemsTonight + "/" + jobs.Gems.Count);
            wardrobe.AddGem();
            SaveWardrobe();
            storeAudio?.Gem();
            int left = jobs.Gems.Count - gemsTonight;
            Toast("Gem " + gemsTonight + " of " + jobs.Gems.Count + "   +" + pts + "   ·   " + wardrobe.Tokens + (wardrobe.Tokens == 1 ? " gem" : " gems") + " to spend on outfits (Tab)"
                + (left > 0 ? "" : "   ·   all found tonight"), 4.5f);
        }

        private void SaveWardrobe()
        {
            if (testRun) return;
            PlayerPrefs.SetString(WardrobeKey, wardrobe.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Menu keys: O looks at the next outfit, B buys it (or puts it on when owned). Everyone local wears the same.</summary>
        private void WardrobeKeys(Keyboard keyboard)
        {
            if (keyboard.oKey.wasPressedThisFrame)
            {
                int index = System.Array.IndexOf(Wardrobe.All, menuOutfit);
                menuOutfit = Wardrobe.All[(index + 1) % Wardrobe.All.Length];
            }
            if (keyboard.bKey.wasPressedThisFrame)
            {
                if (wardrobe.Owns(menuOutfit))
                {
                    if (wardrobe.TryWear(menuOutfit)) Toast("Wearing " + Wardrobe.Name(menuOutfit) + " — " + Wardrobe.Describe(menuOutfit), 3f);
                }
                else if (wardrobe.TryBuy(menuOutfit)) Toast("Bought and wearing " + Wardrobe.Name(menuOutfit) + " — " + Wardrobe.Describe(menuOutfit), 3.5f);
                else Toast(Wardrobe.Name(menuOutfit) + " costs " + Wardrobe.Cost(menuOutfit) + " gems; you have " + wardrobe.Tokens + ". Gems glint after lights out.", 3.5f);
                SaveWardrobe();
                foreach (var pawn in pawns) pawn.Visual?.SetOutfit(wardrobe.Worn);
            }
        }

        private string WardrobeCopy()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("WARDROBE   ").Append(wardrobe.Tokens).Append(wardrobe.Tokens == 1 ? " gem" : " gems").Append(" to spend   ·   O next outfit, B buy / wear\n");
            foreach (var outfit in Wardrobe.All)
            {
                bool cursor = outfit == menuOutfit, worn = outfit == wardrobe.Worn, owned = wardrobe.Owns(outfit);
                sb.Append(cursor ? "> " : "   ").Append(Wardrobe.Name(outfit));
                sb.Append(worn ? "   (wearing)" : owned ? "   (owned)" : "   " + Wardrobe.Cost(outfit) + " gems");
                sb.Append("   —   ").Append(Wardrobe.Describe(outfit)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Lockdown: the front shutter comes down and the fire exit upstairs becomes the way out.</summary>
        private void CloseFront(bool loud)
        {
            if (jobs == null || jobs.Shutter == null || jobs.Shutter.Down) return;
            jobs.Shutter.Drop();
            if (jobs.FireExit != null) escapeGuide = jobs.FireExit;
            if (loud && storeAudio != null) storeAudio.ShutterDown();
            if (loud) Toast("The front shutter is down. The FIRE EXIT is upstairs — take the escalator by Clothing.", 6f);
        }

        private static string PhaseName(NightPhase phase) => phase switch
        {
            NightPhase.Open => "STORE OPEN",
            NightPhase.Closing => "CLOSING",
            NightPhase.Dark => "LIGHTS OUT",
            NightPhase.Lockdown => "LOCKDOWN",
            _ => "DAWN"
        };

        private static string NextPhaseName(NightPhase phase) => phase switch
        {
            NightPhase.Open => "closing",
            NightPhase.Closing => "lights out",
            NightPhase.Dark => "lockdown",
            _ => "dawn"
        };

        // ---------------- simulation ----------------

        private void FixedUpdate()
        {
            if (authority == null || pawns.Count == 0 || !network.IsAuthority || authority.Flow.Ended) return;
            float delta = Time.fixedDeltaTime;
            if (motors.Length != pawns.Count)
            {
                motors = new PlayerMotor[pawns.Count];
                vision = new DetectionSystem[pawns.Count];
                observations = new bool[pawns.Count];
            }
            ApplySchedule();
            bool anyoneSeen = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var command = pawn.Input.Consume();
                if (authority.Flow.Phase != MatchPhase.Night) command = default;
                bool posing = pawn.Mannequin.Tick(pawn.Input.PoseHeld && i == active, delta);
                if (posing) command = default; // a held pose is a full-body freeze
                var before = pawn.Detection == null ? DetectionState.Green : pawn.Detection.Detection.State;
                int suspicion = pawn.Detection == null ? 0 : pawn.Detection.Detection.Suspicion.Value;
                pawn.Motor.Simulate(command, delta);
                if (pawn.Motor.ActualSpeed > rules.movementThreshold && pawn.Motor.Record.Free) pawn.LastMoved = Time.time;
                if (pawn.Detection != null)
                {
                    var detection = pawn.Detection.Detection;
                    pawn.Detection.PoseCamouflage = pawn.Mannequin.Matches || Wardrobe.Fits(wardrobe.Worn, pawn.Mannequin.Zone);
                    if (pawn.Mannequin.TwitchedThisTick) { pawn.Detection.Twitch = true; if (i == active) Toast("Your arm twitched — held the pose too long", 2.5f); }
                    if (Covered(pawn.Motor)) { detection.Reset(); pawn.Detection.Twitch = false; }
                    else pawn.Detection.Tick(delta);
                    var now = detection.State;
                    if (now != before)
                    {
                        if (now == DetectionState.Orange && detection.FreshSighting)
                        {
                            authority.Audio.Publish(AudioCue.DetectionWarning);
                            pawn.CloseCallPending = Time.time - pawn.LastMoved < 1.5f;
                            pawn.SightingSlipped = false;
                            if (pawn.Mannequin.Matches) { int pts = score.Add(ScoreEvent.PoseMatch); if (i == active) Toast("Perfect pose for " + HudText.ZoneLabel(pawn.Mannequin.Zone) + "   +" + pts, 2.5f); }
                            if (i == active) Tip("seen", "SEEN. Stop now. You have " + rules.orangeDuration.ToString("0.0") + " s before the guard is sure.", 5f);
                        }
                        else if (now == DetectionState.Red)
                        {
                            authority.Audio.Publish(AudioCue.DetectionRed);
                            score.BreakStreak(); // the guard is sure he saw something: the clean run ends here
                            if (i == active) Tip("red", "Frozen in his sight. Hold still: every move now adds a strike.", 5f);
                        }
                        else if (now == DetectionState.Discovered) authority.Audio.Publish(AudioCue.Discovery);
                        else if (now == DetectionState.Green && (before == DetectionState.Orange || before == DetectionState.Red))
                        {
                            storeAudio?.Exhale();
                            if (pawn.CloseCallPending && !pawn.SightingSlipped)
                            {
                                int pts = score.Add(ScoreEvent.CloseCall);
                                if (i == active) Toast("Close call   +" + pts, 2.5f);
                            }
                            pawn.CloseCallPending = false;
                            if (i == active) Tip("away", "He looked away. Move while you can.", 4f);
                        }
                    }
                    if (detection.Suspicion.Value > suspicion)
                    {
                        authority.Audio.Publish(AudioCue.SuspicionIncreased);
                        pawn.SightingSlipped = true;
                        score.BreakStreak();
                    }
                    if (now == DetectionState.Discovered && pawn.Motor.Record.Free)
                    {
                        var carry = pawn.Motor.GetComponent<CarrySystem>();
                        if (carry != null) carry.Release(false);
                        authority.TryCapture(pawn.Motor.Record.Id);
                    }
                    pawn.Pose.Tick();
                    if (pawn.Detection.Observed || !pawn.Motor.Record.Free || Covered(pawn.Motor)) anyoneSeen = true;
                }
                observations[i] = pawn.Detection != null && pawn.Detection.Observed;
                motors[i] = pawn.Motor; vision[i] = pawn.Detection != null ? pawn.Detection.Detection : null;
            }
            if (!anyoneSeen && authority.Flow.Phase == MatchPhase.Night) score.TickUnseen(delta);
            if (guardController.PlayerDriven)
            {
                var command = guardInput.Consume();
                if (authority.Flow.Phase != MatchPhase.Night) command = default;
                guardController.Drive(command.Move, command.Sprint ? rules.sprintSpeed : rules.walkSpeed, delta);
            }
            else if (!authority.Flow.Ended) guardController.TickGroup(motors, vision, delta, observations);
            if (missions != null && missions.Complete && !missionsAnnounced)
            {
                missionsAnnounced = true; authority.Audio.Publish(AudioCue.MissionComplete);
                Tip("exit", "All jobs done. Follow the gold marker to the exit.", 6f);
            }
            foreach (var bonus in bonusDisplays)
                if (bonus.Complete && scoredBonuses.Add(bonus))
                { int pts = score.Add(ScoreEvent.OptionalJob); Toast(bonus.Label + " swapped   +" + pts, 3f); }
            RefreshSurveillance();
            if (backroom != null) backroom.TickAll();
            authority.Tick(delta);
            if (authority.Flow.Ended)
            {
                var agent = guardController.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
        }

        private void OnMissionChanged(MissionTracker job)
        {
            if (!job.Complete || !scoredMissions.Add(job)) return;
            int pts = score.Add(ScoreEvent.RequiredJob, job.Rule.Title);
            Toast(job.Rule.Title + "   +" + pts, 3f);
        }

        private void OnPhaseChanged(MatchPhase next)
        {
            if (next != MatchPhase.Victory && next != MatchPhase.Defeat) return;
            if (next == MatchPhase.Victory) score.AddEscapeTime(authority.Clock.Remaining);
            ended = true;
            newBest = score.Total > bestBefore;
            if (newBest) PlayerPrefs.SetInt("ns-best-" + shift, score.Total);
            if (next == MatchPhase.Victory) PlayerPrefs.SetInt("ns-career", Career.Add(careerBefore, score.Total));
            PlayerPrefs.SetInt("ns-nights", PlayerPrefs.GetInt("ns-nights", 0) + 1);
            PlayerPrefs.Save();
            if (board != null && !testRun) board.Record(shift, score.Total, score.Grade(next == MatchPhase.Victory));
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
            help = false;
        }

        private string nameDraft = "";
        /// <summary>Typing on the end card renames the player. Text comes from the Input System's text event so Shift and layouts work.</summary>
        private void NameEdit(bool start)
        {
            if (board == null || Keyboard.current == null) return;
            if (start == editingName) return;
            editingName = start;
            if (start) { nameDraft = board.PlayerName; Keyboard.current.onTextInput += OnNameChar; }
            else
            {
                Keyboard.current.onTextInput -= OnNameChar;
                board.SetName(nameDraft, shift);
            }
        }

        private void OnNameChar(char c)
        {
            if (c == '\b') { if (nameDraft.Length > 0) nameDraft = nameDraft.Substring(0, nameDraft.Length - 1); return; }
            if (c == '\n' || c == '\r' || c == '\t' || c == 27) return;
            if (nameDraft.Length < 16 && (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')) nameDraft += c;
        }

        private readonly List<EntitySighting> entities = new List<EntitySighting>();
        /// <summary>Camera view of everyone: mannequins, shoppers, staff, and the guard. Positions only.</summary>
        private List<EntitySighting> Entities(List<PlayerSighting> players, Vector3 guardPosition)
        {
            entities.Clear();
            foreach (var player in players) entities.Add(new EntitySighting(player.Id, EntityKind.Mannequin, player.Position));
            if (population != null)
                foreach (var npc in population.Active)
                {
                    Vector3 p = npc.transform.position;
                    entities.Add(new EntitySighting(npc.name, npc.Role == NpcRole.Employee ? EntityKind.Employee : EntityKind.Customer, new MapPoint(p.x, p.y, p.z)));
                }
            entities.Add(new EntitySighting("guard", EntityKind.Guard, new MapPoint(guardPosition.x, guardPosition.y, guardPosition.z)));
            return entities;
        }

        private void RefreshSurveillance()
        {
            var players = surveillancePlayers; players.Clear();
            for (int i = 0; i < pawns.Count; i++)
            {
                Vector3 position = pawns[i].Motor.transform.position;
                players.Add(new PlayerSighting(pawns[i].Motor.Record.Id, pawns[i].Motor.Record.State, new MapPoint(position.x, position.y, position.z)));
            }
            var doorFacts = surveillanceDoors; doorFacts.Clear();
            for (int i = 0; i < doors.Count; i++)
                doorFacts.Add(new DoorSighting(doors[i].DoorId, doors[i].Lock.Open, doors[i].Lock.Locked));
            var objectives = surveillanceObjectives; objectives.Clear();
            if (missions != null)
                for (int i = 0; i < missions.Missions.Count; i++)
                {
                    var mission = missions.Missions[i];
                    objectives.Add(new ObjectiveSighting(mission.Rule.Id, mission.Progress, mission.Rule.Quantity, mission.Complete, mission.Failed));
                }
            Vector3 guardPosition = guardController.transform.position;
            authority.Surveillance.ReportGuard(guardController.Brain.State, new MapPoint(guardPosition.x, guardPosition.y, guardPosition.z));
            authority.Surveillance.ReportLighting(authority.Lighting.Mode);
            authority.Surveillance.ReportPlayers(players);
            authority.Surveillance.ReportEntities(Entities(players, guardPosition));
            authority.Surveillance.ReportDoors(doorFacts);
            authority.Surveillance.ReportObjectives(objectives);
        }

        // ---------------- input and HUD ----------------

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || authority == null) return;
            if (authority.Flow.Ended)
            {
                if (editingName)
                {
                    // Letters arrive through onTextInput (see NameEdit); here only the finish keys.
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                        NameEdit(false);
                    return;
                }
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) { RetryNight(true); return; }
                if (keyboard.nKey.wasPressedThisFrame) { RetryNight(false); return; }
                if (keyboard.tabKey.wasPressedThisFrame) { NameEdit(true); return; }
            }
            if (keyboard.f8Key.wasPressedThisFrame && storeAudio != null) storeAudio.TestSound();
            if (keyboard.tabKey.wasPressedThisFrame) ToggleMenu(help ? (MenuPage?)null : MenuPage.Night);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (help) ToggleMenu(null);
                else Cursor.lockState = CursorLockMode.None;
            }
            if (Debug.isDebugBuild && keyboard.backquoteKey.wasPressedThisFrame)
            {
                testingKeys = !testingKeys;
                Toast(testingKeys ? "Testing keys ON — see Controls page" : "Testing keys off", 2.5f);
            }
            if (help)
            {
                if (menuPage == MenuPage.Night) WardrobeKeys(keyboard);
                if (keyboard.digit1Key.wasPressedThisFrame) menuPage = MenuPage.Night;
                if (keyboard.digit2Key.wasPressedThisFrame) menuPage = MenuPage.Missions;
                if (keyboard.digit3Key.wasPressedThisFrame) menuPage = MenuPage.Inventory;
                if (keyboard.digit4Key.wasPressedThisFrame) menuPage = MenuPage.Reports;
                if (keyboard.digit5Key.wasPressedThisFrame || keyboard.vKey.wasPressedThisFrame) menuPage = MenuPage.Cameras;
                if (keyboard.digit6Key.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame) menuPage = MenuPage.Controls;
            }
            else
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && authority.Flow.Phase == MatchPhase.Night)
                    Cursor.lockState = CursorLockMode.Locked;
                controls.Poll(keyboard);
                if (testingKeys) testing.Poll(keyboard);
                if (Mouse.current != null && active < pawns.Count && Cursor.lockState == CursorLockMode.Locked)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        pawns[active].Mannequin.Cycle(scroll > 0 ? 1 : -1);
                        Toast("Pose: " + PoseLibrary.Name(pawns[active].Mannequin.Selected) + " — " + PoseLibrary.Hint(pawns[active].Mannequin.Selected), 2.2f);
                    }
                }
                if (!testingKeys && active < pawns.Count && Cursor.lockState == CursorLockMode.Locked && gadgets != null)
                {
                    if (keyboard.xKey.wasPressedThisFrame) gadgets.TryToy(pawns[active].Motor);
                    if (keyboard.zKey.wasPressedThisFrame) gadgets.TryGun(pawns[active].Motor, pawns[active].View.View);
                }
                TutorialZoneTip();
            }
            if (active < pawns.Count)
            {
                var pawn = pawns[active];
                pawn.View.ShowPose = autoPoseCam && pawn.Mannequin.Holding && !pawn.Motor.Record.InBackroom;
                bool firstPerson = pawn.View.Effective == ViewMode.FirstPerson;
                if (pawn.Visual != null && pawn.FirstPersonShown != firstPerson) { pawn.Visual.SetFirstPerson(firstPerson); pawn.FirstPersonShown = firstPerson; }
            }
            if (hud != null)
            {
                var eyes = active == pawns.Count ? guardView : pawns[active].View;
                hud.Eyes = eyes != null ? eyes.View : null;
                if (Time.unscaledTime >= nextHudRefresh || hudPawn != active || hudModel.MenuOpen != help || hudModel.Page != menuPage)
                {
                    FillHud(); nextHudRefresh = Time.unscaledTime + 0.05f; hudPawn = active;
                }
                if (guideBeacon != null)
                    guideBeacon.Show(hudModel.Guide && active != pawns.Count && !ended, new Vector3(hudModel.GuideX, hudModel.GuideY, hudModel.GuideZ), hud.Eyes);
                hud.Show(hudModel);
            }
            if (storeAudio != null) storeAudio.Heartbeat(danger, authority.Flow.Ended || help);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (PauseOnFocusLoss && !focused && !help && authority != null && authority.Flow.Phase == MatchPhase.Night && !ended)
                ToggleMenu(MenuPage.Night);
        }

        public void RetryNight() => RetryNight(true);

        public void RetryNight(bool sameShift)
        {
            if (authority == null || !authority.Flow.Ended || restarting) return;
            restarting = true;
            NextShift = sameShift ? shift : UnityEngine.Random.Range(1000, 99999);
            Time.timeScale = 1;
            fusion?.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Prototype");
        }

        private void ToggleMenu(MenuPage? page)
        {
            if (ended) return;
            bool open = !help;
            if (page.HasValue) { menuPage = page.Value; open = true; }
            if (open == help && !page.HasValue) return;
            help = open;
            if (open)
            {
                timeScaleBeforeMenu = Time.timeScale <= 0.01f ? 1f : Time.timeScale;
                clockPausedBeforeMenu = authority.Clock.Paused;
                Time.timeScale = 0f;
                authority.Clock.Paused = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Time.timeScale = timeScaleBeforeMenu;
                authority.Clock.Paused = clockPausedBeforeMenu;
                if (authority.Flow.Phase == MatchPhase.Night) Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void OpenCameras()
        {
            ToggleMenu(MenuPage.Cameras);
            if (Player == null) return;
            string id = Player.Record.Id;
            if (Player.Record.State == PlayerState.Captured) authority.TryEnterSurveillance(id, id);
        }

        private void RescueFromDebug()
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                var record = pawns[i].Motor.Record;
                if (!record.Free) continue;
                if (authority.TryRescueGroup(record.Id, record.Id) > 0) return;
            }
        }

        private void CycleDetection()
        {
            if (Player == null || pawns[active].Detection == null) return;
            var detection = pawns[active].Detection.Detection;
            DetectionState next = detection.State switch
            {
                DetectionState.Green => DetectionState.Orange,
                DetectionState.Orange => DetectionState.Red,
                DetectionState.Red => DetectionState.Discovered,
                _ => DetectionState.Green
            };
            detection.DebugOverride(next, next == DetectionState.Discovered ? rules.discoveryThreshold : detection.Suspicion.Value);
        }

        private void AddSuspicion()
        {
            if (Player == null || pawns[active].Detection == null) return;
            var detection = pawns[active].Detection.Detection;
            int value = detection.Suspicion.Value + 1;
            detection.DebugOverride(value >= rules.discoveryThreshold ? DetectionState.Discovered : detection.State, value);
        }

        private void CycleLighting()
        {
            LightingMode next = authority.Lighting.Mode switch
            {
                LightingMode.Normal => LightingMode.Dark,
                LightingMode.Dark => LightingMode.Emergency,
                LightingMode.Emergency => LightingMode.Partial,
                LightingMode.Partial => LightingMode.Dawn,
                _ => LightingMode.Normal
            };
            authority.Lighting.Set(next);
            authority.Surveillance.ReportLighting(next);
        }

        private void CycleGuard()
        {
            GuardState next = guardController.Brain.State switch
            {
                GuardState.Patrol => GuardState.Investigate,
                GuardState.Investigate => GuardState.Search,
                GuardState.Search => GuardState.Chase,
                GuardState.Chase => GuardState.Capture,
                GuardState.Capture => GuardState.ReturnToPatrol,
                _ => GuardState.Patrol
            };
            guardController.Brain.Transition(next);
        }

        private void SpawnCrate()
        {
            if (Player == null) return;
            var data = ScriptableObject.CreateInstance<ItemDefinition>(); data.canBreak = true; data.id = "debug-crate";
            Vector3 position = Player.transform.position + Player.transform.forward * 1.5f + Vector3.up;
            var box = PrimitiveWorld.Box(transform, "Debug crate", position, Vector3.one * 0.6f, Color.magenta);
            box.layer = 3;
            box.AddComponent<PhysicalItem>().Configure(data, signals);
        }

        private void SetActive(int index)
        {
            active = index;
            for (int i = 0; i < pawns.Count; i++)
            {
                bool on = i == active;
                pawns[i].Input.Active = on;
                pawns[i].View.Active = on;
                pawns[i].View.View.gameObject.SetActive(on);
                if (pawns[i].Visual != null) pawns[i].Visual.SetFirstPerson(on);
                pawns[i].FirstPersonShown = on;
            }
            bool driving = active == pawns.Count;
            if (guardController != null) guardController.PlayerDriven = driving;
            if (guardInput != null) guardInput.Active = driving;
            if (guardView != null) { guardView.Active = driving; guardView.View.gameObject.SetActive(driving); }
            if (guardVisual != null) guardVisual.SetFirstPerson(driving);
            if (storeAudio != null) storeAudio.Bind(driving ? null : pawns[active].Motor, signals);
        }

        private string BuildPrompt()
        {
            if (authority.Flow.Ended || help) return "";
            if (active < pawns.Count && pawns[active].Motor.Record.InBackroom && backroom != null)
            {
                // The room rule by default; what you are looking at (a door, the gap, a figure) when there is one.
                string look = pawns[active].Probe.Prompt;
                return backroom.NoteShowing(pawns[active].Motor) || string.IsNullOrEmpty(look) ? backroom.PromptFor(pawns[active].Motor) : look;
            }
            if (Cursor.lockState != CursorLockMode.Locked && authority.Flow.Phase == MatchPhase.Night) return "Click to play";
            if (active == pawns.Count) return "";
            var pawn = pawns[active];
            if (pawn.Motor.Record.State == PlayerState.Captured) return "Captured. V opens the cameras. A teammate can free you at the warehouse panel.";
            if (pawn.Motor.Record.State == PlayerState.Surveillance) return "Watching the live cameras. Tab opens the map. A teammate can free you at the warehouse panel.";
            if (pawn.Mannequin.Holding)
                return pawn.Mannequin.Matches ? "Posing — this fits " + HudText.ZoneLabel(pawn.Mannequin.Zone) + ". Release right mouse to move."
                    : "Posing — wrong pose for " + HudText.ZoneLabel(pawn.Mannequin.Zone) + ". Scroll to change, or release to move.";
            var carried = pawn.Motor.GetComponent<CarrySystem>();
            if (carried != null && carried.Held != null && string.IsNullOrEmpty(pawn.Probe.Prompt))
                return jobs.CarryPrompt(carried.Held, missions.Missions);
            if (!string.IsNullOrEmpty(pawn.Probe.Prompt)) return pawn.Probe.Prompt;
            if (jobs.Window != null && !jobs.Window.Complete && jobs.Window.Worker == pawn.Motor) return jobs.Window.PromptFor(pawn.Motor);
            var at = pawn.Motor.transform.position;
            if (at.x > -8 && at.x < 0 && at.z > 5 && at.z < 15)
                return pawn.Inventory.Items.Count("shirt") == 0 ? "Clothing — E on a blue shirt on either table" :
                    "Clothing — blue model stand by the mannequins: empty hands, stand still, E to pose";
            return "";
        }

        private string MissionHow(string id) => jobs != null ? jobs.How(id) : "";

        private MissionTracker MissionBy(string id)
        {
            if (missions == null) return null;
            for (int i = 0; i < missions.Missions.Count; i++)
                if (missions.Missions[i].Rule.Id == id) return missions.Missions[i];
            return null;
        }

        private void ApplyGuide(PlayerMotor motor)
        {
            hudModel.Guide = false;
            if (motor == null || !motor.Record.Free || help) return;
            if (missions != null && missions.Complete)
            {
                if (escapeGuide != null) PointGuide(escapeGuide.position, "ESCAPE");
                return;
            }
            if (missions == null || jobs == null) return;
            // The first unfinished job that can show a target gets the marker; a job that needs the hands
            // you are using (carrying something else) passes to the next one.
            foreach (var job in missions.Missions)
            {
                if (job.Complete || job.Failed) continue;
                if (jobs.Guide(job, motor, out var at, out var label)) { PointGuide(at, label); return; }
            }
        }

        private void PointGuide(Vector3 world, string label)
        {
            hudModel.Guide = true;
            hudModel.GuideText = label;
            hudModel.GuideX = world.x;
            hudModel.GuideY = world.y;
            hudModel.GuideZ = world.z;
        }

        private void OnReport(SuspiciousActivityEvent report)
        {
            reportLog.Add(report);
            if (reportLog.Count > 8) reportLog.RemoveAt(0);
            int pts = score.Add(ScoreEvent.Report);
            score.BreakStreak();
            reportToast = HudText.ReportLine(report) + "   " + pts;
            reportToastUntil = Time.unscaledTime + 4.5f;
        }

        private void Toast(string text, float seconds)
        {
            toastText = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        private void ShowBanner(string title, string detail, float seconds, bool dangerous)
        {
            banner = title; bannerDetail = detail; bannerDanger = dangerous;
            bannerUntil = Time.unscaledTime + seconds;
        }

        private void Tip(string key, string text, float seconds)
        {
            if (!tutorial || tipsShown.Contains(key) || Time.unscaledTime < tipCooldown) return;
            tipsShown.Add(key);
            tipText = text;
            tipUntil = Time.unscaledTime + seconds;
            tipCooldown = tipUntil + 1.5f;
        }

        private void TutorialZoneTip()
        {
            if (!tutorial || active >= pawns.Count) return;
            var pawn = pawns[active];
            var carry = pawn.Motor.GetComponent<CarrySystem>();
            if (carry != null && carry.Held != null) Tip("carry", "Carrying: you cannot pose with full hands. G drops it.", 5f);
            var zone = pawn.Mannequin.Zone;
            bool department = zone == ZoneType.Clothing || zone == ZoneType.Electronics || zone == ZoneType.Home;
            if (department && !pawn.Mannequin.Holding) zoneDwell += Time.unscaledDeltaTime; else zoneDwell = 0;
            if (zoneDwell > 2f) Tip("pose", "Hold RIGHT MOUSE to pose. Scroll picks the pose; the right one for this department buys extra doubt.", 7f);
        }

        private void FillHud()
        {
            hudModel.ClearLists();
            hudModel.Solo = Solo;
            hudModel.MenuOpen = help;
            hudModel.Guide = false;
            hudModel.Page = menuPage;
            hudModel.Clock = HudText.Clock(authority);
            hudModel.Paused = authority.Clock.Paused;
            hudModel.Phase = authority.Flow.Phase.ToString();
            hudModel.Shift = shift;
            hudModel.Best = Mathf.Max(bestBefore, ended && newBest ? score.Total : 0);
            hudModel.Score = score.Total;
            hudModel.Multiplier = (float)score.Multiplier;
            double streak = score.Streak;
            double tier = streak >= NightScore.StreakTierTwo ? NightScore.StreakTierTwo : streak >= NightScore.StreakTierOne ? NightScore.StreakTierTwo : NightScore.StreakTierOne;
            double floor = streak >= NightScore.StreakTierTwo ? NightScore.StreakTierTwo : streak >= NightScore.StreakTierOne ? NightScore.StreakTierOne : 0;
            hudModel.StreakFraction = tier > floor ? (float)((streak - floor) / (tier - floor)) : 1f;
            hudModel.StreakLabel = streak < 1 ? "unseen streak" : "unseen " + HudText.Clock(streak);
            double untilNext = NightSchedule.UntilNextPhase(authority.Clock.Remaining, rules.matchDuration);
            hudModel.PhaseLabel = PhaseName(phase);
            hudModel.PhaseCountdown = untilNext > 0 ? NextPhaseName(phase) + " in " + HudText.Clock(untilNext) : "";
            hudModel.Tactics = (Solo ? "" : "Rescued " + authority.Stats.Rescues + "   ") + "Bonus " + authority.Stats.Bonuses + "/" + bonusDisplays.Count;
            if (gadgets != null && gadgets.Status.Length > 0) hudModel.Tactics += "\n" + gadgets.Status;
            hudModel.GuardName = guardProfile != null ? guardProfile.Name.ToUpperInvariant() : "GUARD";
            hudModel.GuardTell = guardProfile != null ? guardProfile.Name + " — " + guardProfile.Trait + ". " + guardProfile.Tell : "";
            hudModel.WardrobeText = WardrobeCopy();
            hudModel.GemText = jobs == null || jobs.Gems.Count == 0 ? "" : "Hidden gems tonight: " + jobs.Gems.Count + " · found " + gemsTonight
                + (phase == NightPhase.Dark || phase == NightPhase.Lockdown ? " · they glint now (cyan on the map)" : " · they only glint after lights out");
            if (jobs != null)
                foreach (var gem in jobs.Gems)
                    if (gem.Lit && !gem.Taken) hudModel.Marks.Add(new MapMark(gem.transform.position, "#5CE1FF", "gem"));
            if (active < pawns.Count && !help)
            {
                var zone = pawns[active].Mannequin.Zone;
                if (zone.HasValue && Wardrobe.Fits(wardrobe.Worn, zone) && outfitNoted.Add(zone.Value))
                    Toast("Your " + Wardrobe.Name(wardrobe.Worn).ToLowerInvariant() + " fits " + HudText.ZoneLabel(zone) + ": stand still and the guard doubts what he saw.", 4f);
            }
            hudModel.Prompt = BuildPrompt();
            hudModel.Toast = Time.unscaledTime < reportToastUntil ? reportToast : Time.unscaledTime < toastUntil ? toastText : "";
            hudModel.Tip = Time.unscaledTime < tipUntil ? tipText : "";
            hudModel.Banner = Time.unscaledTime < bannerUntil ? banner : "";
            hudModel.BannerDetail = bannerDetail;
            hudModel.BannerDanger = bannerDanger;
            hudModel.Reports = reportLog.Count;
            hudModel.SuspicionMax = rules.discoveryThreshold;
            hudModel.CameraNote = Solo ? "Gold is your next job; purple marks optional swaps. Caught? You clear the back hall and come back; only dawn ends the night."
                : "Press V for this map. Captured teammates can use the live feed to time a rescue.";
            hudModel.LiveCameras = false;
            hudModel.Posing = false;
            float dangerTarget = 0f;
            if (active == pawns.Count)
            {
                hudModel.Role = "Guard";
                hudModel.BodyState = guardController.Brain.State.ToString();
                hudModel.Detection = "—";
                hudModel.DetectionTint = "#9FB3C8";
                hudModel.Crowd = "Watching the floor";
                hudModel.CrowdTint = "#9FB3C8";
                hudModel.Suspicion = 0; hudModel.SuspicionMax = 0;
            }
            else
            {
                var pawn = pawns[active];
                var target = pawn.Motor.GetComponent<PerceptionTarget>();
                var crowd = population != null && target != null ? population.StrongestAwarenessOf(target) : AwarenessState.Unaware;
                hudModel.Role = Solo ? "Solo mannequin" : "Mannequin " + (active + 1);
                hudModel.BodyState = pawn.Motor.Record.State.ToString();
                var camouflage = pawn.Motor.GetComponent<DisplayCamouflage>();
                if (camouflage != null && camouflage.Active)
                    hudModel.Tactics = "Display pose · stay still · +2 s guard doubt\n" + hudModel.Tactics;
                if (distractionBell != null && distractionBell.Armed)
                    hudModel.Tactics += "\n" + distractionBell.Prompt;
                if (pawn.Detection != null)
                {
                    var status = pawn.Detection.Detection;
                    hudModel.Detection = Covered(pawn.Motor) ? "COVERED" : HudText.GuardLabel(status, rules.discoveryThreshold);
                    hudModel.DetectionTint = Covered(pawn.Motor) ? "#7CE3B0" : HudText.DetectionColor(status.State);
                    hudModel.Suspicion = status.Suspicion.Value;
                    hudModel.Seen = status.State != DetectionState.Green;
                    dangerTarget = status.State switch
                    {
                        DetectionState.Orange => 0.35f + 0.25f * (1f - (float)(status.GraceRemaining / (rules.orangeDuration + (status.DisplayDoubt ? 2 : 0)))),
                        DetectionState.Red => 0.6f + 0.4f * status.Suspicion.Value / Mathf.Max(1, rules.discoveryThreshold),
                        DetectionState.Discovered => 1f,
                        _ => status.AttentionLingering ? 0.15f : 0f
                    };
                }
                else { hudModel.Detection = "CLEAR"; hudModel.DetectionTint = "#7CE38B"; }
                hudModel.Crowd = HudText.CrowdPlain(crowd, population != null && population.WatchingCount(target) > 0);
                hudModel.CrowdTint = HudText.CrowdTint(crowd);
                var mannequin = pawn.Mannequin;
                hudModel.Posing = mannequin.Holding;
                if (mannequin.Holding)
                {
                    hudModel.PoseName = PoseLibrary.Name(mannequin.Strain.Current);
                    hudModel.PoseMatches = mannequin.Matches;
                    string where = HudText.ZoneLabel(mannequin.Zone);
                    hudModel.PoseHint = mannequin.Matches ? "fits " + where + " — extra guard doubt" : PoseLibrary.ExpectedIn(mannequin.Zone) == Core.Stance.Neutral ? "nothing to blend into here" : "wrong for " + where + " — scroll to " + PoseLibrary.Name(PoseLibrary.ExpectedIn(mannequin.Zone));
                    hudModel.Strain = mannequin.Wobble;
                    hudModel.ComfortLeft = (float)mannequin.Strain.ComfortLeft;
                }
                if (missions != null)
                    foreach (var mission in missions.Missions)
                        hudModel.Missions.Add(new HudLine(mission.Rule.Title, mission.Progress + "/" + mission.Rule.Quantity + "  " + MissionHow(mission.Rule.Id), null, mission.Complete, mission.Failed));
                foreach (var bonus in bonusDisplays)
                {
                    hudModel.Missions.Add(new HudLine("Optional · " + bonus.Label, "4 s still at the purple stand; the noise draws the guard. +300.", null, bonus.Complete, false, true));
                    if (!bonus.Complete) hudModel.Marks.Add(new MapMark(bonus.transform.position, "#B34DCC", bonus.Label));
                }
                var carried = pawn.Motor.GetComponent<CarrySystem>();
                if (carried != null && carried.Held != null)
                    hudModel.Items.Add(new HudLine(carried.Held.Definition.displayName, "in your hands. G to drop"));
                if (pawn.Inventory != null)
                {
                    hudModel.SlotsUsed = pawn.Inventory.Items.UsedSlots;
                    hudModel.SlotsMax = pawn.Inventory.Items.Capacity;
                    foreach (var pair in pawn.Inventory.Items.Snapshot())
                        hudModel.Items.Add(new HudLine(HudText.ItemLabel(pair.Key), "x" + pair.Value));
                }
                ApplyGuide(pawn.Motor);
                bool live = !Solo && (pawn.Motor.Record.State == PlayerState.Captured || pawn.Motor.Record.State == PlayerState.Surveillance);
                if (hudModel.Guide)
                    hudModel.Marks.Add(new MapMark(hudModel.GuideX, hudModel.GuideZ, "#F4C15D", hudModel.GuideText, hudModel.GuideY > 2.5f));
                if (!live && hudModel.Guide)
                    hudModel.CameraNote = Solo ? "Gold: next job. Purple: optional swaps. Use this map if the gold marker is out of sight." : "Gold marks the next job. Captured teammates can see people here.";
                if (live && authority.TryReadSurveillance(pawn.Motor.Record.Id, pawn.Motor.Record.Id, out var view))
                {
                    hudModel.LiveCameras = true;
                    hudModel.CameraNote = "Help time the rescue: red is the guard; green is the rescue console; amber is the delayed bell. Guard is " +
                        Vector3.Distance(guardController.transform.position, rescueGuide.position).ToString("0") + " m from the console.";
                    hudModel.Marks.Add(new MapMark(rescueGuide.position.x, rescueGuide.position.z, "#40DF80", "Rescue"));
                    hudModel.Marks.Add(new MapMark(distractionBell.transform.position.x, distractionBell.transform.position.z, "#F4C15D", "Bell"));
                    foreach (var entity in view.Entities)
                    {
                        string tint = entity.Kind == EntityKind.Guard ? "#FF4B4B" : entity.Kind == EntityKind.Customer ? "#F4C15D"
                            : entity.Kind == EntityKind.Employee ? "#B07CFF" : "#F4EFE6";
                        hudModel.Marks.Add(new MapMark(entity.Position.X, entity.Position.Z, tint, "", entity.Position.Y > 2.5f));
                    }
                    foreach (var alert in view.Alerts)
                        hudModel.Marks.Add(new MapMark(alert.LastKnownPosition.X, alert.LastKnownPosition.Z, "#FFE27A", "report", alert.LastKnownPosition.Y > 2.5f));
                }
            }
            danger = Mathf.Lerp(danger, dangerTarget, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime * 2f));
            hudModel.Danger = ended ? 0f : danger;
            for (int i = 0; i < reportLog.Count; i++)
            {
                var report = reportLog[reportLog.Count - 1 - i];
                hudModel.ReportLines.Add(new HudLine(HudText.ReportLine(report), "Last seen near " + HudText.ZoneLabel(report.Zone)));
            }
            foreach (var entry in controls.Entries)
                hudModel.Controls.Add(new HudLine(entry.Label, entry.Description));
            if (testingKeys)
                foreach (var entry in testing.Entries)
                    hudModel.Testing.Add(new HudLine(entry.Label, entry.Description));
            FillEndCard();
        }

        private void FillEndCard()
        {
            hudModel.Ended = ended && authority.Flow.Ended;
            if (!hudModel.Ended) return;
            bool victory = authority.Phase == MatchPhase.Victory;
            hudModel.Victory = victory;
            hudModel.Grade = score.Grade(victory);
            hudModel.EndTitle = victory ? "ESCAPED" : "DAWN";
            string best = newBest ? "NEW BEST for shift #" + shift : bestBefore > 0 ? "Best for shift #" + shift + ": " + bestBefore.ToString("N0") : "First attempt at shift #" + shift;
            hudModel.EndDetail = (victory ? "Out before dawn. " : "Time ran out before the escape. ") + best + "\n"
                + score.CloseCalls + (score.CloseCalls == 1 ? " close call" : " close calls") + " · best unseen streak " + HudText.Clock(score.BestStreakSeconds)
                + " · " + authority.Stats.Reports + (authority.Stats.Reports == 1 ? " report" : " reports") + " · " + authority.Stats.Captures + (authority.Stats.Captures == 1 ? " capture" : " captures");
            // Collapse repeated events into one line each so the card stays short.
            var totals = new Dictionary<ScoreEvent, (int count, int points)>();
            foreach (var line in score.Lines)
            {
                totals.TryGetValue(line.Event, out var sum);
                totals[line.Event] = (sum.count + 1, sum.points + line.Points);
            }
            foreach (var pair in totals)
            {
                string label = NightScore.Describe(pair.Key) + (pair.Value.count > 1 ? "  x" + pair.Value.count : "");
                hudModel.ScoreLines.Add(new HudLine(label, pair.Value.points.ToString("+#,0;-#,0;0"), null, pair.Value.points > 0, pair.Value.points < 0));
            }
            hudModel.ScoreLines.Add(new HudLine("Total", score.Total.ToString("N0"), null, false, false));
            int careerAfter = victory ? Career.Add(careerBefore, score.Total) : careerBefore;
            var nextUnlock = Career.Next(careerAfter);
            string unlocked = victory && Career.Next(careerBefore).HasValue && (!nextUnlock.HasValue || nextUnlock.Value.gadget != Career.Next(careerBefore).Value.gadget)
                ? "   UNLOCKED: " + Career.Name(Career.Next(careerBefore).Value.gadget) + " — " + Career.Describe(Career.Next(careerBefore).Value.gadget) : "";
            hudModel.EndDetail += "\nCareer " + careerAfter.ToString("N0") + (nextUnlock.HasValue ? " · " + nextUnlock.Value.pointsAway.ToString("N0") + " to " + Career.Name(nextUnlock.Value.gadget) : " · everything unlocked") + unlocked;
            FillBoard();
        }

        private void FillBoard()
        {
            if (board == null) return;
            var local = board.Local(shift);
            var entries = local.Entries;
            hudModel.BoardTitle = (board.Online ? "SHARED BOARD" : "LOCAL BOARD") + " — SHIFT #" + shift;
            string me = editingName ? nameDraft : board.PlayerName;
            int myRank = local.RankOf(board.PlayerName);
            for (int i = 0; i < entries.Count && i < 7; i++)
            {
                bool mine = string.Equals(entries[i].Name, board.PlayerName, StringComparison.OrdinalIgnoreCase);
                hudModel.BoardLines.Add(new HudLine(entries[i].Name + (entries[i].Grade.Length > 0 ? "   " + entries[i].Grade : ""), entries[i].Points.ToString("N0"), null, mine));
            }
            if (entries.Count == 0) hudModel.BoardLines.Add(new HudLine("No scores yet", "", null));
            hudModel.PlayerName = me;
            hudModel.EditingName = editingName;
            string rank = myRank > 0 ? (myRank == 1 ? "You hold the top spot. " : "You are #" + myRank + ". ") : "";
            hudModel.BoardNote = rank + (board.Online ? board.Status : "Scores stay on this Mac. To compare with friends, run Tools/leaderboard_server.py and set its URL — see README.");
        }

        /// <summary>Letters and numbers only: Mac keyboards send F1-F12 as media keys and lack Home/End/Delete.</summary>
        private void BuildKeyMaps()
        {
            controls = new KeyCommandMap("CONTROLS")
                .Describe("WASD", "move").Describe("Mouse", "look").Describe("Shift", "run").Describe("Space", "jump")
                .Describe("Right mouse", "hold to freeze in a pose").Describe("Scroll", "choose the pose")
                .Describe("O / B", "Night page: next outfit / buy or wear it")
                .Describe("E", "use / pick up").Describe("G / Q", "drop / throw")
                .Describe("Tab", "briefing menu (pauses)")
                .Bind(Key.V, "store map", OpenCameras)
                .Bind(Key.H, "controls", () => ToggleMenu(MenuPage.Controls))
                .Bind(Key.T, "guard flashlight", ToggleFlashlight)
                .Bind(Key.C, "camera: eyes / shoulder / front", CycleCamera)
                .Describe("X / Z", "gadgets once unlocked (wind-up toy / price gun)")
                .Describe("Enter / N", "after the night: replay this shift / new shift")
                .Describe("Esc", "close menu / free the mouse")
                .Describe("`", "testing keys (development only)");
            testing = new KeyCommandMap("TESTING")
                .Bind(Key.LeftBracket, "previous mannequin", () => SetActive((active + pawns.Count) % (pawns.Count + 1)), "[")
                .Bind(Key.RightBracket, "next mannequin / guard", () => SetActive((active + 1) % (pawns.Count + 1)), "]")
                .Bind(Key.Semicolon, "capture me", CaptureActive, ";")
                .Bind(Key.R, "rescue captured players", RescueFromDebug)
                .Bind(Key.M, "complete all missions", () => ForEachMission(m => m.DebugComplete()))
                .Bind(Key.J, "next detection level", CycleDetection)
                .Bind(Key.K, "add suspicion", AddSuspicion)
                .Bind(Key.P, "pause the clock", () => authority.Clock.Paused = !authority.Clock.Paused)
                .Bind(Key.F, "skip 60 seconds", () => authority.DebugAdvance(60))
                .Bind(Key.L, "next lighting mode", CycleLighting)
                .Bind(Key.B, "next guard state", CycleGuard)
                .Bind(Key.O, "show guard vision", () => guardController.DebugVision = !guardController.DebugVision)
                .Bind(Key.Z, "NPC debug labels", () => npcDebug.Labels = !npcDebug.Labels)
                .Bind(Key.X, "NPC perception debug", () => npcDebug.Perception = !npcDebug.Perception)
                .Bind(Key.Y, "NPC navigation debug", () => npcDebug.Navigation = !npcDebug.Navigation)
                .Bind(Key.Digit1, "spawn a crate", SpawnCrate)
                .Bind(Key.Digit8, "skip to dawn", () => authority.DebugAdvance(authority.Clock.Remaining))
                .Bind(Key.Digit9, "force victory", () => authority.DebugForce(MatchPhase.Victory))
                .Bind(Key.Digit0, "force defeat", () => authority.DebugForce(MatchPhase.Defeat));
        }

        private void ToggleFlashlight()
        {
            if (guardController.Flashlight != null) guardController.Flashlight.SetEnabled(!guardController.Flashlight.Model.Enabled);
        }

        private void CaptureActive()
        {
            if (Player != null) authority.TryCapture(Player.Record.Id);
        }

        private void ForEachMission(Action<MissionTracker> action)
        {
            if (missions == null) return;
            for (int i = 0; i < missions.Missions.Count; i++) action(missions.Missions[i]);
        }

        private void OnDestroy()
        {
            missions?.Dispose(); audioSubscription?.Dispose(); actionSubscription?.Dispose(); noiseSubscription?.Dispose();
            reportSubscription?.Dispose();
            if (authority != null) authority.Flow.Changed -= OnPhaseChanged;
            if (editingName && Keyboard.current != null) Keyboard.current.onTextInput -= OnNameChar;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
        }

        private sealed class Pawn
        {
            public PlayerMotor Motor;
            public PlayerInputReader Input;
            public PlayerView View;
            public InteractionProbe Probe;
            public PlayerInventory Inventory;
            public PoseDriver Pose;
            public MannequinPose Mannequin;
            public DetectionCoordinator Detection;
            public CharacterVisual Visual;
            public float LastMoved = -10f;
            public bool CloseCallPending, SightingSlipped, FirstPersonShown = true;
        }
    }

    public static class LightingPresenter
    {
        public static void Apply(LightingMode mode)
        {
            if (StoreLighting.Active != null) { StoreLighting.Active.Apply(mode); return; }
            RenderSettings.ambientLight = mode switch
            {
                LightingMode.Dark => new Color(0.04f, 0.04f, 0.06f),
                LightingMode.Emergency => new Color(0.35f, 0.05f, 0.05f),
                LightingMode.Partial => new Color(0.12f, 0.12f, 0.1f),
                LightingMode.Dawn => new Color(0.75f, 0.55f, 0.38f),
                _ => new Color(0.48f, 0.47f, 0.42f)
            };
        }
    }
}
