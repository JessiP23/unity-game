using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using static NightSupermarket.Game.DressingKit;
using static NightSupermarket.Game.ShelfStocker;
using static NightSupermarket.Game.SignFactory;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Dresses the primitive gameplay layout with fetched Poly Haven props and surfaces, signs, and lights.
    /// Runs after the NavMesh bake. Gameplay colliders stay on the primitives; visuals carry no colliders
    /// unless a prop needs to block movement, in which case it also carves the NavMesh.
    /// </summary>
    public static class StoreDressing
    {
        private static readonly float[] ShelfLevels = { 0.14f, 0.64f, 1.14f, 1.66f };
        private static readonly float[] RackLevels = { 0.06f, 0.44f, 0.92f, 1.42f };
        private static readonly float[] BakeryLevels = { 0.38f, 0.80f, 1.16f };
        private static readonly string[][] Aisles =
        {
            new[] { "russian_food_cans_01", "long_life_food", "russian_food_cans_01", "long_life_food" },
            new[] { "multi_cleaner_bottle", "bleach_bottle", "all_purpose_cleaner", "multi_cleaner_5_litre" },
            new[] { "wine_bottles_01", "hamburger_buns", "long_life_food", "wine_bottles_01" },
        };
        private static readonly string[] AisleNames = { "1  CANNED & DRY FOOD", "2  CLEANING & HOUSEHOLD", "3  WINE & BAKERY" };

        public static void Apply(Transform root, LightingMode mode)
        {
            var lighting = StoreLighting.Create(root);
            var fixed_ = new GameObject("Dressing").transform; fixed_.SetParent(root, false);
            var live = new GameObject("Dressing (lit)").transform; live.SetParent(root, false);
            var hosts = root.GetComponentsInChildren<MeshRenderer>(true);
            bool art = ArtLibrary.Available;
            var rng = new Random(1987);
            int shelf = 0;
            Vector3 placementZone = new Vector3(-4.2f, 0.15f, 7f);
            foreach (var renderer in hosts)
            {
                string name = renderer.gameObject.name;
                var t = renderer.transform;
                switch (name)
                {
                    case "Floor": Skin(renderer, "tiled_floor_001", 2.2f, new Color(0.98f, 0.97f, 0.94f), 0.55f, true); break;
                    case "North wall": case "South wall": case "West wall": case "East wall":
                        Skin(renderer, "white_plaster_02", 3f, new Color(0.9f, 0.9f, 0.88f), 0.15f, false); break;
                    case "Warehouse wall": case "Warehouse south wall left": case "Warehouse south wall right": case "Security partition":
                    case "Staff wall left": case "Staff wall right": case "Staff wall west": case "Staff wall lintel":
                        Skin(renderer, "concrete_block_wall_02", 2.5f, new Color(0.78f, 0.78f, 0.76f), 0.1f, false); break;
                    case "Entrance":
                        renderer.enabled = false;
                        break;
                    case "Clothing placement zone":
                        renderer.enabled = false;
                        placementZone = t.position;
                        break;
                    case "Checkout counter 1": case "Checkout counter 2":
                        renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.55f, 0.56f, 0.58f), 0.55f, 0.8f);
                        if (art) Checkout(fixed_, t.position, name.EndsWith("1") ? 1 : 2); break;
                    case "Produce table 1": case "Produce table 2":
                        Skin(renderer, "wood_table_001", 1.2f, new Color(0.8f, 0.7f, 0.6f), 0.3f, false);
                        if (art) Produce(fixed_, t.position, t.localScale, rng); break;
                    case "Employee counter": Skin(renderer, "wood_table_001", 1.2f, Color.white, 0.35f, false); break;
                    case "Escape door": case "Fire exit": Skin(renderer, "blue_metal_plate", 1.2f, new Color(0.55f, 0.62f, 0.56f), 0.45f, false); break;
                    case "Mezzanine floor": case "Mezzanine landing": Skin(renderer, "smooth_concrete_floor", 2.5f, new Color(0.8f, 0.79f, 0.76f), 0.3f, false); break;
                    case "Escalator": renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.42f, 0.43f, 0.46f), 0.75f, 0.9f); break;
                    case "Escalator side": case "Mezzanine rail": case "Rail post": renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.2f, 0.21f, 0.24f), 0.7f, 0.85f); break;
                    case "Gallery lounge":
                        renderer.sharedMaterial = ArtLibrary.Surface("wool_boucle", new Vector2(3, 3), new Color(0.6f, 0.5f, 0.36f), 0.02f) ?? ArtLibrary.Lit(new Color(0.5f, 0.42f, 0.3f), 0.05f);
                        if (art) DepartmentDressing.Lounge(fixed_, t.position); break;
                    case "Clothing table": case "Home table": Skin(renderer, "wood_table_001", 1.2f, new Color(0.85f, 0.75f, 0.65f), 0.4f, false); break;
                    case "Mannequin platform": renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.92f, 0.92f, 0.9f), 0.8f); break;
                    case "Electronics wall": renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.22f, 0.28f, 0.36f), 0.45f); break;
                    case "Electronics table": renderer.sharedMaterial = ArtLibrary.Lit(new Color(0.88f, 0.88f, 0.9f), 0.7f); break;
                    case "Service desk": Skin(renderer, "wood_table_001", 1.2f, new Color(0.72f, 0.58f, 0.4f), 0.35f, false); break;
                    case "Shirt": renderer.sharedMaterial = ArtLibrary.Surface("cotton_jersey", Vector2.one, new Color(0.25f, 0.4f, 0.8f), 0.05f) ?? renderer.sharedMaterial; break;
                    case "Employee door": Skin(renderer, "blue_metal_plate", 1.5f, new Color(0.7f, 0.75f, 0.85f), 0.45f, false); break;
                }
                if (!art) continue;
                switch (name)
                {
                    case "Shelf": renderer.enabled = false; Gondola(fixed_, t.position, shelf, rng); shelf++; break;
                    case "Bakery shelf": renderer.enabled = false; Bakery(fixed_, t.position, rng); break;
                    case "Warehouse rack": renderer.enabled = false; Racks(fixed_, t.position, rng); break;
                    case "Security desk": renderer.enabled = false; SecurityDesk(fixed_, lighting, t.position); break;
                    case "Surveillance terminal": renderer.enabled = false; break;
                    case "Rescue console": renderer.enabled = false; RescuePanel(fixed_, lighting, t.position); break;
                    case "Collectible crate": case "Debug crate":
                        renderer.enabled = false;
                        var box = ArtLibrary.Spawn("cardboard_box_01", t, Vector3.zero, 0, 1f);
                        if (box != null) box.transform.localScale = new Vector3(0.98f / 0.39f, 0.98f / 0.34f, 0.98f / 0.52f);
                        break;
                    case "Employee key": renderer.enabled = false; KeyCard(t, lighting); break;
                    case "Clothing rack": renderer.enabled = false; DepartmentDressing.ClothingRack(fixed_, t.position, t.localScale, rng); break;
                    case "Clothing table": DepartmentDressing.FoldedTable(fixed_, t.position, t.localScale, rng); break;
                    case "Mannequin platform": DepartmentDressing.DisplayMannequins(root, t.position, t.localScale); break;
                    case "Electronics wall": DepartmentDressing.TvWall(fixed_, lighting, t.position, t.localScale, rng); break;
                    case "Electronics table": DepartmentDressing.GadgetTable(fixed_, t.position, t.localScale, rng); break;
                    case "Home shelf": renderer.enabled = false; DepartmentDressing.HomeShelves(fixed_, t.position, rng); break;
                    case "Home table": DepartmentDressing.DiningTable(fixed_, t.position, t.localScale); break;
                    case "Service desk": DepartmentDressing.ServiceDesk(fixed_, t.position, t.localScale); break;
                }
            }
            Shell(fixed_, lighting, art);
            if (art) { Front(fixed_, lighting); Warehouse(fixed_); StaffRoom(fixed_); StoreFloor(fixed_, rng); DepartmentDressing.Floors(fixed_); DepartmentDressing.LivingRoom(fixed_); }
            DepartmentDressing.Entrance(fixed_, lighting);
            Signs(fixed_, lighting);
            DepartmentDressing.Signs(fixed_, placementZone);
            Fixtures(live, lighting, art);
            Batch(fixed_);
            lighting.Apply(mode);
        }

        // ---- Structure -------------------------------------------------------------------------

        private static void Shell(Transform parent, StoreLighting lighting, bool art)
        {
            var ceiling = Panel(parent, "Ceiling", new Vector3(0, PrimitiveWorld.CeilingY, 0), new Vector3(30, 0.1f, 30));
            var ceilingMaterial = ArtLibrary.Surface("ceiling_interior", new Vector2(12, 12), new Color(0.95f, 0.95f, 0.93f), 0.05f);
            ceiling.sharedMaterial = ceilingMaterial != null ? ceilingMaterial : ArtLibrary.Lit(new Color(0.6f, 0.6f, 0.6f), 0.05f);
            var concrete = ArtLibrary.Surface("smooth_concrete_floor", new Vector2(2.5f, 2.5f), new Color(0.75f, 0.75f, 0.72f), 0.35f);
            var staffFloor = ArtLibrary.Surface("brushed_concrete", new Vector2(2.5f, 3.5f), new Color(0.7f, 0.7f, 0.68f), 0.25f);
            if (concrete != null) Panel(parent, "Warehouse floor", new Vector3(-11.5f, 0.004f, 11.6f), new Vector3(6.9f, 0.008f, 6.8f)).sharedMaterial = concrete;
            if (staffFloor != null) Panel(parent, "Staff floor", new Vector3(11.6f, 0.004f, 10.1f), new Vector3(6.8f, 0.008f, 9.8f)).sharedMaterial = staffFloor;
            var band = ArtLibrary.Lit(new Color(0.92f, 0.28f, 0.12f), 0.25f);
            var kick = ArtLibrary.Lit(new Color(0.82f, 0.82f, 0.8f), 0.2f);
            foreach (var (position, size) in new[]
            {
                (new Vector3(0, 0, 14.73f), new Vector3(29.4f, 1, 0.02f)), (new Vector3(0, 0, -14.73f), new Vector3(29.4f, 1, 0.02f)),
                (new Vector3(-14.73f, 0, 0), new Vector3(0.02f, 1, 29.4f)), (new Vector3(14.73f, 0, 0), new Vector3(0.02f, 1, 29.4f)),
            })
            {
                Panel(parent, "Wall band", position + Vector3.up * 2.32f, Vector3.Scale(size, new Vector3(1, 0.26f, 1))).sharedMaterial = band;
                Panel(parent, "Wall base", position + Vector3.up * 0.075f, Vector3.Scale(size, new Vector3(1, 0.15f, 1))).sharedMaterial = kick;
            }
            var emergencyLamps = new List<Vector3>
            {
                new Vector3(-2, 2.85f, -14.7f), new Vector3(-14.7f, 2.85f, -4), new Vector3(14.7f, 2.85f, -4),
                new Vector3(0, 2.85f, 14.7f), new Vector3(-12.5f, 2.85f, 8.25f), new Vector3(11.5f, 2.85f, 5.2f),
                new Vector3(12.5f, PrimitiveWorld.UpstairsY + 2.85f, 14.7f)
            };
            // Fire exit upstairs: a green sign that never goes out, so lockdown still shows the way.
            Panel(parent, "Fire exit sign", PrimitiveWorld.FireExit + new Vector3(0, 1.5f, -0.25f), new Vector3(0.9f, 0.3f, 0.05f)).sharedMaterial = ArtLibrary.Emissive(new Color(0.2f, 1f, 0.45f), 1.6f);
            lighting.AddGlow(PrimitiveWorld.FireExit + new Vector3(0, 1.4f, -0.6f), new Color(0.3f, 1f, 0.5f), 4f, 1.2f);
            foreach (var point in emergencyLamps)
            {
                Panel(parent, "Emergency lamp", point, new Vector3(0.3f, 0.1f, 0.3f)).sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.15f, 0.1f), 1.2f);
                lighting.AddEmergency(point + Vector3.down * 0.2f, 12f);
            }
            lighting.AddDawn(new Vector3(0, 2.7f, -14.5f), new Vector3(0, 0, -4));
            // Skylights in the raised ceiling over the open sales floor: moonlight after lights out.
            foreach (var at in new[] { new Vector3(-8f, 0, -7f), new Vector3(-8f, 0, 6f), new Vector3(6f, 0, -6f), new Vector3(7.5f, 0, 10f) })
            {
                Panel(parent, "Skylight pane", new Vector3(at.x, PrimitiveWorld.CeilingY - 0.06f, at.z), new Vector3(2.4f, 0.02f, 2.4f)).sharedMaterial = ArtLibrary.Emissive(new Color(0.5f, 0.62f, 0.9f), 0.35f);
                lighting.AddSkylight(new Vector3(at.x, PrimitiveWorld.CeilingY - 0.1f, at.z));
            }
        }

        private static void Fixtures(Transform parent, StoreLighting lighting, bool art)
        {
            int index = 0;
            foreach (float x in new[] { -10f, -3.5f, 3.5f, 10f })
                foreach (float z in new[] { -10f, -3f, 4f, 11f })
                {
                    if (x < -8 && z > 7.6f || x > 8 && z > 5) continue;
                    var visual = art ? ArtLibrary.Spawn("mounted_fluorescent_lights", parent, new Vector3(x, 3.0f, z), 90) : null;
                    lighting.AddFixture(visual, new Vector3(x, 2.92f, z), 7.5f, 13f, index == 5, false);
                    index++;
                    // The sales floor is open to the raised ceiling; fixtures hang on pendants except under the mezzanine.
                    bool underMezzanine = x > 1f && z > 5f;
                    if (!underMezzanine)
                        Panel(parent, "Pendant", new Vector3(x, (3.0f + PrimitiveWorld.CeilingY) * 0.5f, z), new Vector3(0.05f, PrimitiveWorld.CeilingY - 3.0f, 0.05f)).sharedMaterial = ArtLibrary.Lit(new Color(0.15f, 0.15f, 0.16f), 0.4f);
                }
            foreach (float x in new[] { 4f, 11f })
            {
                var visual = art ? ArtLibrary.Spawn("mounted_fluorescent_lights", parent, new Vector3(x, PrimitiveWorld.CeilingY - 0.05f, 10f), 90) : null;
                lighting.AddFixture(visual, new Vector3(x, PrimitiveWorld.CeilingY - 0.13f, 10f), 7.5f, 13f, false, false);
            }
            foreach (var position in new[] { new Vector3(-12.6f, 3f, 10.2f), new Vector3(-10f, 3f, 12f) })
            {
                var cage = art ? ArtLibrary.Spawn("caged_hanging_light", parent, position, 0) : null;
                lighting.AddFixture(cage, position + Vector3.down * 0.9f, 5.5f, 9f, position.x > -11, true, new Color(1f, 0.88f, 0.7f));
                Panel(parent, "Pendant", new Vector3(position.x, (3.0f + PrimitiveWorld.CeilingY) * 0.5f, position.z), new Vector3(0.05f, PrimitiveWorld.CeilingY - 3.0f, 0.05f)).sharedMaterial = ArtLibrary.Lit(new Color(0.15f, 0.15f, 0.16f), 0.4f);
            }
            foreach (var position in new[] { new Vector3(11.5f, 3f, 8f), new Vector3(11.5f, 3f, 12.5f) })
            {
                var visual = art ? ArtLibrary.Spawn("mounted_fluorescent_lights", parent, position, 0) : null;
                lighting.AddFixture(visual, position + Vector3.down * 0.08f, 6.5f, 10f, false, true);
            }
        }

        // ---- Sales floor ------------------------------------------------------------------------

        private static void Gondola(Transform parent, Vector3 center, int aisle, Random rng)
        {
            var products = Aisles[Mathf.Clamp(aisle, 0, Aisles.Length - 1)];
            for (int side = -1; side <= 1; side += 2)
                for (int u = 0; u < 8; u++)
                {
                    var unit = new GameObject("Shelf unit").transform;
                    unit.SetParent(parent, false);
                    unit.localPosition = new Vector3(center.x + side * 0.25f, 0, -3.85f + u * 1.1f);
                    unit.localRotation = Quaternion.Euler(0, side > 0 ? 90 : -90, 0);
                    ArtLibrary.Spawn("steel_frame_shelves_01", unit, Vector3.zero, 0);
                    foreach (float level in ShelfLevels) Stock(unit, level, 0.5f, 0.21f, products, rng);
                    int boxes = rng.Next(0, 3);
                    for (int b = 0; b < boxes; b++)
                        ArtLibrary.Spawn("cardboard_box_01", unit, new Vector3(-0.3f + b * 0.5f, 2.14f + 0.17f, 0), rng.Next(-8, 8) + 90);
                }
        }

        private static void Produce(Transform parent, Vector3 table, Vector3 scale, Random rng)
        {
            string[] fruit = { "food_apple_01", "lemon", "food_lime_01", "yellow_onion", "sweet_potato", "food_avocado_01", "food_apple_01", "lemon", "yellow_onion", "sweet_potato" };
            float top = table.y + scale.y * 0.5f;
            int slot = table.z < -8 ? 0 : 5;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 2; col++)
                {
                    var crate = new Vector3(table.x - 0.27f + col * 0.54f, top, table.z - 1.02f + row * 0.51f);
                    ArtLibrary.Spawn("plastic_crate_02", parent, crate, 90);
                    Heap(parent, fruit[(slot + row * 2 + col) % fruit.Length], crate + Vector3.up * 0.12f, new Vector2(0.17f, 0.2f), rng);
                }
            if (slot == 0)
                for (int b = 0; b < 3; b++)
                    ArtLibrary.Spawn("bananas", parent, new Vector3(table.x + 0.15f, top + 0.27f, table.z - 0.9f + b * 0.35f), 90 + b * 12);
        }

        private static void Bakery(Transform parent, Vector3 center, Random rng)
        {
            string[] bread = { "croissant", "hamburger_buns", "croissant", "hamburger_buns" };
            for (int u = 0; u < 4; u++)
            {
                var unit = new GameObject("Bakery unit").transform;
                unit.SetParent(parent, false);
                unit.localPosition = new Vector3(center.x + 0.02f, 0, center.z - 1.62f + u * 1.08f);
                unit.localRotation = Quaternion.Euler(0, 90, 0);
                ArtLibrary.Spawn("wooden_display_shelves_01", unit, Vector3.zero, 0);
                foreach (float level in BakeryLevels) Stock(unit, level, 0.48f, 0.15f, bread, rng);
            }
        }

        private static void Checkout(Transform parent, Vector3 counter, int lane)
        {
            Panel(parent, "Conveyor belt", counter + new Vector3(0, 0.46f, 0.3f), new Vector3(0.5f, 0.02f, 2.1f)).sharedMaterial = ArtLibrary.Lit(new Color(0.22f, 0.22f, 0.24f), 0.35f);
            ArtLibrary.Spawn("CashRegister_01", parent, counter + new Vector3(0.02f, 0.45f, -1.15f), -90);
            Panel(parent, "Lane pole", counter + new Vector3(0.28f, 1.35f, -1.35f), new Vector3(0.05f, 1.8f, 0.05f)).sharedMaterial = ArtLibrary.Lit(new Color(0.85f, 0.18f, 0.16f), 0.4f);
            Panel(parent, "Lane light", counter + new Vector3(0.28f, 2.3f, -1.35f), new Vector3(0.22f, 0.22f, 0.22f)).sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.25f, 0.2f), 0.8f);
            Sign(parent, lane.ToString(), counter + new Vector3(0.28f, 2.3f, -1.47f), 180, 0.2f, DepartmentDressing.Cajas, Color.white, true, 0.5f);
            ArtLibrary.Spawn("plastic_crate_01", parent, counter + new Vector3(-0.55f, 0.46f, -0.2f), 90, 0.7f);
            ArtLibrary.Spawn("wicker_basket_02", parent, counter + new Vector3(0.55f, 0.46f, 0.4f), 0, 0.85f);
        }

        private static void StoreFloor(Transform parent, Random rng)
        {
            Solid(ArtLibrary.Spawn("WetFloorSign_01", parent, new Vector3(2.4f, 0, -1.5f), 35));
            Solid(ArtLibrary.Spawn("metal_trash_can", parent, new Vector3(12.8f, 0, -14.3f), 180, 0.9f));
            Solid(ArtLibrary.Spawn("korean_fire_extinguisher_01", parent, new Vector3(6.3f, 0.02f, -14.45f), 180));
            Wall(parent, "fire_alarm", new Vector3(6.3f, 1.45f, -14.73f), 0);
            Wall(parent, "wall_clock", new Vector3(14.73f, 2.4f, -7f), -90);
            Wall(parent, "wall_clock", new Vector3(-14.73f, 2.4f, 6f), 90);
            Wall(parent, "security_camera_02", new Vector3(-14.5f, 2.8f, -14.5f), 45);
            Wall(parent, "security_camera_02", new Vector3(14.5f, 2.8f, -14.5f), -45);
            Wall(parent, "security_camera_02", new Vector3(7.7f, 2.8f, 14.5f), -135);
            Wall(parent, "security_camera_01", new Vector3(-7.8f, 2.75f, 7.8f), -135);
            Solid(ArtLibrary.Spawn("plastic_crate_01", parent, new Vector3(7.2f, 0, -12.2f), 0, 0.85f));
            ArtLibrary.Spawn("russian_food_cans_01", parent, new Vector3(7.2f, 0.38f, -12.2f), 15);
            Solid(ArtLibrary.Spawn("plastic_crate_01", parent, new Vector3(13.3f, 0, -12.2f), 180, 0.85f));
            ArtLibrary.Spawn("long_life_food", parent, new Vector3(13.3f, 0.38f, -12.2f), -20);
            Solid(ArtLibrary.Spawn("wicker_basket_02", parent, new Vector3(-2.5f, 0, -13.5f), 20));
            Solid(ArtLibrary.Spawn("wicker_basket_02", parent, new Vector3(2.5f, 0, -13.5f), -15));
            Solid(ArtLibrary.Spawn("plastic_crate_02", parent, new Vector3(-2.55f, 0, -12.6f), 90));
            Solid(ArtLibrary.Spawn("plastic_crate_02", parent, new Vector3(2.55f, 0, -12.6f), 90));
            var stripeA = ArtLibrary.Lit(new Color(0.16f, 0.58f, 0.3f), 0.2f);
            var stripeB = ArtLibrary.Lit(new Color(0.88f, 0.88f, 0.84f), 0.25f);
            for (int i = 0; i < 8; i++)
            {
                float x = -1.75f + i * 0.5f;
                Panel(parent, "Entrance stripe", new Vector3(x, 0.018f, -13f), new Vector3(0.46f, 0.008f, 1.9f)).sharedMaterial = i % 2 == 0 ? stripeA : stripeB;
            }
        }

        private static void Front(Transform parent, StoreLighting lighting)
        {
            Panel(parent, "Push bar", new Vector3(5, 1.0f, -14.42f), new Vector3(1.1f, 0.06f, 0.06f)).sharedMaterial = ArtLibrary.Lit(new Color(0.75f, 0.75f, 0.78f), 0.7f, 0.9f);
            lighting.AddGlow(new Vector3(5, 2.55f, -14.3f), new Color(0.25f, 0.95f, 0.4f), 2.8f, 0.55f);
        }

        // ---- Back of house ----------------------------------------------------------------------

        private static void Racks(Transform parent, Vector3 center, Random rng)
        {
            string[] stock = { "cardboard_box_01", "plastic_container", "industrial_pastic_container", "cardboard_box_01", "multi_cleaner_5_litre" };
            for (int u = 0; u < 4; u++)
            {
                var unit = new GameObject("Rack unit").transform;
                unit.SetParent(parent, false);
                unit.localPosition = new Vector3(center.x + 0.02f, 0, center.z - 1.38f + u * 0.92f);
                unit.localRotation = Quaternion.Euler(0, 90, 0);
                ArtLibrary.Spawn("worn_metal_rack", unit, Vector3.zero, 0);
                foreach (float level in RackLevels)
                {
                    string id = stock[rng.Next(stock.Length)];
                    var (size, offset) = Footprint(id);
                    if (size == Vector3.zero || size.y > 0.46f) id = "cardboard_box_01";
                    Stock(unit, level, 0.43f, 0.27f, new[] { id }, rng);
                }
                ArtLibrary.Spawn("cardboard_box_01", unit, new Vector3(0, 1.9f + 0.17f, 0), rng.Next(-10, 10));
            }
        }

        private static void Warehouse(Transform parent)
        {
            Solid(ArtLibrary.Spawn("hand_truck", parent, new Vector3(-8.7f, 0, 9.1f), -120));
            Blocker(parent, "Crates", new Vector3(-8.75f, 0, 11.3f), new Vector3(0.6f, 0.95f, 1.2f));
            ArtLibrary.Spawn("wooden_crate_02", parent, new Vector3(-8.75f, 0, 11.3f), 90);
            ArtLibrary.Spawn("wooden_crate_02", parent, new Vector3(-8.75f, 0.46f, 11.3f), 93);
            Solid(ArtLibrary.Spawn("Barrel_02", parent, new Vector3(-8.6f, 0, 12.6f), 0));
            Solid(ArtLibrary.Spawn("plastic_container", parent, new Vector3(-13.9f, 0, 8.65f), 0));
            ArtLibrary.Spawn("plastic_container", parent, new Vector3(-13.9f, 0.43f, 8.65f), 4);
            Solid(ArtLibrary.Spawn("trashbag", parent, new Vector3(-9.3f, 0, 14.4f), 20));
            Solid(ArtLibrary.Spawn("trashbag", parent, new Vector3(-8.7f, 0, 14.2f), 140));
            Wall(parent, "industrial_wall_lamp", new Vector3(-8.2f, 2.3f, 10f), -90);
            Wall(parent, "power_box_01", new Vector3(-8.22f, 1.5f, 12.4f), -90);
        }

        private static void SecurityDesk(Transform parent, StoreLighting lighting, Vector3 desk)
        {
            var floor = new Vector3(desk.x, 0, desk.z);
            ArtLibrary.Spawn("metal_office_desk", parent, floor, 180);
            Solid(ArtLibrary.Spawn("metal_stool_03", parent, floor + new Vector3(0.2f, 0, -0.95f), 20));
            ArtLibrary.Spawn("clipboard", parent, floor + new Vector3(0.65f, 0.78f, -0.15f), 15);
            var screen = ArtLibrary.Emissive(new Color(0.35f, 0.95f, 0.65f), 1.6f);
            foreach (float x in new[] { -0.45f, 0.35f })
            {
                ArtLibrary.Spawn("television_02", parent, floor + new Vector3(x, 0.78f, 0.12f), 180);
                Panel(parent, "Monitor screen", floor + new Vector3(x, 0.78f + 0.22f, -0.075f), new Vector3(0.26f, 0.2f, 0.005f)).sharedMaterial = screen;
            }
            lighting.AddGlow(floor + new Vector3(0, 1.1f, -0.5f), new Color(0.35f, 0.95f, 0.65f), 2.2f, 0.45f);
        }

        private static void RescuePanel(Transform parent, StoreLighting lighting, Vector3 console)
        {
            var floor = new Vector3(console.x, 0, console.z);
            Solid(ArtLibrary.Spawn("utility_box_01", parent, floor, 180));
            Panel(parent, "Release lamp", floor + new Vector3(0, 1.18f, -0.2f), new Vector3(0.08f, 0.08f, 0.04f)).sharedMaterial = ArtLibrary.Emissive(new Color(0.2f, 1f, 0.4f), 3f);
            lighting.AddGlow(floor + new Vector3(0, 1.2f, -0.5f), new Color(0.2f, 1f, 0.4f), 1.8f, 0.4f);
        }

        private static void StaffRoom(Transform parent)
        {
            Solid(ArtLibrary.Spawn("ladder_sectioned_01", parent, new Vector3(14.6f, 0, 12f), -90));
            ArtLibrary.Spawn("plastic_broom", parent, new Vector3(14.45f, 0, 9.2f), -90);
            Solid(ArtLibrary.Spawn("metal_trash_can", parent, new Vector3(9.4f, 0, 14.4f), 0, 0.9f));
            Solid(ArtLibrary.Spawn("trashbag", parent, new Vector3(13.2f, 0, 14.3f), 60));
            Solid(ArtLibrary.Spawn("Barrel_02", parent, new Vector3(14.4f, 0, 14.4f), 0));
            Solid(ArtLibrary.Spawn("hand_truck", parent, new Vector3(8.7f, 0, 8.6f), 60));
            Solid(ArtLibrary.Spawn("WetFloorSign_01", parent, new Vector3(9.6f, 0, 7.2f), 0));
            ArtLibrary.Spawn("clipboard", parent, new Vector3(12.3f, 1.2f, 8f), -30);
            Wall(parent, "power_box_01", new Vector3(8.17f, 1.5f, 11f), 90);
            Wall(parent, "wall_clock", new Vector3(8.17f, 2.3f, 8f), 90);
            Solid(ArtLibrary.Spawn("metal_office_desk", parent, new Vector3(11.6f, 0, 13.4f), 180));
            Solid(ArtLibrary.Spawn("metal_stool_03", parent, new Vector3(11.8f, 0, 12.5f), 10));
            ArtLibrary.Spawn("clipboard", parent, new Vector3(11.6f, 0.78f, 13.25f), 20);
        }

        private static void KeyCard(Transform key, StoreLighting lighting)
        {
            var card = Panel(key, "Key card", new Vector3(0, -0.44f, 0), new Vector3(0.6f, 0.035f, 0.38f));
            card.sharedMaterial = ArtLibrary.Lit(new Color(0.92f, 0.92f, 0.9f), 0.6f);
            var stripe = Panel(card.transform, "Key stripe", new Vector3(0, 0.52f, 0.2f), new Vector3(1f, 0.2f, 0.3f));
            stripe.sharedMaterial = ArtLibrary.Emissive(new Color(0.1f, 0.6f, 1f), 2.5f);
            var glow = new GameObject("Key glow").AddComponent<Light>();
            glow.transform.SetParent(key, false);
            glow.type = LightType.Point; glow.range = 1f; glow.intensity = 0.35f; glow.color = new Color(0.2f, 0.7f, 1f);
        }

        // ---- Signs ------------------------------------------------------------------------------

        private static void Signs(Transform parent, StoreLighting lighting)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 5f;
                foreach (float z in new[] { -4.75f, 4.75f })
                    Sign(parent, AisleNames[i], new Vector3(x, 2.5f, z), 0, 0.17f, DepartmentDressing.Supermercado, Color.white, true, 0, true);
            }
            Sign(parent, "SUPERMERCADO", new Vector3(0, 2.72f, 0.2f), 0, 0.22f, DepartmentDressing.Supermercado, Color.white, true, 0, true);
            Sign(parent, "NIGHT MART", new Vector3(0, 2.72f, -14.68f), 180, 0.3f, Color.white, DepartmentDressing.Cajas, false, 0.35f);
            Sign(parent, "SALIDA", new Vector3(5, 2.72f, -14.68f), 180, 0.2f, DepartmentDressing.Entrada, Color.white, false, 1.6f);
            Sign(parent, "EMPLEADOS", new Vector3(10, 2.8f, 4.83f), 0, 0.16f, DepartmentDressing.Empleados, Color.white, false);
            Sign(parent, "ALMACEN", new Vector3(-11.1f, 2.6f, 7.78f), 0, 0.18f, DepartmentDressing.Almacen, Color.white, false);
            Sign(parent, "SOLO PERSONAL", new Vector3(-11.1f, 2.3f, 7.78f), 0, 0.08f, DepartmentDressing.Almacen, Color.white, false);
            Sign(parent, "SEGURIDAD", new Vector3(-12f, 1.9f, 13.03f), 0, 0.12f, DepartmentDressing.Seguridad, Color.white, false);
            Sign(parent, "RELEASE PANEL", new Vector3(-11.1f, 1.55f, 6.32f), 0, 0.06f, new Color(0.12f, 0.14f, 0.16f), new Color(0.3f, 1f, 0.45f), false);
            Sign(parent, "PRODUCE", new Vector3(-12.3f, 2.55f, -8.1f), 90, 0.2f, new Color(0.22f, 0.62f, 0.28f), Color.white, true, 0, true);
            Sign(parent, "BAKERY", new Vector3(-14.68f, 2.0f, 0.5f), -90, 0.2f, new Color(0.82f, 0.55f, 0.18f), Color.white, false);
            Sign(parent, "CAJAS", new Vector3(9.5f, 2.55f, -8.6f), 0, 0.2f, DepartmentDressing.Cajas, Color.white, true, 0, true);
        }

        private static void Wall(Transform parent, string id, Vector3 position, float yaw) => ArtLibrary.Spawn(id, parent, position, yaw);
    }
}
