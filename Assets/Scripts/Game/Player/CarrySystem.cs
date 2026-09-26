using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    public sealed class CarrySystem : MonoBehaviour
    {
        public PhysicalItem Held { get; private set; }
        private PlayerMotor player;
        public void Configure(PlayerMotor motor) { player = motor; }
        public bool TryHold(PhysicalItem item)
        {
            if (Held != null || item.Holder != null || !player.Record.Free) return false;
            Held = item; item.Holder = this; item.Body.isKinematic = true;
            item.GetComponent<Collider>().enabled = false;
            item.transform.SetParent(transform, true); item.transform.localPosition = new Vector3(0, 1.1f, 1.1f);
            player.Record.SetState(PlayerState.Carrying); return true;
        }
        public bool Release(bool throwing)
        {
            if (Held == null || (throwing && !Held.Definition.canThrow)) return false;
            var item = Held; Held = null; item.Holder = null; item.transform.SetParent(null, true);
            // Keep the object on the player's side of nearby walls.
            Vector3 origin = transform.position + Vector3.up;
            Vector3 destination = origin + transform.forward;
            if (Physics.SphereCast(origin, 0.35f, transform.forward, out var hit, 1, ~(1 << 2), QueryTriggerInteraction.Ignore))
                destination = origin + transform.forward * Mathf.Max(0, hit.distance - 0.05f);
            item.transform.position = destination;
            item.GetComponent<Collider>().enabled = true; item.Body.isKinematic = false;
            if (throwing) item.Body.AddForce(transform.forward * player.Rules.throwForce, ForceMode.VelocityChange);
            if (player.Record.Free) player.Record.SetState(PlayerState.Normal);
            return true;
        }
        private void Update()
        {
            var view = GetComponent<PlayerView>();
            if (view == null || !view.Active || !player.Record.Free || Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (Keyboard.current.gKey.wasPressedThisFrame) Release(false);
            if (Keyboard.current.qKey.wasPressedThisFrame) Release(true);
        }
        private void OnDestroy() { if (Held != null) Release(false); }
    }
}
