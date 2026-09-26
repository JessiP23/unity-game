using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    [CreateAssetMenu(menuName = "Night Supermarket/Game Rules")]
    public sealed class GameRulesAsset : ScriptableObject
    {
        public MissionDefinition[] missions = new MissionDefinition[0];
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
        [Tooltip("Metres per second gained per second when starting to move.")]
        [Min(0.1f)] public float acceleration = 20;
        [Tooltip("Metres per second lost per second when releasing input.")]
        [Min(0.1f)] public float deceleration = 26;
        [Range(0, 1)] public float airControl = 0.35f;
        [Tooltip("Degrees of rotation per mouse count.")]
        [Min(0.01f)] public float lookSensitivity = 0.22f;
        [Min(0.1f)] public float interactionDistance = 2.5f;
        [Min(0.1f)] public float throwForce = 8;
        [Header("Guard")]
        [Min(1)] public float visionDistance = 12;
        [Range(1, 179)] public float fieldOfView = 90;
        [Min(0)] public float hearingRadius = 12;
        [Min(0.1f)] public float guardSpeed = 2;
        [Min(0.1f)] public float guardChaseSpeed = 3.6f;
        [Tooltip("Degrees per second the guard body turns toward where it walks.")]
        [Min(1)] public float guardTurnSpeed = 300;
        [Min(0.1f)] public float searchDuration = 4;
        [Min(0.1f)] public float patrolWait = 1;
        [Min(0.1f)] public float captureDistance = 1.5f;
        [Header("Rescue and escape")]
        [Min(1)] public int rescueCount = 1;
        [Min(1)] public int requiredEscapes = 1;
        public bool allowWarehouseSelfRescue;
        public bool requireMissionsToEscape = true;
        public bool defeatWhenNoRescueRemains = true;
        [Header("Flashlight")]
        [Min(0.1f)] public float flashlightRange = 10;
        [Range(1, 179)] public float flashlightCone = 40;
        public GameRules CreateRules() => new GameRules(matchDuration, orangeDuration, suspicionInterval,
            discoveryThreshold, movementThreshold, continuedMovementToDiscover, rescueCount, requiredEscapes,
            allowWarehouseSelfRescue, requireMissionsToEscape, defeatWhenNoRescueRemains);
    }
}
