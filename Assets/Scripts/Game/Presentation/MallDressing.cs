using System.Collections.Generic;
using UnityEngine;
using static NightSupermarket.Game.DressingKit;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Looks for the mall and the climb: escalator treads that actually travel, glass balustrades and
    /// an under-light strip; the atrium's glass roof, fountain, planters and benches; the toy store's
    /// stocked shelves (which rattle if you run past them) and lights. Primitives and the shared prop
    /// library only, so the frame cost is what the supermarket already pays.
    /// </summary>
    public static class MallDressing
    {
        public static void Escalator(Transform parent, StoreLighting lighting)
        {
            float run = PrimitiveWorld.RampEndZ - PrimitiveWorld.RampStartZ, rise = PrimitiveWorld.UpstairsY;
            var tilt = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
            // Moving parts live beside the batched dressing, never under it (static batching would freeze them).
            var steps = new GameObject("Escalator treads").AddComponent<EscalatorTreads>();
            steps.transform.SetParent(parent.parent, false);
            steps.Configure(new Vector3(0f, 0.01f, PrimitiveWorld.RampStartZ), tilt, Mathf.Sqrt(run * run + rise * rise), 0.4f, 0.55f);
            var glass = ArtLibrary.Glass(new Color(0.75f, 0.85f, 0.95f, 0.3f));
            var rubber = ArtLibrary.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f);
            var strip = ArtLibrary.Emissive(new Color(0.55f, 0.8f, 1f), 1.4f);
            Vector3 along = tilt * Vector3.forward, up = tilt * Vector3.up;
            Vector3 foot = new Vector3(0f, 0f, PrimitiveWorld.RampStartZ);
            float length = Mathf.Sqrt(run * run + rise * rise);
            foreach (float side in new[] { -1.04f, 1.04f })
            {
                Vector3 mid = foot + along * (length * 0.5f + 0.1f) + new Vector3(side, 0f, 0f);
                var pane = Panel(parent, "Escalator glass", mid + up * 0.85f, new Vector3(0.04f, 0.55f, length - 1.0f)); pane.sharedMaterial = glass; pane.transform.rotation = tilt;
                var rail = Panel(parent, "Escalator handrail", mid + up * 1.14f, new Vector3(0.1f, 0.06f, length - 1.0f)); rail.sharedMaterial = rubber; rail.transform.rotation = tilt;
                var light = Panel(parent, "Escalator light strip", mid + up * 0.06f + new Vector3(side > 0 ? -0.06f : 0.06f, 0f, 0f), new Vector3(0.03f, 0.03f, length - 1.0f)); light.sharedMaterial = strip; light.transform.rotation = tilt;
            }
            lighting.AddGlow(foot + along * 1.5f + Vector3.up * 0.4f, new Color(0.55f, 0.8f, 1f), 3.5f, 0.6f);
            lighting.AddGlow(foot + along * (length - 1.5f) + Vector3.up * 0.4f, new Color(0.55f, 0.8f, 1f), 3.5f, 0.6f);
            SignFactory.Sign(parent, "UP: ESCALATOR (IT MOVES YOU)  ·  STAIRS ON THE RIGHT (YOU CAN STOP)", new Vector3(0.9f, 2.4f, PrimitiveWorld.RampStartZ - 0.6f), 0f, 0.1f, new Color(0.12f, 0.14f, 0.2f), Color.white, true, 0.5f);
        }

        public static void Atrium(Transform parent, StoreLighting lighting, System.Random rng)
        {
            float roof = 8f;
            var tile = ArtLibrary.Surface("tiled_floor_001", new Vector2(8, 6), new Color(0.9f, 0.9f, 0.88f), 0.55f);
            if (tile != null) Panel(parent, "Atrium tiles", new Vector3(0f, 0.004f, -24f), new Vector3(23.8f, 0.008f, 17.8f)).sharedMaterial = tile;
            // Glass roof panes between the beams; the moon comes through them.
            var pane = ArtLibrary.Emissive(new Color(0.35f, 0.45f, 0.7f), 0.25f);
            foreach (float z in new[] { -21f, -27f })
                foreach (float x in new[] { -8f, -2.7f, 2.7f, 8f })
                    Panel(parent, "Roof pane", new Vector3(x, roof - 0.02f, z), new Vector3(4.6f, 0.02f, 5.2f)).sharedMaterial = pane;
            lighting.AddSkylight(new Vector3(-5f, roof - 0.1f, -24f));
            lighting.AddSkylight(new Vector3(5f, roof - 0.1f, -24f));
            // Hanging fixtures on long pendants from the roof.
            foreach (var at in new[] { new Vector3(-7f, 0f, -19f), new Vector3(7f, 0f, -19f), new Vector3(-7f, 0f, -29f), new Vector3(7f, 0f, -29f) })
            {
                Panel(parent, "Pendant", new Vector3(at.x, (roof + 4.5f) * 0.5f, at.z), new Vector3(0.05f, roof - 4.5f, 0.05f)).sharedMaterial = ArtLibrary.Lit(new Color(0.15f, 0.15f, 0.16f), 0.4f);
                var visual = ArtLibrary.Spawn("mounted_fluorescent_lights", parent, new Vector3(at.x, 4.5f, at.z), 90);
                lighting.AddFixture(visual, new Vector3(at.x, 4.42f, at.z), 8f, 14f, false, false);
            }
            Panel(parent, "Emergency lamp", new Vector3(0f, 2.85f, -32.7f), new Vector3(0.3f, 0.1f, 0.3f)).sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.15f, 0.1f), 1.2f);
            lighting.AddEmergency(new Vector3(0f, 2.65f, -32.7f), 14f);
            FountainDressing.Build(parent, parent.parent, lighting, rng);
            // Planters: a real potted plant on each stone planter (blobs of green only when the art is missing).
            var leaf = ArtLibrary.Lit(new Color(0.2f, 0.42f, 0.22f), 0.2f);
            foreach (var at in new[] { new Vector3(-9.5f, 0, -17.5f), new Vector3(9.5f, 0, -17.5f), new Vector3(-9.5f, 0, -30.5f), new Vector3(9.5f, 0, -30.5f) })
            {
                var plant = ObjectiveDressing.FitProp(rng.Next(2) == 0 ? "potted_plant_01" : "potted_plant_02", parent, at + Vector3.up * 0.8f, (float)rng.NextDouble() * 360f, 1.7f);
                if (plant != null) continue;
                for (int i = 0; i < 5; i++)
                {
                    var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blob.name = "Planter green"; blob.transform.SetParent(parent, false);
                    blob.transform.position = at + new Vector3((float)rng.NextDouble() * 0.9f - 0.45f, 1.0f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 0.9f - 0.45f);
                    blob.transform.localScale = Vector3.one * (0.55f + (float)rng.NextDouble() * 0.5f);
                    blob.GetComponent<Renderer>().sharedMaterial = leaf;
                    Object.Destroy(blob.GetComponent<Collider>());
                }
            }
            // Smaller pots along the walls between the benches.
            foreach (var at in new[] { new Vector3(-11.4f, 0, -31.9f), new Vector3(11.4f, 0, -16.2f), new Vector3(11.4f, 0, -21f), new Vector3(11.4f, 0, -27f) }) // clear of the toy store opening (x -12, z -27..-18)
                ObjectiveDressing.FitProp("potted_plant_02", parent, at, (float)rng.NextDouble() * 360f, 1.0f);
            // Mall doors: open sliding glass, a dark night beyond, the escape door's push bar and sign glow.
            var frame = ArtLibrary.Lit(new Color(0.55f, 0.56f, 0.6f), 0.7f, 0.9f);
            float front = PrimitiveWorld.MallFrontZ;
            Panel(parent, "Night outside", new Vector3(0f, 1.5f, front - 0.3f), new Vector3(3.6f, 3f, 0.01f)).sharedMaterial = ArtLibrary.Emissive(new Color(0.05f, 0.07f, 0.14f), 0.55f);
            foreach (float x in new[] { -1.85f, 1.85f }) Panel(parent, "Door post", new Vector3(x, 1.5f, front + 0.2f), new Vector3(0.1f, 3f, 0.12f)).sharedMaterial = frame;
            Panel(parent, "Door header", new Vector3(0f, 3.05f, front + 0.2f), new Vector3(3.8f, 0.12f, 0.14f)).sharedMaterial = frame;
            var glass = ArtLibrary.Glass(new Color(0.7f, 0.85f, 0.95f, 0.18f));
            foreach (float x in new[] { -2.6f, 2.6f }) Panel(parent, "Sliding door", new Vector3(x, 1.5f, front + 0.32f), new Vector3(1.5f, 2.9f, 0.03f)).sharedMaterial = glass;
            lighting.AddGlow(new Vector3(0f, 2.6f, front + 0.6f), new Color(0.45f, 0.85f, 1f), 3f, 0.4f);
            Panel(parent, "Push bar", PrimitiveWorld.EscapeDoor + new Vector3(0f, -0.3f, 0.2f), new Vector3(1.1f, 0.06f, 0.06f)).sharedMaterial = ArtLibrary.Lit(new Color(0.75f, 0.75f, 0.78f), 0.7f, 0.9f);
            lighting.AddGlow(PrimitiveWorld.EscapeDoor + new Vector3(0f, 1.25f, 0.4f), new Color(0.25f, 0.95f, 0.4f), 2.8f, 0.55f);
        }

        public static void ToyStore(Transform parent, StoreLighting lighting, WorldSignals signals, System.Random rng)
        {
            var carpet = ArtLibrary.Surface("wool_boucle", new Vector2(6, 5), new Color(0.55f, 0.3f, 0.35f), 0.02f) ?? ArtLibrary.Lit(new Color(0.5f, 0.28f, 0.32f), 0.05f);
            Panel(parent, "Toy carpet", new Vector3(-21f, 0.004f, -25.5f), new Vector3(17.8f, 0.008f, 14.8f)).sharedMaterial = carpet;
            foreach (var at in new[] { new Vector3(-25f, 0f, -22f), new Vector3(-17f, 0f, -29f) })
            {
                var visual = ArtLibrary.Spawn("mounted_fluorescent_lights", parent, new Vector3(at.x, 3.95f, at.z), 90);
                lighting.AddFixture(visual, new Vector3(at.x, 3.87f, at.z), 7f, 12f, false, false, new Color(1f, 0.93f, 0.85f));
            }
            Panel(parent, "Emergency lamp", new Vector3(-13f, 3.75f, -19f), new Vector3(0.3f, 0.1f, 0.3f)).sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.15f, 0.1f), 1.2f);
            lighting.AddEmergency(new Vector3(-13f, 3.55f, -19f), 12f);
            SignFactory.Sign(parent, "TOY STORE", new Vector3(-11.7f, 4.6f, -22.5f), 270f, 0.3f, new Color(0.85f, 0.25f, 0.3f), Color.white, true, 0.6f);
            // Stock the shelves with toy boxes and make the faces you can run past rattle.
            foreach (var shelf in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (shelf.name != "Toy shelf" && shelf.name != "Robot shelf") continue;
                var stock = new List<Transform>();
                Vector3 size = shelf.localScale;
                bool alongX = size.x > size.z;
                // Shelves are solid units; stock goes on top where it can be seen (and shaken).
                int levels = 1;
                for (int level = 0; level < levels; level++)
                {
                    float top = size.y - 0.02f;
                    int count = alongX ? 6 : 6;
                    for (int i = 0; i < count; i++)
                    {
                        float t = (i + 0.5f) / count - 0.5f;
                        Vector3 local = alongX ? new Vector3(t * size.x * 0.9f, 0, 0) : new Vector3(0, 0, t * size.z * 0.9f);
                        var toy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        toy.name = "Toy box"; toy.transform.SetParent(parent.parent, false); // shaken later, so not batched
                        toy.transform.position = shelf.position + new Vector3(local.x, top - size.y * 0.5f + 0.16f, local.z);
                        toy.transform.localScale = new Vector3(0.26f + (float)rng.NextDouble() * 0.12f, 0.24f + (float)rng.NextDouble() * 0.16f, 0.24f);
                        toy.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 10f - 5f, 0);
                        toy.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(ToyColour(rng), 0.35f);
                        Object.Destroy(toy.GetComponent<Collider>());
                        stock.Add(toy.transform);
                    }
                }
                if (shelf.name != "Toy shelf") continue;
                // The rattle zone wraps the shelf's open faces by 0.7 m.
                var zone = new GameObject("Toy shelf rattle");
                zone.transform.SetParent(parent, false);
                zone.transform.position = shelf.position + Vector3.up * 0.1f;
                var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true;
                box.size = new Vector3(size.x + 1.4f, 2.2f, size.z + 1.4f);
                zone.AddComponent<NoisyShelf>().Configure(signals, stock.ToArray());
            }
        }

        private static Color ToyColour(System.Random rng)
        {
            Color[] palette = { new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.55f, 0.9f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.75f, 0.4f), new Color(0.95f, 0.55f, 0.75f) /* candy pink, kept clear of the magenta warning colour */, new Color(0.95f, 0.55f, 0.2f) };
            return palette[rng.Next(palette.Length)];
        }
    }

    /// <summary>Escalator treads: thin panels that travel up the ramp and wrap, so the belt visibly moves.</summary>
    public sealed class EscalatorTreads : MonoBehaviour
    {
        private Vector3 foot, along;
        private float length, spacing, speed, offset;
        private readonly List<Transform> treads = new List<Transform>();
        public void Configure(Vector3 rampFoot, Quaternion tilt, float rampLength, float treadSpacing, float beltSpeed)
        {
            foot = rampFoot; along = tilt * Vector3.forward; length = rampLength; spacing = treadSpacing; speed = beltSpeed;
            var dark = ArtLibrary.Lit(new Color(0.28f, 0.29f, 0.31f), 0.6f, 0.8f);
            var edge = ArtLibrary.Emissive(new Color(0.95f, 0.85f, 0.3f), 0.8f);
            int count = Mathf.CeilToInt(length / spacing);
            for (int i = 0; i < count; i++)
            {
                var tread = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tread.name = "Tread"; tread.transform.SetParent(transform, false);
                tread.transform.localScale = new Vector3(1.9f, 0.035f, spacing * 0.86f);
                tread.transform.rotation = tilt;
                tread.GetComponent<Renderer>().sharedMaterial = dark;
                Object.Destroy(tread.GetComponent<Collider>());
                var lip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lip.name = "Tread lip"; lip.transform.SetParent(tread.transform, false);
                lip.transform.localPosition = new Vector3(0, 0.6f, -0.46f); lip.transform.localScale = new Vector3(1f, 0.6f, 0.08f);
                lip.GetComponent<Renderer>().sharedMaterial = edge;
                Object.Destroy(lip.GetComponent<Collider>());
                treads.Add(tread.transform);
            }
            Place();
        }
        private void Update() { offset = Mathf.Repeat(offset + speed * Time.deltaTime, spacing); Place(); }
        private void Place()
        {
            for (int i = 0; i < treads.Count; i++)
            {
                float d = Mathf.Repeat(i * spacing + offset, length);
                treads[i].position = foot + along * d + Vector3.up * 0.03f;
                treads[i].gameObject.SetActive(d > 0.2f && d < length - 0.2f);
            }
        }
    }
}
