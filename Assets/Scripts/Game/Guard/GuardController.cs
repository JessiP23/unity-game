using System;
using System.Collections.Generic;
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
        /// <summary>Input replaces patrol goals; turning and interpolation follow who drives the body.</summary>
        public bool PlayerDriven
        {
            get => playerDriven;
            set
            {
                playerDriven = value;
                var interpolator = GetComponent<MotionInterpolator>();
                if (interpolator != null) { interpolator.Interpolating = value; interpolator.Snap(); }
            }
        }
        public GuardFlashlight Flashlight { get; set; }
        private NavMeshAgent agent;
        private GameRulesAsset rules;
        private Vector3[] patrol;
        private int waypoint;
        private IDisposable hearing, reports;
        /// <summary>Last report this guard acted on; debug and tests read it.</summary>
        public SuspiciousActivityEvent? LastReport { get; private set; }
        private GuardVisionSystem vision;
        private bool playerDriven, searching;
        private float cruiseSpeed;
        public void Configure(GameRulesAsset config, WorldSignals world, Vector3[] points)
        {
            rules = config; patrol = points; Brain = new GuardBrain(config.patrolWait, config.searchDuration);
            agent = GetComponent<NavMeshAgent>(); agent.speed = config.guardSpeed; agent.baseOffset = 1;
            agent.radius = 0.35f; agent.height = 2; agent.stoppingDistance = 0.3f;
            agent.acceleration = 6f; agent.autoBraking = true; agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (!TryGetComponent(out MotionInterpolator interpolator)) interpolator = gameObject.AddComponent<MotionInterpolator>();
            interpolator.Interpolating = playerDriven;
            vision = new GuardVisionSystem(config.visionDistance, config.fieldOfView, ~(1 << 2));
            hearing = world.Noise.Subscribe(noise =>
            {
                if (Vector3.Distance(transform.position, noise.Position) <= config.hearingRadius * noise.Loudness
                    && Brain.State != GuardState.Chase && Brain.State != GuardState.Capture)
                    BeginInvestigation(noise.Position);
            });
        }
        /// <summary>
        /// Civilian reports send the guard to investigate where the mannequin was last seen, the same way
        /// a noise does. The report never carries the mannequin's current position.
        /// </summary>
        public void InvestigateReports(EventStream<SuspiciousActivityEvent> stream)
        {
            reports?.Dispose();
            reports = stream?.Subscribe(report =>
            {
                if (Brain == null || Brain.State == GuardState.Chase || Brain.State == GuardState.Capture) return;
                LastReport = report;
                BeginInvestigation(new Vector3(report.LastKnownPosition.X, report.LastKnownPosition.Y, report.LastKnownPosition.Z));
            });
        }
        /// <summary>
        /// Starts walking to a heard or reported spot straight away. Otherwise a guard standing at the end of
        /// its old path would count as already arrived and search where it stood.
        /// </summary>
        private void BeginInvestigation(Vector3 position)
        {
            LastKnownPosition = position;
            Brain.Hear();
            if (playerDriven || agent == null || !agent.isOnNavMesh) return;
            if (!NavMesh.SamplePosition(position, out var hit, 3, NavMesh.AllAreas)) return;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        public void TickGroup(IReadOnlyList<PlayerMotor> targets, IReadOnlyList<DetectionSystem> detections, float delta)
        {
            int chosen = -1, suspicious = -1;
            float best = float.PositiveInfinity, suspiciousDistance = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || detections[i] == null || !target.Record.Free) continue;
                Vector3 eye = transform.position + Vector3.up * 0.6f;
                var result = vision.CanSee(eye, transform.forward, target.transform.position + Vector3.up, target.transform);
                var lamp = Flashlight != null ? Flashlight.Model : null;
                bool visible = result.Visible || (lamp != null && vision.Observed(eye, transform.forward, target.transform.position + Vector3.up, target.transform, lamp));
                if (!visible) continue;
                if (detections[i].Suspicion.Value > 0 && result.Distance < suspiciousDistance)
                { suspicious = i; suspiciousDistance = result.Distance; }
                if (result.Distance < best) { chosen = i; best = result.Distance; }
            }
            int index = suspicious >= 0 ? suspicious : chosen;
            if (index < 0) Tick(null, null, delta);
            else Tick(targets[index], detections[index], delta);
        }
        /// <summary>Same body and perception as the AI. Input replaces patrol goals; it does not create a second rule set.</summary>
        public void Drive(Vector2 move, float speed, float delta)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.ResetPath();
            agent.updateRotation = false;
            agent.isStopped = true;
            Vector2 clamped = Vector2.ClampMagnitude(move, 1f);
            Vector3 world = transform.TransformDirection(new Vector3(clamped.x, 0f, clamped.y));
            if (world.sqrMagnitude < 0.0001f) return;
            agent.Move(world * speed * delta);
        }
        public void Tick(PlayerMotor target, DetectionSystem detection, float delta)
        {
            if (PlayerDriven || Brain == null || !agent.isOnNavMesh) return;
            bool seen = false, threat = false, canCapture = false;
            if (target != null && detection != null && target.Record.Free)
            {
                var lamp = Flashlight != null ? Flashlight.Model : null;
                seen = vision.Observed(transform.position + Vector3.up * 0.6f, transform.forward,
                    target.transform.position + Vector3.up, target.transform, lamp);
                threat = seen && detection.Suspicion.Value > 0;
                if (threat || (seen && detection.State == DetectionState.Discovered)) LastKnownPosition = target.transform.position;
                canCapture = seen && detection.State == DetectionState.Discovered
                    && Vector3.Distance(transform.position, target.transform.position) < rules.captureDistance;
            }
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;
            Brain.Tick(arrived, threat, canCapture, delta);
            if (Brain.AdvancePatrol) waypoint = (waypoint + 1) % patrol.Length;
            bool search = Brain.State == GuardState.Search || Brain.State == GuardState.Capture;
            searching = search;
            agent.isStopped = search;
            cruiseSpeed = Brain.State == GuardState.Chase ? rules.guardChaseSpeed : rules.guardSpeed;
            if (!search)
            {
                Vector3 destination = Brain.State == GuardState.Patrol || Brain.State == GuardState.ReturnToPatrol ? patrol[waypoint] : LastKnownPosition;
                if (NavMesh.SamplePosition(destination, out var hit, 3, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }
        /// <summary>Turns the body toward its walking direction at a human rate instead of snapping.</summary>
        private void Update()
        {
            if (playerDriven || agent == null || rules == null || !agent.isOnNavMesh) return;
            if (searching) { transform.Rotate(0, 45 * Time.deltaTime, 0); return; }
            Vector3 heading = agent.desiredVelocity; heading.y = 0;
            if (heading.sqrMagnitude < 0.04f) return;
            var facing = Quaternion.LookRotation(heading);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, rules.guardTurnSpeed * Time.deltaTime);
            float aligned = Mathf.Clamp01(Vector3.Dot(transform.forward, heading.normalized));
            agent.speed = Mathf.Max(0.1f, (cruiseSpeed > 0 ? cruiseSpeed : rules.guardSpeed) * Mathf.Lerp(0.3f, 1f, aligned));
        }
        private void OnDrawGizmos()
        {
            if (!DebugVision || rules == null) return;
            Gizmos.color = Color.yellow; Vector3 eye = transform.position + Vector3.up * 0.6f;
            Gizmos.DrawRay(eye, Quaternion.Euler(0, -rules.fieldOfView / 2, 0) * transform.forward * rules.visionDistance);
            Gizmos.DrawRay(eye, Quaternion.Euler(0, rules.fieldOfView / 2, 0) * transform.forward * rules.visionDistance);
            Gizmos.DrawWireSphere(transform.position, rules.hearingRadius);
        }
        private void OnDestroy() { hearing?.Dispose(); reports?.Dispose(); }
    }
}
