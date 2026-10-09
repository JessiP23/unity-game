using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>Registry of zones and points for one store. NPCs ask it where to go; it never moves anyone.</summary>
    public sealed class StoreDirectory : MonoBehaviour
    {
        private readonly List<NavigationPoint> points = new List<NavigationPoint>();
        private readonly List<ZoneVolume> zones = new List<ZoneVolume>();
        private readonly List<NavigationPoint> scratch = new List<NavigationPoint>();
        public IReadOnlyList<NavigationPoint> Points => points;
        public IReadOnlyList<ZoneVolume> Zones => zones;

        public ZoneVolume AddZone(ZoneType type, Vector3 min, Vector3 max)
        {
            var volume = new GameObject("Zone " + type).AddComponent<ZoneVolume>();
            volume.transform.SetParent(transform, false);
            volume.transform.position = (min + max) * 0.5f;
            volume.Configure(type, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Max(0.1f, Mathf.Abs(max.y - min.y)), Mathf.Abs(max.z - min.z)));
            zones.Add(volume);
            return volume;
        }

        public T AddPoint<T>(ZoneType zone, PointKind kind, Vector3 position, float yaw) where T : NavigationPoint
        {
            var point = new GameObject($"{kind} {zone}").AddComponent<T>();
            point.transform.SetParent(transform, false);
            point.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            point.zone = zone; point.kind = kind;
            points.Add(point);
            return point;
        }
        public NavigationPoint AddPoint(ZoneType zone, PointKind kind, Vector3 position, float yaw) => AddPoint<NavigationPoint>(zone, kind, position, yaw);

        /// <summary>The most specific zone containing a position (smallest volume wins where zones overlap).</summary>
        public ZoneType? ZoneAt(Vector3 position)
        {
            ZoneVolume best = null;
            foreach (var zone in zones)
                if (zone.Bounds.Contains(position) && (best == null || zone.Area < best.Area)) best = zone;
            return best != null ? best.zone : (ZoneType?)null;
        }

        /// <summary>Random free point of a kind, optionally in a zone, that the given access allows.</summary>
        public NavigationPoint Pick(PointKind kind, ZoneType? zone, ZoneAccess access, Random random, int npc, NavigationPoint exclude = null)
        {
            bool shared = kind == PointKind.Entrance || kind == PointKind.Exit || kind == PointKind.Spawn;
            scratch.Clear();
            foreach (var point in points)
                if (point != exclude && point.kind == kind && (zone == null || point.zone == zone) && access.Allows(point.zone) && (shared || point.Free || point.Occupant == npc))
                    scratch.Add(point);
            if (scratch.Count == 0) return null;
            var chosen = scratch[random.Next(scratch.Count)];
            if (!shared) chosen.TryReserve(npc);
            return chosen;
        }

        public void ReleaseAll(int npc) { foreach (var point in points) point.Release(npc); }
    }
}
