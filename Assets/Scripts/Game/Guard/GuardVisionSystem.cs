namespace NightSupermarket.Game
{
    /// <summary>The guard's eyes: a <see cref="VisionSensor"/> with security range and angle.</summary>
    public sealed class GuardVisionSystem : VisionSensor
    {
        // The guard pivot is the capsule centre (1m above the floor), not its feet.
        public const float EyeHeight = 0.6f;
        public GuardVisionSystem(float distance, float fieldOfView, int obstacleMask) : base(distance, fieldOfView, obstacleMask) { }
    }
}
