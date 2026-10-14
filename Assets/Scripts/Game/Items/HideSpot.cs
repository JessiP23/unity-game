using System;
using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A place to vanish: tucked against low cover (a checkout counter, a planter, a sofa back, a display table).
    /// Crouch and keep still in it and shoppers and staff cannot see you at all; the guard only finds you if he
    /// walks right up to you. A soft ring on the floor shows the spot when you are close.
    /// </summary>
    public sealed class HideSpot : MonoBehaviour
    {
        public const float Radius = 0.75f, FoundWithin = 1.6f, SettleSeconds = 0.35f;
        public string Label = "a hiding spot";
        public ZoneType? Zone;
        private Renderer ring;
        private Material dim, lit;

        public static HideSpot Create(Transform parent, Vector3 at, ZoneType? zone, string label)
        {
            var host = new GameObject("Hiding spot");
            host.transform.SetParent(parent, false);
            host.transform.position = at;
            var spot = host.AddComponent<HideSpot>();
            spot.Zone = zone; spot.Label = label;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Hiding spot ring";
            Destroy(disc.GetComponent<Collider>());
            disc.layer = 2;
            disc.transform.SetParent(host.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            disc.transform.localScale = new Vector3(Radius * 2f, 0.003f, Radius * 2f);
            spot.ring = disc.GetComponent<Renderer>();
            spot.dim = ArtLibrary.Particle(new Color(0.55f, 0.6f, 1f, 0.22f)) ?? ArtLibrary.Emissive(new Color(0.2f, 0.25f, 0.5f), 0.5f);
            spot.lit = ArtLibrary.Particle(new Color(0.45f, 1f, 0.6f, 0.45f)) ?? ArtLibrary.Emissive(new Color(0.3f, 0.8f, 0.45f), 1f);
            spot.ring.sharedMaterial = spot.dim;
            spot.ring.shadowCastingMode = ShadowCastingMode.Off;
            spot.ring.receiveShadows = false;
            disc.SetActive(false);
            return spot;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 d = position - transform.position;
            return Mathf.Abs(d.y) < 0.6f && d.x * d.x + d.z * d.z <= Radius * Radius;
        }

        /// <summary>Shown while someone is near; green while they are hidden in it.</summary>
        public void Show(bool near, bool occupied)
        {
            if (ring == null) return;
            if (ring.gameObject.activeSelf != near) ring.gameObject.SetActive(near);
            ring.sharedMaterial = occupied ? lit : dim;
        }
    }

    /// <summary>
    /// Finds the store's hiding spots once the world has settled, and keeps each mannequin's hidden state:
    /// crouched, still and inside a spot for a moment. Spots sit where a crouched body fits against cover between
    /// knee and chest height, with nothing tall in the way above (so a shelf wall is not "cover").
    /// </summary>
    public sealed class HidingPlaces : MonoBehaviour
    {
        public const int MaxSpots = 16, MaxPerZone = 3;
        public const float Spacing = 5f;
        private StoreDirectory directory;
        private Func<IReadOnlyList<PlayerMotor>> players;
        private readonly List<HideSpot> spots = new List<HideSpot>();
        private readonly Dictionary<PlayerMotor, float> still = new Dictionary<PlayerMotor, float>();
        private readonly Dictionary<PlayerMotor, HideSpot> inside = new Dictionary<PlayerMotor, HideSpot>();
        private float showTimer;
        public IReadOnlyList<HideSpot> Spots => spots;
        public bool Ready { get; private set; }
        /// <summary>Seconds spent hidden tonight (night metrics).</summary>
        public double HiddenSeconds { get; private set; }

        private static readonly ZoneType[] Places = { ZoneType.Supermarket, ZoneType.Clothing, ZoneType.Electronics, ZoneType.Home, ZoneType.Checkout, ZoneType.Atrium, ZoneType.ToyStore, ZoneType.Mezzanine };

        public void Configure(StoreDirectory store, Func<IReadOnlyList<PlayerMotor>> motors)
        {
            directory = store; players = motors;
            StartCoroutine(BuildWhenSettled());
        }

        private IEnumerator BuildWhenSettled()
        {
            yield return null; yield return null;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Build();
            Ready = true;
        }

        private void Build()
        {
            var found = new List<(Vector3 at, ZoneType zone, int score)>();
            foreach (var volume in directory.Zones)
            {
                if (Array.IndexOf(Places, volume.zone) < 0) continue;
                var b = volume.Bounds;
                float floor = volume.zone == ZoneType.Mezzanine ? PrimitiveWorld.UpstairsY : 0f;
                for (float x = b.min.x + 0.6f; x < b.max.x - 0.6f; x += 1.1f)
                    for (float z = b.min.z + 0.6f; z < b.max.z - 0.6f; z += 1.1f)
                    {
                        var probe = new Vector3(x, floor, z);
                        if (!NavMesh.SamplePosition(probe, out var hit, 0.45f, NavigationAreas.CivilianMask)) continue;
                        Vector3 at = hit.position;
                        if (Mathf.Abs(at.y - floor) > 0.35f || directory.ZoneAt(at) != volume.zone) continue;
                        if (Blocked(at)) continue;
                        int score = CoverAround(at);
                        if (score > 0) found.Add((at, volume.zone, score));
                    }
            }
            found.Sort((a, c) => c.score.CompareTo(a.score));
            var perZone = new Dictionary<ZoneType, int>();
            foreach (var (at, zone, _) in found)
            {
                if (spots.Count >= MaxSpots) break;
                perZone.TryGetValue(zone, out int n);
                if (n >= MaxPerZone) continue;
                bool crowded = false;
                foreach (var s in spots) if ((s.transform.position - at).sqrMagnitude < Spacing * Spacing) { crowded = true; break; }
                if (crowded) continue;
                spots.Add(HideSpot.Create(transform, at, zone, "a hiding spot in " + HudText.ZoneLabel(zone)));
                perZone[zone] = n + 1;
            }
        }

        /// <summary>Does anything solid stop a crouched body standing here?</summary>
        private static bool Blocked(Vector3 at)
        {
            // Room to crouch and to stand back up: a spot under an overhang would trap you crouched.
            foreach (var c in Physics.OverlapCapsule(at + Vector3.up * 0.32f, at + Vector3.up * (PlayerMotor.StandHeight - 0.3f), 0.3f, 1 | 1 << 3, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<NpcController>() == null && c.GetComponentInParent<PlayerMotor>() == null) return true;
            return false;
        }

        /// <summary>Directions (of eight) with low cover close by and nothing tall right above it.</summary>
        private static int CoverAround(Vector3 at)
        {
            int score = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (!Physics.Raycast(at + Vector3.up * 0.5f, dir, out var low, 0.75f, 1, QueryTriggerInteraction.Ignore)) continue;
                if (low.collider.GetComponentInParent<NpcController>() != null || low.collider.GetComponentInParent<PlayerMotor>() != null) continue;
                float top = low.collider.bounds.max.y - at.y;
                if (top < 0.6f || top > 1.4f) continue;
                if (Physics.Raycast(at + Vector3.up * 1.5f, dir, 0.95f, 1, QueryTriggerInteraction.Ignore)) continue;
                score++;
            }
            return score;
        }

        public HideSpot SpotFor(PlayerMotor motor) => motor != null && inside.TryGetValue(motor, out var s) ? s : null;
        public bool IsHidden(PlayerMotor motor) => motor != null && motor.TryGetComponent<PerceptionTarget>(out var t) && t.Hidden;

        /// <summary>The spot a mannequin is standing in, hidden or not (for the "crouch to hide" hint).</summary>
        public HideSpot At(Vector3 position)
        {
            foreach (var s in spots) if (s.Contains(position)) return s;
            return null;
        }

        private void Update()
        {
            if (!Ready || players == null) return;
            var all = players();
            float dt = Time.deltaTime;
            for (int i = 0; i < all.Count; i++)
            {
                var motor = all[i];
                if (motor == null) continue;
                var target = motor.GetComponent<PerceptionTarget>();
                var spot = motor.Record != null && motor.Record.Free ? At(motor.transform.position) : null;
                bool settled = spot != null && motor.Crouched && motor.ActualSpeed < 0.15f;
                still.TryGetValue(motor, out float t);
                t = settled ? t + dt : 0f;
                still[motor] = t;
                bool hidden = settled && t >= HideSpot.SettleSeconds;
                if (target != null) target.Hidden = hidden;
                if (hidden) { inside[motor] = spot; if (i == 0) HiddenSeconds += dt; } else inside.Remove(motor);
            }
            showTimer -= dt;
            if (showTimer > 0) return;
            showTimer = 0.2f;
            foreach (var s in spots)
            {
                bool near = false, occupied = false;
                for (int i = 0; i < all.Count; i++)
                {
                    var motor = all[i];
                    if (motor == null) continue;
                    if ((motor.transform.position - s.transform.position).sqrMagnitude < 25f) near = true;
                    if (inside.TryGetValue(motor, out var o) && o == s) occupied = true;
                }
                s.Show(near, occupied);
            }
        }
    }
}
