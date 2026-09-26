using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class RescueInteractable : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        public string Prompt => "E — rescue warehoused mannequin";
        public void Configure(LocalMatchAuthority match) { authority = match; }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || !InteractionValidation.CanReach(player, transform)) return false;
            return authority.TryRescueGroup(player.Record.Id, player.Record.Id) > 0;
        }
    }
}
