using UnityEngine;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Produces the <see cref="PlayerCommand"/> the authority simulates. Locally it reads the keyboard; a
    /// network transport (or a test) calls <see cref="Submit"/> instead, feeding the same simulation path.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public bool Active { get; set; } = true;
        /// <summary>Right mouse held: the body freezes into the selected pose. Read by the root, not the motor.</summary>
        public bool PoseHeld => ForcePose || (Active && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && Mouse.current.rightButton.isPressed);
        /// <summary>Holds the pose without the mouse (scripted tests).</summary>
        public bool ForcePose { get; set; }
        private bool jump;
        private PlayerCommand? submitted;
        /// <summary>Supplies the next tick's command from a remote client or test; takes priority over the keyboard.</summary>
        public void Submit(PlayerCommand command) => submitted = command;
        private void Update() { if (Active && Keyboard.current != null) jump |= Keyboard.current.spaceKey.wasPressedThisFrame; }
        public PlayerCommand Consume()
        {
            if (submitted.HasValue) { var remote = submitted.Value; submitted = null; return remote; }
            var keyboard = Keyboard.current;
            if (!Active || keyboard == null || Cursor.lockState != CursorLockMode.Locked)
            {
                jump = false;
                // Standing still, but a held crouch stays a crouch (unhiding because the cursor came loose would be cruel).
                bool held = Active && keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || keyboard.leftAltKey.isPressed);
                return new PlayerCommand(Vector2.zero, false, false, held);
            }
            Vector2 move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            // Hold Ctrl (or Option/Alt) to crouch. Never Cmd, which the Mac reserves; Q is already throw.
            bool crouch = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || keyboard.leftAltKey.isPressed;
            var command = new PlayerCommand(move, keyboard.leftShiftKey.isPressed, jump, crouch); jump = false; return command;
        }
    }
}
