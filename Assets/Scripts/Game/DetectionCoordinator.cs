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
        public GuardFlashlight Flashlight { get; set; }
        public DetectionCoordinator(PlayerMotor player, Transform guard, GameRulesAsset rules)
        {
            this.player = player; this.guard = guard;
            Detection = new DetectionSystem(rules.CreateRules());
            vision = new GuardVisionSystem(rules.visionDistance, rules.fieldOfView, ~(1 << 2));
        }
        public void Tick(float delta)
        {
            if (!player.Record.Free) return;
            var lamp = Flashlight != null ? Flashlight.Model : null;
            bool visible = vision.Observed(guard.position + Vector3.up * 0.6f, guard.forward,
                player.transform.position + Vector3.up, player.transform, lamp);
            Detection.Tick(visible, player.ActualSpeed, delta);
        }
    }
}
