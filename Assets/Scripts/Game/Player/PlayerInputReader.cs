using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public bool Active { get; set; } = true;
        private bool jump;
        private void Update() { if (Active && Keyboard.current != null) jump |= Keyboard.current.spaceKey.wasPressedThisFrame; }
        public PlayerCommand Consume()
        {
            var keyboard = Keyboard.current;
            if (!Active || keyboard == null || Cursor.lockState != CursorLockMode.Locked) { jump = false; return default; }
            Vector2 move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            var command = new PlayerCommand(move, keyboard.leftShiftKey.isPressed, jump); jump = false; return command;
        }
    }
}
