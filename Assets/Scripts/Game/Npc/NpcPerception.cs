using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using Unity.Profiling;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// An NPC's eyes plus its reading of each mannequin. Samples on a staggered interval, rejects by
    /// distance and angle before any raycast, and turns sightings into <see cref="AwarenessTracker"/> updates.
    /// Knows nothing about players: it only sees bodies and whether they moved.
    /// </summary>
    public sealed class NpcPerception : MonoBehaviour
    {
        public float eyeHeight = 1.62f;
        private static readonly ProfilerMarker SampleMarker = new ProfilerMarker("NPC.Perception");
        /// <summary>Line-of-sight rays cast by all NPCs since start (performance checks).</summary>
        public static long TotalLineTests { get; private set; }
        private VisionSensor sensor;
        private AwarenessSettings awareness;
        private float interval = 0.2f, clock;
        private readonly Dictionary<PerceptionTarget, AwarenessTracker> trackers = new Dictionary<PerceptionTarget, AwarenessTracker>();
        private readonly List<PerceptionTarget> stale = new List<PerceptionTarget>();
        public VisionSensor Sensor => sensor;
        public IReadOnlyDictionary<PerceptionTarget, AwarenessTracker> Trackers => trackers;
        /// <summary>The target this NPC is most wound up about, if any.</summary>
        public PerceptionTarget Focus { get; private set; }
        public AwarenessTracker FocusTracker => Focus != null && trackers.TryGetValue(Focus, out var t) ? t : null;
        public AwarenessState Strongest => FocusTracker != null ? FocusTracker.State : AwarenessState.Unaware;
        public Vector3 Eye => transform.position + Vector3.up * eyeHeight;
        public event Action<PerceptionTarget, AwarenessTracker> BecameSuspicious;
        public event Action<PerceptionTarget, AwarenessTracker> CalmedDown;
        public event Action<PerceptionTarget, AwarenessTracker> ReportReady;

        public void Configure(float range, float fieldOfView, float sampleInterval, AwarenessSettings settings, float phase)
        {
            sensor = new VisionSensor(range, fieldOfView, ~(1 << 2)) { CloseRange = 2.4f, CloseFieldOfView = 190f };
            awareness = settings; interval = Mathf.Max(0.02f, sampleInterval);
            clock = phase * interval;
            trackers.Clear(); Focus = null;
        }

        /// <summary>Called by the controller on the state authority only.</summary>
        public void Tick(float delta)
        {
            if (sensor == null) return;
            clock += delta;
            if (clock < interval) return;
            float elapsed = clock; clock = 0;
            Sample(elapsed);
        }

        private void Sample(float elapsed)
        {
            using var marker = SampleMarker.Auto();
            int before = sensor.LineTests;
            Vector3 eye = Eye, forward = transform.forward;
            float reach = sensor.Range + 1f;
            Focus = null; double strongest = -1;
            foreach (var target in PerceptionTarget.Active)
            {
                if (target == null) continue;
                if (!trackers.TryGetValue(target, out var tracker))
                {
                    tracker = new AwarenessTracker(awareness);
                    var captured = target;
                    tracker.Changed += state => OnChanged(captured, tracker, state);
                    trackers[target] = tracker;
                }
                bool visible = false;
                if (target.Noticeable && (target.transform.position - transform.position).sqrMagnitude <= reach * reach)
                    visible = sensor.CanSee(eye, forward, target.AimPoint, target.transform).Visible;
                Vector3 p = target.transform.position;
                if (tracker.Tick(visible, target.Speed, new MapPoint(p.x, p.y, p.z), elapsed)) ReportReady?.Invoke(target, tracker);
                double weight = (int)tracker.State * 10 + tracker.Suspicion;
                if (tracker.State != AwarenessState.Unaware && weight > strongest) { strongest = weight; Focus = target; }
            }
            TotalLineTests += sensor.LineTests - before;
            stale.Clear();
            foreach (var target in trackers.Keys) if (target == null || !target.isActiveAndEnabled) stale.Add(target);
            foreach (var target in stale) trackers.Remove(target);
        }

        private void OnChanged(PerceptionTarget target, AwarenessTracker tracker, AwarenessState state)
        {
            if (state == AwarenessState.Suspicious) BecameSuspicious?.Invoke(target, tracker);
            else if (state == AwarenessState.Observing || state == AwarenessState.Unaware) CalmedDown?.Invoke(target, tracker);
        }

        public AwarenessState StateFor(PerceptionTarget target) => target != null && trackers.TryGetValue(target, out var t) ? t.State : AwarenessState.Unaware;

        /// <summary>A moving mannequin near the eyes. No raycast; the vision sample still confirms line of sight.</summary>
        public PerceptionTarget NearbyMover(float range, float degrees)
        {
            PerceptionTarget best = null;
            float bestDistance = range;
            Vector3 forward = transform.forward;
            foreach (var target in PerceptionTarget.Active)
            {
                if (target == null || !target.Noticeable || target.Speed < awareness.MovementThreshold) continue;
                Vector3 flat = target.transform.position - transform.position;
                flat.y = 0;
                float distance = flat.magnitude;
                if (distance < 0.2f || distance > bestDistance) continue;
                if (Vector3.Angle(forward, flat) > degrees) continue;
                // Looking toward a passer must not reveal someone through a shelf.
                if (!trackers.TryGetValue(target, out var tracker) || !tracker.Visible) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Forget everything (after reporting, or when returned to the pool).</summary>
        public void ResetAll() { foreach (var tracker in trackers.Values) tracker.Reset(); Focus = null; }
    }
}
