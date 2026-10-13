using System.Collections.Generic;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Small procedural meshes for round things primitives cannot make: fountain bowls, a carousel
    /// canopy, a pedestal. A lathe spins a profile of (radius, height) points around the Y axis; u runs
    /// round the circumference, v along the profile, so tiling textures (marble, stripes) wrap cleanly.
    /// </summary>
    public static class MeshKit
    {
        public static Mesh Lathe(string name, IReadOnlyList<Vector2> profile, int segments)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float length = 0f;
            var along = new float[profile.Count];
            for (int i = 1; i < profile.Count; i++) { length += Vector2.Distance(profile[i - 1], profile[i]); along[i] = length; }
            for (int s = 0; s <= segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                for (int i = 0; i < profile.Count; i++)
                {
                    vertices.Add(new Vector3(profile[i].x * cos, profile[i].y, profile[i].x * sin));
                    uvs.Add(new Vector2(s / (float)segments, length > 0 ? along[i] / length : 0));
                }
            }
            int rows = profile.Count;
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < rows - 1; i++)
                {
                    int a = s * rows + i, b = (s + 1) * rows + i;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A visible object with a lathed mesh and a material; no collider.</summary>
        public static GameObject Spin(Transform parent, string name, Vector3 position, IReadOnlyList<Vector2> profile, int segments, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = Lathe(name, profile, segments);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>Both faces of a profile: lathe it there and back so thin shells (bowls, canopies) look solid from below.</summary>
        public static Vector2[] Shell(IReadOnlyList<Vector2> outer, float thickness)
        {
            var points = new List<Vector2>(outer);
            for (int i = outer.Count - 1; i >= 0; i--) points.Add(new Vector2(Mathf.Max(0f, outer[i].x - thickness), outer[i].y - thickness));
            return points.ToArray();
        }
    }
}
