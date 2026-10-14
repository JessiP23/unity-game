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
        private CrowdDirector crowd;
        public CrowdDirector Crowd => crowd;
        /// <summary>Tonight's jobs as built in the store (tests read the holds and markers).</summary>
        public JobBuilder Jobs => jobs;
        private HidingPlaces hiding;
        public HidingPlaces Hiding => hiding;
        /// <summary>The shift the player clocked in for (Relaxed, Normal, Rush hour). Test runs are always Normal.</summary>
        private Difficulty difficulty = Difficulty.Normal;
        public Difficulty Difficulty => difficulty;
        private ShiftCard shiftCard;
        private SettingsPanel settingsPanel;
        private bool choosingShift;
        private ScenarioDirector scenario;
        public ScenarioDirector Scenario => scenario;
        private NightKind nightKind = NightKind.Regular;
        public NightKind Night => nightKind;
        private bool featuredNight;
        private readonly CrowdMetrics metrics = new CrowdMetrics();
        private float metricsTimer;
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
            if (!testRun) GameSettings.Load(); // before the crowd is sized: graphics caps it
            var session = new LocalSession();
            authority = new LocalMatchAuthority(rules.CreateRules(), session);
            authority.TryBeginNight();
            authority.Flow.Changed += OnPhaseChanged;
            layout = new System.Random(shift * 7919 + 17);
            bestBefore = PlayerPrefs.GetInt("ns-best-" + shift, 0);
            SetUpDare();
            tutorial = PlayerPrefs.GetInt("ns-nights", 0) < 2 && !testRun;
            difficulty = testRun ? Difficulty.Normal : DifficultyRules.Parse(PlayerPrefs.GetInt("ns-difficulty", 1));
            if (!DifficultyRules.Unlocked(difficulty, PlayerPrefs.GetInt("ns-nights", 0))) difficulty = Difficulty.Normal;
            score.DifficultyFactor = DifficultyRules.ScoreFactor(difficulty);
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
            foreach (var pawn in pawns) Dress(pawn);
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
            StoreDressing.Apply(transform, authority.Lighting.Mode, signals);
            jobs.AfterDressing();
            jobs.AfterSeating();
            population = gameObject.AddComponent<CustomerPopulationManager>();
            // Real nights shop the whole mall; test runs keep the classic four departments their scenarios script.
            if (!testRun) population.Catalog = ShoppingCatalog.Mall;
            population.Configure(directory, NpcPopulationSettings.Load(), report => Time.time >= hushUntil && authority.TryReport(report), () => Time.timeAsDouble, network.IsAuthority, shift == 0 ? Environment.TickCount : shift);
            // The mall gets busier as the player gets better; flash sales flood the departments tonight's jobs are in.
            crowd = gameObject.AddComponent<CrowdDirector>();
            crowd.Configure(population, directory, population.Catalog, shift, testRun ? 0 : CrowdPlan.Level(PlayerPrefs.GetInt("ns-nights", 0)),
                () => authority.Clock.Remaining, rules.matchDuration, JobSpots(), Toast, (title, detail) => ShowBanner(title, detail, 2.8f, false),
                governBudget: !testRun && !Application.isBatchMode, withSales: !testRun);
            // Crowds at the jobs, families, phone zombies, the influencer and the cleaning crew: real nights only
            // (scripted tests turn them on when they are about them).
            crowd.JobSites = FillJobSites;
            crowd.PressureEnabled = !testRun;
            crowd.EnableKinds(!testRun);
            crowd.SetDifficulty(testRun ? Difficulty.Normal : difficulty);
            population.Populate();
            foreach (var pawn in pawns) pawn.Cover = CrowdCover.Attach(pawn.Motor, population);
            scenario = gameObject.AddComponent<ScenarioDirector>();
            scenario.Configure(crowd, population, directory, authority, signals, () => rules.matchDuration - authority.Clock.Remaining, rules.matchDuration,
                Toast, (title, detail) => ShowBanner(title, detail, 3.2f, false), () => storeAudio);
            scenario.Award = evt => { int pts = score.Add(evt); Toast(NightScore.Describe(evt) + "   +" + pts, 4f); };
            hiding = gameObject.AddComponent<HidingPlaces>();
            hiding.Configure(directory, () => motors);
            var debugHost = new GameObject("NPC debug");
            debugHost.transform.SetParent(transform, false);
            npcDebug = debugHost.AddComponent<NpcDebugOverlay>();
            npcDebug.Configure(population);
            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Configure();
            hud.PagePicked = page =>
            {
                if (page == MenuPage.Settings) { settingsPanel?.Show(); return; }
                menuPage = page;
            };
            storeAudio = StoreAudio.Create(transform);
            storeAudio.Bind(pawns[0].Motor, signals);
            storeAudio.BindGuard(guardController);
            storeAudio.Watch(missions);
            storeAudio.Listen(authority.Audio);
            crowd.Audio = storeAudio;
            WetFloor.Signals = signals;
            WetFloor.Players = () => motors;
            WetFloor.Said = text => Toast(text, 2.5f);
            crowd.Viral = OnViral;
            if (StoreLighting.Active != null) StoreLighting.Active.BankOff = _ => storeAudio.LightsClunk();
            guideBeacon = GuideBeacon.Create(transform);
            badge = InteractBadge.Create(transform);
            photos = PhotoBooth.Create(transform, hud != null ? hud.Canvas : null);
            photos.Enabled = !testRun;
            WireFittingRooms();
            WireFountain();
            dressedHook = m => PawnOf(m)?.Borrowed.HasValue ?? false;
            DisplayCamouflage.Dressed = dressedHook;
            reactions = gameObject.AddComponent<ShopperReactions>();
            reactions.Configure(population, () => motors, StillFor, m => PawnOf(m)?.Mannequin.Holding ?? false, OnAdmired, storeAudio, shift * 13 + 5);
            // Two cameras sweep the atrium: the rule they teach is one line, "don't move in the red light".
            SecurityCamera.Create(transform, "1", new Vector3(-11.3f, 5.6f, -16.3f), new Vector3(-1f, 0f, -24f), () => motors, CameraReport, CameraFirstSeen, new Vector3(-11.75f, 5.6f, -16.3f));
            SecurityCamera.Create(transform, "2", new Vector3(11.3f, 5.6f, -31.7f), new Vector3(1f, 0f, -24f), () => motors, CameraReport, CameraFirstSeen, new Vector3(11.75f, 5.6f, -31.7f));
            gadgets = gameObject.AddComponent<GadgetKit>();
            gadgets.Configure(transform, signals, authority, careerBefore, text => Toast(text, 3.5f));
            board = gameObject.AddComponent<LeaderboardStore>();
            board.Refresh(shift);
            SetActive(0);
            if (!testRun) GameSettings.Apply(true); // test runs keep the defaults, whatever this machine has saved
            if (hud != null && hud.Canvas != null) settingsPanel = SettingsPanel.Create(hud.Canvas);
            if (!testRun && network.IsAuthority && hud != null && hud.Canvas != null)
            {
                // The main menu first: the night waits, frozen, until the player picks a night and a shift.
                shiftCard = ShiftCard.Create(hud.Canvas);
                shiftCard.Confirmed = ClockIn;
                shiftCard.SettingsWanted = () => settingsPanel?.Show();
                choosingShift = true;
                foreach (var pawn in pawns) pawn.Input.Active = false; // keys on the card never reach the mannequin
                Time.timeScale = 0f;
                authority.Clock.Paused = true;
                Cursor.lockState = CursorLockMode.None;
                var remembered = (NightKind)Mathf.Clamp(PlayerPrefs.GetInt("ns-night", 0), 0, 4);
                shiftCard.Show("Shift #" + shift + "  ·  Guard: " + guardProfile.Name + " (" + guardProfile.Trait + ")  ·  Crowd: " + CrowdPlan.LevelName(crowd.Level)
                    + "  ·  Dare: " + NightDare.Title(dare), difficulty, remembered, NightKinds.Featured(DailyShift()), PlayerPrefs.GetInt("ns-nights", 0));
                return;
            }
            StartNight();
        }

        /// <summary>The shift card closed: apply the choice and let the night run.</summary>
        private void ClockIn()
        {
            if (!choosingShift) return;
            choosingShift = false;
            difficulty = shiftCard.Choice;
            featuredNight = shiftCard.FeaturedPicked;
            shiftCard.Hide();
            settingsPanel?.Hide();
            PlayerPrefs.SetInt("ns-difficulty", (int)difficulty);
            PlayerPrefs.SetInt("ns-night", (int)shiftCard.Night);
            PlayerPrefs.Save();
            BeginScenario(shiftCard.Night, difficulty, featuredNight);
            help = false; timeScaleBeforeMenu = 1f; clockPausedBeforeMenu = false;
            Time.timeScale = 1f;
            authority.Clock.Paused = false;
            SetActive(active); // hands input back to whoever is being played
            for (int i = 0; i < pawns.Count; i++) pawns[i].Input.Consume();
            StartNight();
        }

        /// <summary>
        /// Applies tonight's night and shift: the crowd, the sales, the score and the special night's set pieces.
        /// The main menu calls it on clock-in; tests call it directly (scripted test nights are otherwise ordinary).
        /// </summary>
        public void BeginScenario(NightKind kind, Difficulty shiftDifficulty = Difficulty.Normal, bool featured = false)
        {
            nightKind = kind; difficulty = shiftDifficulty;
            crowd.SetDifficulty(shiftDifficulty);
            crowd.SetSalesCount(NightKinds.Sales(kind, DifficultyRules.Sales(shiftDifficulty)));
            if (kind != NightKind.Regular) crowd.SalesEnabled = true;
            score.DifficultyFactor = DifficultyRules.ScoreFactor(shiftDifficulty) * NightKinds.ScoreFactor(kind) * (featured ? NightKinds.FeaturedBonus : 1);
            metrics.Difficulty = shiftDifficulty; metrics.Night = kind;
            scenario.Begin(kind);
            phaseApplied = false; // lights, torch and gems follow the special night from now on
        }

        private void StartNight()
        {
            Cursor.lockState = CursorLockMode.Locked;
            if (tutorial) Tip("start", "Move with WASD. When the guard or a shopper looks your way, STOP.", 7f);
            else
            {
                var top = board.Local(shift).Entries;
                string target = top.Count > 0 && !string.Equals(top[0].Name, board.PlayerName, StringComparison.OrdinalIgnoreCase)
                    ? "  ·  " + top[0].Name + " holds " + top[0].Points.ToString("N0")
                    : bestBefore > 0 ? "  ·  best " + bestBefore.ToString("N0") : "";
                Toast("Shift #" + shift + "  ·  " + DifficultyRules.Name(difficulty) + "  ·  Guard: " + guardProfile.Name + ", " + guardProfile.Trait + target + "  ·  Crowd: " + CrowdPlan.LevelName(crowd.Level)
                    + "\nDare: " + NightDare.Title(dare) + " — " + NightDare.Describe(dare) + "\nWalk WITH a group of shoppers and they hide you. Running gives you away.", 7f);
            }
        }

        /// <summary>Night metrics: once a second, the crowd and the frame rate; every frame, time spent near unfinished jobs.</summary>
        private void TrackCrowdMetrics()
        {
            if (crowd == null || authority.Flow.Phase != MatchPhase.Night || authority.Clock.Paused) return;
            var me = Player;
            if (me != null && me.Record != null && me.Record.Free) crowd.TrackWait(me.transform.position, Time.deltaTime);
            metricsTimer -= Time.unscaledDeltaTime;
            if (metricsTimer > 0) return;
            metricsTimer = 1f;
            metrics.Sample(population.Customers, population.Cap, Time.smoothDeltaTime * 1000.0);
        }

        /// <summary>
        /// Appends tonight's crowd numbers to crowd-metrics.csv (next to the game's saved data), so the crowd can be
        /// tuned against real nights: waits per job, reports per minute, jobs finished, by difficulty and level.
        /// </summary>
        private void WriteCrowdMetrics(bool victory)
        {
            if (testRun || crowd == null) return;
            metrics.Level = crowd.Level; metrics.Difficulty = difficulty;
            metrics.Reports = authority.Stats.Reports; metrics.InfluencerReports = crowd.InfluencerClips;
            metrics.NightSeconds = rules.matchDuration - Math.Max(0, authority.Clock.Remaining);
            metrics.JobsTotal = missions != null ? missions.Missions.Count : 0;
            metrics.JobsDone = 0;
            if (missions != null) foreach (var m in missions.Missions) if (m.Complete) metrics.JobsDone++;
            metrics.CoveredSeconds = 0;
            foreach (var pawn in pawns) if (pawn.Cover != null) metrics.CoveredSeconds += pawn.Cover.CoveredSeconds;
            metrics.WaitSeconds = new double[metrics.JobsTotal];
            for (int i = 0; i < metrics.JobsTotal; i++) metrics.WaitSeconds[i] = crowd.WaitSeconds.TryGetValue(i, out var w) ? w : 0;
            try
            {
                string file = System.IO.Path.Combine(Application.persistentDataPath, "crowd-metrics.csv");
                // A file started by an older version has other columns: keep it aside and start a fresh one.
                if (System.IO.File.Exists(file))
                    using (var reader = new System.IO.StreamReader(file))
                        if (reader.ReadLine() != CrowdMetrics.Header) { reader.Close(); System.IO.File.Move(file, System.IO.Path.Combine(Application.persistentDataPath, "crowd-metrics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv")); }
                bool fresh = !System.IO.File.Exists(file);
                System.IO.File.AppendAllText(file, (fresh ? CrowdMetrics.Header + "\n" : "") + metrics.Row(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), shift, victory) + "\n");
            }
            catch (Exception error) { Debug.LogWarning("[CROWD] could not write metrics: " + error.Message); }
        }

        /// <summary>Tonight's jobs for the crowd: where the player has to be for each, and whether it is done.</summary>
        private void FillJobSites(List<JobSite> into)
        {
            if (missions == null || jobs == null || pawns.Count == 0) return;
            var motor = Player ?? pawns[0].Motor;
            for (int i = 0; i < missions.Missions.Count; i++)
            {
                var job = missions.Missions[i];
                bool known = jobs.Guide(job, motor, out var at, out _);
                into.Add(new JobSite(i, job.Complete || job.Failed, known, at));
            }
        }

        /// <summary>An influencer filmed a mannequin moving: a funny moment, a photo, and a report the guard acts on.</summary>
        private void OnViral(CustomerBehavior who)
        {
            string[] captions = { "\"THE MANNEQUIN MOVED\" — 12k views and climbing", "\"tell me why the mannequin just WALKED\" — trending in the mall", "\"mall mannequins are alive?? part 1\" — 48k likes" };
            Toast("You went viral! " + captions[viralCount++ % captions.Length] + "\nThe guard has seen the clip.", 5f);
            if (photos != null) photos.Snap("viral", 0.1f);
        }
        private int viralCount;

        /// <summary>Where tonight's jobs send the player, for choosing flash-sale departments.</summary>
        private List<Vector3> JobSpots()
        {
            var spots = new List<Vector3>();
            if (missions == null || jobs == null || pawns.Count == 0) return spots;
            foreach (var job in missions.Missions)
                if (jobs.Guide(job, pawns[0].Motor, out var at, out _)) spots.Add(at);
            return spots;
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
                new[] { new Vector3(3, 0, -8), new Vector3(10, 0, 0), new Vector3(3.6f, 0, 8), new Vector3(-10, 0, 0) },
                new[] { new Vector3(-10, 0, -8), new Vector3(3, 0, -8), new Vector3(6, 0, 3), new Vector3(3.6f, 0, 8), new Vector3(-6, 0, 3) },
                new[] { // One guard also checks the atrium: the cameras are not the only eyes out there.
                new Vector3(10, 0, -5), new Vector3(3.6f, 0, 8), new Vector3(-10, 0, 0), new Vector3(-3, 0, -3), new Vector3(0, 0, -21) },
                // One guard takes the escalator: from upstairs the whole floor is in view, and so is he.
                new[] { new Vector3(3, 0, -8), new Vector3(10, 0, 0), new Vector3(9, PrimitiveWorld.UpstairsY, 10), new Vector3(5, PrimitiveWorld.UpstairsY, 12), new Vector3(-6, 0, 3) }
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
            scenario?.OnCaptured(pawn.Motor);
            if (pawn.Borrowed.HasValue) { pawn.Borrowed = null; Dress(pawn); }
            int moves = pawn.Detection != null ? pawn.Detection.Detection.Suspicion.Value : rules.discoveryThreshold;
            int penalty = score.Add(ScoreEvent.Capture);
            score.BreakStreak();
            int seed = authority.Stats.Captures * 31 + shift * 7 + index * 17, difficulty = authority.Stats.Captures;
            if (testRun || pawn.Visual == null)
            {
                // Tests (and bodies without art) enter the hall at once so the state change is immediate.
                backroom.Admit(pawn.Motor, seed, difficulty);
                StartCoroutine(CaughtBeat(pawn, index, moves, penalty));
                return;
            }
            StartCoroutine(Plank(pawn, index, moves, penalty, seed, difficulty));
        }

        /// <summary>
        /// The funny part of being caught: the mannequin keels over stiff as a board, thuds, the camera
        /// swings round to show it and a photo is taken; then off to the back hall. The capture already
        /// happened in the rules; this is 1.3 s of presentation (a captive still counts toward escape).
        /// </summary>
        private IEnumerator Plank(Pawn pawn, int index, int moves, int penalty, int seed, int difficulty)
        {
            pawn.Input.Active = false;
            pawn.Toppling = true;
            pawn.Visual.Topple(true);
            ShowBanner("CAUGHT", "Down like a plank. The guard saw you move " + moves + (moves == 1 ? " time" : " times") + ".   " + penalty.ToString("+#;-#;0") + " pts", 2.4f, true);
            if (index == active) photos?.Snap("Caught! Down like a plank", 0.55f);
            yield return new WaitForSeconds(0.42f);
            storeAudio?.LightsClunk(0.9f);
            yield return new WaitForSeconds(0.9f);
            pawn.Visual.Topple(false);
            pawn.Toppling = false;
            if (ended) yield break;
            // A teammate may have freed you during the fall; only a captive goes to the hall.
            bool admitted = pawn.Motor.Record.State == PlayerState.Captured;
            if (admitted) backroom.Admit(pawn.Motor, seed, difficulty);
            else { pawn.Input.Active = index == active; yield break; }
            yield return new WaitForSeconds(0.9f);
            if (ended) yield break;
            ShowBanner("THE BACK HALL", "Clear 3 rooms to get back on the floor. Read the sign on each gate. The clock keeps running.", 2.6f, false);
            yield return new WaitForSeconds(1.0f);
            if (!ended) pawn.Input.Active = index == active;
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
            if (dare == Dare.Selfie && what.Contains("photo")) CompleteDare();
            if (what.Contains("photo") && pawns[active].Motor == motor) photos?.Snap("A shopper's selfie with you", 0.3f);
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
            var lights = NightKinds.Lighting(nightKind, moment.Lighting);
            authority.Lighting.Set(lights);
            authority.Surveillance.ReportLighting(lights);
            guardController.Pace = (float)moment.GuardPace;
            if (population != null) population.CrowdScale = (float)moment.Crowd;
            if (guardController.Flashlight != null && !guardController.PlayerDriven) guardController.Flashlight.SetEnabled(moment.Flashlight || NightKinds.GuardTorchAllNight(nightKind));
            if (moment.Phase == NightPhase.Lockdown || moment.Phase == NightPhase.Dawn) CloseFront(!first);
            bool gemsOut = moment.Phase == NightPhase.Dark || moment.Phase == NightPhase.Lockdown || nightKind == NightKind.Blackout;
            if (jobs != null) foreach (var gem in jobs.Gems) gem.Lit = gemsOut;
            if (first || moment.Phase == NightPhase.Dawn) return;
            if (moment.Phase == NightPhase.Dark && jobs != null && jobs.Gems.Count > 0)
                Toast("Lights out: the hidden gems glint now. Tab shows them on the map. Gems buy outfits.", 6f);
            string title = moment.Phase switch { NightPhase.Closing => "CLOSING TIME", NightPhase.Dark => "LIGHTS OUT", _ => "LOCKDOWN" };
            ShowBanner(title, moment.Headline, 2.6f, moment.Phase == NightPhase.Lockdown);
            authority.Audio.Publish(AudioCue.Door);
        }

        private InteractBadge badge;
        private PhotoBooth photos;
        private Func<PlayerMotor, bool> dressedHook;
        private Dare dare;
        private bool dareDone, coinTossed, ranTonight;
        private int dareStreak;
        private float carouselSeconds, hushUntil = -1f;
        // The live stream: are you in the shot, how long you have held a pose in it, and which stream already gave you a shout-out.
        private bool inShot;
        private float streamHold, liveDistance = float.MaxValue;
        private int shoutoutFrom = -1;

        private void TickStream(Pawn pawn, float delta)
        {
            var stream = crowd != null ? crowd.Stream : null;
            var motor = pawn.Motor;
            inShot = stream != null && motor.Record.Free && stream.Sees(motor.transform.position + Vector3.up * 1.2f, motor.transform);
            liveDistance = stream != null ? Vector3.Distance(stream.transform.position, motor.transform.position) : float.MaxValue;
            bool still = motor.ActualSpeed <= rules.movementThreshold;
            bool posed = pawn.Mannequin.Holding || motor.Mounted;
            if (inShot && still && posed && shoutoutFrom != stream.StreamId) streamHold += delta;
            else streamHold = Mathf.Max(0f, streamHold - delta * 2f);
            if (stream == null || streamHold < LiveStream.ShoutoutSeconds) return;
            streamHold = 0f;
            shoutoutFrom = stream.StreamId;
            int pts = score.Add(ScoreEvent.Shoutout);
            stream.Spike(1500 + UnityEngine.Random.Range(0, 2500));
            Toast("SHOUT-OUT! \"this mannequin is SERVING\" — " + stream.Viewers.ToString("N0") + " watching   +" + pts, 3.5f);
            storeAudio?.Exhale();
            if (photos != null) photos.Snap("stream", 0.1f);
        }
        private Wish wish;
        private FountainEffects fountainFx;

        private void SetUpDare()
        {
            dare = NightDare.For(shift);
            wish = FountainWish.For(shift);
            dareStreak = testRun ? 0 : PlayerPrefs.GetInt("ns-dare-streak", 0);
            EscapeInteractable.ResetLast();
        }

        /// <summary>Tonight's dare is met: points, a gem, and (on the daily shift) one more day on the streak.</summary>
        private void CompleteDare()
        {
            if (dareDone) return;
            dareDone = true;
            int pts = score.Add(ScoreEvent.Dare, NightDare.Title(dare));
            wardrobe.AddGem(); SaveWardrobe();
            if (!testRun && shift == DailyShift())
            {
                int today = DailyShift();
                dareStreak = NightDare.NextStreak(PlayerPrefs.GetInt("ns-dare-day", 0), PlayerPrefs.GetInt("ns-dare-streak", 0), today);
                PlayerPrefs.SetInt("ns-dare-day", today); PlayerPrefs.SetInt("ns-dare-streak", dareStreak); PlayerPrefs.Save();
            }
            storeAudio?.Gem();
            Toast("DARE DONE — " + NightDare.Title(dare) + "   +" + pts + " and a gem" + (dareStreak > 1 ? "   ·   streak " + dareStreak + " days" : ""), 5f);
        }

        /// <summary>The fountain: coins on the rim, splashing in the pool, and the effects both use.</summary>
        private void WireFountain()
        {
            fountainFx = FountainEffects.Create(transform);
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Fountain basin" || t.GetComponent<FountainCoin>() != null) continue;
                var coin = t.gameObject.AddComponent<FountainCoin>();
                coin.Used = () => coinTossed;
                coin.Toss = TossCoin;
            }
            var pool = new GameObject("Fountain pool");
            pool.transform.SetParent(transform, false);
            pool.transform.position = FountainDressing.Centre + new Vector3(0f, 0.6f, 0f);
            var box = pool.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(FountainDressing.PoolHalf * 2f, 0.9f, FountainDressing.PoolHalf * 2f);
            pool.layer = 2;
            pool.AddComponent<FountainWater>().Configure(signals, at => { fountainFx.Splash(at, 14); storeAudio?.Splash(0.45f); });
        }

        private bool TossCoin(PlayerMotor motor)
        {
            if (coinTossed) return false;
            coinTossed = true;
            Vector3 from = motor.transform.position + Vector3.up * 1.4f + motor.transform.forward * 0.3f;
            Vector3 flat = FountainDressing.Centre - motor.transform.position; flat.y = 0f;
            Vector3 to = motor.transform.position + flat.normalized * Mathf.Min(flat.magnitude - 0.8f, 1.6f);
            to.y = FountainDressing.WaterY;
            fountainFx.ThrowCoin(from, to, () =>
            {
                storeAudio?.Splash();
                signals.Noise.Publish(new NoiseEvent(to, FountainWish.SplashLoudness, "a coin in the fountain"));
                string granted;
                switch (wish)
                {
                    case Wish.Fortune: score.Add(ScoreEvent.Wish, "Fountain wish: fortune"); granted = "Fortune: +" + FountainWish.FortunePoints + " points"; break;
                    case Wish.Gem: wardrobe.AddGem(); SaveWardrobe(); granted = "A gem washes up: +1 gem (Tab to spend)"; break;
                    default: hushUntil = Time.time + FountainWish.HushSeconds; granted = "Hush: no shopper will report you for " + (int)FountainWish.HushSeconds + " s"; break;
                }
                Toast("Splash! The guard heard that.   Wish granted — " + granted, 5f);
            });
            return true;
        }

        /// <summary>What a pawn is wearing right now: borrowed clothes first, then the bought outfit.</summary>
        private Outfit Effective(Pawn pawn) => pawn.Borrowed ?? wardrobe.Worn;
        private void Dress(Pawn pawn) => pawn.Visual?.SetOutfit(Effective(pawn));

        /// <summary>Every Clothing rack and table lends you clothes: the borrowed piece fits Clothing (and the toy store).</summary>
        private void WireFittingRooms()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Clothing rack" && t.name != "Clothing table") continue;
                if (t.GetComponent<FittingRack>() != null) continue;
                var rack = t.gameObject.AddComponent<FittingRack>();
                rack.Piece = t.name == "Clothing rack" ? "a jacket from the rack" : "a folded shirt";
                rack.Wearing = m => PawnOf(m)?.Borrowed.HasValue ?? false;
                rack.Toggle = m =>
                {
                    var pawn = PawnOf(m);
                    if (pawn == null) return false;
                    if (pawn.Borrowed.HasValue)
                    {
                        pawn.Borrowed = null; Dress(pawn);
                        Toast("You put the clothes back.", 2.5f);
                    }
                    else
                    {
                        pawn.Borrowed = Outfit.BoutiqueBlack; Dress(pawn);
                        storeAudio?.Exhale();
                        Toast("You slipped into " + (t.name == "Clothing rack" ? "a jacket" : "a shirt") + " off the display. It hides you in Clothing and the toy store while you stand still. Caught? You lose it.", 5f);
                    }
                    return true;
                };
            }
        }

        private void OnGem(Gem gem, PlayerMotor by)
        {
            gemsTonight++;
            int pts = score.Add(ScoreEvent.Gem, "Hidden gem " + gemsTonight + "/" + jobs.Gems.Count);
            wardrobe.AddGem();
            SaveWardrobe();
            storeAudio?.Gem();
            int left = jobs.Gems.Count - gemsTonight;
            if (left == 0 && dare == Dare.AllGems) CompleteDare();
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
                foreach (var pawn in pawns) Dress(pawn);
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
            if (loud && storeAudio != null) storeAudio.ShutterDown();
            if (loud) Toast("The supermarket shutter is down. Inside? The FIRE EXIT is upstairs. In the mall? The front doors are still open.", 6f);
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
                if (posing) command = new PlayerCommand(Vector2.zero, false, false, command.Crouch); // a held pose is a full-body freeze (a crouch stays a crouch)
                var before = pawn.Detection == null ? DetectionState.Green : pawn.Detection.Detection.State;
                int suspicion = pawn.Detection == null ? 0 : pawn.Detection.Detection.Suspicion.Value;
                pawn.Motor.Simulate(command, delta);
                bool chair = pawn.Motor.SeatOwner != null && pawn.Motor.SeatOwner.Style == SeatStyle.Chair;
                pawn.View.Crouching = pawn.Motor.Crouched || chair; // seated eyes are about where crouched ones are
                if (pawn.Visual != null) { pawn.Visual.SetCrouch(pawn.Motor.Crouched); pawn.Visual.SetRide(pawn.Motor.Seat, chair ? SeatStyle.Chair : SeatStyle.Astride); }
                if (pawn.Motor.Record.State == PlayerState.Running) ranTonight = true;
                if (pawn.Motor.Surface is CarouselRide turning && turning.Speed > 1f && pawn.Motor.Record.Free)
                {
                    carouselSeconds += delta;
                    if (dare == Dare.CarouselRide && carouselSeconds >= NightDare.CarouselSeconds) CompleteDare();
                }
                if (pawn.Motor.ActualSpeed > rules.movementThreshold && pawn.Motor.Record.Free) pawn.LastMoved = Time.time;
                if (i == active) TickStream(pawn, delta);
                if (pawn.Detection != null)
                {
                    var detection = pawn.Detection.Detection;
                    // A mannequin sitting on a carousel horse is the most natural thing in the toy store.
                    pawn.Detection.PoseCamouflage = pawn.Mannequin.Matches || pawn.Motor.Mounted || Wardrobe.Fits(Effective(pawn), pawn.Mannequin.Zone);
                    pawn.Detection.CrowdCover = pawn.Cover != null && pawn.Cover.Strength >= CoverRules.GuardDoubtFrom;
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
            if (next == MatchPhase.Victory && scenario != null && scenario.PaysOnEscape) { int pts = score.Add(ScoreEvent.Diamond); Toast("You got away with the diamond!   +" + pts, 5f); }
            if (next == MatchPhase.Victory && !dareDone)
            {
                bool viaFire = jobs != null && jobs.FireExit != null && EscapeInteractable.Last != null && EscapeInteractable.Last.transform == jobs.FireExit;
                bool met = dare switch
                {
                    Dare.Wish => coinTossed,
                    Dare.Unreported => authority.Stats.Reports == 0,
                    Dare.NeverRun => !ranTonight,
                    Dare.FireExit => viaFire,
                    _ => false
                };
                if (met) CompleteDare();
            }
            ended = true;
            newBest = score.Total > bestBefore;
            if (newBest) PlayerPrefs.SetInt("ns-best-" + shift, score.Total);
            if (next == MatchPhase.Victory) PlayerPrefs.SetInt("ns-career", Career.Add(careerBefore, score.Total));
            PlayerPrefs.SetInt("ns-nights", PlayerPrefs.GetInt("ns-nights", 0) + 1);
            PlayerPrefs.Save();
            WriteCrowdMetrics(next == MatchPhase.Victory);
            if (board != null && !testRun) board.Record(shift, score.Total, score.Grade(next == MatchPhase.Victory));
            if (next == MatchPhase.Victory) photos?.Snap("Escaped! Shift #" + shift + " · " + score.Total.ToString("N0") + " pts", 0.05f);
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
            jobs?.TickGallery(Time.deltaTime);
            var keyboard = Keyboard.current;
            if (keyboard == null || authority == null) return;
            if (settingsPanel != null && settingsPanel.Visible)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) settingsPanel.Hide();
                return;
            }
            if (choosingShift)
            {
                if (keyboard.nKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) shiftCard.StepNight(1);
                if (keyboard.upArrowKey.wasPressedThisFrame) shiftCard.StepNight(-1);
                if (keyboard.sKey.wasPressedThisFrame) settingsPanel?.Show();
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) shiftCard.Pick(0);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) shiftCard.Pick(1);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) shiftCard.Pick(2);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) shiftCard.Pick(Mathf.Max(0, shiftCard.Selected - 1));
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) shiftCard.Pick(Mathf.Min(2, shiftCard.Selected + 1));
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ClockIn();
                return;
            }
            TrackCrowdMetrics();
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
                if (keyboard.digit7Key.wasPressedThisFrame) settingsPanel?.Show();
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
                pawn.View.ShowPose = pawn.Toppling || autoPoseCam && pawn.Mannequin.Holding && !pawn.Motor.Record.InBackroom;
                if (badge != null) badge.Show(help || ended || Cursor.lockState != CursorLockMode.Locked ? null : pawn.Probe.Target, pawn.View.View);
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
            if (PauseOnFocusLoss && !focused && !help && !choosingShift && authority != null && authority.Flow.Phase == MatchPhase.Night && !ended)
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
            if (Cursor.lockState != CursorLockMode.Locked && authority.Flow.Phase == MatchPhase.Night && !choosingShift) return "Click to play";
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
            if (jobs.RideSpot != null && !jobs.RideSpot.Complete && (jobs.RideSpot.Worker == pawn.Motor || pawn.Motor.Surface == jobs.Carousel)) return jobs.RideSpot.PromptFor(pawn.Motor);
            if (jobs.Shoot != null && !jobs.Shoot.Complete && jobs.Shoot.Worker == pawn.Motor) return jobs.Shoot.PromptFor(pawn.Motor) + " · E or a move key stands up";
            if (pawn.Motor.Surface is EscalatorBelt) return "The escalator carries you — and a mannequin riding up is a mannequin that moves. The stairs let you stop.";
            if (pawn.Motor.Mounted && pawn.Motor.SeatOwner != null && pawn.Motor.SeatOwner.Style == SeatStyle.Chair) return "Sitting on " + pawn.Motor.SeatOwner.What + ": a seated mannequin on display. Keep still. E or a move key stands up.";
            if (pawn.Motor.Mounted) return pawn.Motor.Surface is CarouselRide horse && horse.Speed > 0.5f ? "Riding a horse: you are part of the ride. E or a move key climbs down." : "The ride has stopped. A mannequin on a horse — keep still. E or a move key climbs down.";
            if (pawn.Motor.Surface is CarouselRide spinning) return spinning.Speed > 0.5f ? "The carousel turns: riders are not moving. E on a horse to climb on." : "The carousel has stopped. You are a mannequin standing on a ride — hold still.";
            var at = pawn.Motor.transform.position;
            if (at.y < 2.5f && at.x > -8 && at.x < 0 && at.z > 5 && at.z < 15)
            {
                // Only mention the blue shirt on nights that have the shirt job; otherwise point at the racks.
                var shirtJob = MissionBy("steal-shirt");
                if (shirtJob != null && !shirtJob.Complete && pawn.Inventory.Items.Count("shirt") == 0)
                    return "Clothing — the BLUE shirt with the SHIRT · E sign is your job. Any rack or table: E to try clothes on.";
                if (pawn.Inventory.Items.Count("shirt") > 0)
                    return "Clothing — blue model stand by the mannequins: empty hands, stand still, E to pose";
                return pawn.Borrowed.HasValue ? "Clothing — you are wearing borrowed clothes: they hide you here while you stand still."
                    : "Clothing — walk up to a rack or a table and press E to try something on. It disguises you here.";
            }
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
                // After lockdown the supermarket is shuttered: inside, the fire exit upstairs; out in the mall, the front door.
                bool shuttered = jobs != null && jobs.Shutter != null && jobs.Shutter.Down;
                bool inside = motor.transform.position.z > -14.5f;
                var target = shuttered && inside && jobs.FireExit != null ? jobs.FireExit : escapeGuide;
                if (target != null) PointGuide(target.position, target == jobs.FireExit ? "FIRE EXIT" : "ESCAPE");
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

        private bool CameraReport(PlayerMotor body)
        {
            var at = body.transform.position;
            var zone = directory != null ? directory.ZoneAt(at) : null;
            var report = new SuspiciousActivityEvent("Camera", body.Record.Id, new MapPoint(at.x, at.y, at.z), Time.timeAsDouble, 0.9, ReportKind.MovingMannequin, zone);
            bool accepted = authority.TryReport(report);
            if (accepted) Toast("The camera caught you moving in its light. The guard has been told.", 4f);
            return accepted;
        }

        private void CameraFirstSeen(PlayerMotor body)
        {
            if (PawnOf(body) == null || PawnOf(body) != pawns[Mathf.Min(active, pawns.Count - 1)]) return;
            Toast("Security camera: you are in its red light. Don't move while it is on you; wait for it to sweep past.", 5f);
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
            hudModel.DareText = "Tonight's dare — " + NightDare.Title(dare) + ": " + NightDare.Describe(dare) + (dareDone ? "   DONE +" + NightDare.Points + " and a gem" : "   (+" + NightDare.Points + " and a gem)")
                + (dareStreak > 1 ? "   ·   dare streak " + dareStreak + " days" : "");
            if (crowd != null)
            {
                hudModel.CrowdText = crowd.NightLine;
                hudModel.EventText = crowd.EventLine;
                hudModel.EventHot = crowd.Stage == SaleStage.Open;
                if (string.IsNullOrEmpty(hudModel.EventText) && scenario != null) { hudModel.EventText = scenario.EventLine(out bool hot); hudModel.EventHot = hot; }
                hudModel.ObjectiveText = scenario != null ? scenario.Objective(Player) : "";
                if (scenario != null && scenario.TryPrizeMark(out var prizeAt, out var prizeLabel)) hudModel.Marks.Add(new MapMark(prizeAt, "#FFD24D", prizeLabel));
                if (crowd.TryMark(out var saleAt, out var saleLabel)) hudModel.Marks.Add(new MapMark(saleAt, "#FF8A3D", saleLabel));
                int next = -1;
                if (missions != null) for (int i = 0; i < missions.Missions.Count && next < 0; i++) if (!missions.Missions[i].Complete && !missions.Missions[i].Failed) next = i;
                string crowdTint = "#9FB3C8";
                hudModel.JobCrowdText = next >= 0 ? crowd.JobLine(next, out crowdTint) : "";
                hudModel.JobCrowdTint = crowdTint;
                if (missions != null)
                    for (int i = 0; i < missions.Missions.Count; i++)
                        if (crowd.TryQuietMark(i, out var quietAt)) hudModel.Marks.Add(new MapMark(quietAt, "#40DF80", "quiet"));
                var filming = crowd.InfluencerOnFloor;
                if (filming != null) hudModel.Marks.Add(new MapMark(filming.transform.position, "#FF3B30", "REC"));
                foreach (var h in crowd.Heat)
                    hudModel.Heat.Add((HudText.MapZone(h.Zone), h.Level, h.Trend, h.People));
                var cover = active < pawns.Count ? pawns[active].Cover : null;
                var me = active < pawns.Count ? pawns[active].Motor : null;
                var nook = hiding != null && me != null && me.Record.Free ? hiding.At(me.transform.position) : null;
                bool hidden = hiding != null && hiding.IsHidden(me);
                var live = crowd.Stream;
                bool shouted = live != null && shoutoutFrom == live.StreamId;
                hudModel.CoverLive = !hidden && (inShot || (live != null && liveDistance < 14f && (cover == null || !cover.Covered) && nook == null));
                hudModel.CoverOn = hidden || (cover != null && cover.Covered);
                hudModel.CoverText = hidden ? "HIDDEN · nobody can see you — unless the guard walks right up"
                    : inShot ? (shouted ? "● IN THE LIVE SHOT · don't move — it goes straight to the guard"
                        : streamHold > 0.1f ? "● ON THE STREAM · hold it… shout-out in " + Mathf.CeilToInt(LiveStream.ShoutoutSeconds - streamHold) + "s"
                        : "● IN THE LIVE SHOT · freeze, or hold a pose for a shout-out")
                    : hudModel.CoverLive ? "● LIVE " + Mathf.RoundToInt(liveDistance) + " m away · their red shot sees through any crowd"
                    : nook != null ? (me.Crouched ? "Keep still…" : "Hiding spot — hold Ctrl to crouch here and vanish")
                    : cover == null ? "" : cover.Covered ? "IN THE CROWD · " + cover.Companions + " shoppers hide you" : cover.Hint;
            }
            hudModel.GemText = jobs == null || jobs.Gems.Count == 0 ? "" : "Hidden gems tonight: " + jobs.Gems.Count + " · found " + gemsTonight
                + (phase == NightPhase.Dark || phase == NightPhase.Lockdown ? " · they glint now (cyan on the map)" : " · they only glint after lights out");
            if (jobs != null)
                foreach (var gem in jobs.Gems)
                    if (gem.Lit && !gem.Taken) hudModel.Marks.Add(new MapMark(gem.transform.position, "#5CE1FF", "gem"));
            if (active < pawns.Count && !help)
            {
                var zone = pawns[active].Mannequin.Zone;
                var wearing = Effective(pawns[active]);
                if (zone.HasValue && Wardrobe.Fits(wearing, zone) && outfitNoted.Add(zone.Value))
                    Toast("Your " + (pawns[active].Borrowed.HasValue ? "borrowed clothes fit" : Wardrobe.Name(wearing).ToLowerInvariant() + " fits") + " " + HudText.ZoneLabel(zone) + ": stand still and the guard doubts what he saw.", 4f);
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
                + " · " + authority.Stats.Reports + (authority.Stats.Reports == 1 ? " report" : " reports") + " · " + authority.Stats.Captures + (authority.Stats.Captures == 1 ? " capture" : " captures")
                + "\n" + DifficultyRules.Name(difficulty) + " shift (score ×" + DifficultyRules.ScoreFactor(difficulty).ToString("0.0#") + ") · crowd " + CrowdPlan.LevelName(crowd != null ? crowd.Level : 0)
                + (pawns.Count > 0 && pawns[0].Cover != null && pawns[0].Cover.CoveredSeconds >= 1 ? " · hid in the crowd for " + HudText.Clock(pawns[0].Cover.CoveredSeconds) : "")
                + (crowd != null && crowd.InfluencerClips > 0 ? " · went viral " + crowd.InfluencerClips + "×" : "");
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
            hudModel.EndDetail += "\nDare " + (dareDone ? "done: " : "missed: ") + NightDare.Title(dare) + (dareStreak > 1 ? " · streak " + dareStreak + " days" : "")
                + (photos != null && photos.Taken > 0 ? "   ·   " + photos.Taken + (photos.Taken == 1 ? " photo" : " photos") + " saved to Pictures/Night Mall" : "");
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
                .Describe("Ctrl / Option", "hold to crouch: low cover hides you; crouch still at a hiding spot to vanish")
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
            if (DisplayCamouflage.Dressed == dressedHook) DisplayCamouflage.Dressed = null;
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
            /// <summary>Clothes taken off a Clothing rack tonight; they override the worn outfit until caught or put back.</summary>
            public Outfit? Borrowed;
            /// <summary>True for the second it takes to fall over after a capture; the camera shows it.</summary>
            public bool Toppling;
            public bool CloseCallPending, SightingSlipped, FirstPersonShown = true;
            /// <summary>Walking with the crowd (see <see cref="CrowdCover"/>).</summary>
            public CrowdCover Cover;
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
