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
