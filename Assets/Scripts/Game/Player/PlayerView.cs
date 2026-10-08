using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
namespace NightSupermarket.Game
{
    /// <summary>
    /// First-person camera for any body. Mouse yaw turns the body, pitch tilts the camera. The camera
    /// follows the interpolated body position so fixed-step movement renders smoothly, with a light
    /// head bob and a sprint FOV kick derived from how fast the body actually moves.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float BaseFieldOfView = 62f;
        public Camera View { get; private set; }
        public bool Active { get; set; } = true;
        [Tooltip("Vertical head bob in metres at walking pace. Zero disables it.")]
        public float bobAmount = 0.028f;
        [Tooltip("Speed above which the camera widens as if sprinting.")]
        public float sprintFovSpeed = 4f;
        private float pitch, sensitivity, eyeHeight, bobPhase, bobWeight, speed;
        private Vector3 lastPosition;
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
            View.transform.position = body + Vector3.up * (eyeHeight + bob) + transform.right * sway;
            View.transform.localRotation = Quaternion.Euler(pitch, 0, Mathf.Cos(bobPhase * 0.5f) * 0.35f * bobWeight);
            float fov = BaseFieldOfView + (speed > sprintFovSpeed ? 6f : 0f);
            View.fieldOfView = Mathf.Lerp(View.fieldOfView, fov, 1f - Mathf.Exp(-6f * delta));
        }
    }
}
