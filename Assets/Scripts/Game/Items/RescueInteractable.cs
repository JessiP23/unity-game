using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class RescueInteractable : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        public string Prompt => authority != null && authority.Warehouse.Count > 0 ? "E — rescue teammate · use the bell to draw the guard away" : "No teammates in warehouse";
        public void Configure(LocalMatchAuthority match) { authority = match; }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || !InteractionValidation.CanReach(player, transform)) return false;
            return authority.TryRescueGroup(player.Record.Id, player.Record.Id) > 0;
        }
    }
}
