using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
namespace NightSupermarket.Game
{
    public enum ViewMode { FirstPerson, Shoulder, Front }

    /// <summary>
    /// Camera for any body. Mouse yaw turns the body, pitch tilts the view. Three modes: first person
    /// (eyes), over the shoulder (you see your own body and the aisle ahead) and front (the camera
    /// stands in front of you, looking back, so you can see the pose you are holding). The camera
    /// never clips through shelves: a sphere cast pulls it in when a wall is in the way. The camera
    /// follows the interpolated body position so fixed-step movement renders smoothly.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float BaseFieldOfView = 62f;
        public Camera View { get; private set; }
        public bool Active { get; set; } = true;
        public ViewMode Mode { get; set; } = ViewMode.FirstPerson;
        /// <summary>While true the view swings to the front mode even if Mode is first person.</summary>
        public bool ShowPose { get; set; }
        public ViewMode Effective => ShowPose && Mode == ViewMode.FirstPerson ? ViewMode.Front : Mode;
        [Tooltip("Vertical head bob in metres at walking pace. Zero disables it.")]
        public float bobAmount = 0.028f;
        [Tooltip("Speed above which the camera widens as if sprinting.")]
        public float sprintFovSpeed = 4f;
        private float pitch, sensitivity, eyeHeight, bobPhase, bobWeight, speed;
        private Vector3 lastPosition, smoothedCamera;
        private Quaternion smoothedLook = Quaternion.identity;
        private bool snapped;
        private MotionInterpolator interpolator;
        public void Configure(PlayerMotor player) => Configure(player.Rules.lookSensitivity, 1.6f);
        public void ConfigureStandalone(float lookSensitivity, float eye) => Configure(lookSensitivity, eye);
        private void Configure(float lookSensitivity, float eye)
        {
            sensitivity = lookSensitivity; eyeHeight = eye;
            interpolator = GetComponent<MotionInterpolator>();
            if (interpolator == null) interpolator = gameObject.AddComponent<MotionInterpolator>();
            if (View != null) return;
            var cameraObject = new GameObject("Player View", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false); cameraObject.transform.localPosition = Vector3.up * eyeHeight;
            View = cameraObject.GetComponent<Camera>(); View.nearClipPlane = 0.05f; View.fieldOfView = BaseFieldOfView;
            View.farClipPlane = 80; View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = Color.black;
            var data = View.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            lastPosition = transform.position;
        }
        private void Update()
        {
            if (!Active || View == null || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = Mouse.current.delta.ReadValue() * sensitivity;
            transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch - look.y, -85, 85);
        }
        private void LateUpdate()
        {
            if (View == null) return;
            float delta = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 body = interpolator != null ? interpolator.Position : transform.position;
            Vector3 moved = body - lastPosition; moved.y = 0;
            lastPosition = body;
            speed = Mathf.Lerp(speed, moved.magnitude > 2f ? 0f : moved.magnitude / delta, 1f - Mathf.Exp(-12f * delta));
            bobWeight = Mathf.MoveTowards(bobWeight, speed > 0.3f ? 1f : 0f, delta * 4f);
            bobPhase += delta * Mathf.Lerp(7f, 11f, Mathf.InverseLerp(2f, 5f, speed)) * Mathf.Clamp01(speed / 1.5f);
            float bob = Mathf.Sin(bobPhase) * bobAmount * bobWeight * Mathf.Clamp(speed / 3f, 0.4f, 1.4f);
            float sway = Mathf.Cos(bobPhase * 0.5f) * bobAmount * 0.6f * bobWeight;
            Vector3 eyes = body + Vector3.up * (eyeHeight + bob) + transform.right * sway;
            float fov = BaseFieldOfView + (speed > sprintFovSpeed ? 6f : 0f);
            var mode = Effective;
            if (mode == ViewMode.FirstPerson)
            {
                View.transform.position = eyes;
                View.transform.rotation = transform.rotation * Quaternion.Euler(pitch, 0, Mathf.Cos(bobPhase * 0.5f) * 0.35f * bobWeight);
                snapped = false;
            }
            else
            {
                // Orbit around a pivot at chest height. Shoulder: behind and a little to the right, looking
                // where the body looks. Front: a mirror in front of you, looking back at the body.
                Vector3 pivot = body + Vector3.up * (eyeHeight - 0.25f);
                Quaternion orbit = transform.rotation * Quaternion.Euler(pitch * 0.6f, 0, 0);
                Vector3 wanted, lookAt;
                if (mode == ViewMode.Shoulder)
                {
                    wanted = pivot + orbit * new Vector3(0.55f, 0.35f, -2.4f);
                    lookAt = pivot + orbit * new Vector3(0.2f, 0.1f, 4f);
                }
                else
                {
                    wanted = pivot + transform.rotation * new Vector3(0.9f, 0.25f, 2.6f);
                    lookAt = body + Vector3.up * (eyeHeight * 0.6f);
                }
                // Pull the camera in front of anything between the pivot and where it wants to be.
                Vector3 toCamera = wanted - pivot;
                if (Physics.SphereCast(pivot, 0.22f, toCamera.normalized, out var hit, toCamera.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    wanted = pivot + toCamera.normalized * Mathf.Max(0.35f, hit.distance - 0.05f);
                var look = Quaternion.LookRotation(lookAt - wanted, Vector3.up);
                if (!snapped) { smoothedCamera = wanted; smoothedLook = look; snapped = true; }
                float follow = 1f - Mathf.Exp(-14f * delta);
                smoothedCamera = Vector3.Lerp(smoothedCamera, wanted, follow);
                smoothedLook = Quaternion.Slerp(smoothedLook, look, follow);
                View.transform.position = smoothedCamera;
                View.transform.rotation = smoothedLook;
                fov = mode == ViewMode.Front ? 50f : BaseFieldOfView;
            }
            View.fieldOfView = Mathf.Lerp(View.fieldOfView, fov, 1f - Mathf.Exp(-6f * delta));
        }
    }
}
