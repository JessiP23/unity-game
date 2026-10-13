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
        public static readonly Vector3 FireExit = new Vector3(12f, UpstairsY + 1.2f, 14.62f);
        /// <summary>The stairs beside the escalator (x 1.1..2.7): slow, but you can stop on any step.</summary>
        public const float StairsMinX = 1.1f, StairsMaxX = 2.7f;
        public const int StairSteps = 20;
        /// <summary>The mall: atrium south of the supermarket doors, toy store west of the atrium.</summary>
        public const float MallFrontZ = -33f;
        public static readonly Vector3 MallDoor = new Vector3(0f, 0f, MallFrontZ);
        public static readonly Vector3 EscapeDoor = new Vector3(5f, 1.3f, MallFrontZ - 0.1f);
        public static readonly Vector3 CarouselCentre = new Vector3(-21f, 0f, -25.5f);
        public static readonly Vector3 RobotShelf = new Vector3(-16.2f, 0f, -31.2f);

        public static GameObject Box(Transform parent, string label, Vector3 position, Vector3 scale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = label;
            box.transform.SetParent(parent, false); box.transform.position = position; box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            renderer.sharedMaterial = material;
            return box;
        }

        /// <summary>
        /// Keeps a box out of the walkable NavMesh: roofs, ceilings, benches and the fountain would otherwise
        /// bake as floor (an agent could plan a route over a bench, a marker could snap to the toy-store roof).
        /// </summary>
        public static GameObject NotFloor(GameObject box)
        {
            var modifier = box.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            modifier.overrideArea = true; modifier.area = 1; // Not Walkable
            return box;
        }

        /// <summary>Height of the escalator surface at a store z, for anything that must sit on it.</summary>
        public static float RampHeightAt(float z) => Mathf.Clamp01((z - RampStartZ) / (RampEndZ - RampStartZ)) * UpstairsY;
        public static void Build(Transform parent)
        {
            Box(parent, "Floor", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30), Color.gray);
            Box(parent, "North wall", new Vector3(0, 1.5f, 15), new Vector3(30, 3, 0.5f), Color.white);
            // The south wall has the supermarket's door opening (x -1.8..1.8) into the mall atrium.
            Box(parent, "South wall", new Vector3(-8.4f, 1.5f, -15), new Vector3(13.2f, 3, 0.5f), Color.white);
            Box(parent, "South wall", new Vector3(8.4f, 1.5f, -15), new Vector3(13.2f, 3, 0.5f), Color.white);
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
            BuildMall(parent, fixture);
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
            // The slab leaves a stairwell (x 1..3, z 5..13) beside the escalator; the landing strip along the north joins both.
            Box(parent, "Mezzanine floor", new Vector3(9f, UpstairsY - 0.15f, 9f), new Vector3(12f, 0.3f, 8f), slab);
            Box(parent, "Mezzanine floor", new Vector3(8f, UpstairsY - 0.15f, 14f), new Vector3(14f, 0.3f, 2f), slab);
            // Stairs: twenty steps from z 6.5 to z 13, each a box down to the floor so there is no gap beneath.
            float tread = (RampEndZ - RampStartZ) / StairSteps;
            for (int i = 0; i < StairSteps; i++)
            {
                float top = UpstairsY / StairSteps * (i + 1);
                Box(parent, "Stair", new Vector3((StairsMinX + StairsMaxX) * 0.5f, top * 0.5f, RampStartZ + tread * (i + 0.5f)), new Vector3(StairsMaxX - StairsMinX, top, tread + 0.01f), slab);
            }
            Box(parent, "Stair rail", new Vector3(StairsMaxX + 0.04f, UpstairsY * 0.5f + 0.6f, (RampStartZ + RampEndZ) * 0.5f), new Vector3(0.06f, 1.0f, Mathf.Sqrt((RampEndZ - RampStartZ) * (RampEndZ - RampStartZ) + UpstairsY * UpstairsY) - 0.8f), metal)
                .transform.rotation = Quaternion.Euler(-Mathf.Atan2(UpstairsY, RampEndZ - RampStartZ) * Mathf.Rad2Deg, 0, 0);
            Box(parent, "Mezzanine landing", new Vector3(0f, UpstairsY - 0.15f, 14f), new Vector3(2f, 0.3f, 2f), slab);
            // Escalator: a box tilted so its top runs from (z 5, y 0) to (z 13, y 3.3).
            float run = RampEndZ - RampStartZ, rise = UpstairsY;
            float length = Mathf.Sqrt(run * run + rise * rise), angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var tilt = Quaternion.Euler(-angle, 0, 0);
            Vector3 centre = new Vector3(0f, rise * 0.5f - 0.15f * Mathf.Cos(angle * Mathf.Deg2Rad), (RampStartZ + RampEndZ) * 0.5f + 0.15f * Mathf.Sin(angle * Mathf.Deg2Rad));
            var ramp = Box(parent, "Escalator", centre, new Vector3(2f, 0.3f, length), metal);
            ramp.transform.rotation = tilt;
            Vector3 up = tilt * Vector3.up;
            // The belt carries whoever stands on it up the ramp; see MovingSurface for why that counts as moving.
            var belt = ramp.AddComponent<EscalatorBelt>();
            belt.Direction = tilt * Vector3.forward;
            // Side panels start half a metre up the ramp so their leaning end faces never overhang the floor at the foot.
            Vector3 along = tilt * Vector3.forward;
            // Solid skirting up to knee height; the glass balustrade and handrail above it are dressing.
            foreach (float side in new[] { -1.04f, 1.04f })
            {
                // From 0.6 m up the ramp to 0.4 m before its top, so neither end pinches the floor beside it.
                var rail = Box(parent, "Escalator side", centre + up * 0.375f + along * 0.1f + new Vector3(side, 0, 0), new Vector3(0.08f, 0.45f, length - 1.0f), metal);
                rail.transform.rotation = tilt;
            }
            Box(parent, "Mezzanine rail", new Vector3(9f, UpstairsY + 0.55f, 5.04f), new Vector3(12f, 1.1f, 0.08f), metal);
            Box(parent, "Mezzanine rail", new Vector3(3.0f, UpstairsY + 0.55f, 9f), new Vector3(0.08f, 1.1f, 8f), metal);
            Box(parent, "Mezzanine rail", new Vector3(-1.0f, UpstairsY + 0.55f, 14f), new Vector3(0.08f, 1.1f, 2f), metal);
            for (float x = 3f; x <= 15f; x += 2f) Box(parent, "Rail post", new Vector3(x, UpstairsY + 0.55f, 5.04f), new Vector3(0.07f, 1.1f, 0.07f), metal);
            for (float z = 5f; z <= 13f; z += 2f) Box(parent, "Rail post", new Vector3(3.0f, UpstairsY + 0.55f, z), new Vector3(0.07f, 1.1f, 0.07f), metal);
            // Upstairs gallery: Home & Electronics.
            // The gallery TV wall starts at x 3.8 so the landing above the stairs and the escalator stays a full 1.75 m deep.
            Box(parent, "Electronics wall", new Vector3(7.0f, UpstairsY + 1.0f, 14.45f), new Vector3(6.4f, 2.0f, 0.55f), fixture);
            Box(parent, "Electronics table", new Vector3(4.6f, UpstairsY + 0.45f, 10.5f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
            Box(parent, "Electronics table", new Vector3(8.0f, UpstairsY + 0.45f, 10.5f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
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
            Box(parent, "Electronics table", new Vector3(4.0f, 0.45f, 11.0f), new Vector3(1.6f, 0.9f, 0.8f), fixture);
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

        /// <summary>
        /// The mall around the supermarket. The atrium (x -12..12, z -33..-15) lies through the front
        /// doors: a fountain, planters, benches, a glass roof at 8 m, and two sweeping security cameras.
        /// The toy store (x -30..-12, z -33..-18) opens off its west side and holds the carousel and the
        /// tall shelf with the robot. The mall's own doors are at z -33; the escape door is beside them.
        /// </summary>
        private static void BuildMall(Transform parent, Color fixture)
        {
            var wall = Color.white;
            var stone = new Color(0.72f, 0.7f, 0.66f);
            float roof = 8f;
            Box(parent, "Atrium floor", new Vector3(0, -0.5f, -24f), new Vector3(24f, 1f, 18f), Color.gray);
            Box(parent, "Atrium wall", new Vector3(12f, roof * 0.5f, -24f), new Vector3(0.5f, roof, 18f), wall);
            Box(parent, "Atrium wall", new Vector3(-12f, roof * 0.5f, -16.5f), new Vector3(0.5f, roof, 3f), wall);     // north of the toy store door
            Box(parent, "Atrium wall", new Vector3(-12f, roof * 0.5f, -30f), new Vector3(0.5f, roof, 6f), wall);       // south of it
            Box(parent, "Atrium wall", new Vector3(-12f, roof * 0.5f + 2f, -22.5f), new Vector3(0.5f, roof - 4f, 9f), wall); // lintel over the toy store opening (z -27..-18, 4 m tall)
            // Mall front at z -33: the mall doors (x -1.8..1.8, 3 m) and the escape door (x 4.3..5.7, 2.6 m) are openings.
            Box(parent, "Mall front", new Vector3(-6.9f, roof * 0.5f, MallFrontZ), new Vector3(10.2f, roof, 0.5f), wall);
            Box(parent, "Mall front", new Vector3(3.05f, roof * 0.5f, MallFrontZ), new Vector3(2.5f, roof, 0.5f), wall);
            Box(parent, "Mall front", new Vector3(8.85f, roof * 0.5f, MallFrontZ), new Vector3(6.3f, roof, 0.5f), wall);
            Box(parent, "Mall front", new Vector3(0f, 5.5f, MallFrontZ), new Vector3(3.6f, 5f, 0.5f), wall);
            Box(parent, "Mall front", new Vector3(5f, 5.3f, MallFrontZ), new Vector3(1.4f, 5.4f, 0.5f), wall);
            // Supermarket front above the door opening is the existing wall; the atrium's north wall is the supermarket's south wall.
            NotFloor(Box(parent, "Atrium roof", new Vector3(0, roof + 0.1f, -24f), new Vector3(24.5f, 0.2f, 18.5f), new Color(0.6f, 0.7f, 0.85f)));
            foreach (float z in new[] { -18f, -24f, -30f })
                NotFloor(Box(parent, "Roof beam", new Vector3(0, roof - 0.2f, z), new Vector3(24f, 0.25f, 0.25f), new Color(0.3f, 0.3f, 0.33f)));
            // Fountain in the middle: a rim you can hide behind, water inside it you cannot walk into.
            NotFloor(Box(parent, "Fountain basin", new Vector3(0, 0.3f, -21.6f), new Vector3(5.2f, 0.6f, 0.4f), stone));
            NotFloor(Box(parent, "Fountain basin", new Vector3(0, 0.3f, -26.4f), new Vector3(5.2f, 0.6f, 0.4f), stone));
            NotFloor(Box(parent, "Fountain basin", new Vector3(-2.4f, 0.3f, -24f), new Vector3(0.4f, 0.6f, 5.2f), stone));
            NotFloor(Box(parent, "Fountain basin", new Vector3(2.4f, 0.3f, -24f), new Vector3(0.4f, 0.6f, 5.2f), stone));
            NotFloor(Box(parent, "Fountain bed", new Vector3(0, 0.1f, -24f), new Vector3(4.6f, 0.2f, 4.6f), stone));
            NotFloor(Box(parent, "Fountain column", new Vector3(0, 1.1f, -24f), new Vector3(0.8f, 2.0f, 0.8f), stone));
            // The atrium roof is higher than the supermarket's walls: close the slot above its front.
            Box(parent, "Atrium wall", new Vector3(0, (CeilingY + roof) * 0.5f, -15f), new Vector3(24.5f, roof - CeilingY, 0.4f), wall);
            // Planters in the corners and benches between: cover that reads as furniture.
            foreach (var at in new[] { new Vector3(-9.5f, 0, -17.5f), new Vector3(9.5f, 0, -17.5f), new Vector3(-9.5f, 0, -30.5f), new Vector3(9.5f, 0, -30.5f) })
                NotFloor(Box(parent, "Planter", at + new Vector3(0, 0.4f, 0), new Vector3(1.8f, 0.8f, 1.8f), stone));
            foreach (var (at, yaw) in new[] { (new Vector3(-5.5f, 0, -20f), 0f), (new Vector3(5.5f, 0, -28f), 0f), (new Vector3(-6f, 0, -28f), 90f), (new Vector3(6f, 0, -20f), 90f) })
            {
                var bench = NotFloor(Box(parent, "Bench", at + new Vector3(0, 0.23f, 0), new Vector3(1.9f, 0.46f, 0.5f), fixture));
                bench.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
            // Toy store.
            Box(parent, "Toy floor", new Vector3(-21f, -0.5f, -25.5f), new Vector3(18f, 1f, 15f), Color.gray);
            Box(parent, "Toy wall", new Vector3(-30f, 2f, -25.5f), new Vector3(0.5f, 4f, 15f), wall);
            Box(parent, "Toy wall", new Vector3(-21f, 2f, -33f), new Vector3(18f, 4f, 0.5f), wall);
            Box(parent, "Toy wall", new Vector3(-21f, 2f, -18f), new Vector3(18f, 4f, 0.5f), wall);
            NotFloor(Box(parent, "Toy ceiling", new Vector3(-21f, 4.1f, -25.5f), new Vector3(18f, 0.2f, 15f), new Color(0.93f, 0.9f, 0.72f)));
            // Shelves along the west wall and the robot's tall shelf by the east wall; the step stool in front of it.
            Box(parent, "Toy shelf", new Vector3(-28.9f, 1.0f, -21.5f), new Vector3(0.9f, 2.0f, 5.5f), fixture);
            Box(parent, "Toy shelf", new Vector3(-28.9f, 1.0f, -29.5f), new Vector3(0.9f, 2.0f, 5.5f), fixture);
            Box(parent, "Toy shelf", new Vector3(-21f, 1.0f, -32.1f), new Vector3(6f, 2.0f, 0.9f), fixture);
            NotFloor(Box(parent, "Robot shelf", RobotShelf + new Vector3(0, 1.5f, 0), new Vector3(2.2f, 3.0f, 0.9f), fixture));
            NotFloor(Box(parent, "Step stool", RobotShelf + new Vector3(0, 0.25f, 1.0f), new Vector3(0.8f, 0.5f, 0.7f), new Color(0.85f, 0.6f, 0.2f)));
        }
    }
}
