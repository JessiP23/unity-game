using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    public sealed class PlayerView : MonoBehaviour
    {
        public Camera View { get; private set; }
        public bool Active { get; set; } = true;
        private float pitch;
        private PlayerMotor motor;
        public void Configure(PlayerMotor player)
        {
            motor = player;
            var cameraObject = new GameObject("Player View", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false); cameraObject.transform.localPosition = Vector3.up * 1.6f;
            View = cameraObject.GetComponent<Camera>(); View.nearClipPlane = 0.05f; View.fieldOfView = 75;
        }
        private void Update()
        {
            if (!Active || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = Mouse.current.delta.ReadValue() * motor.Rules.lookSensitivity;
            transform.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -80, 80);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
