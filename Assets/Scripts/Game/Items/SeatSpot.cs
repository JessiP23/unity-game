using System.Collections.Generic;
using UnityEngine;
namespace NightSupermarket.Game
{
    public enum SeatStyle { Astride, Chair }

    /// <summary>
    /// Somewhere to sit: a carousel horse (astride, carried by the ride) or a piece of furniture (a showroom sofa,
    /// an armchair, an atrium bench). E sits on the free seat nearest you; E again, or any move key, gets up.
    /// A mannequin sitting on display furniture or riding a horse looks like it belongs there, so seated you pass
    /// for a display pose. Sitting down and getting up are still you moving.
    /// </summary>
    public sealed class SeatSpot : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        public SeatStyle Style;
        /// <summary>The ride that carries the seat (the carousel), or null for furniture.</summary>
        public MovingSurface Surface;
        public string What = "the sofa";
        private readonly List<Transform> seats = new List<Transform>();
        private readonly List<PlayerMotor> riders = new List<PlayerMotor>();
        private readonly List<float> drops = new List<float>();
        public IReadOnlyList<Transform> Seats => seats;
        /// <summary>Every seat in the store, for tests and the hint text.</summary>
        public static readonly List<SeatSpot> All = new List<SeatSpot>();

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        /// <summary>Adds a seat. <paramref name="drop"/> is from the seat down to the body's root (the floor for a chair).</summary>
        public Transform AddSeat(Vector3 localPosition, Quaternion localRotation, float drop)
        {
            var seat = new GameObject("Seat " + (seats.Count + 1)).transform;
            seat.SetParent(transform, false);
            seat.localPosition = localPosition; seat.localRotation = localRotation;
            seats.Add(seat); riders.Add(null); drops.Add(drop);
            return seat;
        }

        public float Drop(Transform seat) { int i = seats.IndexOf(seat); return i >= 0 ? drops[i] : 0.45f; }

        public PlayerMotor RiderOf(int i) => i >= 0 && i < riders.Count && riders[i] != null && riders[i].Seat == seats[i] ? riders[i] : null;

        public PlayerMotor FirstRider { get { for (int i = 0; i < seats.Count; i++) { var r = RiderOf(i); if (r != null) return r; } return null; } }

        public string Prompt => (Style == SeatStyle.Astride ? "E — climb on " : "E — sit on ") + What;

        public string PromptFor(PlayerMotor player)
        {
            if (player != null && player.SeatOwner == this) return Style == SeatStyle.Astride ? "E (or move) — climb down" : "E (or move) — stand up";
            if (Free(player) < 0) return "Someone is sitting there";
            if (Style == SeatStyle.Astride) return Surface is CarouselRide ride && ride.Speed > 0.5f ? "E — climb on " + What + " (riders are part of the ride)" : Prompt;
            return Prompt + " (a seated mannequin is on display)";
        }

