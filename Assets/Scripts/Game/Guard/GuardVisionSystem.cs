namespace NightSupermarket.Game
{
    /// <summary>The guard's eyes: a <see cref="VisionSensor"/> with security range and angle.</summary>
    public sealed class GuardVisionSystem : VisionSensor
    {
        public GuardVisionSystem(float distance, float fieldOfView, int obstacleMask) : base(distance, fieldOfView, obstacleMask) { }
    }
}
