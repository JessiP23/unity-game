using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    [CreateAssetMenu(menuName = "Night Supermarket/Game Rules")]
    public sealed class GameRulesAsset : ScriptableObject
    {
        [Min(1)] public float matchDuration = 600;
        [Min(0.01f)] public float orangeDuration = 1.5f;
        [Min(0.01f)] public float suspicionInterval = 1;
        [Min(1)] public int discoveryThreshold = 3;
        [Min(0.001f)] public float movementThreshold = 0.08f;
        public bool continuedMovementToDiscover = true;
        [Min(0)] public int inventoryCapacity = 8;
        [Header("Movement")]
        [Min(0.1f)] public float walkSpeed = 3;
        [Min(0.1f)] public float sprintSpeed = 5;
        [Min(0)] public float jumpHeight = 1;
        [Min(0.1f)] public float gravity = 20;
        [Min(0.01f)] public float lookSensitivity = 0.12f;
        [Min(0.1f)] public float interactionDistance = 2.5f;
        [Min(0.1f)] public float throwForce = 8;
        [Header("Guard")]
        [Min(1)] public float visionDistance = 12;
        [Range(1, 179)] public float fieldOfView = 90;
        [Min(0)] public float hearingRadius = 12;
        [Min(0.1f)] public float guardSpeed = 2;
        [Min(0.1f)] public float searchDuration = 4;
        [Min(0.1f)] public float patrolWait = 1;
        [Min(0.1f)] public float captureDistance = 1.5f;
        [Header("Rescue and escape")]
        [Min(1)] public int rescueCount = 1;
        [Min(1)] public int requiredEscapes = 1;
        public bool allowWarehouseSelfRescue;
        public GameRules CreateRules() => new GameRules(matchDuration, orangeDuration, suspicionInterval,
            discoveryThreshold, movementThreshold, continuedMovementToDiscover);
    }
}
