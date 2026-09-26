namespace NightSupermarket.Core
{
    public enum PresentationPose { Idle, Walk, Run, Jump, Carry, Throw, Push, Freeze, Captured, Escape }
    /// <summary>Maps authoritative player state to a pose. An animator can bind this later.</summary>
    public static class PoseMap
    {
        public static PresentationPose From(PlayerState state, float speed, DetectionState detection, bool pushing)
        {
            if (state == PlayerState.Captured || state == PlayerState.Surveillance) return PresentationPose.Captured;
            if (state == PlayerState.Escaped || state == PlayerState.Escaping) return PresentationPose.Escape;
            if (state == PlayerState.Throwing) return PresentationPose.Throw;
            if (state == PlayerState.Carrying) return PresentationPose.Carry;
            if (pushing) return PresentationPose.Push;
            if (state == PlayerState.Jumping) return PresentationPose.Jump;
            if (detection == DetectionState.Red && speed <= 0.05f) return PresentationPose.Freeze;
            if (state == PlayerState.Running) return PresentationPose.Run;
            if (speed > 0.05f) return PresentationPose.Walk;
            return PresentationPose.Idle;
        }
    }
}
