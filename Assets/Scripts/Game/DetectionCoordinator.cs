using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class DetectionCoordinator
    {
        public DetectionSystem Detection { get; }
        private readonly GuardVisionSystem vision;
        private readonly PlayerMotor player;
        private readonly Transform guard;
        private readonly DisplayCamouflage camouflage;
        public GuardFlashlight Flashlight { get; set; }
        public bool Observed { get; private set; }
        /// <summary>Set by the pose system when the held pose fits the department: counts like the display stand.</summary>
        public bool PoseCamouflage { get; set; }
        /// <summary>One forced movement on the next tick (a strain twitch). Consumed by Tick.</summary>
        public bool Twitch { get; set; }
        public DetectionCoordinator(PlayerMotor player, Transform guard, GameRulesAsset rules)
        {
            camouflage = player.GetComponent<DisplayCamouflage>();
            this.player = player; this.guard = guard;
            Detection = new DetectionSystem(rules.CreateRules());
            vision = new GuardVisionSystem(rules.visionDistance, rules.fieldOfView, ~(1 << 2));
        }
        public void Tick(float delta)
        {
            if (!player.Record.Free) { Observed = false; return; }
            var lamp = Flashlight != null ? Flashlight.Model : null;
            bool visible = vision.Observed(guard.position + Vector3.up * GuardVisionSystem.EyeHeight, guard.forward,
                player.transform.position + Vector3.up, player.transform, lamp);
            Observed = visible;
            double speed = Twitch ? player.Rules.movementThreshold * 4 : player.ActualSpeed;
            Twitch = false;
            Detection.Tick(visible, speed, delta, PoseCamouflage || (camouflage != null && camouflage.Active));
        }
    }
}
