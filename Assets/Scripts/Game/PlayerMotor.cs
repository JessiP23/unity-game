using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public readonly struct PlayerCommand
    {
        public readonly Vector2 Move;
        public readonly bool Sprint, Jump;
        public PlayerCommand(Vector2 move, bool sprint, bool jump) { Move = move; Sprint = sprint; Jump = jump; }
    }
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public PlayerRecord Record { get; private set; }
        public float ActualSpeed { get; private set; }
        public GameRulesAsset Rules { get; private set; }
        private CharacterController controller;
        private float vertical;
        public void Configure(GameRulesAsset rules, PlayerRecord record)
        {
            Rules = rules; Record = record; controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = new Vector3(0, 0.9f, 0);
            controller.minMoveDistance = 0; controller.stepOffset = 0.25f;
        }
        /// <summary>Simulates one authoritative step; detection reads actual displacement.</summary>
        public void Simulate(PlayerCommand input, float delta)
        {
            if (Record == null || !Record.Free) { ActualSpeed = 0; return; }
            GameRules.RequireDelta(delta); if (delta == 0) return;
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1);
            if (controller.isGrounded && vertical < 0) vertical = -2;
            if (input.Jump && controller.isGrounded) vertical = Mathf.Sqrt(2 * Rules.gravity * Rules.jumpHeight);
            vertical -= Rules.gravity * delta;
            Vector3 desired = transform.TransformDirection(new Vector3(move.x, 0, move.y));
            desired *= input.Sprint ? Rules.sprintSpeed : Rules.walkSpeed;
            desired.y = vertical;
            Vector3 before = transform.position;
            controller.Move(desired * delta);
            ActualSpeed = (transform.position - before).magnitude / delta;
            if (Record.State != PlayerState.Carrying)
                Record.SetState(!controller.isGrounded ? PlayerState.Jumping : input.Sprint && move.sqrMagnitude > 0 ? PlayerState.Running : PlayerState.Normal);
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var item = hit.collider.GetComponent<PhysicalItem>();
            if (item != null && Mathf.Abs(hit.moveDirection.y) < 0.5f)
                item.Push(hit.moveDirection * 0.5f, this);
        }
        public void Teleport(Vector3 position)
        {
            controller.enabled = false; transform.position = position; controller.enabled = true;
            vertical = 0; ActualSpeed = 0;
        }
    }
}
