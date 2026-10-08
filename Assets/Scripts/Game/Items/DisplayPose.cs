using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>A deliberate pose at a clothing display, cancelled by movement or full hands.</summary>
    public sealed class DisplayPose : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        private LocalMatchAuthority authority;
        public void Configure(LocalMatchAuthority match) { authority = match; }
        public string Prompt => "E — pose: shirt in bag + empty hands + stand still";
        public string PromptFor(PlayerMotor player)
        {
            if (player.GetComponent<DisplayCamouflage>()?.Active == true) return "DISPLAY POSE ACTIVE · move to leave";
            if (player.GetComponent<PlayerInventory>()?.Items.Count("shirt") == 0) return "Need a shirt: E on a marked blue shirt on the Clothing tables";
            if (player.GetComponent<CarrySystem>()?.Held != null) return "G — put your crate down before posing";
            if (!player.Grounded || player.ActualSpeed > player.Rules.movementThreshold) return "Stand still, then E to pose";
            return "E — blend into this display · +2s guard doubt";
        }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || authority.Phase != MatchPhase.Night || !InteractionValidation.CanReach(player, transform)) return false;
            var pose = player.GetComponent<DisplayCamouflage>();
            return pose != null && pose.TryPose(this);
        }
    }
}
