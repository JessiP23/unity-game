using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class EscapeInteractable : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        private string requiredKey = "";
        public string Prompt => string.IsNullOrEmpty(requiredKey) ? "E — escape" : "E — escape (key required)";
        public void Configure(LocalMatchAuthority match, string key) { authority = match; requiredKey = key ?? ""; }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || !InteractionValidation.CanReach(player, transform)) return false;
            var inventory = player.GetComponent<PlayerInventory>();
            return authority.TryEscape(player.Record.Id, player.Record.Id, true, inventory != null ? inventory.Items : null, requiredKey);
        }
    }
}
