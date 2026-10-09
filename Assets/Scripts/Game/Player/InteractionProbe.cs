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
            // A small aim tolerance helps thin folded shirts without allowing use through walls; wider when the camera is not the eyes.
            float tolerance = view != null && view.Effective != ViewMode.FirstPerson ? 0.32f : 0.16f;
            int count = Physics.SphereCastNonAlloc(origin, tolerance, forward, hits, motor.Rules.interactionDistance, mask, QueryTriggerInteraction.Ignore);
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
        /// <summary>
        /// Where the look-ray starts and points. In first person it is the camera. Over the shoulder or in
        /// the front (pose) view the camera is metres from the body, so the ray starts at the body's eyes
        /// and points where the body faces, tilted down a little to catch things on the floor.
        /// </summary>
        public void Aim(out Vector3 origin, out Vector3 forward)
        {
            if (view.Effective == ViewMode.FirstPerson)
            {
                origin = view.View.transform.position; forward = view.View.transform.forward;
                return;
            }
            origin = motor.transform.position + Vector3.up * 1.45f;
            forward = (motor.transform.forward + Vector3.down * 0.35f).normalized;
        }

        private void Update()
        {
            Prompt = "";
            if (!view.Active || (!motor.Record.Free && !motor.Record.InBackroom)) return;
            Aim(out var origin, out var forward);
            var target = FindTarget(origin, forward);
            if (target == null) return;
            var carry = motor.GetComponent<CarrySystem>();
            bool handsFull = carry != null && carry.Held != null && target is PhysicalItem item && item.Holder == null;
            Prompt = handsFull ? "Hands full. Press G, then E." : target is IPlayerPrompt contextual ? contextual.PromptFor(motor) : target.Prompt;
            if (!handsFull && Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                target.TryInteract(motor);
        }
    }
}
