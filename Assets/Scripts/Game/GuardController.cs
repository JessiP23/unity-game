using System;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GuardController : MonoBehaviour
    {
        public GuardBrain Brain { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool DebugVision { get; set; } = true;
        private NavMeshAgent agent;
        private GameRulesAsset rules;
        private Vector3[] patrol;
        private int waypoint;
        private IDisposable hearing;
        private GuardVisionSystem vision;
        public void Configure(GameRulesAsset config, WorldSignals world, Vector3[] points)
        {
            rules = config; patrol = points; Brain = new GuardBrain(config.patrolWait, config.searchDuration);
            agent = GetComponent<NavMeshAgent>(); agent.speed = config.guardSpeed; agent.baseOffset = 1;
            agent.radius = 0.35f; agent.height = 2; agent.stoppingDistance = 0.3f;
            vision = new GuardVisionSystem(config.visionDistance, config.fieldOfView, ~(1 << 2));
            hearing = world.Noise.Subscribe(noise =>
            {
                if (Vector3.Distance(transform.position, noise.Position) <= config.hearingRadius * noise.Loudness
                    && Brain.State != GuardState.Chase && Brain.State != GuardState.Capture)
                { LastKnownPosition = noise.Position; Brain.Hear(); }
            });
        }
        public void Tick(PlayerMotor target, DetectionSystem detection, float delta)
        {
            if (Brain == null || !agent.isOnNavMesh) return;
            bool seen = target.Record.Free && vision.CanSee(transform.position + Vector3.up * 0.6f, transform.forward,
                target.transform.position + Vector3.up, target.transform).Visible;
            bool threat = seen && detection.Suspicion.Value > 0;
            if (threat) LastKnownPosition = target.transform.position;
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;
            Brain.Tick(arrived, threat, seen && detection.State == DetectionState.Discovered
                && Vector3.Distance(transform.position, target.transform.position) < rules.captureDistance, delta);
            if (Brain.AdvancePatrol) waypoint = (waypoint + 1) % patrol.Length;
            bool search = Brain.State == GuardState.Search || Brain.State == GuardState.Capture;
            agent.isStopped = search; agent.updateRotation = !search;
            if (search) transform.Rotate(0, 45 * delta, 0);
            else
            {
                Vector3 destination = Brain.State == GuardState.Patrol || Brain.State == GuardState.ReturnToPatrol ? patrol[waypoint] : LastKnownPosition;
                if (NavMesh.SamplePosition(destination, out var hit, 3, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }
        private void OnDrawGizmos()
        {
            if (!DebugVision || rules == null) return;
            Gizmos.color = Color.yellow; Vector3 eye = transform.position + Vector3.up * 0.6f;
            Gizmos.DrawRay(eye, Quaternion.Euler(0, -rules.fieldOfView / 2, 0) * transform.forward * rules.visionDistance);
            Gizmos.DrawRay(eye, Quaternion.Euler(0, rules.fieldOfView / 2, 0) * transform.forward * rules.visionDistance);
            Gizmos.DrawWireSphere(transform.position, rules.hearingRadius);
        }
        private void OnDestroy() => hearing?.Dispose();
    }
}