        private int Free(PlayerMotor player)
        {
            int best = -1; float distance = float.MaxValue;
            for (int i = 0; i < seats.Count; i++)
            {
                if (RiderOf(i) != null) continue;
                float d = player != null ? (seats[i].position - player.transform.position).sqrMagnitude : i;
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        public bool TryInteract(PlayerMotor player)
        {
            if (player == null || player.Record == null || !player.Record.Free) return false;
            if (player.SeatOwner == this) { player.Dismount(); return true; }
            if (player.Seat != null) return false;
            int i = Free(player);
            if (i < 0 || !player.Mount(this, seats[i])) return false;
            riders[i] = player;
            return true;
        }

        /// <summary>Called by the motor when its rider leaves.</summary>
        public void Release(PlayerMotor player)
        {
            for (int i = 0; i < riders.Count; i++) if (riders[i] == player) riders[i] = null;
        }

        /// <summary>Where you stand after getting up: in front of a chair; on the carousel deck inside the ring of horses, else outside it.</summary>
        public Vector3 StandUpPoint(Transform seat)
        {
            var candidates = new List<Vector3>();
            if (Surface != null)
            {
                Vector3 center = Surface.transform.position;
                Vector3 outward = seat.position - center; outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f) outward = -seat.right;
                outward.Normalize();
                float deck = center.y + CarouselRide.PlatformHeight + 0.03f;
                foreach (float radius in new[] { 1.45f, 2.95f, 1.2f })
                    candidates.Add(new Vector3(center.x, deck, center.z) + outward * radius);
            }
            else
            {
                Vector3 floor = seat.position - Vector3.up * Drop(seat) + Vector3.up * 0.03f;
                Vector3 front = seat.forward; front.y = 0f; front.Normalize();
                // Step out from the seat until a body fits (a sofa's depth and the gap to a coffee table vary), then try the sides.
                Vector3 side = Vector3.Cross(Vector3.up, front);
                for (float d = 0.5f; d <= 1.6f; d += 0.1f) candidates.Add(floor + front * d);
                for (float k = 0.7f; k <= 1.5f; k += 0.2f) { candidates.Add(floor + front * 0.5f + side * k); candidates.Add(floor + front * 0.5f - side * k); }
            }
            const float r = 0.28f;
            // Prefer a free spot on walkable floor (so the guide marker and the shoppers can reach it), then any free spot.
            for (int pass = 0; pass < 2; pass++)
                foreach (var at in candidates)
                {
                    if (Physics.CheckCapsule(at + Vector3.up * (r + 0.05f), at + Vector3.up * (PlayerMotor.StandHeight - r), r, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    if (pass == 0 && !UnityEngine.AI.NavMesh.SamplePosition(at, out _, 0.35f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    return at;
                }
            return candidates.Count > 0 ? candidates[0] : seat.position;
        }
    }

    /// <summary>
    /// Turns the store's sit-able furniture into seats once it is dressed: the showroom sofas and armchairs,
    /// the ottomans and the atrium benches. Seat heights come from each model's own cushion (its vertices), so
    /// a body sits on the sofa rather than in it or above it.
    /// </summary>
    public static class Seating
    {
        private static readonly string[] Sofas = { "sofa_02" }, Chairs = { "ArmChair_01", "modern_arm_chair_01" }, Stools = { "Ottoman_01" };

        public static int Furnish(Transform root)
        {
            int made = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                string name = t.name;
                if (System.Array.IndexOf(Sofas, name) >= 0) made += Furniture(t, "the sofa", new[] { -0.24f, 0.24f }, 0.14f) + (t.GetComponent<SeatSpot>() == null && SofaFromCollider(t) != null ? 1 : 0);
                else if (System.Array.IndexOf(Chairs, name) >= 0) made += Furniture(t, "the armchair", new[] { 0f }, 0.12f);
                else if (System.Array.IndexOf(Stools, name) >= 0) made += Furniture(t, "the ottoman", new[] { 0f }, 0f);
                else if (name == "Bench") made += Bench(t);
            }
            return made;
        }

        /// <summary>
        /// A model whose front is its local +z. <paramref name="across"/> places seats as fractions of its width;
        /// <paramref name="forward"/> moves them toward the front (as a fraction of its depth) to clear the backrest.
        /// </summary>
        private static int Furniture(Transform holder, string what, float[] across, float forward)
        {
            if (holder.GetComponent<SeatSpot>() != null || holder.GetComponent<Collider>() == null) return 0;
            var filters = holder.GetComponentsInChildren<MeshFilter>();
            var points = new List<Vector3>();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var toHolder = holder.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) points.Add(toHolder.MultiplyPoint3x4(v));
            }
            if (points.Count == 0) return 0;
            Vector3 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float width = max.x - min.x, depth = max.z - min.z, height = max.y - min.y;
            if (width < 0.3f || depth < 0.3f || height < 0.25f) return 0;
            var spot = holder.gameObject.AddComponent<SeatSpot>();
            spot.Style = SeatStyle.Chair; spot.What = what;
            float z = (min.z + max.z) * 0.5f + depth * forward;
            foreach (float a in across)
            {
                float x = (min.x + max.x) * 0.5f + width * a;
                // The cushion: the highest point of the model right there, ignoring anything above the backrest line.
                float top = float.MinValue;
                foreach (var p in points)
                    if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < 0.12f * 0.12f && (forward <= 0f || p.y < min.y + height * 0.8f) && p.y > top) top = p.y; // a backrest only on pieces that have one
                if (top < 0.2f || top > 0.75f) top = 0.45f;
                spot.AddSeat(new Vector3(x, top, z), Quaternion.identity, top);
            }
            return 1;
        }

        /// <summary>A sofa whose mesh could not be read: two seats at sitting height in front of its back, from its collider box.</summary>
        public static SeatSpot SofaFromCollider(Transform holder)
        {
            if (holder.GetComponent<SeatSpot>() != null) return holder.GetComponent<SeatSpot>();
            var box = holder.GetComponent<BoxCollider>();
            if (box == null) return null;
            var spot = holder.gameObject.AddComponent<SeatSpot>();
            spot.Style = SeatStyle.Chair; spot.What = "the sofa";
            float z = box.center.z + box.size.z * 0.14f;
            foreach (float a in new[] { -0.24f, 0.24f })
                spot.AddSeat(new Vector3(box.center.x + box.size.x * a, 0.45f, z), Quaternion.identity, 0.45f);
            return spot;
        }

        /// <summary>An atrium bench: two seats along it, facing the fountain.</summary>
        private static int Bench(Transform bench)
        {
            if (bench.GetComponent<SeatSpot>() != null || bench.GetComponent<Collider>() == null) return 0;
            Vector3 facing = bench.forward;
            Vector3 toFountain = new Vector3(0f, 0f, -24f) - bench.position; // the fountain
            toFountain.y = 0f;
            if (Vector3.Dot(facing, toFountain) < 0f) facing = -facing;
            return SeatsOn(bench, facing, "the bench") != null ? 1 : 0;
        }

        /// <summary>Two seats along the top of a box (a bench, a stand-in sofa), facing <paramref name="facing"/>.</summary>
        public static SeatSpot SeatsOn(Transform box, Vector3 facing, string what)
        {
            var collider = box.GetComponent<Collider>();
            if (collider == null) return null;
            Physics.SyncTransforms();
            var bounds = collider.bounds;
            float top = bounds.max.y, floor = bounds.min.y;
            facing.y = 0f; facing.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, facing).normalized;
            var spot = box.gameObject.AddComponent<SeatSpot>();
            spot.Style = SeatStyle.Chair; spot.What = what;
            foreach (float a in new[] { -0.45f, 0.45f })
            {
                var seat = spot.AddSeat(Vector3.zero, Quaternion.identity, top - floor);
                seat.localScale = new Vector3(1f / box.localScale.x, 1f / box.localScale.y, 1f / box.localScale.z);
                seat.SetPositionAndRotation(new Vector3(bounds.center.x, top, bounds.center.z) + along * a, Quaternion.LookRotation(facing, Vector3.up));
            }
            return spot;
        }
    }
}
