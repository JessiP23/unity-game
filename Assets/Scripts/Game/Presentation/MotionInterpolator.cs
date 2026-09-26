using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Smooths a transform that is moved in FixedUpdate so cameras and bodies render at the display
    /// rate. Records the pose after every simulation step and blends the last two. Presentation only:
    /// gameplay keeps reading the real transform.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class MotionInterpolator : MonoBehaviour
    {
        [Tooltip("Jumps larger than this are treated as teleports and not blended.")]
        public float teleportDistance = 1.5f;
        /// <summary>When false, <see cref="Position"/> follows the transform directly (for bodies moved in Update).</summary>
        public bool Interpolating { get; set; } = true;
        private Vector3 previous, current;
        public Vector3 Position
        {
            get
            {
                if (!Interpolating || !isActiveAndEnabled) return transform.position;
                float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
                return Vector3.Lerp(previous, current, alpha);
            }
        }
        public void Snap() => previous = current = transform.position;
        private void OnEnable() => Snap();
        private void FixedUpdate()
        {
            previous = current;
            current = transform.position;
            if ((current - previous).sqrMagnitude > teleportDistance * teleportDistance) previous = current;
        }
    }
}
