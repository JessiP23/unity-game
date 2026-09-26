using UnityEngine;
namespace NightSupermarket.Game
{
    public static class PrimitiveWorld
    {
        public static GameObject Box(Transform parent, string label, Vector3 position, Vector3 scale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = label;
            box.transform.SetParent(parent, false); box.transform.position = position; box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            renderer.sharedMaterial = material;
            return box;
        }
        public static void Build(Transform parent)
        {
            Box(parent, "Floor", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30), Color.gray);
            Box(parent, "North wall", new Vector3(0, 1.5f, 15), new Vector3(30, 3, 0.5f), Color.white);
            Box(parent, "South wall", new Vector3(0, 1.5f, -15), new Vector3(30, 3, 0.5f), Color.white);
            Box(parent, "West wall", new Vector3(-15, 1.5f, 0), new Vector3(0.5f, 3, 30), Color.white);
            Box(parent, "East wall", new Vector3(15, 1.5f, 0), new Vector3(0.5f, 3, 30), Color.white);
            for (int i = -1; i <= 1; i++)
                Box(parent, "Shelf", new Vector3(i * 5, 1, 0), new Vector3(1.5f, 2, 9), new Color(0.3f, 0.35f, 0.4f));
            Box(parent, "Entrance", new Vector3(0, 0.02f, -13), new Vector3(4, 0.04f, 2), new Color(0.2f, 0.45f, 0.25f));
            Box(parent, "Warehouse wall", new Vector3(-8, 1.5f, 11.75f), new Vector3(0.4f, 3, 6.5f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall left", new Vector3(-13.2f, 1.5f, 8), new Vector3(3.2f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall right", new Vector3(-9.4f, 1.5f, 8), new Vector3(2.4f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Security partition", new Vector3(-12, 1.5f, 13.2f), new Vector3(6, 3, 0.3f), new Color(0.15f, 0.18f, 0.22f));
            Box(parent, "Employee counter", new Vector3(12, 0.6f, 8), new Vector3(2, 1.2f, 1), new Color(0.35f, 0.3f, 0.45f));
        }
    }
}
