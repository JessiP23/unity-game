using System.Collections.Generic;
using UnityEngine;
using static NightSupermarket.Game.DressingKit;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The atrium fountain as something you would stop and look at: a square marble basin with a
    /// coping, a tiered centrepiece (pedestal, wide lower bowl, stem, upper bowl, finial) spun from
    /// profiles, rippling water in the pool and both bowls, a jet and two falling curtains of spray,
    /// coins on the bottom and rubber ducks drifting round. Gameplay colliders stay the simple boxes
    /// in PrimitiveWorld; everything here is looks. About 200 particles and four materials.
    /// </summary>
    public static class FountainDressing
    {
        public static readonly Vector3 Centre = new Vector3(0f, 0f, -24f);
        public const float PoolHalf = 2.2f, WaterY = 0.46f, LowerBowlY = 1.27f, UpperBowlY = 1.89f, JetY = 2.25f;

        public static void Build(Transform parent, Transform live, StoreLighting lighting, System.Random rng)
        {
            var marble = ArtLibrary.Surface("marble_01", new Vector2(1.5f, 1.5f), new Color(0.95f, 0.94f, 0.92f), 0.75f)
                ?? ArtLibrary.Surface("smooth_concrete_floor", new Vector2(1.5f, 1.5f), new Color(0.9f, 0.89f, 0.86f), 0.6f)
                ?? ArtLibrary.Lit(new Color(0.9f, 0.89f, 0.86f), 0.7f);
            var marbleDark = ArtLibrary.Surface("marble_01", new Vector2(2f, 2f), new Color(0.78f, 0.77f, 0.75f), 0.85f) ?? marble;
            // Coping: a wider, glossier slab along the top of each rim wall.
            foreach (var (at, size) in new[]
            {
                (new Vector3(0f, 0.635f, -21.6f), new Vector3(5.5f, 0.07f, 0.55f)), (new Vector3(0f, 0.635f, -26.4f), new Vector3(5.5f, 0.07f, 0.55f)),
                (new Vector3(-2.4f, 0.635f, -24f), new Vector3(0.55f, 0.07f, 5.5f)), (new Vector3(2.4f, 0.635f, -24f), new Vector3(0.55f, 0.07f, 5.5f)),
            })
                Panel(parent, "Fountain coping", at, size).sharedMaterial = marbleDark;
            // Pool bed: blue mosaic tiles under the water.
            var tiles = ArtLibrary.Surface("tiled_floor_001", new Vector2(6, 6), new Color(0.35f, 0.62f, 0.8f), 0.6f) ?? ArtLibrary.Lit(new Color(0.3f, 0.55f, 0.75f), 0.6f);
            Panel(parent, "Fountain tiles", Centre + new Vector3(0f, 0.205f, 0f), new Vector3(PoolHalf * 2f, 0.01f, PoolHalf * 2f)).sharedMaterial = tiles;
            // Centrepiece.
            MeshKit.Spin(parent, "Fountain pedestal", Centre, new[]
            {
                new Vector2(0.62f, 0.2f), new Vector2(0.62f, 0.3f), new Vector2(0.5f, 0.36f), new Vector2(0.36f, 0.52f),
                new Vector2(0.3f, 0.75f), new Vector2(0.28f, 0.98f), new Vector2(0.36f, 1.06f), new Vector2(0f, 1.06f)
            }, 40, marble);
            MeshKit.Spin(parent, "Fountain lower bowl", Centre, MeshKit.Shell(new[]
            {
                new Vector2(1.3f, 1.34f), new Vector2(1.22f, 1.24f), new Vector2(0.95f, 1.14f), new Vector2(0.55f, 1.07f), new Vector2(0.3f, 1.05f)
            }, 0.06f), 48, marble);
            MeshKit.Spin(parent, "Fountain stem", Centre, new[]
            {
                new Vector2(0.2f, 1.1f), new Vector2(0.15f, 1.3f), new Vector2(0.11f, 1.55f), new Vector2(0.13f, 1.72f), new Vector2(0.2f, 1.8f), new Vector2(0f, 1.8f)
            }, 32, marble);
            MeshKit.Spin(parent, "Fountain upper bowl", Centre, MeshKit.Shell(new[]
            {
                new Vector2(0.68f, 1.95f), new Vector2(0.62f, 1.88f), new Vector2(0.4f, 1.82f), new Vector2(0.15f, 1.79f)
            }, 0.05f), 40, marble);
            MeshKit.Spin(parent, "Fountain finial", Centre, new[]
            {
                new Vector2(0.09f, 1.86f), new Vector2(0.1f, 2.0f), new Vector2(0.06f, 2.1f), new Vector2(0.09f, 2.16f), new Vector2(0.04f, 2.24f), new Vector2(0f, 2.26f)
            }, 24, marble);
            // Water: the pool and both bowls. They live with the moving dressing so the ripple can scroll.
            var water = ArtLibrary.Water(new Color(0.32f, 0.58f, 0.72f, 0.62f));
            Panel(live, "Fountain water", Centre + new Vector3(0f, WaterY, 0f), new Vector3(PoolHalf * 2f - 0.02f, 0.01f, PoolHalf * 2f - 0.02f)).sharedMaterial = water;
            Disc(live, "Fountain bowl water", Centre + new Vector3(0f, LowerBowlY, 0f), 1.18f, water);
            Disc(live, "Fountain bowl water", Centre + new Vector3(0f, UpperBowlY, 0f), 0.6f, water);
            live.gameObject.AddComponent<WaterRipple>().Configure(water);
            // Coins on the bottom: wishes other people made.
            var gold = ArtLibrary.Lit(new Color(0.95f, 0.78f, 0.3f), 0.85f, 1f);
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f, radius = 0.7f + (float)rng.NextDouble() * 1.35f;
                var coin = Panel(parent, "Fountain coin", Centre + new Vector3(Mathf.Cos(angle) * radius, 0.215f, Mathf.Sin(angle) * radius), new Vector3(0.05f, 0.004f, 0.05f));
                coin.sharedMaterial = gold;
                coin.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
            }
            // Spray: a jet from the finial and curtains off both bowl rims.
            var spray = ArtLibrary.Particle(new Color(0.85f, 0.93f, 1f, 0.55f));
            if (spray != null)
            {
                Spray(live, "Fountain jet", Centre + new Vector3(0f, JetY, 0f), spray, 50f, 1.1f, 2.3f, 0.05f, 0.09f, ParticleSystemShapeType.Cone, 0.02f, 9f);
                Spray(live, "Fountain upper curtain", Centre + new Vector3(0f, 1.95f, 0f), spray, 55f, 0.45f, 0.15f, 0.035f, 0.06f, ParticleSystemShapeType.Circle, 0.66f, 0f);
                Spray(live, "Fountain lower curtain", Centre + new Vector3(0f, 1.33f, 0f), spray, 80f, 0.6f, 0.15f, 0.04f, 0.07f, ParticleSystemShapeType.Circle, 1.28f, 0f);
            }
            // Rubber ducks. Someone thought it was funny; it is.
            var ducks = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var duck = ObjectiveDressing.FitProp("rubber_duck_toy", live, Centre + new Vector3(1.4f, WaterY, 0f), i * 120f, 0.14f);
                if (duck != null) ducks.Add(duck.transform);
            }
            if (ducks.Count > 0) live.gameObject.AddComponent<DuckPond>().Configure(Centre, WaterY, ducks);
            // A soft cool glow up through the water, and the sound of it.
            lighting.AddGlow(Centre + new Vector3(0f, 0.7f, 0f), new Color(0.45f, 0.75f, 1f), 5.5f, 1.3f);
            var source = new GameObject("Fountain sound").AddComponent<AudioSource>();
            source.transform.SetParent(live, false);
            source.transform.position = Centre + Vector3.up;
            source.clip = WaterLoop(); source.loop = true; source.spatialBlend = 1f; source.minDistance = 3f; source.maxDistance = 20f; source.volume = 0.3f;
            source.Play();
        }

        private static void Disc(Transform parent, string name, Vector3 at, float radius, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Object.Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.position = at;
            disc.transform.localScale = new Vector3(radius * 2f, 0.004f, radius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Spray(Transform parent, string name, Vector3 at, Material material, float rate, float life, float speed, float minSize, float maxSize,
            ParticleSystemShapeType shape, float radius, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true; main.startLifetime = life; main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.gravityModifier = 1f; main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            var emission = system.emission; emission.rateOverTime = rate;
            var form = system.shape;
            form.shapeType = shape; form.radius = radius; form.angle = angle;
            if (shape == ParticleSystemShapeType.Circle) { form.radiusThickness = 0f; form.rotation = Vector3.zero; }
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = 0.06f; renderer.lengthScale = 1.2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.Play();
        }

        /// <summary>Three seconds of soft, filtered noise: running water without a sample file.</summary>
        private static AudioClip loop;
        private static AudioClip WaterLoop()
        {
            if (loop != null) return loop;
            const int rate = 22050;
            int count = rate * 3;
            var data = new float[count];
            var rng = new System.Random(5);
            float low = 0f, mid = 0f;
            for (int i = 0; i < count; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                low += (white - low) * 0.02f;
                mid += (white - mid) * 0.18f;
                float burble = 0.75f + 0.25f * Mathf.Sin(i / (float)rate * 6.3f) * Mathf.Sin(i / (float)rate * 2.1f);
                data[i] = (low * 2.2f + (mid - low) * 0.35f) * burble * 0.5f;
            }
            // Cross-fade the ends so the loop has no click.
            int fadeLength = rate / 10;
            for (int i = 0; i < fadeLength; i++) { float t = i / (float)fadeLength; data[i] = data[i] * t + data[count - fadeLength + i] * (1 - t); }
            loop = AudioClip.Create("Fountain water", count - fadeLength, 1, rate, false);
            var trimmed = new float[count - fadeLength];
            System.Array.Copy(data, trimmed, trimmed.Length);
            loop.SetData(trimmed, 0);
            return loop;
        }
    }

    /// <summary>Scrolls the shared water material's ripple normals two ways at once so the surface moves.</summary>
    public sealed class WaterRipple : MonoBehaviour
    {
        private Material water;
        private static readonly int Base = Shader.PropertyToID("_BaseMap"); // URP Lit offsets the normal map with _BaseMap_ST
        public void Configure(Material material) => water = material;
        private void Update()
        {
            if (water == null) return;
            float t = Time.time;
            water.SetTextureOffset(Base, new Vector2(t * 0.035f, Mathf.Sin(t * 0.3f) * 0.05f + t * 0.02f));
        }
    }

    /// <summary>Rubber ducks drifting slowly round the pool, bobbing, turning to face the way they float.</summary>
    public sealed class DuckPond : MonoBehaviour
    {
        private Vector3 centre; private float water;
        private readonly List<Transform> ducks = new List<Transform>();
        public void Configure(Vector3 middle, float waterY, List<Transform> list) { centre = middle; water = waterY; ducks.AddRange(list); }
        private void Update()
        {
            for (int i = 0; i < ducks.Count; i++)
            {
                if (ducks[i] == null) continue;
                float angle = Time.time * (0.12f + i * 0.03f) + i * 2.1f;
                float radius = 1.55f + Mathf.Sin(Time.time * 0.2f + i) * 0.25f;
                var at = centre + new Vector3(Mathf.Cos(angle) * radius, water + Mathf.Sin(Time.time * 1.7f + i) * 0.008f, Mathf.Sin(angle) * radius);
                ducks[i].position = at;
                ducks[i].rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + i) * 4f, -angle * Mathf.Rad2Deg, 0f);
            }
        }
    }
}
