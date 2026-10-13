using NightSupermarket.Core;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Turns "go shopping in Clothing" into a concrete, free, allowed point from the store directory,
    /// and holds that point's reservation so two shoppers don't stand in the same spot.
    /// </summary>
    public sealed class NpcNavigation : MonoBehaviour
    {
        private StoreDirectory directory;
        private ZoneAccess access;
        private Random random;
        private int owner;
        public NavigationPoint Target { get; private set; }
        public ZoneAccess Access => access;

        public void Configure(StoreDirectory store, ZoneAccess zones, Random rng, int npc)
        { directory = store; access = zones; random = rng; owner = npc; }

        /// <summary>Picks a point for the destination; <paramref name="avoid"/> is skipped when alternatives exist.</summary>
        public NavigationPoint Choose(DestinationKind kind, ZoneType? zone, NavigationPoint avoid = null, bool sale = false, int linger = -1)
        {
            Release();
            if (directory == null) return null;
            // Hanging around a job: only that job's spots will do.
            if (linger >= 0 && kind == DestinationKind.Department) return Target = directory.Pick(PointKind.Linger, zone, access, random, owner, avoid, linger);
            // A flash sale fills the floor round the sale bins first, then the zone's ordinary shelves.
            if (sale && kind == DestinationKind.Department && zone.HasValue)
            {
                Target = directory.Pick(PointKind.Sale, zone, access, random, owner, avoid);
                if (Target != null) return Target;
            }
            Target = kind switch
            {
                DestinationKind.Entrance => directory.Pick(PointKind.Entrance, null, access, random, owner, avoid),
                DestinationKind.Department => zone.HasValue ? directory.Pick(PointKind.Browse, zone, access, random, owner, avoid) : null,
                DestinationKind.Checkout => directory.Pick(PointKind.Checkout, ZoneType.Checkout, access, random, owner, avoid),
                DestinationKind.Exit => directory.Pick(PointKind.Exit, null, access, random, owner, avoid),
                _ => null
            };
            return Target;
        }

        /// <summary>A free spot round one of tonight's jobs (staff errands use any zone their access allows).</summary>
        public NavigationPoint ChooseLinger(int job) { Release(); return Target = directory != null ? directory.Pick(PointKind.Linger, null, access, random, owner, null, job) : null; }

        public NavigationPoint ChooseWork() { Release(); return Target = directory != null ? directory.Pick(PointKind.Work, null, access, random, owner) : null; }

        public void Release()
        {
            if (Target != null) Target.Release(owner);
            Target = null;
        }

        public ZoneType? CurrentZone => ZoneAt(transform.position);
        public ZoneType? ZoneAt(Vector3 position) => directory != null ? directory.ZoneAt(position) : null;
        private void OnDisable() => Release();
    }
}
