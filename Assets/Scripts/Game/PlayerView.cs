using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
namespace NightSupermarket.Game
{
    public sealed class PlayerView : MonoBehaviour
    {
        public Camera View { get; private set; }
        public bool Active { get; set; } = true;
        private float pitch;
        private float sensitivity;
        public void Configure(PlayerMotor player) { sensitivity = player.Rules.lookSensitivity; EnsureCamera(1.6f); }
        public void ConfigureStandalone(float lookSensitivity, float eyeHeight) { sensitivity = lookSensitivity; EnsureCamera(eyeHeight); }
        private void EnsureCamera(float eyeHeight)
        {
            if (View != null) return;
            var cameraObject = new GameObject("Player View", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false); cameraObject.transform.localPosition = Vector3.up * eyeHeight;
            View = cameraObject.GetComponent<Camera>(); View.nearClipPlane = 0.05f; View.fieldOfView = 75;
            View.farClipPlane = 80; View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = Color.black;
            var data = View.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
        private void Update()
        {
            if (!Active || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = Mouse.current.delta.ReadValue() * sensitivity;
            transform.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -80, 80);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
