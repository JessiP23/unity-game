using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        public DoorLock Lock { get; private set; }
        public string DoorId => id;
        private WorldSignals signals;
        private Vector3 closedPosition;
        private readonly string id = Guid.NewGuid().ToString("N");
        public string Prompt => Lock.Locked ? "E — unlock (employee key required)" : "E — open/close door";
        public void Configure(string key, WorldSignals events)
        { Lock = new DoorLock(key); signals = events; closedPosition = transform.position; }
        public bool TryInteract(PlayerMotor player)
        {
            if (!InteractionValidation.CanReach(player, transform)) return false;
            if (Lock.Locked)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                if (inventory == null || !Lock.TryUnlock(inventory.Items, false)) return false;
                signals.Actions.Publish(new ObjectAction(ActionKind.Unlock, id, "door", player.Record.Id));
            }
            if (!Lock.TryToggle()) return false;
            transform.rotation = Quaternion.Euler(0, Lock.Open ? 90 : 0, 0);
            signals.Noise.Publish(new NoiseEvent(closedPosition, 0.5f, id)); return true;
        }
    }
}
