using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Records the pose gameplay would send to an animator. No clip is required.</summary>
    public sealed class PoseDriver : MonoBehaviour
    {
        public PresentationPose Pose { get; private set; }
        private PlayerMotor motor;
        private DetectionSystem detection;
        public void Configure(PlayerMotor player, DetectionSystem vision) { motor = player; detection = vision; }
        public void Tick()
        {
            if (motor == null || motor.Record == null) return;
            var state = detection != null ? detection.State : DetectionState.Green;
            Pose = PoseMap.From(motor.Record.State, motor.ActualSpeed, state, false);
        }
    }
}
