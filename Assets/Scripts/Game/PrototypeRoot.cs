using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>Scene composition and simulation scheduling. Match rules stay in <see cref="LocalMatchAuthority"/>.</summary>
    public sealed class PrototypeRoot : MonoBehaviour
    {
        public GameRulesAsset rules;
        public PlayerMotor Player => active >= 0 && active < pawns.Count ? pawns[active].Motor : null;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly List<DoorInteractable> doors = new List<DoorInteractable>();
        private readonly WorldSignals signals = new WorldSignals();
        private readonly GameLog log = new GameLog(true);
        private LocalMatchAuthority authority;
        private MissionSystem missions;
        private GuardController guardController;
        private PlayerInputReader guardInput;
        private PlayerView guardView;
        private CharacterVisual guardVisual;
        private PrototypeHud hud;
        private KeyCommandMap controls, testing;
        private StoreDirectory directory;
        private CustomerPopulationManager population;
        private NpcDebugOverlay npcDebug;
        private INetworkService network = new LocalNetworkService();
        private FusionSession fusion;
        public CustomerPopulationManager Population => population;
        public StoreDirectory Directory => directory;
        public LocalMatchAuthority Authority => authority;
        public GuardController Guard => guardController;
        public int MannequinCount => pawns.Count;
        public PlayerMotor MannequinAt(int index) => pawns[index].Motor;
        public PlayerInputReader InputAt(int index) => pawns[index].Input;
        public MissionSystem Missions => missions;
        private int active;
        private bool help;
        private MenuPage menuPage = MenuPage.Night;
        private float timeScaleBeforeMenu = 1f;
        private bool clockPausedBeforeMenu;
        private string reportToast;
        private float reportToastUntil;
        private readonly List<SuspiciousActivityEvent> reportLog = new List<SuspiciousActivityEvent>();
        private readonly HudModel hudModel = new HudModel();
        private bool logAudio = true;
        private bool missionsAnnounced;
        private IDisposable audioSubscription;
        private IDisposable actionSubscription;
        private IDisposable noiseSubscription;
        private IDisposable reportSubscription;
        private void Start()
        {
            PrimitiveWorld.Build(transform);
            directory = StoreLayout.Build(transform);
            BuildKeyMaps();
            fusion = gameObject.AddComponent<FusionSession>();
            network = fusion;
            if (!Application.isBatchMode) fusion.ConnectHost();
            var session = new LocalSession();
            authority = new LocalMatchAuthority(rules.CreateRules(), session);
            authority.TryBeginNight();
            authority.Lighting.Changed += LightingPresenter.Apply;
            LightingPresenter.Apply(authority.Lighting.Mode);
            audioSubscription = authority.Audio.Subscribe(cue => { if (logAudio) log.Write("AUDIO", cue.ToString()); });
            reportSubscription = authority.Reports.Subscribe(OnReport);
            for (int i = 0; i < rules.mannequinPlayers; i++)
                pawns.Add(CreatePawn(session, new Vector3(-5.4f + (i % 4) * 0.9f, 0.1f, 12.4f - (i / 4) * 0.8f)));
            var zone = PrimitiveWorld.Box(transform, "Clothing placement zone", new Vector3(-4.2f, 0.15f, 7.0f), new Vector3(3, 0.3f, 3), Color.green);
            zone.AddComponent<PlacementZone>();
            var itemData = ScriptableObject.CreateInstance<ItemDefinition>(); itemData.canBreak = true;
            for (int i = 0; i < 3; i++)
            {
                var box = PrimitiveWorld.Box(transform, "Collectible crate", new Vector3(-2 + i * 2, 0.5f, -8), Vector3.one * 0.6f, Color.yellow);
                box.layer = 3;
                box.AddComponent<PhysicalItem>().Configure(itemData, signals);
            }
            var shirtData = ScriptableObject.CreateInstance<ItemDefinition>(); shirtData.id = "shirt";
            shirtData.displayName = "Shirt"; shirtData.missionTag = "shirt"; shirtData.inventoryOnly = true; shirtData.slotCost = 1;
            foreach (float x in new[] { -6.2f, -2.4f })
            {
                var shirt = PrimitiveWorld.Box(transform, "Shirt", new Vector3(x, 1.0f, 9.0f), new Vector3(0.4f, 0.08f, 0.32f), new Color(0.2f, 0.35f, 0.7f));
                shirt.layer = 3;
                shirt.AddComponent<PhysicalItem>().Configure(shirtData, signals);
            }
            var keyData = ScriptableObject.CreateInstance<ItemDefinition>(); keyData.id = "employee-key";
            keyData.displayName = "Employee key"; keyData.inventoryOnly = true; keyData.slotCost = 0;
            var key = PrimitiveWorld.Box(transform, "Employee key", new Vector3(9, 0.5f, -10), Vector3.one * 0.3f, Color.cyan);
            key.layer = 3;
            key.AddComponent<PhysicalItem>().Configure(keyData, signals);
            doors.Add(CreateDoor("Employee door", new Vector3(10, 1.3f, 5), "employee-key", Color.blue));
            var rescue = PrimitiveWorld.Box(transform, "Rescue console", new Vector3(-11.1f, 0.7f, 6.6f), new Vector3(0.8f, 1.2f, 0.5f), new Color(0.1f, 0.8f, 0.4f));
            rescue.AddComponent<RescueInteractable>().Configure(authority);
            var exit = PrimitiveWorld.Box(transform, "Escape door", new Vector3(5, 1.3f, -14.6f), new Vector3(1.4f, 2.4f, 0.3f), new Color(0.8f, 0.2f, 0.7f));
            exit.AddComponent<EscapeInteractable>().Configure(authority, "");
            var terminal = PrimitiveWorld.Box(transform, "Surveillance terminal", new Vector3(-12, 1f, 12.4f), new Vector3(0.8f, 0.6f, 0.4f), Color.black);
            terminal.transform.GetComponent<Renderer>().material.color = new Color(0.1f, 0.9f, 0.5f);
            missions = new MissionSystem(rules.missions, signals, InventoryFor);
            actionSubscription = signals.Actions.Subscribe(action =>
            {
                if (action.Kind == ActionKind.Unlock) authority.Audio.Publish(AudioCue.Door);
                if (action.Kind == ActionKind.Break) authority.Audio.Publish(AudioCue.Break);
            });
            noiseSubscription = signals.Noise.Subscribe(noise => authority.Surveillance.ReportNoise(
                new NoiseSighting(new MapPoint(noise.Position.x, noise.Position.y, noise.Position.z), noise.Loudness, noise.Source, noise.Timestamp)));
            var surface = gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            var guard = GameObject.CreatePrimitive(PrimitiveType.Capsule); guard.name = "Guard";
            guard.transform.SetParent(transform); guard.transform.position = new Vector3(3, 1, -5);
            guard.layer = 2; guard.transform.rotation = Quaternion.Euler(0, 180, 0);
            var flashlight = guard.AddComponent<GuardFlashlight>();
            flashlight.Configure(rules.flashlightRange, rules.flashlightCone);
            guardController = guard.AddComponent<GuardController>();
            guardController.Flashlight = flashlight;
            guardController.Configure(rules, signals, new[] { new Vector3(3, 0, -8), new Vector3(10, 0, 0), new Vector3(3, 0, 8), new Vector3(-10, 0, 0) });
            guardController.InvestigateReports(authority.Reports);
            guardInput = guard.AddComponent<PlayerInputReader>(); guardInput.Active = false;
            guardView = guard.AddComponent<PlayerView>(); guardView.ConfigureStandalone(rules.lookSensitivity, 0.6f);
            guardView.Active = false; guardView.View.gameObject.SetActive(false);
            guardVisual = CharacterVisual.Attach(guard.transform, CharacterProfile.Guard);
            if (guardVisual != null) guard.GetComponent<MeshRenderer>().enabled = false;
            foreach (var pawn in pawns)
            {
                pawn.Detection = new DetectionCoordinator(pawn.Motor, guard.transform, rules) { Flashlight = flashlight };
                pawn.Pose.Configure(pawn.Motor, pawn.Detection.Detection);
            }
            StoreDressing.Apply(transform, authority.Lighting.Mode);
            population = gameObject.AddComponent<CustomerPopulationManager>();
            population.Configure(directory, NpcPopulationSettings.Load(), authority.TryReport, () => Time.timeAsDouble, network.IsAuthority, Environment.TickCount);
            population.Populate();
            var debugHost = new GameObject("NPC debug");
            debugHost.transform.SetParent(transform, false);
            npcDebug = debugHost.AddComponent<NpcDebugOverlay>();
            npcDebug.Configure(population);
            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Configure();
            hud.PagePicked = page => menuPage = page;
            SetActive(0);
            Cursor.lockState = CursorLockMode.Locked;
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
            var bodyObstacle = actor.AddComponent<NavMeshObstacle>();
            bodyObstacle.shape = NavMeshObstacleShape.Capsule; bodyObstacle.radius = 0.3f; bodyObstacle.height = 1.8f;
            bodyObstacle.center = new Vector3(0, 0.9f, 0); bodyObstacle.carving = false;
            authority.Register(record);
            int index = pawns.Count;
            var visual = CharacterVisual.Attach(actor.transform, CharacterProfile.MannequinFor(index));
            bool warehoused = false;
            record.Changed += state =>
            {
                if (state == PlayerState.Captured || state == PlayerState.Surveillance) warehoused = true;
                else if (state == PlayerState.Normal && warehoused)
                {
                    warehoused = false;
                    motor.Teleport(new Vector3(-1 + index * 2f, 0.1f, -9));
                }
            };
            return new Pawn { Motor = motor, Input = input, View = view, Probe = probe, Inventory = inventory, Pose = pose, Visual = visual };
        }
        private DoorInteractable CreateDoor(string label, Vector3 position, string key, Color color)
        {
            var door = PrimitiveWorld.Box(transform, label, position, new Vector3(2, 2.6f, 0.3f), color);
            var interactable = door.AddComponent<DoorInteractable>();
            interactable.Configure(key, signals);
            door.AddComponent<NavMeshObstacle>().carving = true;
            return interactable;
        }
        private PlayerInventory InventoryFor(string id)
        {
            for (int i = 0; i < pawns.Count; i++) if (pawns[i].Motor.Record.Id == id) return pawns[i].Inventory;
            return null;
        }
        /// <summary>
        /// Authoritative simulation step: player commands, detection, guard AI, and the match clock. Only the
        /// state authority runs it; remote clients will receive the results instead (see networking.md).
        /// </summary>
        private void FixedUpdate()
        {
            if (authority == null || pawns.Count == 0 || !network.IsAuthority) return;
            float delta = Time.fixedDeltaTime;
            var motors = new PlayerMotor[pawns.Count];
            var vision = new DetectionSystem[pawns.Count];
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var command = pawn.Input.Consume();
                if (authority.Flow.Phase != MatchPhase.Night) command = default;
                var before = pawn.Detection == null ? DetectionState.Green : pawn.Detection.Detection.State;
                int suspicion = pawn.Detection == null ? 0 : pawn.Detection.Detection.Suspicion.Value;
                pawn.Motor.Simulate(command, delta);
                if (pawn.Detection != null)
                {
                    pawn.Detection.Tick(delta);
                    var now = pawn.Detection.Detection.State;
                    if (now != before)
                    {
                        if (now == DetectionState.Orange) authority.Audio.Publish(AudioCue.DetectionWarning);
                        else if (now == DetectionState.Red) authority.Audio.Publish(AudioCue.DetectionRed);
                        else if (now == DetectionState.Discovered) authority.Audio.Publish(AudioCue.Discovery);
                    }
                    if (pawn.Detection.Detection.Suspicion.Value > suspicion) authority.Audio.Publish(AudioCue.SuspicionIncreased);
                    if (now == DetectionState.Discovered && pawn.Motor.Record.Free)
                    {
                        var carry = pawn.Motor.GetComponent<CarrySystem>();
                        if (carry != null) carry.Release(false);
                        if (authority.TryCapture(pawn.Motor.Record.Id))
                            pawn.Motor.Teleport(new Vector3(-13.2f + authority.Warehouse.Count * 0.9f, 0.1f, 11));
                    }
                    pawn.Pose.Tick();
                }
                motors[i] = pawn.Motor; vision[i] = pawn.Detection != null ? pawn.Detection.Detection : null;
            }
            if (guardController.PlayerDriven)
            {
                var command = guardInput.Consume();
                if (authority.Flow.Phase != MatchPhase.Night) command = default;
                guardController.Drive(command.Move, command.Sprint ? rules.sprintSpeed : rules.walkSpeed, delta);
            }
            else guardController.TickGroup(motors, vision, delta);
            if (missions != null && missions.Complete && !missionsAnnounced)
            { missionsAnnounced = true; authority.Audio.Publish(AudioCue.MissionComplete); }
            authority.MissionsComplete = () => missions != null && missions.Complete;
            RefreshSurveillance();
            authority.Tick(delta);
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
            var players = new List<PlayerSighting>(pawns.Count);
            for (int i = 0; i < pawns.Count; i++)
            {
                Vector3 position = pawns[i].Motor.transform.position;
                players.Add(new PlayerSighting(pawns[i].Motor.Record.Id, pawns[i].Motor.Record.State, new MapPoint(position.x, position.y, position.z)));
            }
            var doorFacts = new List<DoorSighting>(doors.Count);
            for (int i = 0; i < doors.Count; i++)
                doorFacts.Add(new DoorSighting(doors[i].DoorId, doors[i].Lock.Open, doors[i].Lock.Locked));
            var objectives = new List<ObjectiveSighting>();
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
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || authority == null) return;
            if (keyboard.tabKey.wasPressedThisFrame) ToggleMenu(help ? (MenuPage?)null : MenuPage.Night);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (help) ToggleMenu(null);
                else Cursor.lockState = CursorLockMode.None;
            }
            if (help)
            {
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
                testing.Poll(keyboard);
            }
            if (hud != null)
            {
                FillHud();
                hud.Show(hudModel);
            }
        }
        private void ToggleMenu(MenuPage? page)
        {
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
        private void ToggleSurveillance()
        {
            if (Player == null) return;
            if (Player.Record.State == PlayerState.Surveillance)
                authority.TryLeaveSurveillance(Player.Record.Id, Player.Record.Id);
            else OpenCameras();
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
            }
            bool driving = active == pawns.Count;
            if (guardController != null) guardController.PlayerDriven = driving;
            if (guardInput != null) guardInput.Active = driving;
            if (guardView != null) { guardView.Active = driving; guardView.View.gameObject.SetActive(driving); }
            if (guardVisual != null) guardVisual.SetFirstPerson(driving);
        }
        private string BuildStatus()
        {
            if (active == pawns.Count) return HudText.GuardStatus(authority, guardController.Brain.State);
            var pawn = pawns[active];
            var target = pawn.Motor.GetComponent<PerceptionTarget>();
            var crowd = population != null && target != null ? population.StrongestAwarenessOf(target) : AwarenessState.Unaware;
            return HudText.MannequinStatus(authority, active + 1, pawn.Motor.Record,
                pawn.Detection != null ? pawn.Detection.Detection : null, missions != null ? missions.Missions : null, pawn.Inventory, crowd)
                + (fusion != null ? "\nPhoton " + fusion.Status : "");
        }
        private string BuildPrompt()
        {
            if (help) return "";
            if (Cursor.lockState != CursorLockMode.Locked && authority.Flow.Phase == MatchPhase.Night) return "Click to play";
            if (active == pawns.Count) return "";
            var pawn = pawns[active];
            if (pawn.Motor.Record.State == PlayerState.Captured) return "Captured. V opens the cameras. A teammate can free you at the warehouse panel.";
            if (pawn.Motor.Record.State == PlayerState.Surveillance) return "Watching the live cameras. Tab opens the map. A teammate can free you at the warehouse panel.";
            return pawn.Probe.Prompt;
        }
        private void OnReport(SuspiciousActivityEvent report)
        {
            reportLog.Add(report);
            if (reportLog.Count > 8) reportLog.RemoveAt(0);
            reportToast = HudText.ReportLine(report);
            reportToastUntil = Time.unscaledTime + 4.5f;
        }
        private void FillHud()
        {
            hudModel.ClearLists();
            hudModel.MenuOpen = help;
            hudModel.Page = menuPage;
            hudModel.Clock = HudText.Clock(authority);
            hudModel.Paused = authority.Clock.Paused;
            hudModel.Phase = authority.Flow.Phase.ToString();
            hudModel.Prompt = BuildPrompt();
            hudModel.Toast = Time.unscaledTime < reportToastUntil ? reportToast : "";
            hudModel.Reports = reportLog.Count;
            hudModel.CameraNote = "This is the store map. Live dots appear after you are captured.";
            hudModel.LiveCameras = false;
            if (active == pawns.Count)
            {
                hudModel.Role = "Guard";
                hudModel.BodyState = guardController.Brain.State.ToString();
                hudModel.Detection = "—";
                hudModel.DetectionTint = "#9FB3C8";
                hudModel.Crowd = "Watching the floor";
                hudModel.CrowdTint = "#9FB3C8";
            }
            else
            {
                var pawn = pawns[active];
                var target = pawn.Motor.GetComponent<PerceptionTarget>();
                var crowd = population != null && target != null ? population.StrongestAwarenessOf(target) : AwarenessState.Unaware;
                hudModel.Role = "Mannequin " + (active + 1);
                hudModel.BodyState = pawn.Motor.Record.State.ToString();
                if (pawn.Detection != null)
                {
                    hudModel.Detection = pawn.Detection.Detection.State.ToString().ToUpperInvariant();
                    hudModel.DetectionTint = HudText.DetectionColor(pawn.Detection.Detection.State);
                }
                else { hudModel.Detection = "CLEAR"; hudModel.DetectionTint = "#7CE38B"; }
                hudModel.Crowd = HudText.CrowdPlain(crowd);
                hudModel.CrowdTint = HudText.CrowdTint(crowd);
                if (missions != null)
                    foreach (var mission in missions.Missions)
                        hudModel.Missions.Add(new HudLine(mission.Rule.Title, mission.Progress + " / " + mission.Rule.Quantity, null, mission.Complete, mission.Failed));
                if (pawn.Inventory != null)
                {
                    hudModel.SlotsUsed = pawn.Inventory.Items.UsedSlots;
                    hudModel.SlotsMax = pawn.Inventory.Items.Capacity;
                    foreach (var pair in pawn.Inventory.Items.Snapshot())
                        hudModel.Items.Add(new HudLine(HudText.ItemLabel(pair.Key), "x" + pair.Value));
                }
                bool live = pawn.Motor.Record.State == PlayerState.Captured || pawn.Motor.Record.State == PlayerState.Surveillance;
                if (live && authority.TryReadSurveillance(pawn.Motor.Record.Id, pawn.Motor.Record.Id, out var view))
                {
                    hudModel.LiveCameras = true;
                    hudModel.CameraNote = "Live security feed. White is a mannequin. Gold is a shopper. Red is the guard.";
                    foreach (var entity in view.Entities)
                    {
                        string tint = entity.Kind == EntityKind.Guard ? "#FF4B4B" : entity.Kind == EntityKind.Customer ? "#F4C15D"
                            : entity.Kind == EntityKind.Employee ? "#B07CFF" : "#F4EFE6";
                        hudModel.Marks.Add(new MapMark(entity.Position.X, entity.Position.Z, tint));
                    }
                    foreach (var alert in view.Alerts)
                        hudModel.Marks.Add(new MapMark(alert.LastKnownPosition.X, alert.LastKnownPosition.Z, "#FFE27A", "report"));
                }
            }
            for (int i = 0; i < reportLog.Count; i++)
            {
                var report = reportLog[reportLog.Count - 1 - i];
                hudModel.ReportLines.Add(new HudLine(HudText.ReportLine(report), "Last seen near " + HudText.ZoneLabel(report.Zone)));
            }
            foreach (var entry in controls.Entries)
                hudModel.Controls.Add(new HudLine(entry.Label, entry.Description));
            foreach (var entry in testing.Entries)
                hudModel.Testing.Add(new HudLine(entry.Label, entry.Description));
        }
        /// <summary>Letters and numbers only: Mac keyboards send F1-F12 as media keys and lack Home/End/Delete.</summary>
        private void BuildKeyMaps()
        {
            controls = new KeyCommandMap("CONTROLS")
                .Describe("WASD", "move").Describe("Mouse", "look").Describe("Shift", "run").Describe("Space", "jump")
                .Describe("E", "use / pick up").Describe("G / Q", "drop / throw")
                .Describe("Tab", "open the briefing menu")
                .Bind(Key.V, "cameras and store map", OpenCameras)
                .Bind(Key.H, "open controls", () => ToggleMenu(MenuPage.Controls))
                .Bind(Key.T, "guard flashlight", ToggleFlashlight)
                .Describe("Esc", "close menu / free the mouse");
            testing = new KeyCommandMap("TESTING")
                .Bind(Key.LeftBracket, "previous mannequin", () => SetActive((active + pawns.Count) % (pawns.Count + 1)), "[")
                .Bind(Key.RightBracket, "next mannequin / guard", () => SetActive((active + 1) % (pawns.Count + 1)), "]")
                .Bind(Key.C, "capture me", CaptureActive)
                .Bind(Key.R, "rescue captured players", RescueFromDebug)
                .Bind(Key.M, "complete all missions", () => ForEachMission(m => m.DebugComplete()))
                .Bind(Key.N, "fail a mission", FailOneMission)
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
            if (Player != null && authority.TryCapture(Player.Record.Id))
                Player.Teleport(new Vector3(-13.2f + authority.Warehouse.Count * 0.9f, 0.1f, 11));
        }
        private void ForEachMission(Action<MissionTracker> action)
        {
            if (missions == null) return;
            for (int i = 0; i < missions.Missions.Count; i++) action(missions.Missions[i]);
        }
        private void FailOneMission()
        {
            if (missions == null) return;
            for (int i = 0; i < missions.Missions.Count; i++) if (!missions.Missions[i].Complete) { missions.Missions[i].Fail(); return; }
        }
        private void OnGUI()
        {
            if (authority == null || pawns.Count == 0 || (hud != null && hud.Ready)) return;
            GUI.Label(new Rect(16, 16, 520, 400), BuildStatus() + "\n" + BuildPrompt());
        }
        private void OnDestroy()
        {
            missions?.Dispose(); audioSubscription?.Dispose(); actionSubscription?.Dispose(); noiseSubscription?.Dispose();
            reportSubscription?.Dispose();
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
            public DetectionCoordinator Detection;
            public CharacterVisual Visual;
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
