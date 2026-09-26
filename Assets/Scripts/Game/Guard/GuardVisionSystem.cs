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
        public int LineTests { get; private set; }
        public GuardVisionSystem(float distance, float fieldOfView, int obstacleMask)
        { range = distance; halfFov = fieldOfView / 2; mask = obstacleMask; }
        public VisionResult CanSee(Vector3 eye, Vector3 forward, Vector3 target, Transform targetRoot = null)
        {
            Vector3 direction = target - eye; float distance = direction.magnitude;
            if (distance > range) return new VisionResult(false, false, distance, 180);
            float angle = Vector3.Angle(forward, direction);
            if (angle > halfFov) return new VisionResult(false, false, distance, angle);
            bool clear = ClearLine(eye, target, targetRoot);
            return new VisionResult(clear, clear, distance, angle);
        }
        public void ResetLineTests() => LineTests = 0;
        public bool ClearLine(Vector3 eye, Vector3 target, Transform targetRoot)
        {
            LineTests++;
            Vector3 direction = target - eye; float distance = direction.magnitude;
            if (distance <= 0.05f) return true;
            if (!Physics.Raycast(eye, direction.normalized, out var hit, distance, mask, QueryTriggerInteraction.Ignore)) return true;
            return targetRoot != null && (hit.transform == targetRoot || hit.transform.IsChildOf(targetRoot));
        }
        /// <summary>Eyes first, then an optional gameplay flashlight cone. Both still require line of sight.</summary>
        public bool Observed(Vector3 eye, Vector3 forward, Vector3 target, Transform targetRoot, NightSupermarket.Core.FlashlightModel lamp)
        {
            if (CanSee(eye, forward, target, targetRoot).Visible) return true;
            if (lamp == null || !lamp.Enabled) return false;
            Vector3 direction = target - eye; float distance = direction.magnitude;
            float angle = distance <= 0.001f ? 0 : Vector3.Angle(forward, direction);
            return lamp.Covers(distance, angle) && ClearLine(eye, target, targetRoot);
        }
    }
}
