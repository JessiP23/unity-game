using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    public interface IInteractable
    {
        string Prompt { get; }
        bool TryInteract(PlayerMotor player);
    }
    public sealed class InteractionProbe : MonoBehaviour
    {
        private PlayerMotor motor;
        private PlayerView view;
        public string Prompt { get; private set; } = "";
        public void Configure(PlayerMotor player, PlayerView cameraView) { motor = player; view = cameraView; }
        private void Update()
        {
            Prompt = "";
            if (!view.Active || !motor.Record.Free) return;
            if (Physics.Raycast(view.View.transform.position, view.View.transform.forward, out var hit,
                motor.Rules.interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (target == null) return;
                Prompt = target.Prompt;
                if (Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    target.TryInteract(motor);
            }
        }
    }
}
