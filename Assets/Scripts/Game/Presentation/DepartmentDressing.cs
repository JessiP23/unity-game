using System.Collections.Generic;
using UnityEngine;
using static NightSupermarket.Game.DressingKit;
using static NightSupermarket.Game.ShelfStocker;
using static NightSupermarket.Game.SignFactory;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>Looks for the non-food departments: clothing, electronics, home, customer service, entrance.</summary>
    public static class DepartmentDressing
    {
        public static readonly Color Entrada = new Color(0.18f, 0.68f, 0.36f);
        public static readonly Color Cajas = new Color(0.86f, 0.2f, 0.22f);
        public static readonly Color Supermercado = new Color(0.93f, 0.55f, 0.14f);
        public static readonly Color Ropa = new Color(0.58f, 0.32f, 0.78f);
        public static readonly Color Hogar = new Color(0.95f, 0.78f, 0.18f);
        public static readonly Color Electronica = new Color(0.18f, 0.72f, 0.86f);
        public static readonly Color Almacen = new Color(0.22f, 0.48f, 0.86f);
        public static readonly Color Empleados = new Color(0.55f, 0.38f, 0.82f);
        public static readonly Color Seguridad = new Color(0.92f, 0.42f, 0.62f);
        private static readonly string[] Fabrics = { "cotton_jersey", "rough_linen", "ribbed_corduroy" };
        private static readonly Color[] Garments =
        {
            new Color(0.15f, 0.2f, 0.45f), new Color(0.75f, 0.72f, 0.66f), new Color(0.55f, 0.1f, 0.12f), new Color(0.1f, 0.1f, 0.1f),
            new Color(0.3f, 0.45f, 0.3f), new Color(0.85f, 0.55f, 0.2f), new Color(0.5f, 0.55f, 0.6f), new Color(0.65f, 0.35f, 0.45f),
        };

        public static Material Fabric(Random rng) =>
            ArtLibrary.Surface(Fabrics[rng.Next(Fabrics.Length)], Vector2.one, Garments[rng.Next(Garments.Length)], 0.05f)
            ?? ArtLibrary.Lit(Garments[rng.Next(Garments.Length)], 0.05f);

        public static void Floors(Transform parent)
        {
            var wood = ArtLibrary.Surface("wood_table_001", new Vector2(4, 5), new Color(0.88f, 0.74f, 0.58f), 0.4f);
            if (wood != null) Panel(parent, "Clothing floor", new Vector3(-4.1f, 0.004f, 9.95f), new Vector3(7.7f, 0.008f, 9.5f)).sharedMaterial = wood;
            var tile = ArtLibrary.Surface("tiled_floor_001", new Vector2(3, 4), new Color(0.88f, 0.94f, 0.98f), 0.5f);
            if (tile != null) Panel(parent, "Electronics floor", new Vector3(3.9f, 0.004f, 9.95f), new Vector3(7.7f, 0.008f, 9.5f)).sharedMaterial = tile;
            var rug = ArtLibrary.Surface("wool_boucle", new Vector2(3, 2), new Color(0.78f, 0.68f, 0.42f), 0.02f);
            if (rug != null) Panel(parent, "Home rug", new Vector3(12.6f, 0.006f, 0f), new Vector3(3.6f, 0.012f, 4.4f)).sharedMaterial = rug;
            Panel(parent, "Clothing stripe", new Vector3(-4.1f, 0.01f, 5.15f), new Vector3(7.7f, 0.01f, 0.18f)).sharedMaterial = ArtLibrary.Lit(Ropa, 0.25f);
            Panel(parent, "Electronics stripe", new Vector3(3.9f, 0.01f, 5.15f), new Vector3(7.7f, 0.01f, 0.18f)).sharedMaterial = ArtLibrary.Lit(Electronica, 0.25f);
            Panel(parent, "Fitting mirror", new Vector3(-7.82f, 1.35f, 10.2f), new Vector3(0.02f, 1.6f, 1.1f)).sharedMaterial = ArtLibrary.Glass(new Color(0.75f, 0.85f, 0.95f, 0.35f));
            var drop = ArtLibrary.Surface("wool_boucle", new Vector2(2, 2), new Color(0.22f, 0.38f, 0.24f), 0.04f) ?? ArtLibrary.Lit(new Color(0.28f, 0.4f, 0.3f), 0.08f);
            Panel(parent, "Clothing drop", new Vector3(-4.2f, 0.02f, 7f), new Vector3(2.8f, 0.012f, 2.8f)).sharedMaterial = drop;
        }

        /// <summary>Chrome rail with garments hung along it.</summary>
        public static void ClothingRack(Transform parent, Vector3 center, Vector3 size, Random rng)
        {
            var chrome = ArtLibrary.Lit(new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
            float half = size.x * 0.5f, top = center.y + size.y * 0.5f - 0.05f;
            Panel(parent, "Rail", new Vector3(center.x, top, center.z), new Vector3(size.x, 0.03f, 0.03f)).sharedMaterial = chrome;
            foreach (float side in new[] { -1f, 1f })
            {
                Panel(parent, "Upright", new Vector3(center.x + side * half, top * 0.5f, center.z), new Vector3(0.03f, top, 0.03f)).sharedMaterial = chrome;
                Panel(parent, "Foot", new Vector3(center.x + side * half, 0.015f, center.z), new Vector3(0.05f, 0.03f, 0.5f)).sharedMaterial = chrome;
            }
            for (float x = -half + 0.12f; x <= half - 0.1f; x += 0.11f)
            {
                float length = 0.65f + (float)rng.NextDouble() * 0.25f;
                var garment = Panel(parent, "Garment", new Vector3(center.x + x, top - 0.05f - length * 0.5f, center.z), new Vector3(0.035f, length, 0.44f));
                garment.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 8 - 4, 0);
                garment.sharedMaterial = Fabric(rng);
            }
        }

        /// <summary>Table of folded shirt stacks, with shoes or hats at the ends.</summary>
        public static void FoldedTable(Transform parent, Vector3 center, Vector3 size, Random rng)
        {
            float top = center.y + size.y * 0.5f;
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    var material = Fabric(rng);
                    int height = 2 + rng.Next(4);
                    for (int h = 0; h < height; h++)
                    {
                        var shirt = Panel(parent, "Folded shirt", new Vector3(center.x - 0.55f + ix * 0.36f, top + 0.025f + h * 0.05f, center.z - 0.17f + iz * 0.34f), new Vector3(0.3f, 0.045f, 0.26f));
                        shirt.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 6 - 3, 0);
                        shirt.sharedMaterial = material;
                    }
                }
            ArtLibrary.Spawn(rng.NextDouble() < 0.5 ? "rubber_boots" : "fishermans_hat", parent, new Vector3(center.x + size.x * 0.5f + 0.25f, 0, center.z), 90);
        }

        /// <summary>Posed display mannequins. They never move, so players standing among them blend in.</summary>
        public static void DisplayMannequins(Transform root, Vector3 platform, Vector3 size)
        {
            var group = new GameObject("Display mannequins").transform;
            group.SetParent(root, false);
            float top = platform.y + size.y * 0.5f;
            float[] offsets = { -1.2f, -0.4f, 0.4f, 1.2f };
            for (int i = 0; i < offsets.Length; i++)
            {
                var host = new GameObject("Display mannequin " + (i + 1));
                host.transform.SetParent(group, false);
                host.transform.SetPositionAndRotation(new Vector3(platform.x + offsets[i], top, platform.z), Quaternion.Euler(0, 180 + (i - 1.5f) * 12f, 0));
                var body = host.AddComponent<CapsuleCollider>();
                body.radius = 0.25f; body.height = 1.8f; body.center = new Vector3(0, 0.9f, 0);
                var visual = CharacterVisual.Attach(host.transform, i % 2 == 0 ? CharacterProfile.MannequinMale : CharacterProfile.MannequinFemale);
                if (visual != null) visual.HoldPose(1.0 + i * 2.3);
            }
        }

        /// <summary>Display wall of glowing televisions.</summary>
        public static void TvWall(Transform parent, StoreLighting lighting, Vector3 center, Vector3 size, Random rng)
        {
            float front = center.z - size.z * 0.5f;
            float floor = center.y - size.y * 0.5f; // the same wall stands on the ground floor and on the mezzanine
            Panel(parent, "TV ledge", new Vector3(center.x, floor + 1.0f, front - 0.2f), new Vector3(size.x, 0.05f, 0.45f)).sharedMaterial = ArtLibrary.Lit(new Color(0.1f, 0.1f, 0.11f), 0.5f);
            Color[] glows = { new Color(0.3f, 0.55f, 1f), new Color(0.4f, 1f, 0.6f), new Color(1f, 0.6f, 0.3f), new Color(0.8f, 0.4f, 1f) };
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    float x = center.x - size.x * 0.5f + 0.8f + i * 1.6f;
                    float y = row == 0 ? floor + 1.03f : center.y + size.y * 0.5f;
                    float z = row == 0 ? front - 0.2f : center.z;
                    string id = (i + row) % 2 == 0 ? "Television_01" : "television_02";
                    var tv = ArtLibrary.Spawn(id, parent, new Vector3(x, y, z), 180);
                    if (tv == null) continue;
                    var bounds = ArtLibrary.BoundsOf(tv);
                    var glow = glows[rng.Next(glows.Length)];
                    Panel(parent, "TV screen", new Vector3(x, bounds.center.y + bounds.size.y * 0.06f, bounds.min.z - 0.005f), new Vector3(bounds.size.x * 0.62f, bounds.size.y * 0.5f, 0.005f))
                        .sharedMaterial = ArtLibrary.Emissive(glow, 1.4f);
                }
            lighting.AddGlow(new Vector3(center.x, floor + 1.6f, front - 0.8f), new Color(0.45f, 0.75f, 1f), 3.2f, 0.4f);
        }

        public static void GadgetTable(Transform parent, Vector3 center, Vector3 size, Random rng)
        {
            var unit = new GameObject("Gadget table").transform;
            unit.SetParent(parent, false);
            unit.localPosition = new Vector3(center.x, 0, center.z);
            unit.localRotation = Quaternion.Euler(0, 180, 0);
            Stock(unit, center.y + size.y * 0.5f, size.x * 0.46f, size.z * 0.4f,
                new[] { "gaming_console", "classic_laptop", "boombox", "cassette_player", "portable_cassette_player", "gamepad", "vintage_video_camera" }, rng);
        }

        public static void HomeShelves(Transform parent, Vector3 center, Random rng)
        {
            string[] decor = { "ceramic_vase_01", "ceramic_vase_02", "ceramic_vase_03", "mantel_clock_01", "standing_picture_frame_01", "desk_lamp_arm_01" };
            for (int u = 0; u < 3; u++)
            {
                var unit = new GameObject("Home shelf unit").transform;
                unit.SetParent(parent, false);
                unit.localPosition = new Vector3(center.x, 0, center.z - 1.08f + u * 1.08f);
                unit.localRotation = Quaternion.Euler(0, -90, 0);
                ArtLibrary.Spawn("wooden_display_shelves_01", unit, Vector3.zero, 0);
                foreach (float level in new[] { 0.38f, 0.80f, 1.16f, 1.56f }) Stock(unit, level, 0.46f, 0.14f, decor, rng);
            }
        }

        /// <summary>The upstairs showroom: a sofa set around a rug, where the Lounging pose belongs.</summary>
        public static void Lounge(Transform parent, Vector3 rug)
        {
            float y = rug.y;
            Solid(ArtLibrary.Spawn("sofa_02", parent, new Vector3(rug.x + 2.0f, y, rug.z), -90));
            Solid(ArtLibrary.Spawn("coffee_table_round_01", parent, new Vector3(rug.x + 0.2f, y, rug.z), 0));
            Solid(ArtLibrary.Spawn("ArmChair_01", parent, new Vector3(rug.x - 0.2f, y, rug.z + 1.9f), 160));
            Solid(ArtLibrary.Spawn("modern_arm_chair_01", parent, new Vector3(rug.x - 0.2f, y, rug.z - 1.9f), 20));
            Solid(ArtLibrary.Spawn("side_table_01", parent, new Vector3(rug.x + 2.1f, y, rug.z + 1.7f), -90));
            ArtLibrary.Spawn("desk_lamp_arm_01", parent, new Vector3(rug.x + 2.1f, y + 0.55f, rug.z + 1.7f), -120);
            Solid(ArtLibrary.Spawn("Ottoman_01", parent, new Vector3(rug.x - 1.6f, y, rug.z + 0.6f), 10));
            ArtLibrary.Spawn("ceramic_vase_01", parent, new Vector3(rug.x + 0.2f, y + 0.42f, rug.z), 0);
        }

        public static void LivingRoom(Transform parent)
        {
            Solid(ArtLibrary.Spawn("sofa_02", parent, new Vector3(14.1f, 0, 0), -90));
            Solid(ArtLibrary.Spawn("coffee_table_round_01", parent, new Vector3(12.5f, 0, 0), 0));
            Solid(ArtLibrary.Spawn("ArmChair_01", parent, new Vector3(12.3f, 0, 1.75f), 160));
            Solid(ArtLibrary.Spawn("modern_arm_chair_01", parent, new Vector3(12.3f, 0, -1.75f), 20));
            Solid(ArtLibrary.Spawn("side_table_01", parent, new Vector3(14.3f, 0, 1.45f), -90));
            ArtLibrary.Spawn("desk_lamp_arm_01", parent, new Vector3(14.3f, 0.55f, 1.45f), -120);
            Solid(ArtLibrary.Spawn("Ottoman_01", parent, new Vector3(11.1f, 0, 0.9f), 10));
            ArtLibrary.Spawn("ceramic_vase_02", parent, new Vector3(12.5f, 0.42f, 0), 0);
        }

        public static void DiningTable(Transform parent, Vector3 center, Vector3 size)
        {
            float top = center.y + size.y * 0.5f;
            Solid(ArtLibrary.Spawn("dining_chair_02", parent, new Vector3(center.x - 0.45f, 0, center.z - 0.75f), 0));
            Solid(ArtLibrary.Spawn("dining_chair_02", parent, new Vector3(center.x + 0.45f, 0, center.z - 0.75f), 0));
            ArtLibrary.Spawn("ceramic_vase_01", parent, new Vector3(center.x, top, center.z), 0);
            Solid(ArtLibrary.Spawn("drawer_cabinet", parent, new Vector3(center.x, 0, center.z - 2.3f), 0));
        }

        public static void ServiceDesk(Transform parent, Vector3 center, Vector3 size)
        {
            float top = center.y + size.y * 0.5f;
            ArtLibrary.Spawn("CashRegister_01", parent, new Vector3(center.x - 0.5f, top, center.z), 180);
            ArtLibrary.Spawn("clipboard", parent, new Vector3(center.x + 0.4f, top, center.z + 0.05f), 170);
            ArtLibrary.Spawn("wicker_basket_02", parent, new Vector3(center.x + 1.6f, 0, center.z + 0.4f), 0);
        }

        /// <summary>Open glass doors onto the dark street, where customers come and go.</summary>
        public static void Entrance(Transform parent, StoreLighting lighting)
        {
            var frame = ArtLibrary.Lit(new Color(0.55f, 0.56f, 0.6f), 0.7f, 0.9f);
            Panel(parent, "Night outside", new Vector3(0, 1.25f, -14.735f), new Vector3(3.2f, 2.5f, 0.01f)).sharedMaterial = ArtLibrary.Emissive(new Color(0.05f, 0.07f, 0.14f), 0.55f);
            foreach (float x in new[] { -1.65f, 1.65f })
                Panel(parent, "Door post", new Vector3(x, 1.25f, -14.68f), new Vector3(0.1f, 2.5f, 0.12f)).sharedMaterial = frame;
            Panel(parent, "Door header", new Vector3(0, 2.55f, -14.68f), new Vector3(3.4f, 0.12f, 0.14f)).sharedMaterial = frame;
            var glass = ArtLibrary.Glass(new Color(0.7f, 0.85f, 0.95f, 0.18f));
            foreach (float x in new[] { -1.25f, 1.25f })
                Panel(parent, "Sliding door", new Vector3(x, 1.22f, -14.6f), new Vector3(0.75f, 2.35f, 0.03f)).sharedMaterial = glass;
            lighting.AddGlow(new Vector3(0, 2.3f, -14.3f), new Color(0.45f, 0.85f, 1f), 2.6f, 0.35f);
        }

        public static void Signs(Transform parent, Vector3 placementZone)
        {
            Sign(parent, "ROPA", new Vector3(-4.2f, 2.55f, 5.6f), 0, 0.22f, Ropa, Color.white, true, 0, true);
            Sign(parent, "ELECTRONICA", new Vector3(4.2f, 2.55f, 5.6f), 0, 0.2f, Electronica, Color.white, true, 0, true);
            Sign(parent, "HOGAR", new Vector3(9.3f, 2.55f, -1.5f), 90, 0.22f, Hogar, Color.black, true, 0, true);
            Sign(parent, "CUSTOMER SERVICE", new Vector3(-6.8f, 2.25f, -14.68f), 180, 0.12f, Supermercado, Color.white, false);
            Sign(parent, "ENTRADA", new Vector3(0, 2.3f, -14.55f), 180, 0.12f, Entrada, Color.white, false, 0.25f);
            Sign(parent, "CLOTHING DROP", placementZone + new Vector3(0, 2.4f, 0), 0, 0.15f, new Color(0.12f, 0.55f, 0.22f), Color.white, true, 0, true);
        }
    }
}
