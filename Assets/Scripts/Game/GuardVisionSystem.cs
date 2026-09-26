using UnityEngine;
namespace NightSupermarket.Game
{
    public readonly struct VisionResult
    {
        public readonly bool Visible, LineOfSight;
        public readonly float Distance, Angle;
        public VisionResult(bool visible, bool lineOfSight, float distance, float angle)
        { Visible = visible; LineOfSight = lineOfSight; Distance = distance; Angle = angle; }
    }
    public sealed class GuardVisionSystem
    {
        private readonly float range, halfFov;
        private readonly int mask;
        public GuardVisionSystem(float distance, float fieldOfView, int obstacleMask)
        { range = distance; halfFov = fieldOfView / 2; mask = obstacleMask; }
        public VisionResult CanSee(Vector3 eye, Vector3 forward, Vector3 target, Transform targetRoot = null)
        {
            Vector3 direction = target - eye; float distance = direction.magnitude;
            if (distance > range) return new VisionResult(false, false, distance, 180);
            float angle = Vector3.Angle(forward, direction);
            if (angle > halfFov) return new VisionResult(false, false, distance, angle);
            bool clear = !Physics.Raycast(eye, direction.normalized, out var hit, distance, mask, QueryTriggerInteraction.Ignore)
                || (targetRoot != null && hit.transform.IsChildOf(targetRoot));
            return new VisionResult(clear, clear, distance, angle);
        }
    }
}
