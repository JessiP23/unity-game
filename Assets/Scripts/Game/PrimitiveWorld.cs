using UnityEngine;
namespace NightSupermarket.Game
{
    public static class PrimitiveWorld
    {
        /// <summary>Top of the upstairs floor. The ground floor keeps its 3 m headroom underneath.</summary>
        public const float UpstairsY = 3.3f;
        /// <summary>Ceiling height of the open sales floor; the mezzanine looks down onto it.</summary>
        public const float CeilingY = 6.35f;
        /// <summary>
        /// The escalator: 2 m wide at x = -1..1, rising from z = 6.5 (floor, two metres clear of the middle
        /// gondola which ends at z 4.4) to z = 13 (upstairs), then a flat landing to z = 15.
        /// </summary>
        public const float RampStartZ = 6.5f, RampEndZ = 13f;
        public static readonly Vector3 FireExit = new Vector3(9.5f, UpstairsY + 1.2f, 14.62f);

        public static GameObject Box(Transform parent, string label, Vector3 position, Vector3 scale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = label;
            box.transform.SetParent(parent, false); box.transform.position = position; box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            renderer.sharedMaterial = material;
            return box;
        }

        /// <summary>Height of the escalator surface at a store z, for anything that must sit on it.</summary>
        public static float RampHeightAt(float z) => Mathf.Clamp01((z - RampStartZ) / (RampEndZ - RampStartZ)) * UpstairsY;
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
            BuildDepartments(parent, fixture);
            BuildMezzanine(parent, fixture);
        }

