using UnityEngine;
namespace NightSupermarket.Game
{
    public static class InteractionValidation
    {
        public static bool CanReach(PlayerMotor player, Transform target)
        {
            if (player == null || player.Record == null || !player.Record.Free) return false;
            Vector3 origin = player.transform.position + Vector3.up;
            Vector3 delta = target.position - origin;
            if (delta.magnitude > player.Rules.interactionDistance) return false;
            return !Physics.Raycast(origin, delta.normalized, out var hit, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore)
                || hit.transform == target || hit.transform.IsChildOf(target);
        }
    }
}
