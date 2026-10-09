using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Semantic map of the prototype store: department zones, where people browse, queue, work, enter, and
    /// leave. Built before the NavMesh bake so restricted zones become a separate NavMesh area.
    /// </summary>
    public static class StoreLayout
    {
        public static StoreDirectory Build(Transform root)
        {
            var directory = new GameObject("Store directory").AddComponent<StoreDirectory>();
            directory.transform.SetParent(root, false);
            Zones(directory);
            Aisles(directory);
            Departments(directory);
            Doors(directory);
            Work(directory);
            return directory;
        }

        private static void Zones(StoreDirectory d)
        {
            Zone(d, ZoneType.Supermarket, -15, -12, 8, 5);
            Zone(d, ZoneType.EntranceExit, -3, -15, 3, -12);
            Zone(d, ZoneType.Checkout, 6.5f, -13.5f, 13.5f, -8);
            Zone(d, ZoneType.CustomerService, -8.5f, -15, -5, -11.5f);
            Zone(d, ZoneType.Clothing, -8, 5, 0, 15);
            Zone(d, ZoneType.Electronics, 0, 5, 8, 15);
            Zone(d, ZoneType.Home, 8, -7.5f, 15, 5);
            Zone(d, ZoneType.Warehouse, -15, 8, -8, 11.6f);
            Zone(d, ZoneType.Security, -15, 11.6f, -8, 15);
            Zone(d, ZoneType.Employee, 8, 5, 15, 15);
            // Upstairs sits over Electronics and the staff room; zones are told apart by height.
            d.AddZone(ZoneType.Mezzanine, new Vector3(-1.2f, PrimitiveWorld.UpstairsY - 0.7f, 5), new Vector3(15, PrimitiveWorld.CeilingY + 0.5f, 15));
        }

        private static void Aisles(StoreDirectory d)
        {
            foreach (float z in new[] { -3f, 0f, 3f })
            {
                Browse(d, ZoneType.Supermarket, -6.4f, z, 90);
                Browse(d, ZoneType.Supermarket, -3.6f, z, 270);
                Browse(d, ZoneType.Supermarket, -1.4f, z, 90);
                Browse(d, ZoneType.Supermarket, 1.4f, z, 270);
                Browse(d, ZoneType.Supermarket, 3.6f, z, 90);
                Browse(d, ZoneType.Supermarket, 6.4f, z, 270);
            }
            Browse(d, ZoneType.Supermarket, -11.0f, -10f, 270);
            Browse(d, ZoneType.Supermarket, -11.0f, -6.2f, 270);
            Browse(d, ZoneType.Supermarket, -13.5f, -0.6f, 270);
            Browse(d, ZoneType.Supermarket, -13.5f, 1.6f, 270);
        }

        private static void Departments(StoreDirectory d)
        {
            Browse(d, ZoneType.Clothing, -6.2f, 10.3f, 0);
            Browse(d, ZoneType.Clothing, -2.4f, 10.3f, 0);
            Browse(d, ZoneType.Clothing, -6.3f, 8.1f, 0);
            Browse(d, ZoneType.Clothing, -2.2f, 7.6f, 0);
            Browse(d, ZoneType.Clothing, -2.2f, 12.9f, 270);
            Browse(d, ZoneType.Clothing, -7.3f, 12.6f, 90);
            Browse(d, ZoneType.Electronics, 2.4f, 13.4f, 0);
            Browse(d, ZoneType.Electronics, 4.2f, 13.4f, 0);
            Browse(d, ZoneType.Electronics, 6.0f, 13.4f, 0);
            Browse(d, ZoneType.Electronics, 2.4f, 10.1f, 0);
            Browse(d, ZoneType.Electronics, 6.0f, 10.1f, 0);
            Browse(d, ZoneType.Home, 11.3f, 0f, 90);
            Browse(d, ZoneType.Home, 13.5f, -4.8f, 90);
            Browse(d, ZoneType.Home, 13.5f, -3.4f, 90);
            Browse(d, ZoneType.Home, 11.2f, -4.2f, 180);
            Browse(d, ZoneType.Home, 10.6f, 2.6f, 90);
            Browse(d, ZoneType.Clothing, -4.8f, 11.4f, 0);
            Browse(d, ZoneType.Clothing, -3.4f, 8.8f, 180);
            Browse(d, ZoneType.Electronics, 4.2f, 8.2f, 0);
            Browse(d, ZoneType.Electronics, 7.0f, 12.2f, 270);
            Browse(d, ZoneType.Home, 12.6f, 2.1f, 180);
            Browse(d, ZoneType.Supermarket, -8.6f, -4.2f, 90);
            float up = PrimitiveWorld.UpstairsY;
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(3.0f, up, 12.6f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(5.4f, up, 12.6f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(3.0f, up, 9.0f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(6.4f, up, 9.0f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(9.0f, up, 10.5f), 90);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(11.8f, up, 7.4f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Browse, new Vector3(4.0f, up, 6.2f), 180);
            Browse(d, ZoneType.Supermarket, 5.2f, -6.2f, 180);
            Browse(d, ZoneType.CustomerService, -6.8f, -12.5f, 180);
            d.AddPoint(ZoneType.Checkout, PointKind.Checkout, new Vector3(9.5f, 0, -10.4f), 180);
            d.AddPoint(ZoneType.Checkout, PointKind.Checkout, new Vector3(12.1f, 0, -10.4f), 180);
            d.AddPoint(ZoneType.Checkout, PointKind.Checkout, new Vector3(9.5f, 0, -8.6f), 180);
            d.AddPoint(ZoneType.Checkout, PointKind.Checkout, new Vector3(12.1f, 0, -8.6f), 180);
        }

        private static void Doors(StoreDirectory d)
        {
            d.AddPoint(ZoneType.EntranceExit, PointKind.Entrance, new Vector3(0, 0, -12.2f), 0);
            d.AddPoint(ZoneType.EntranceExit, PointKind.Entrance, new Vector3(-1.2f, 0, -12.6f), 0);
            d.AddPoint(ZoneType.EntranceExit, PointKind.Entrance, new Vector3(1.2f, 0, -12.6f), 0);
            d.AddPoint<NpcSpawnPoint>(ZoneType.EntranceExit, PointKind.Spawn, new Vector3(-0.7f, 0, -14.2f), 0);
            d.AddPoint<NpcSpawnPoint>(ZoneType.EntranceExit, PointKind.Spawn, new Vector3(0.7f, 0, -14.2f), 0);
            d.AddPoint<NpcExitPoint>(ZoneType.EntranceExit, PointKind.Exit, new Vector3(-0.7f, 0, -14.2f), 180);
            d.AddPoint<NpcExitPoint>(ZoneType.EntranceExit, PointKind.Exit, new Vector3(0.7f, 0, -14.2f), 180);
        }

        private static void Work(StoreDirectory d)
        {
            d.AddPoint(ZoneType.Supermarket, PointKind.Work, new Vector3(-3.6f, 0, -1.5f), 270);
            d.AddPoint(ZoneType.Supermarket, PointKind.Work, new Vector3(3.6f, 0, 1.5f), 90);
            d.AddPoint(ZoneType.Supermarket, PointKind.Work, new Vector3(-11.0f, 0, -8f), 270);
            d.AddPoint(ZoneType.Supermarket, PointKind.Work, new Vector3(-13.5f, 0, 0.5f), 270);
            d.AddPoint(ZoneType.Warehouse, PointKind.Work, new Vector3(-13.6f, 0, 10f), 270);
            d.AddPoint(ZoneType.Warehouse, PointKind.Work, new Vector3(-13.6f, 0, 11.2f), 270);
            d.AddPoint(ZoneType.Electronics, PointKind.Work, new Vector3(4.2f, 0, 13.3f), 0);
            d.AddPoint(ZoneType.Clothing, PointKind.Work, new Vector3(-4.2f, 0, 10.1f), 0);
            d.AddPoint(ZoneType.Mezzanine, PointKind.Work, new Vector3(4.2f, PrimitiveWorld.UpstairsY, 13.3f), 0);
        }

        private static void Zone(StoreDirectory d, ZoneType type, float x0, float z0, float x1, float z1) =>
            d.AddZone(type, new Vector3(x0, -1, z0), new Vector3(x1, 3, z1));

        private static void Browse(StoreDirectory d, ZoneType zone, float x, float z, float yaw) =>
            d.AddPoint(zone, PointKind.Browse, new Vector3(x, 0, z), yaw);
    }
}
