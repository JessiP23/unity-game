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
        private bool jump;
        private PlayerCommand? submitted;
        /// <summary>Supplies the next tick's command from a remote client or test; takes priority over the keyboard.</summary>
        public void Submit(PlayerCommand command) => submitted = command;
        private void Update() { if (Active && Keyboard.current != null) jump |= Keyboard.current.spaceKey.wasPressedThisFrame; }
        public PlayerCommand Consume()
        {
            if (submitted.HasValue) { var remote = submitted.Value; submitted = null; return remote; }
            var keyboard = Keyboard.current;
            if (!Active || keyboard == null || Cursor.lockState != CursorLockMode.Locked) { jump = false; return default; }
            Vector2 move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            var command = new PlayerCommand(move, keyboard.leftShiftKey.isPressed, jump); jump = false; return command;
        }
    }
}
