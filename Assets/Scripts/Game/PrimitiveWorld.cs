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
                Box(parent, "Shelf", new Vector3(i * 5, 1.07f, 0), new Vector3(1.05f, 2.14f, 8.8f), new Color(0.3f, 0.35f, 0.4f));
            var fixture = new Color(0.3f, 0.3f, 0.32f);
            Box(parent, "Checkout counter 1", new Vector3(8, 0.45f, -10.5f), new Vector3(0.7f, 0.9f, 3f), fixture);
            Box(parent, "Checkout counter 2", new Vector3(11, 0.45f, -10.5f), new Vector3(0.7f, 0.9f, 3f), fixture);
            Box(parent, "Produce table 1", new Vector3(-12.3f, 0.4f, -10f), new Vector3(1.1f, 0.8f, 2.6f), fixture);
            Box(parent, "Produce table 2", new Vector3(-12.3f, 0.4f, -6.2f), new Vector3(1.1f, 0.8f, 2.6f), fixture);
            Box(parent, "Bakery shelf", new Vector3(-14.55f, 0.78f, 0.5f), new Vector3(0.4f, 1.56f, 4.4f), fixture);
            Box(parent, "Warehouse rack", new Vector3(-14.45f, 0.95f, 10.8f), new Vector3(0.6f, 1.9f, 3.7f), fixture);
            Box(parent, "Security desk", new Vector3(-12f, 0.39f, 12.55f), new Vector3(2f, 0.78f, 0.95f), fixture);
            var staff = new Color(0.25f, 0.22f, 0.2f);
            Box(parent, "Staff wall left", new Vector3(8.5f, 1.5f, 5f), new Vector3(1f, 3, 0.3f), staff);
            Box(parent, "Staff wall right", new Vector3(13f, 1.5f, 5f), new Vector3(4f, 3, 0.3f), staff);
            Box(parent, "Staff wall west", new Vector3(8f, 1.5f, 10f), new Vector3(0.3f, 3, 10f), staff);
            Box(parent, "Staff wall lintel", new Vector3(10f, 2.8f, 5f), new Vector3(2f, 0.4f, 0.3f), staff);
            Box(parent, "Entrance", new Vector3(0, 0.02f, -13), new Vector3(4, 0.04f, 2), new Color(0.2f, 0.45f, 0.25f));
            Box(parent, "Warehouse wall", new Vector3(-8, 1.5f, 11.75f), new Vector3(0.4f, 3, 6.5f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall left", new Vector3(-13.2f, 1.5f, 8), new Vector3(3.2f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall right", new Vector3(-9.4f, 1.5f, 8), new Vector3(2.4f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Security partition", new Vector3(-12, 1.5f, 13.2f), new Vector3(6, 3, 0.3f), new Color(0.15f, 0.18f, 0.22f));
            Box(parent, "Employee counter", new Vector3(12, 0.6f, 8), new Vector3(2, 1.2f, 1), new Color(0.35f, 0.3f, 0.45f));
        }
    }
}
