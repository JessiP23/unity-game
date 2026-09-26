using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    /// <summary>Scene composition and simulation scheduling; rules live in dedicated services.</summary>
    public sealed class PrototypeRoot : MonoBehaviour
    {
        public GameRulesAsset rules;
        public PlayerMotor Player { get; private set; }
        private PlayerInputReader input;
        private InteractionProbe interaction;
        private DetectionCoordinator detection;
        private MissionSystem missions;
        private readonly WorldSignals signals = new WorldSignals();
        private void Start()
        {
            PrimitiveWorld.Build(transform);
            var actor = new GameObject("Mannequin"); actor.transform.SetParent(transform);
            actor.transform.position = new Vector3(0, 0.1f, -11);
            Player = actor.AddComponent<PlayerMotor>(); Player.Configure(rules, new PlayerRecord(new LocalSession().Join()));
            actor.AddComponent<CarrySystem>().Configure(Player);
            actor.AddComponent<PlayerInventory>().Configure(rules.inventoryCapacity);
            input = actor.AddComponent<PlayerInputReader>();
            var view = actor.AddComponent<PlayerView>(); view.Configure(Player);
            interaction = actor.AddComponent<InteractionProbe>(); interaction.Configure(Player, view);
            missions = new MissionSystem(rules.missions, signals, id => id == Player.Record.Id ? Player.GetComponent<PlayerInventory>() : null);
            var zone = PrimitiveWorld.Box(transform, "Clothing placement zone", new Vector3(-10, 0.15f, -7), new Vector3(3, 0.3f, 3), Color.green);
            zone.AddComponent<PlacementZone>();
            var itemData = ScriptableObject.CreateInstance<ItemDefinition>(); itemData.canBreak = true;
            for (int i = 0; i < 3; i++)
            {
                var box = PrimitiveWorld.Box(transform, "Collectible crate", new Vector3(-2 + i * 2, 0.5f, -8), Vector3.one * 0.6f, Color.yellow);
                box.AddComponent<PhysicalItem>().Configure(itemData, signals);
            }
            var keyData = ScriptableObject.CreateInstance<ItemDefinition>(); keyData.id = "employee-key";
            keyData.displayName = "Employee key"; keyData.inventoryOnly = true; keyData.slotCost = 0;
            var key = PrimitiveWorld.Box(transform, "Employee key", new Vector3(9, 0.5f, -10), Vector3.one * 0.3f, Color.cyan);
            key.AddComponent<PhysicalItem>().Configure(keyData, signals);
            var door = PrimitiveWorld.Box(transform, "Employee door", new Vector3(10, 1.3f, 5), new Vector3(2, 2.6f, 0.3f), Color.blue);
            door.AddComponent<DoorInteractable>().Configure("employee-key", signals);
            var guard = GameObject.CreatePrimitive(PrimitiveType.Capsule); guard.name = "Guard";
            guard.transform.SetParent(transform); guard.transform.position = new Vector3(3, 1, -5);
            guard.layer = 2; guard.transform.rotation = Quaternion.Euler(0, 180, 0);
            detection = new DetectionCoordinator(Player, guard.transform, rules);
            Cursor.lockState = CursorLockMode.Locked;
        }
        private void FixedUpdate() { if (Player != null) { Player.Simulate(input.Consume(), Time.fixedDeltaTime); detection.Tick(Time.fixedDeltaTime); } }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(15, 15, 700, 70), "NIGHT SUPERMARKET — PRIMITIVE PROTOTYPE\nWASD move · Shift sprint · Space jump · Mouse look · E interact · G drop · Q throw · Esc release mouse");
            if (detection != null) GUI.Label(new Rect(15, 120, 500, 30), $"Detection: {detection.Detection.State} | Suspicion: {detection.Detection.Suspicion.Value}");
            if (missions != null)
            {
                int y = 155;
                foreach (var mission in missions.Missions) { GUI.Label(new Rect(15, y, 700, 25), $"{mission.Rule.Title}: {mission.Progress}/{mission.Rule.Quantity}"); y += 25; }
            }
            if (interaction != null) GUI.Label(new Rect(15, 85, 500, 30), interaction.Prompt);
        }
        private void OnDestroy() { missions?.Dispose(); Cursor.lockState = CursorLockMode.None; }
    }
}
