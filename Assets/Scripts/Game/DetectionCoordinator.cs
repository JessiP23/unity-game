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
        public DetectionCoordinator(PlayerMotor player, Transform guard, GameRulesAsset rules)
        {
            this.player = player; this.guard = guard;
            Detection = new DetectionSystem(rules.CreateRules());
            vision = new GuardVisionSystem(rules.visionDistance, rules.fieldOfView, ~(1 << 2));
        }
        public void Tick(float delta)
        {
            if (!player.Record.Free) return;
            bool visible = vision.CanSee(guard.position + Vector3.up * 0.6f, guard.forward,
                player.transform.position + Vector3.up, player.transform).Visible;
            Detection.Tick(visible, player.ActualSpeed, delta);
        }
    }
}
