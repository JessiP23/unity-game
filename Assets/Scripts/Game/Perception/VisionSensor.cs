using NightSupermarket.Core;
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

    /// <summary>
    /// Shared eyes for every observer type: cheap distance and field-of-view rejection first, then at most two
    /// line-of-sight rays (chest, then head). Guards, customers, and employees differ only in the range and angle they pass in;
    /// what an observer makes of a sighting is decided elsewhere.
    /// </summary>
    public class VisionSensor
    {
        private readonly float range, halfFov;
        private readonly int mask;
        /// <summary>Someone this close is noticed well outside the focused cone. Zero disables it.</summary>
        public float CloseRange;
        public float CloseFieldOfView = 190f;
        public float Range => range;
        public float FieldOfView => halfFov * 2;
        public int LineTests { get; private set; }
        public VisionSensor(float distance, float fieldOfView, int obstacleMask)
        { range = distance; halfFov = fieldOfView / 2; mask = obstacleMask; }
        public VisionResult CanSee(Vector3 eye, Vector3 forward, Vector3 target, Transform targetRoot = null)
        {
            Vector3 direction = target - eye; float distance = direction.magnitude;
            if (distance > range) return new VisionResult(false, false, distance, 180);
            float angle = HorizontalAngle(forward, direction);
            float limit = halfFov;
            if (CloseRange > 0 && distance <= CloseRange) limit = Mathf.Max(halfFov, CloseFieldOfView * 0.5f);
            if (angle > limit) return new VisionResult(false, false, distance, angle);
            bool clear = ClearLine(eye, target, targetRoot);
            if (!clear)
                clear = ClearLine(eye, target + Vector3.up * 0.45f, targetRoot);
            return new VisionResult(clear, clear, distance, angle);
        }
        // Field of view describes a horizontal cone. A chest below eye level must not
        // disappear merely because the observer is close to a standing mannequin.
        private static float HorizontalAngle(Vector3 forward, Vector3 direction)
        {
            forward.y = 0; direction.y = 0;
            return direction.sqrMagnitude < 0.000001f ? 0 : Vector3.Angle(forward, direction);
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
        public bool Observed(Vector3 eye, Vector3 forward, Vector3 target, Transform targetRoot, FlashlightModel lamp)
        {
            if (CanSee(eye, forward, target, targetRoot).Visible) return true;
            if (lamp == null || !lamp.Enabled) return false;
            Vector3 direction = target - eye; float distance = direction.magnitude;
            float angle = distance <= 0.001f ? 0 : Vector3.Angle(forward, direction);
            return lamp.Covers(distance, angle) && ClearLine(eye, target, targetRoot);
        }
    }
}
