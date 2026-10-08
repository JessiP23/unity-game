using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Hands an inventory item in: E with the item removes it and counts the job.</summary>
    public sealed class ReturnPoint : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        public string ItemId = "toy";
        public string ItemName = "the lost toy";
        public string Where = "Lost & Found";
        public bool Complete { get; private set; }
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private ActionKind kind;
        private string actionTagId, destination;

        public void Configure(LocalMatchAuthority match, WorldSignals world, ActionKind actionKind, string actionTag, string actionDestination)
        { authority = match; signals = world; kind = actionKind; actionTagId = actionTag; destination = actionDestination; }

        public string Prompt => Complete ? Where + " · returned" : "E — hand in " + ItemName;
        public string PromptFor(PlayerMotor player)
        {
            if (Complete) return Where + " · returned";
            var inventory = player.GetComponent<PlayerInventory>();
            return inventory != null && inventory.Items.Count(ItemId) > 0 ? "E — hand in " + ItemName : Where + " · bring " + ItemName + " here";
        }

        public bool TryInteract(PlayerMotor player)
        {
            if (Complete || authority == null || authority.Phase != MatchPhase.Night || !InteractionValidation.CanReach(player, transform)) return false;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || inventory.Items.Count(ItemId) == 0) return false;
            Complete = true;
            // Publish while the item is still in the bag: the mission's required-item check runs inside Publish.
            signals.Actions.Publish(new ObjectAction(kind, name + GetInstanceID(), actionTagId, player.Record.Id, destination));
            inventory.Items.TryRemove(ItemId, 1);
            authority.Audio.Publish(AudioCue.MissionComplete);
            return true;
        }
    }
}
