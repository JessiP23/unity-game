using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// NavMeshAgent wrapper for walking people: human acceleration and turn rate, local avoidance with
    /// varied priorities so crowds untangle, stuck detection, and an optional facing target while standing.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcMovement : MonoBehaviour
    {
        [Min(1)] public float turnSpeed = 240f;
        [Min(0.5f)] public float stuckSeconds = 4f;
        private NavMeshAgent agent;
        private Vector3? faceTarget;
        private float stuckTimer, baseSpeed = 1.2f;
        private Vector3 requested;
        public NavMeshAgent Agent => agent;
        public float Speed => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        public bool HasDestination { get; private set; }
        public bool Stuck { get; private set; }

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public void Configure(float walkSpeed, int areaMask, int avoidancePriority)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            baseSpeed = walkSpeed;
            agent.speed = walkSpeed; agent.acceleration = 4f; agent.angularSpeed = 0; agent.updateRotation = false;
            agent.radius = 0.28f; agent.height = 1.8f; agent.baseOffset = 0; agent.stoppingDistance = 0.25f;
            agent.autoBraking = true; agent.autoRepath = true; agent.areaMask = areaMask;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = avoidancePriority;
        }

        /// <summary>Remote proxies never path; they follow replicated snapshots.</summary>
        public void SetSimulated(bool simulated)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = simulated;
        }

        public void SetPace(float multiplier) { if (agent != null) agent.speed = baseSpeed * multiplier; }

        public bool GoTo(Vector3 destination)
        {
            faceTarget = null; Stuck = false; stuckTimer = 0;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            agent.isStopped = false;
            requested = destination;
            HasDestination = agent.SetDestination(destination);
            return HasDestination;
        }

        public void Stop()
        {
            HasDestination = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        }

        public void Face(Vector3 point) => faceTarget = point;
        public void ClearFacing() => faceTarget = null;

        /// <summary>Reached the destination, or the path ended as close as the NavMesh allows.</summary>
        public bool Arrived
        {
            get
            {
                if (!HasDestination || agent == null || !agent.enabled || agent.pathPending || Unreachable) return false;
                return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
            }
        }

        /// <summary>
        /// The destination is off the walkable area this NPC may use, or the best path stops well short of it
        /// (a partial path would otherwise "arrive" at the wrong place).
        /// </summary>
        public bool Unreachable
        {
            get
            {
                if (!HasDestination || agent == null || !agent.enabled || agent.pathPending) return false;
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
                if (agent.pathStatus != NavMeshPathStatus.PathPartial) return false;
                var corners = agent.path.corners;
                Vector3 end = corners.Length > 0 ? corners[corners.Length - 1] : transform.position;
                end.y = requested.y;
                return Vector3.Distance(end, requested) > 1f;
            }
        }

        public Vector3[] Corners => agent != null && agent.enabled && agent.hasPath ? agent.path.corners : new Vector3[0];

        public void Warp(Vector3 position)
        {
            if (agent != null && agent.enabled) agent.Warp(position); else transform.position = position;
        }

        private void Update()
        {
            if (agent == null || !agent.enabled) return;
            Vector3 heading = agent.desiredVelocity; heading.y = 0;
            if (faceTarget.HasValue && heading.sqrMagnitude < 0.04f)
            {
                heading = faceTarget.Value - transform.position; heading.y = 0;
            }
            if (heading.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading), turnSpeed * Time.deltaTime);
            if (HasDestination && !Arrived && !agent.pathPending && agent.velocity.sqrMagnitude < 0.0025f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0;
            Stuck = stuckTimer > stuckSeconds;
        }
    }
}
