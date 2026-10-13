using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class EscapeInteractable : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        private string requiredKey = "";
        /// <summary>Set when a shutter has come down over this door; the prompt then says where to go instead.</summary>
        public bool Blocked { get; set; }
        public string BlockedPrompt { get; set; } = "Shutter down. Find another way out.";
        public string Prompt => Blocked ? BlockedPrompt : string.IsNullOrEmpty(requiredKey) ? "E — escape" : "E — escape (key required)";
        public void Configure(LocalMatchAuthority match, string key) { authority = match; requiredKey = key ?? ""; }
        /// <summary>The door most recently tried; read while the escape resolves (the night ends inside TryEscape).</summary>
        public static EscapeInteractable Last { get; private set; }
        public static void ResetLast() => Last = null;
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || Blocked || !InteractionValidation.CanReach(player, transform)) return false;
            var inventory = player.GetComponent<PlayerInventory>();
            Last = this;
            return authority.TryEscape(player.Record.Id, player.Record.Id, true, inventory != null ? inventory.Items : null, requiredKey);
        }
    }
}
