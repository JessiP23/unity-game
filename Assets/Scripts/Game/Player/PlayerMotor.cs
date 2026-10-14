using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public readonly struct PlayerCommand
    {
        public readonly Vector2 Move;
        public readonly bool Sprint, Jump, Crouch;
        public PlayerCommand(Vector2 move, bool sprint, bool jump, bool crouch = false) { Move = move; Sprint = sprint; Jump = jump; Crouch = crouch; }
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
        public bool Grounded => Mounted || (controller != null && controller.enabled && controller.isGrounded);
        public GameRulesAsset Rules { get; private set; }
        /// <summary>Walking with a group, you fall into step: walking speed is held to theirs (sprinting ignores it).</summary>
        public float PaceCap { get; set; } = float.PositiveInfinity;
        /// <summary>Crouched: lower (counters and sofas hide you), slower, quiet. Stands back up only where there is headroom.</summary>
        public bool Crouched { get; private set; }
        public const float StandHeight = 1.8f, CrouchHeight = 1.15f, CrouchPace = 0.45f;
        /// <summary>The moving floor under the feet this step, if any.</summary>
        public MovingSurface Surface { get; private set; }
        /// <summary>The carousel saddle you sit on, or null. Mounted, the body goes where the horse goes.</summary>
        public Transform Seat { get; private set; }
        public bool Mounted => Seat != null;
        /// <summary>What you are sitting on (a horse, a sofa, a bench), or null.</summary>
        public SeatSpot SeatOwner { get; private set; }
        /// <summary>From a carousel saddle down to the body's root while riding (a chair's own height for furniture).</summary>
        public const float SaddleDrop = 0.95f;
        private MovingSurface seatSurface;
        private float seatDrop;
        private CharacterController controller;
        private MovingSurface SurfaceUnder()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<MovingSurface>() : null;
        }
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
            if (Seat != null && (Record == null || !Record.Free)) { if (Record == null) LeaveSeat(); else Dismount(); } // caught while seated: put down beside the seat, never inside the horse
            if (Record == null || (!Record.Free && !pocket)) { ActualSpeed = 0; planar = Vector3.zero; return; }
            if (Seat != null)
            {
                // A move key or jump climbs down; otherwise ride: the body is wherever the saddle is, turned with the ride.
                if (input.Move.sqrMagnitude > 0.04f || input.Jump) { Dismount(); return; }
                float yaw = seatSurface != null ? seatSurface.YawDelta(delta) : 0f;
                if (Mathf.Abs(yaw) > 0.0001f) transform.Rotate(0f, yaw, 0f, Space.World);
                transform.position = Seat.position - Vector3.up * seatDrop;
                Surface = seatSurface;
                ActualSpeed = 0f; planar = Vector3.zero; vertical = 0f;
                if (Record.State != PlayerState.Carrying) Record.SetState(PlayerState.Normal);
                return;
            }
            if (pocket) input = new PlayerCommand(input.Move, false, false);
            GameRules.RequireDelta(delta); if (delta == 0) return;
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1);
            SetCrouch(input.Crouch && !pocket);
            if (Crouched) input = new PlayerCommand(input.Move, false, false, true); // no sprinting or jumping from a crouch
            bool grounded = controller.isGrounded;
            if (grounded && vertical < 0) vertical = -2;
            if (input.Jump && grounded) vertical = Mathf.Sqrt(2 * Rules.gravity * Rules.jumpHeight);
            vertical -= Rules.gravity * delta;
            Vector3 target = transform.TransformDirection(new Vector3(move.x, 0, move.y)) * (input.Sprint ? Rules.sprintSpeed : Rules.walkSpeed);
            if (!input.Sprint && target.magnitude > PaceCap) target = target.normalized * PaceCap;
            if (Crouched) target *= CrouchPace;
            float rate = target.sqrMagnitude > planar.sqrMagnitude ? Rules.acceleration : Rules.deceleration;
            if (!grounded) rate *= Rules.airControl;
            planar = Vector3.MoveTowards(planar, target, rate * delta);
            // A moving floor (escalator, carousel) carries the body first; then the body's own move. Each part is
            // measured as what actually happened, and the carry only counts as movement if the floor says so.
            Vector3 before = transform.position;
            Surface = grounded ? SurfaceUnder() : null;
            Vector3 carriedActual = Vector3.zero;
            if (Surface != null)
            {
                Vector3 carry = Surface.Displacement(transform.position, delta);
                float yaw = Surface.YawDelta(delta);
                if (Mathf.Abs(yaw) > 0.0001f) transform.Rotate(0f, yaw, 0f, Space.World);
                if (carry.sqrMagnitude > 0f) { controller.Move(carry); carriedActual = transform.position - before; }
            }
            Vector3 mid = transform.position;
            controller.Move(new Vector3(planar.x, vertical, planar.z) * delta);
            Vector3 own = transform.position - mid;
            Vector3 moved = (own + (Surface != null && Surface.CountsAsMovement ? carriedActual : Vector3.zero)) / delta;
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
        private void SetCrouch(bool down)
        {
            if (down == Crouched || controller == null) return;
            if (!down)
            {
                // Only stand where a standing body fits.
                float r = controller.radius * 0.95f;
                Vector3 bottom = transform.position + Vector3.up * (CrouchHeight - r), top = transform.position + Vector3.up * (StandHeight - r);
                if (Physics.CheckCapsule(bottom, top, r, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            }
            Crouched = down;
            float h = down ? CrouchHeight : StandHeight;
            controller.height = h; controller.center = new Vector3(0, h * 0.5f, 0);
            var target = GetComponent<PerceptionTarget>();
            if (target != null) target.aimHeight = down ? 0.55f : 1f;
        }

        /// <summary>Sits on a seat (a horse's saddle, a sofa cushion). False when you cannot (carrying something, or already seated).</summary>
        public bool Mount(SeatSpot owner, Transform seat)
        {
            if (owner == null || seat == null || Seat != null || controller == null) return false;
            if (Record != null && Record.State == PlayerState.Carrying) return false;
            if (Crouched)
            {
                Crouched = false;
                controller.height = StandHeight; controller.center = new Vector3(0, StandHeight * 0.5f, 0);
                var aim = GetComponent<PerceptionTarget>();
                if (aim != null) aim.aimHeight = 1f;
            }
            Seat = seat; SeatOwner = owner; seatSurface = owner.Surface;
            seatDrop = owner.Style == SeatStyle.Astride ? SaddleDrop : owner.Drop(seat);
            controller.enabled = false;
            Vector3 facing = seat.forward; facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            transform.position = seat.position - Vector3.up * seatDrop;
            vertical = 0; ActualSpeed = 0; planar = Vector3.zero;
            Surface = seatSurface;
            var watched = GetComponent<PerceptionTarget>();
            if (watched != null) watched.aimHeight = owner.Style == SeatStyle.Chair ? 0.7f : 1f; // seated: watchers look at a lower chest
            var interpolator = GetComponent<MotionInterpolator>();
            if (interpolator != null) interpolator.Snap();
            return true;
        }

        /// <summary>Gets up: off the horse onto the deck, or up from the sofa to stand in front of it.</summary>
        public void Dismount()
        {
            if (Seat == null) return;
            Vector3 chosen = SeatOwner != null ? SeatOwner.StandUpPoint(Seat) : Seat.position;
            LeaveSeat();
            Teleport(chosen);
        }

        private void LeaveSeat()
        {
            if (Seat == null) return;
            var owner = SeatOwner;
            Seat = null; seatSurface = null; SeatOwner = null;
            if (controller != null) controller.enabled = true;
            var watched = GetComponent<PerceptionTarget>();
            if (watched != null) watched.aimHeight = Crouched ? 0.55f : 1f;
            owner?.Release(this);
        }

        public void Teleport(Vector3 position)
        {
            LeaveSeat();
            controller.enabled = false; transform.position = position; controller.enabled = true;
            vertical = 0; ActualSpeed = 0; planar = Vector3.zero;
            var interpolator = GetComponent<MotionInterpolator>();
            if (interpolator != null) interpolator.Snap();
        }
    }
}