        /// <summary>
        /// The second floor: a slab over Electronics and the staff room (x 1..15, z 5..15) reached by an
        /// escalator along the Clothing/Electronics boundary, with rails on its open edges so you can
        /// watch the sales floor from above. The fire exit on its north wall is the way out once the
        /// front shutter comes down at lockdown. The store's outer walls grow to the raised ceiling.
        /// </summary>
        private static void BuildMezzanine(Transform parent, Color fixture)
        {
            var slab = new Color(0.72f, 0.7f, 0.66f);
            var metal = new Color(0.25f, 0.26f, 0.28f);
            float upper = (3f + CeilingY) * 0.5f, upperHeight = CeilingY - 3f;
            Box(parent, "North wall", new Vector3(0, upper, 15), new Vector3(30, upperHeight, 0.5f), Color.white);
            Box(parent, "South wall", new Vector3(0, upper, -15), new Vector3(30, upperHeight, 0.5f), Color.white);
            Box(parent, "West wall", new Vector3(-15, upper, 0), new Vector3(0.5f, upperHeight, 30), Color.white);
            Box(parent, "East wall", new Vector3(15, upper, 0), new Vector3(0.5f, upperHeight, 30), Color.white);
            Box(parent, "Mezzanine floor", new Vector3(8f, UpstairsY - 0.15f, 10f), new Vector3(14f, 0.3f, 10f), slab);
            Box(parent, "Mezzanine landing", new Vector3(0f, UpstairsY - 0.15f, 14f), new Vector3(2f, 0.3f, 2f), slab);
            // Escalator: a box tilted so its top runs from (z 5, y 0) to (z 13, y 3.3).
            float run = RampEndZ - RampStartZ, rise = UpstairsY;
            float length = Mathf.Sqrt(run * run + rise * rise), angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var tilt = Quaternion.Euler(-angle, 0, 0);
            Vector3 centre = new Vector3(0f, rise * 0.5f - 0.15f * Mathf.Cos(angle * Mathf.Deg2Rad), (RampStartZ + RampEndZ) * 0.5f + 0.15f * Mathf.Sin(angle * Mathf.Deg2Rad));
            var ramp = Box(parent, "Escalator", centre, new Vector3(2f, 0.3f, length), metal);
            ramp.transform.rotation = tilt;
            Vector3 up = tilt * Vector3.up;
            // Side panels start half a metre up the ramp so their leaning end faces never overhang the floor at the foot.
            Vector3 along = tilt * Vector3.forward;
            foreach (float side in new[] { -1.04f, 1.04f })
            {
                var rail = Box(parent, "Escalator side", centre + up * 0.65f + along * 0.3f + new Vector3(side, 0, 0), new Vector3(0.08f, 1.0f, length - 0.6f), metal);
                rail.transform.rotation = tilt;
            }
            Box(parent, "Mezzanine rail", new Vector3(8f, UpstairsY + 0.55f, 5.04f), new Vector3(14f, 1.1f, 0.08f), metal);
            Box(parent, "Mezzanine rail", new Vector3(1.0f, UpstairsY + 0.55f, 9f), new Vector3(0.08f, 1.1f, 8f), metal);
            Box(parent, "Mezzanine rail", new Vector3(-1.0f, UpstairsY + 0.55f, 14f), new Vector3(0.08f, 1.1f, 2f), metal);
            for (float x = 1f; x <= 15f; x += 2f) Box(parent, "Rail post", new Vector3(x, UpstairsY + 0.55f, 5.04f), new Vector3(0.07f, 1.1f, 0.07f), metal);
            for (float z = 5f; z <= 13f; z += 2f) Box(parent, "Rail post", new Vector3(1.0f, UpstairsY + 0.55f, z), new Vector3(0.07f, 1.1f, 0.07f), metal);
            // Upstairs gallery: Home & Electronics.
            Box(parent, "Electronics wall", new Vector3(4.2f, UpstairsY + 1.0f, 14.45f), new Vector3(6.4f, 2.0f, 0.55f), fixture);
            Box(parent, "Electronics table", new Vector3(3.0f, UpstairsY + 0.45f, 10.5f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Electronics table", new Vector3(6.4f, UpstairsY + 0.45f, 10.5f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            var rug = Box(parent, "Gallery lounge", new Vector3(11.8f, UpstairsY + 0.01f, 10.5f), new Vector3(5.2f, 0.02f, 5.2f), new Color(0.5f, 0.42f, 0.3f));
            Object.Destroy(rug.GetComponent<Collider>());
            Box(parent, "Fire exit", FireExit, new Vector3(1.4f, 2.4f, 0.3f), new Color(0.2f, 0.42f, 0.3f));
        }
        /// <summary>Gameplay fixtures for the non-food departments; dressing supplies their look.</summary>
        private static void BuildDepartments(Transform parent, Color fixture)
        {
            Box(parent, "Clothing rack", new Vector3(-6.2f, 0.75f, 11.2f), new Vector3(1.8f, 1.5f, 0.45f), fixture);
            Box(parent, "Clothing rack", new Vector3(-2.4f, 0.75f, 11.2f), new Vector3(1.8f, 1.5f, 0.45f), fixture);
            Box(parent, "Clothing table", new Vector3(-6.2f, 0.45f, 9.0f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Clothing table", new Vector3(-2.4f, 0.45f, 9.0f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Mannequin platform", new Vector3(-4.2f, 0.075f, 13.9f), new Vector3(3.0f, 0.15f, 1.2f), fixture);
            Box(parent, "Electronics wall", new Vector3(4.2f, 1.0f, 14.45f), new Vector3(6.4f, 2.0f, 0.55f), fixture);
            Box(parent, "Electronics table", new Vector3(2.4f, 0.45f, 11.0f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Electronics table", new Vector3(6.0f, 0.45f, 11.0f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Home shelf", new Vector3(14.5f, 0.8f, -4.5f), new Vector3(0.45f, 1.6f, 3.0f), fixture);
            Box(parent, "Home table", new Vector3(11.2f, 0.375f, -5.2f), new Vector3(1.4f, 0.75f, 0.85f), fixture);
            Box(parent, "Service desk", new Vector3(-6.8f, 0.525f, -13.3f), new Vector3(2.4f, 1.05f, 0.7f), fixture);
            Box(parent, "Entrance", new Vector3(0, 0.02f, -13), new Vector3(4, 0.04f, 2), new Color(0.2f, 0.45f, 0.25f));
            Box(parent, "Warehouse wall", new Vector3(-8, 1.5f, 11.75f), new Vector3(0.4f, 3, 6.5f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall left", new Vector3(-13.2f, 1.5f, 8), new Vector3(3.2f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Warehouse south wall right", new Vector3(-9.4f, 1.5f, 8), new Vector3(2.4f, 3, 0.4f), new Color(0.25f, 0.22f, 0.2f));
            Box(parent, "Security partition", new Vector3(-12, 1.5f, 13.2f), new Vector3(6, 3, 0.3f), new Color(0.15f, 0.18f, 0.22f));
            Box(parent, "Employee counter", new Vector3(12, 0.6f, 8), new Vector3(2, 1.2f, 1), new Color(0.35f, 0.3f, 0.45f));
        }
    }
}
