using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A floor that carries whoever stands on it. <see cref="PlayerMotor"/> asks it each step how far
    /// to move and turn a body at a point. Whether that carried motion counts as "moving" for the
    /// watchers is the surface's call: an escalator carries you in plain sight (a mannequin riding
    /// up is a mannequin that moves); a carousel is supposed to turn, so riders are not moving.
    /// </summary>
    public abstract class MovingSurface : MonoBehaviour
    {
        /// <summary>True when the guard and the shoppers should treat carried motion as the body moving.</summary>
        public abstract bool CountsAsMovement { get; }
        /// <summary>World displacement of a point standing on the surface over one step.</summary>
        public abstract Vector3 Displacement(Vector3 at, float delta);
        /// <summary>Yaw change (degrees) a rider picks up over one step.</summary>
        public virtual float YawDelta(float delta) => 0f;
    }

    /// <summary>The escalator belt: a constant push up (or down) the ramp while it runs.</summary>
    public sealed class EscalatorBelt : MovingSurface
    {
        public Vector3 Direction = Vector3.forward;
        public float Speed = 0.55f;
        public bool Running = true;
        public override bool CountsAsMovement => true;
        public override Vector3 Displacement(Vector3 at, float delta) => Running ? Direction.normalized * Speed * delta : Vector3.zero;
    }
}
