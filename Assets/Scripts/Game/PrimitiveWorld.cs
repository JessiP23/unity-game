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
        }
    }
}
