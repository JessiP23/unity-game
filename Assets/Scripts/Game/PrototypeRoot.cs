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
        private void Start()
        {
            PrimitiveWorld.Build(transform);
            var actor = new GameObject("Mannequin"); actor.transform.SetParent(transform);
            actor.transform.position = new Vector3(0, 0.1f, -11);
            Player = actor.AddComponent<PlayerMotor>(); Player.Configure(rules, new PlayerRecord(new LocalSession().Join()));
            input = actor.AddComponent<PlayerInputReader>();
            var view = actor.AddComponent<PlayerView>(); view.Configure(Player);
            interaction = actor.AddComponent<InteractionProbe>(); interaction.Configure(Player, view);
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
            GUI.Label(new Rect(15, 15, 700, 70), "NIGHT SUPERMARKET — PRIMITIVE PROTOTYPE\nWASD move · Shift sprint · Space jump · Mouse look · E interact · Esc release mouse");
            if (detection != null) GUI.Label(new Rect(15, 120, 500, 30), $"Detection: {detection.Detection.State} | Suspicion: {detection.Detection.Suspicion.Value}");
            if (interaction != null) GUI.Label(new Rect(15, 85, 500, 30), interaction.Prompt);
        }
        private void OnDestroy() { Cursor.lockState = CursorLockMode.None; }
    }
}
