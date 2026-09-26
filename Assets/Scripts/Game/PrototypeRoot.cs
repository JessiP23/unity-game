using System;
using System.Collections.Generic;
using System.Text;
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
        public PlayerMotor Player => pawns.Count > 0 ? pawns[active].Motor : null;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly List<DoorInteractable> doors = new List<DoorInteractable>();
        private readonly WorldSignals signals = new WorldSignals();
        private readonly GameLog log = new GameLog(true);
        private LocalMatchAuthority authority;
        private MissionSystem missions;
        private GuardController guardController;
        private PrototypeHud hud;
        private int active;
        private bool help = true;
        private bool logAudio = true;
        private bool missionsAnnounced;
        private IDisposable audioSubscription;
        private IDisposable actionSubscription;
        private IDisposable noiseSubscription;
        private void Start()
        {
            PrimitiveWorld.Build(transform);
            var session = new LocalSession();
            authority = new LocalMatchAuthority(rules.CreateRules(), session);
            authority.TryBeginNight();
            authority.Lighting.Changed += LightingPresenter.Apply;
            LightingPresenter.Apply(authority.Lighting.Mode);
            audioSubscription = authority.Audio.Subscribe(cue => { if (logAudio) log.Write("AUDIO", cue.ToString()); });
            for (int i = 0; i < 2; i++) pawns.Add(CreatePawn(session, new Vector3(i * 1.6f, 0.1f, -11)));
            var zone = PrimitiveWorld.Box(transform, "Clothing placement zone", new Vector3(-4, 0.15f, -7), new Vector3(3, 0.3f, 3), Color.green);
            zone.AddComponent<PlacementZone>();
            var itemData = ScriptableObject.CreateInstance<ItemDefinition>(); itemData.canBreak = true;
            for (int i = 0; i < 3; i++)
            {
                var box = PrimitiveWorld.Box(transform, "Collectible crate", new Vector3(-2 + i * 2, 0.5f, -8), Vector3.one * 0.6f, Color.yellow);
                box.layer = 3;
                box.AddComponent<PhysicalItem>().Configure(itemData, signals);
            }
            var keyData = ScriptableObject.CreateInstance<ItemDefinition>(); keyData.id = "employee-key";
            keyData.displayName = "Employee key"; keyData.inventoryOnly = true; keyData.slotCost = 0;
            var key = PrimitiveWorld.Box(transform, "Employee key", new Vector3(9, 0.5f, -10), Vector3.one * 0.3f, Color.cyan);
            key.layer = 3;
            key.AddComponent<PhysicalItem>().Configure(keyData, signals);
            doors.Add(CreateDoor("Employee door", new Vector3(10, 1.3f, 5), "employee-key", Color.blue));
            var rescue = PrimitiveWorld.Box(transform, "Rescue console", new Vector3(-11.1f, 0.7f, 6.6f), new Vector3(0.8f, 1.2f, 0.5f), new Color(0.1f, 0.8f, 0.4f));
            rescue.AddComponent<RescueInteractable>().Configure(authority);
            var exit = PrimitiveWorld.Box(transform, "Escape door", new Vector3(5, 1.3f, -13), new Vector3(1.4f, 2.4f, 0.3f), new Color(0.8f, 0.2f, 0.7f));
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
            foreach (var pawn in pawns)
            {
                pawn.Detection = new DetectionCoordinator(pawn.Motor, guard.transform, rules) { Flashlight = flashlight };
                pawn.Pose.Configure(pawn.Motor, pawn.Detection.Detection);
            }
            hud = gameObject.AddComponent<PrototypeHud>(); hud.Configure();
            SetActive(0);
            Cursor.lockState = CursorLockMode.Locked;
        }
        private Pawn CreatePawn(LocalSession session, Vector3 position)
        {
            var actor = new GameObject("Mannequin"); actor.transform.SetParent(transform);
            actor.layer = 2; actor.transform.position = position;
            var record = new PlayerRecord(session.Join());
            var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, record);
            actor.AddComponent<CarrySystem>().Configure(motor);
            var inventory = actor.AddComponent<PlayerInventory>(); inventory.Configure(rules.inventoryCapacity);
            var input = actor.AddComponent<PlayerInputReader>();
            var view = actor.AddComponent<PlayerView>(); view.Configure(motor);
            var probe = actor.AddComponent<InteractionProbe>(); probe.Configure(motor, view);
            var pose = actor.AddComponent<PoseDriver>();
            authority.Register(record);
            int index = pawns.Count;
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
            return new Pawn { Motor = motor, Input = input, View = view, Probe = probe, Inventory = inventory, Pose = pose };
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
        private void FixedUpdate()
        {
            if (authority == null || pawns.Count == 0) return;
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
            guardController.TickGroup(motors, vision, delta);
            if (missions != null && missions.Complete && !missionsAnnounced)
            { missionsAnnounced = true; authority.Audio.Publish(AudioCue.MissionComplete); }
            authority.MissionsComplete = () => missions != null && missions.Complete;
            RefreshSurveillance();
            authority.Tick(delta);
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
            authority.Surveillance.ReportDoors(doorFacts);
            authority.Surveillance.ReportObjectives(objectives);
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || authority == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && authority.Flow.Phase == MatchPhase.Night)
                Cursor.lockState = CursorLockMode.Locked;
            if (keyboard.tabKey.wasPressedThisFrame) SetActive((active + 1) % pawns.Count);
            if (keyboard.vKey.wasPressedThisFrame) ToggleSurveillance();
            if (keyboard.f1Key.wasPressedThisFrame) help = !help;
            if (keyboard.f2Key.wasPressedThisFrame && authority.TryCapture(Player.Record.Id))
                Player.Teleport(new Vector3(-13.2f + authority.Warehouse.Count * 0.9f, 0.1f, 11));
            if (keyboard.f3Key.wasPressedThisFrame) RescueFromDebug();
            if (keyboard.f4Key.wasPressedThisFrame && missions != null)
                for (int i = 0; i < missions.Missions.Count; i++) missions.Missions[i].DebugComplete();
            if (keyboard.f5Key.wasPressedThisFrame && missions != null)
                for (int i = 0; i < missions.Missions.Count; i++) if (!missions.Missions[i].Complete) { missions.Missions[i].Fail(); break; }
            if (keyboard.f6Key.wasPressedThisFrame) CycleDetection();
            if (keyboard.f7Key.wasPressedThisFrame) AddSuspicion();
            if (keyboard.f8Key.wasPressedThisFrame) authority.Clock.Paused = !authority.Clock.Paused;
            if (keyboard.f9Key.wasPressedThisFrame) authority.DebugAdvance(60);
            if (keyboard.f10Key.wasPressedThisFrame) CycleLighting();
            if (keyboard.f11Key.wasPressedThisFrame) CycleGuard();
            if (keyboard.f12Key.wasPressedThisFrame) guardController.DebugVision = !guardController.DebugVision;
            if (keyboard.tKey.wasPressedThisFrame && guardController.Flashlight != null)
                guardController.Flashlight.SetEnabled(!guardController.Flashlight.Model.Enabled);
            if (keyboard.digit1Key.wasPressedThisFrame) SpawnCrate();
            if (keyboard.homeKey.wasPressedThisFrame) authority.DebugAdvance(authority.Clock.Remaining);
            if (keyboard.endKey.wasPressedThisFrame) authority.DebugForce(MatchPhase.Victory);
            if (keyboard.deleteKey.wasPressedThisFrame) authority.DebugForce(MatchPhase.Defeat);
            if (hud != null) hud.Show(BuildHud());
        }
        private void ToggleSurveillance()
        {
            string id = Player.Record.Id;
            if (Player.Record.State == PlayerState.Captured) authority.TryEnterSurveillance(id, id);
            else if (Player.Record.State == PlayerState.Surveillance) authority.TryLeaveSurveillance(id, id);
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
            }
        }
        private string BuildHud()
        {
            var pawn = pawns[active];
            var builder = new StringBuilder();
            int seconds = Mathf.CeilToInt((float)authority.Clock.Remaining);
            builder.Append("NIGHT SUPERMARKET  ").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"));
            if (authority.Clock.Paused) builder.Append("  PAUSED");
            builder.Append("\nPhase ").Append(authority.Flow.Phase).Append("  Player ").Append(active + 1).Append("  ").Append(pawn.Motor.Record.State);
            builder.Append("  Pose ").Append(pawn.Pose.Pose).Append('\n');
            if (pawn.Detection != null)
                builder.Append("Detection ").Append(pawn.Detection.Detection.State).Append("   Suspicion ").Append(pawn.Detection.Detection.Suspicion.Value).Append('\n');
            builder.Append(pawn.Probe.Prompt).Append('\n');
            if (missions != null)
                for (int i = 0; i < missions.Missions.Count; i++)
                {
                    var mission = missions.Missions[i];
                    builder.Append(mission.Rule.Title).Append("  ").Append(mission.Progress).Append('/').Append(mission.Rule.Quantity);
                    if (mission.Failed) builder.Append("  FAILED");
                    builder.Append('\n');
                }
            builder.Append("Inventory ");
            foreach (var pair in pawn.Inventory.Items.Snapshot()) builder.Append(pair.Key).Append('x').Append(pair.Value).Append(' ');
            builder.Append("\nWarehouse ").Append(authority.Warehouse.Count).Append("   Escaped ").Append(authority.Escapes.Count);
            builder.Append("   Guard ").Append(guardController.Brain.State).Append("   Light ").Append(authority.Lighting.Mode).Append('\n');
            if (pawn.Motor.Record.State == PlayerState.Surveillance && authority.TryReadSurveillance(pawn.Motor.Record.Id, pawn.Motor.Record.Id, out var view))
            {
                builder.Append("SURVEILLANCE  guard ").Append(view.GuardState).Append(" @ ").Append(view.GuardPosition.X.ToString("0")).Append(',').Append(view.GuardPosition.Z.ToString("0")).Append('\n');
                for (int i = 0; i < view.Players.Count; i++)
                    builder.Append("man ").Append(i + 1).Append(' ').Append(view.Players[i].State).Append('\n');
            }
            if (help) builder.Append("Tab switch  V surveillance  F1 help  F2 capture  F3 rescue  F4 complete  F5 fail\nF6 detection  F7 suspicion  F8 pause  F9 +60s  F10 light  F11 guard  Home dawn  End win  Del lose");
            return builder.ToString();
        }
        private void OnGUI()
        {
            if (authority == null || pawns.Count == 0 || (hud != null && hud.Ready)) return;
            GUI.Label(new Rect(16, 16, 900, 500), BuildHud());
        }
        private void OnDestroy()
        {
            missions?.Dispose(); audioSubscription?.Dispose(); actionSubscription?.Dispose(); noiseSubscription?.Dispose();
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
        }
    }
    public static class LightingPresenter
    {
        public static void Apply(LightingMode mode)
        {
            RenderSettings.ambientLight = mode switch
            {
                LightingMode.Dark => new Color(0.04f, 0.04f, 0.06f),
                LightingMode.Emergency => new Color(0.35f, 0.05f, 0.05f),
                LightingMode.Partial => new Color(0.12f, 0.12f, 0.1f),
                LightingMode.Dawn => new Color(0.75f, 0.55f, 0.38f),
                _ => new Color(0.35f, 0.35f, 0.4f)
            };
        }
    }
}
