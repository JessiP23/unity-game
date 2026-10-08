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
    /// <summary>
    /// Authoritative character movement, stepped from FixedUpdate. Velocity eases toward the
    /// commanded speed so starts and stops feel physical; detection reads the resulting displacement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public PlayerRecord Record { get; private set; }
        public float ActualSpeed { get; private set; }
        public Vector3 PlanarVelocity => planar;
        public bool Grounded => controller != null && controller.isGrounded;
        public GameRulesAsset Rules { get; private set; }
        private CharacterController controller;
        private Vector3 planar;
        private float vertical;
        public void Configure(GameRulesAsset rules, PlayerRecord record)
        {
            Rules = rules; Record = record; controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = new Vector3(0, 0.9f, 0);
            controller.minMoveDistance = 0; controller.stepOffset = 0.25f; controller.skinWidth = 0.04f;
        }
        /// <summary>Simulates one authoritative step; detection reads actual displacement.</summary>
        public void Simulate(PlayerCommand input, float delta)
        {
            bool pocket = Record != null && Record.InBackroom;
            if (Record == null || (!Record.Free && !pocket)) { ActualSpeed = 0; planar = Vector3.zero; return; }
            if (pocket) input = new PlayerCommand(input.Move, false, false);
            GameRules.RequireDelta(delta); if (delta == 0) return;
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1);
            bool grounded = controller.isGrounded;
            if (grounded && vertical < 0) vertical = -2;
            if (input.Jump && grounded) vertical = Mathf.Sqrt(2 * Rules.gravity * Rules.jumpHeight);
            vertical -= Rules.gravity * delta;
            Vector3 target = transform.TransformDirection(new Vector3(move.x, 0, move.y)) * (input.Sprint ? Rules.sprintSpeed : Rules.walkSpeed);
            float rate = target.sqrMagnitude > planar.sqrMagnitude ? Rules.acceleration : Rules.deceleration;
            if (!grounded) rate *= Rules.airControl;
            planar = Vector3.MoveTowards(planar, target, rate * delta);
            Vector3 before = transform.position;
            controller.Move(new Vector3(planar.x, vertical, planar.z) * delta);
            Vector3 moved = (transform.position - before) / delta;
            ActualSpeed = moved.magnitude;
            var actualPlanar = new Vector3(moved.x, 0, moved.z);
            if (actualPlanar.sqrMagnitude < planar.sqrMagnitude) planar = actualPlanar;
            if (!pocket && Record.State != PlayerState.Carrying)
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
            vertical = 0; ActualSpeed = 0; planar = Vector3.zero;
            var interpolator = GetComponent<MotionInterpolator>();
            if (interpolator != null) interpolator.Snap();
        }
    }
}
