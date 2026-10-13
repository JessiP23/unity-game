using UnityEngine;
namespace NightSupermarket.Game
{
    public static class InteractionValidation
    {
        public static bool CanReach(PlayerMotor player, Transform target)
        {
            // A captive in the back hall is not Free, but the hall's doors, gaps, figures and tags are its only way out.
            if (player == null || player.Record == null || (!player.Record.Free && !player.Record.InBackroom)) return false;
            Vector3 origin = player.transform.position + Vector3.up;
            Vector3 delta = target.position - origin;
            if (delta.magnitude > player.Rules.interactionDistance) return false;
            if (!Physics.Raycast(origin, delta.normalized, out var hit, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore)
                || hit.transform == target || hit.transform.IsChildOf(target)) return true;
            // Dressing hung on a piece of furniture (the jackets on a rack, the shirts on a table) is part of it:
            // a ray stopped by something inside the target's own box still reached it.
            var body = target.GetComponent<Collider>();
            if (body == null) return false;
            var box = body.bounds; box.Expand(0.3f);
            return box.Contains(hit.point);
        }
    }
}
