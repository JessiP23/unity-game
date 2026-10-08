using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    public interface IInteractable
    {
        string Prompt { get; }
        bool TryInteract(PlayerMotor player);
    }
    public interface IPlayerPrompt { string PromptFor(PlayerMotor player); }
    public sealed class InteractionProbe : MonoBehaviour
    {
        private PlayerMotor motor;
        private PlayerView view;
        public string Prompt { get; private set; } = "";
        public void Configure(PlayerMotor player, PlayerView cameraView) { motor = player; view = cameraView; }
        private readonly RaycastHit[] hits = new RaycastHit[16];
        public IInteractable FindTarget(Vector3 origin, Vector3 forward)
        {
            int mask = ~(1 << 2);
            if (Physics.Raycast(origin, forward, out var direct, motor.Rules.interactionDistance, mask, QueryTriggerInteraction.Ignore))
            {
                var exact = direct.collider.GetComponentInParent<IInteractable>();
                if (exact != null && InteractionValidation.CanReach(motor, direct.collider.transform)) return exact;
            }
            // A small aim tolerance helps thin folded shirts without allowing use through walls.
            int count = Physics.SphereCastNonAlloc(origin, 0.16f, forward, hits, motor.Rules.interactionDistance, mask, QueryTriggerInteraction.Ignore);
            IInteractable closest = null; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null || hits[i].distance >= distance) continue;
                var target = (candidate as Component).transform;
                if (!InteractionValidation.CanReach(motor, target)) continue;
                closest = candidate; distance = hits[i].distance;
            }
            return closest;
        }
        private void Update()
        {
            Prompt = "";
            if (!view.Active || (!motor.Record.Free && !motor.Record.InBackroom)) return;
            var target = FindTarget(view.View.transform.position, view.View.transform.forward);
            if (target == null) return;
            var carry = motor.GetComponent<CarrySystem>();
            bool handsFull = carry != null && carry.Held != null && target is PhysicalItem item && item.Holder == null;
            Prompt = handsFull ? "Hands full. Press G, then E." : target is IPlayerPrompt contextual ? contextual.PromptFor(motor) : target.Prompt;
            if (!handsFull && Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                target.TryInteract(motor);
        }
    }
}
