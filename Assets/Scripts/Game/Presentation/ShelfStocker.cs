using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>Fills shelf boards and crates with prop facings, measured from each prop's real bounds.</summary>
    public static class ShelfStocker
    {
        private static readonly Dictionary<string, (Vector3 size, Vector3 offset)> Footprints = new Dictionary<string, (Vector3, Vector3)>();

        /// <summary>Size of a prop and the offset that puts its bounds' bottom-centre on the pivot. Zero if missing.</summary>
        public static (Vector3 size, Vector3 offset) Footprint(string id)
        {
            if (Footprints.TryGetValue(id, out var cached)) return cached;
            var probe = ArtLibrary.Spawn(id, null, Vector3.zero, 0);
            if (probe == null) return Footprints[id] = (Vector3.zero, Vector3.zero);
            var bounds = ArtLibrary.BoundsOf(probe);
            Object.Destroy(probe);
            return Footprints[id] = (bounds.size, new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z));
        }

        /// <summary>
        /// Lines up runs of identical facings along a board in <paramref name="unit"/>'s local space.
        /// Local +Z is the customer side; shallow products get extra rows behind the front one.
        /// </summary>
        public static void Stock(Transform unit, float y, float halfWidth, float halfDepth, IReadOnlyList<string> products, Random rng)
        {
            float x = -halfWidth;
            while (x < halfWidth)
            {
                string id = products[rng.Next(products.Count)];
                var (size, offset) = Footprint(id);
                if (size == Vector3.zero) return;
                int facings = rng.Next(2, 5);
                for (int f = 0; f < facings; f++)
                {
                    float width = size.x + 0.012f;
                    if (x + width > halfWidth) return;
                    int rows = size.z < halfDepth ? Mathf.Clamp(Mathf.FloorToInt(halfDepth * 2 / (size.z + 0.01f)), 1, 3) : 1;
                    for (int r = 0; r < rows; r++)
                    {
                        float z = halfDepth - size.z * 0.5f - r * (size.z + 0.01f);
                        float yaw = (float)(rng.NextDouble() * 6 - 3);
                        var item = ArtLibrary.Spawn(id, unit, Vector3.zero, yaw);
                        if (item != null) item.transform.localPosition = new Vector3(x + width * 0.5f, y, z) + Quaternion.Euler(0, yaw, 0) * offset;
                    }
                    x += width;
                }
            }
        }

        /// <summary>Heaps a produce item into an open crate whose inner floor sits at <paramref name="crate"/>.</summary>
        public static void Heap(Transform parent, string id, Vector3 crate, Vector2 halfInner, Random rng)
        {
            var (size, offset) = Footprint(id);
            if (size == Vector3.zero) return;
            float step = Mathf.Max(size.x, size.z) * 1.02f;
            for (float fx = -halfInner.x; fx <= halfInner.x; fx += step)
                for (float fz = -halfInner.y; fz <= halfInner.y; fz += step)
                {
                    float mound = 0.02f + (halfInner.y - Mathf.Abs(fz)) * 0.25f + (halfInner.x - Mathf.Abs(fx)) * 0.2f;
                    float yaw = rng.Next(0, 360);
                    var item = ArtLibrary.Spawn(id, parent, Vector3.zero, yaw);
                    if (item == null) continue;
                    item.transform.localRotation = Quaternion.Euler(rng.Next(-20, 20), yaw, rng.Next(-20, 20));
                    item.transform.localPosition = crate + new Vector3(fx, mound, fz) + item.transform.localRotation * offset;
                }
        }
    }
}
