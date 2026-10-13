using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>A deliberate pose at a clothing display, cancelled by movement or full hands.</summary>
    public sealed class DisplayPose : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        private LocalMatchAuthority authority;
        public void Configure(LocalMatchAuthority match) { authority = match; }
        public string Prompt => "E — pose: wear clothes + empty hands + stand still";
        public string PromptFor(PlayerMotor player)
        {
            if (player.GetComponent<DisplayCamouflage>()?.Active == true) return "DISPLAY POSE ACTIVE · move to leave";
            bool dressed = player.GetComponent<PlayerInventory>()?.Items.Count("shirt") > 0 || DisplayCamouflage.Dressed != null && DisplayCamouflage.Dressed(player);
            if (!dressed) return "Need a shirt or clothes first: E on any Clothing rack or table to try something on";
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
