using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>Level-agnostic building blocks for dressing a greybox: panels, tiled skins, blockers, batching.</summary>
    public static class DressingKit
    {
        /// <summary>Visual-only cube: no collider, positioned in the parent's space.</summary>
        public static MeshRenderer Panel(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            return box.GetComponent<MeshRenderer>();
        }

        /// <summary>
        /// Puts a glowing screen exactly on a TV or monitor model instead of floating in front of it. It
        /// reads the model's own triangles: among the faces that point at the viewer, the screen face is
        /// the depth with the most surface area (glass and bezel; knobs and stands are small). The panel
        /// covers the middle of that face and sits 3 mm in front of it. Returns null when the mesh cannot
        /// be read, so callers can fall back.
        /// </summary>
        public static MeshRenderer FitScreen(Transform parent, GameObject device, Vector3 front, float coverage, Material glow, string name = "TV screen")
        {
            if (device == null) return null;
            Vector3 n = front.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized, up = Vector3.Cross(n, right);
            // Forward-facing triangles in world space: (depth of centroid, area, corners).
            var faces = new List<(float depth, float area, Vector3 a, Vector3 b, Vector3 c)>();
            foreach (var filter in device.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var toWorld = filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[t]]), b = toWorld.MultiplyPoint3x4(vertices[triangles[t + 1]]), c = toWorld.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                    var cross = Vector3.Cross(b - a, c - a);
                    float area = cross.magnitude * 0.5f;
                    if (area < 1e-7f) continue;
                    // Facing the viewer either way round (some exports flip winding); flat-on only.
                    if (Mathf.Abs(Vector3.Dot(cross.normalized, n)) < 0.75f) continue;
                    faces.Add((Vector3.Dot((a + b + c) / 3f, n), area, a, b, c));
                }
            }
            if (faces.Count < 2) return null;
            // Area-weighted depth histogram in 2 cm bins; the fullest bin among the front half is the screen face.
            float minD = float.MaxValue, maxD = float.MinValue;
            foreach (var f in faces) { minD = Mathf.Min(minD, f.depth); maxD = Mathf.Max(maxD, f.depth); }
            const float bin = 0.02f;
            int bins = Mathf.Max(1, Mathf.CeilToInt((maxD - minD) / bin) + 1);
            var weight = new float[bins];
            foreach (var f in faces) weight[Mathf.Clamp((int)((f.depth - minD) / bin), 0, bins - 1)] += f.area;
            int first = Mathf.Clamp(bins / 2, 0, bins - 1), mode = bins - 1;
            for (int k = first; k < bins; k++) if (weight[k] > weight[mode]) mode = k;
            float faceDepth = minD + (mode + 0.5f) * bin;
            float minR = float.MaxValue, maxR = float.MinValue, minU = float.MaxValue, maxU = float.MinValue;
            foreach (var f in faces)
            {
                if (Mathf.Abs(f.depth - faceDepth) > 0.03f) continue;
                foreach (var p in new[] { f.a, f.b, f.c })
                {
                    float r = Vector3.Dot(p, right), u = Vector3.Dot(p, up);
                    minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r); minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                }
            }
            if (minR == float.MaxValue) return null;
            float cr = (minR + maxR) * 0.5f, cu = (minU + maxU) * 0.5f;
            float halfW = (maxR - minR) * 0.5f * coverage, halfH = (maxU - minU) * 0.5f * coverage;
            if (halfW < 0.02f || halfH < 0.02f) return null;
            // Where is the glass? A ray at the middle of the face finds it exactly (a CRT bulges, a bezel stands
            // proud of a flat panel); the vertex scan is the fallback when the mesh gives no collider.
            float surface = GlassDepth(device, right * cr + up * cu + n * (maxD + 0.5f), -n);
            if (float.IsNaN(surface))
            {
                surface = float.MinValue;
                foreach (var f in faces)
                {
                    if (Mathf.Abs(f.depth - faceDepth) > 0.03f) continue;
                    foreach (var p in new[] { f.a, f.b, f.c })
                    {
                        float r = Vector3.Dot(p, right) - cr, u = Vector3.Dot(p, up) - cu;
                        if (Mathf.Abs(r) <= halfW * 0.4f && Mathf.Abs(u) <= halfH * 0.4f) surface = Mathf.Max(surface, Vector3.Dot(p, n));
                    }
                }
                if (surface == float.MinValue) surface = faceDepth;
            }
            var panel = Panel(parent, name, Vector3.zero, new Vector3(halfW * 2f, halfH * 2f, 0.002f));
            // World placement: the parent need not sit at the origin.
            panel.transform.SetPositionAndRotation(right * cr + up * cu + n * (surface + 0.002f), Quaternion.LookRotation(-n, Vector3.up));
            panel.sharedMaterial = glow;
            return panel;
        }

        /// <summary>
        /// Depth (along -<paramref name="direction"/>) of the first triangle of <paramref name="device"/> a ray hits, using
        /// throwaway mesh colliders so nothing else in the scene can get in the way. NaN when nothing is hit.
        /// </summary>
        private static float GlassDepth(GameObject device, Vector3 origin, Vector3 direction)
        {
            var temporary = new List<MeshCollider>();
            float best = float.NaN, nearest = float.MaxValue;
            try
            {
                foreach (var filter in device.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    temporary.Add(collider);
                }
                if (temporary.Count == 0) return float.NaN;
                Physics.SyncTransforms();
                var ray = new Ray(origin, direction);
                foreach (var collider in temporary)
                    if (collider.Raycast(ray, out var hit, 2f) && hit.distance < nearest) { nearest = hit.distance; best = Vector3.Dot(hit.point, -direction); }
            }
            finally { foreach (var collider in temporary) if (collider != null) Object.DestroyImmediate(collider); }
            return best;
        }

        /// <summary>Applies a tiling surface sized from the object's scale so texel density matches everywhere.</summary>
        public static void Skin(MeshRenderer renderer, string surface, float metersPerTile, Color tint, float smoothness, bool floor)
        {
            var scale = renderer.transform.lossyScale;
            var tiling = floor ? new Vector2(scale.x, scale.z) / metersPerTile
                               : new Vector2(Mathf.Max(scale.x, scale.z), scale.y) / metersPerTile;
            tiling = new Vector2(Mathf.Max(0.5f, Mathf.Round(tiling.x * 2) / 2), Mathf.Max(0.5f, Mathf.Round(tiling.y * 2) / 2));
            var material = ArtLibrary.Surface(surface, tiling, tint, smoothness);
            if (material != null) renderer.sharedMaterial = material;
        }

        /// <summary>Invisible box that blocks players and carves the NavMesh so the guard walks around it.</summary>
        public static GameObject Blocker(Transform parent, string name, Vector3 floor, Vector3 size)
        {
            var blocker = new GameObject(name + " blocker");
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = floor + Vector3.up * size.y * 0.5f;
            AddSolid(blocker, Vector3.zero, size);
            return blocker;
        }

        /// <summary>Gives a spawned floor prop a collider and NavMesh carve matching its visible bounds.</summary>
        public static GameObject Solid(GameObject prop)
        {
            if (prop == null) return null;
            var t = prop.transform;
            var rotation = t.rotation;
            t.rotation = Quaternion.identity;
            var bounds = ArtLibrary.BoundsOf(prop);
            t.rotation = rotation;
            if (bounds.size == Vector3.zero) return prop;
            var scale = t.lossyScale;
            var center = bounds.center - t.position;
            AddSolid(prop, new Vector3(center.x / scale.x, center.y / scale.y, center.z / scale.z),
                new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z));
            return prop;
        }

        private static void AddSolid(GameObject target, Vector3 center, Vector3 size)
        {
            var box = target.AddComponent<BoxCollider>();
            box.center = center; box.size = size;
            var obstacle = target.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = center; obstacle.size = size;
            obstacle.carving = true;
        }

        /// <summary>Static-batches readable meshes under a root. Text meshes are rebuilt by Unity and stay separate.</summary>
        public static void Batch(Transform root)
        {
            var batchable = new List<GameObject>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || filter.GetComponent<TextMesh>() != null) continue;
                batchable.Add(filter.gameObject);
            }
            StaticBatchingUtility.Combine(batchable.ToArray(), root.gameObject);
        }
    }
}
