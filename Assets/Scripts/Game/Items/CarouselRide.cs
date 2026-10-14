using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The toy-store carousel: a turning platform with six horses under a lit canopy. It runs for a
    /// while, slows, rests, and starts again. Riders are carried (see <see cref="MovingSurface"/>),
    /// and because a carousel is supposed to turn, being carried does not count as moving: a
    /// mannequin posed on a horse is the most natural thing in the store. The moment it stops, you
    /// are a mannequin standing on a carousel, and the usual rules apply. One object does the
    /// hiding place, the set piece and a job; it costs one rotating transform.
    /// </summary>
    public sealed class CarouselRide : MovingSurface
    {
        public const float Radius = 3.2f, PlatformHeight = 0.22f; // under the controller's step height: you walk on
        public float RunSeconds = 28f, RestSeconds = 10f, TopSpeed = 18f; // degrees per second: one turn in 20 s, so a whole turn fits in one run
        public bool Running { get; private set; } = true;
        /// <summary>Current angular speed in degrees per second; ramps over two seconds either way.</summary>
        public float Speed { get; private set; }
        public float Angle { get; private set; }
        public Transform Spinner { get; private set; }
        public HoldSpot Ride { get; private set; }
        private float phaseLeft;
        private Transform[] horses = new Transform[0];
        private readonly System.Collections.Generic.List<SeatSpot> seats = new System.Collections.Generic.List<SeatSpot>();
        public System.Collections.Generic.IReadOnlyList<SeatSpot> Seats => seats;
        /// <summary>Someone sitting on a horse right now (for the ride job, which starts by itself).</summary>
        public PlayerMotor FirstRider
        {
            get { foreach (var s in seats) { var r = s.FirstRider; if (r != null) return r; } return null; }
        }
        private Renderer[] bulbs = new Renderer[0];
        private Material bulbOn, bulbOff;
        private AudioSource music;
        private float blink;

        public override bool CountsAsMovement => false;

        public override Vector3 Displacement(Vector3 at, float delta)
        {
            Vector3 offset = at - transform.position; offset.y = 0f;
            return Quaternion.Euler(0f, Speed * delta, 0f) * offset - offset;
        }

        public override float YawDelta(float delta) => Speed * delta;

        public static CarouselRide Create(Transform parent, Vector3 center)
        {
            var host = new GameObject("Carousel");
            host.transform.SetParent(parent, false);
            host.transform.position = center;
            var ride = host.AddComponent<CarouselRide>();
            ride.phaseLeft = ride.RunSeconds;
            ride.Speed = ride.TopSpeed;
            var red = ArtLibrary.Lit(new Color(0.72f, 0.12f, 0.14f), 0.55f);
            var cream = ArtLibrary.Lit(new Color(0.96f, 0.92f, 0.8f), 0.35f);
            var brass = ArtLibrary.Lit(new Color(0.9f, 0.72f, 0.3f), 0.88f, 1f);
            var mirror = ArtLibrary.Lit(new Color(0.82f, 0.86f, 0.9f), 0.97f, 1f);
            var stripes = ArtLibrary.Stripes(new Color(0.76f, 0.1f, 0.13f), new Color(0.97f, 0.93f, 0.82f), 16, 0.4f);
            var deck = ArtLibrary.Surface("wood_table_001", new Vector2(3f, 3f), new Color(0.88f, 0.72f, 0.55f), 0.5f) ?? cream;
            // Static parts: a mirrored centre column (its collider is what you bump into), a striped tent
            // canopy with a scalloped valance and brass trim, and a ring of chasing bulbs round the valance.
            var column = Cylinder(host.transform, "Carousel column", center + Vector3.up * (PlatformHeight + 1.55f), 0.55f, 3.1f, mirror, true);
            // Brass rings at the foot and the head of the mirror column; the mirror shows between them.
            MeshKit.Spin(host.transform, "Carousel column foot", center, new[] { new Vector2(0.63f, PlatformHeight), new Vector2(0.63f, PlatformHeight + 0.16f), new Vector2(0.57f, PlatformHeight + 0.22f), new Vector2(0.54f, PlatformHeight + 0.22f) }, 40, brass);
            MeshKit.Spin(host.transform, "Carousel column head", center, new[] { new Vector2(0.54f, 3.1f), new Vector2(0.58f, 3.12f), new Vector2(0.65f, 3.2f), new Vector2(0.65f, 3.3f), new Vector2(0f, 3.3f) }, 40, brass);
            MeshKit.Spin(host.transform, "Carousel canopy", center, MeshKit.Shell(new[] { new Vector2(Radius + 0.42f, 3.3f), new Vector2(Radius * 0.62f, 3.6f), new Vector2(0.7f, 3.84f), new Vector2(0.18f, 3.9f) }, 0.04f), 64, stripes);
            MeshKit.Spin(host.transform, "Carousel valance", center, MeshKit.Shell(new[] { new Vector2(Radius + 0.44f, 3.02f), new Vector2(Radius + 0.44f, 3.32f) }, 0.025f), 64, stripes);
            MeshKit.Spin(host.transform, "Carousel trim", center, new[] { new Vector2(Radius + 0.5f, 2.98f), new Vector2(Radius + 0.5f, 3.05f), new Vector2(Radius + 0.4f, 3.05f), new Vector2(Radius + 0.4f, 2.98f) }, 64, brass);
            var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            finial.name = "Carousel finial"; Object.Destroy(finial.GetComponent<Collider>());
            finial.transform.SetParent(host.transform, false); finial.transform.position = center + Vector3.up * 3.9f; finial.transform.localScale = Vector3.one * 0.16f;
            finial.GetComponent<Renderer>().sharedMaterial = brass;
            // Shoppers path around the ride; the obstacle lives on the static host so carving never waits for a rest.
            var obstacle = host.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.radius = Radius + 0.3f; obstacle.height = 2f; obstacle.center = Vector3.up; obstacle.carving = true;
            ride.bulbOn = ArtLibrary.Emissive(new Color(1f, 0.85f, 0.45f), 2.4f);
            ride.bulbOff = ArtLibrary.Lit(new Color(0.5f, 0.42f, 0.25f), 0.3f);
            ride.bulbs = new Renderer[24];
            for (int i = 0; i < ride.bulbs.Length; i++)
            {
                float a = i / (float)ride.bulbs.Length * Mathf.PI * 2f;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Carousel bulb";
                bulb.transform.SetParent(host.transform, false);
                bulb.transform.position = center + new Vector3(Mathf.Cos(a) * (Radius + 0.5f), 3.17f, Mathf.Sin(a) * (Radius + 0.5f));
                bulb.transform.localScale = Vector3.one * 0.1f;
                Object.Destroy(bulb.GetComponent<Collider>());
                ride.bulbs[i] = bulb.GetComponent<Renderer>();
            }
            var glow = new GameObject("Carousel glow").AddComponent<Light>();
            glow.transform.SetParent(host.transform, false);
            glow.transform.position = center + Vector3.up * 2.6f;
            glow.type = LightType.Point; glow.range = 7f; glow.intensity = 1.8f; glow.color = new Color(1f, 0.85f, 0.5f); glow.shadows = LightShadows.None;
            // The spinner: a wooden deck with a red-and-brass skirt, six horses on brass poles, the ride zone.
            var spinner = new GameObject("Carousel spinner").transform;
            spinner.SetParent(host.transform, false);
            spinner.position = center;
            ride.Spinner = spinner;
            var platform = Cylinder(spinner, "Carousel platform", center + Vector3.up * PlatformHeight * 0.5f, Radius, PlatformHeight, deck, true);
            platform.layer = 3; // not part of the NavMesh bake; the obstacle on the host keeps shoppers off
            var band = Cylinder(spinner, "Carousel band", center + Vector3.up * PlatformHeight * 0.5f, Radius + 0.05f, PlatformHeight * 0.8f, red, false);
            band.layer = 3;
            MeshKit.Spin(spinner, "Carousel deck trim", center, new[] { new Vector2(Radius + 0.07f, PlatformHeight - 0.02f), new Vector2(Radius + 0.07f, PlatformHeight + 0.015f), new Vector2(Radius - 0.05f, PlatformHeight + 0.015f) }, 64, brass).layer = 3;
            ride.horses = new Transform[6];
            var tints = new[] { new Color(1f, 1f, 1f), new Color(0.98f, 0.9f, 0.86f), new Color(0.88f, 0.93f, 1f), new Color(1f, 0.97f, 0.88f), new Color(0.93f, 0.9f, 1f), new Color(0.9f, 1f, 0.94f) };
            var tintBlock = new MaterialPropertyBlock();
            for (int i = 0; i < ride.horses.Length; i++)
            {
                float a = i / (float)ride.horses.Length * Mathf.PI * 2f;
                var horse = new GameObject("Carousel horse " + (i + 1)).transform;
                horse.SetParent(spinner, false);
                horse.position = center + new Vector3(Mathf.Cos(a) * 2.3f, PlatformHeight, Mathf.Sin(a) * 2.3f);
                horse.rotation = Quaternion.Euler(0f, 180f - a * Mathf.Rad2Deg, 0f); // noses along the ride (tangent)
                // The body keeps an invisible box so you bump into the horse; what you see is the model.
                var body = Part(horse, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(0.4f, 0.5f, 1.1f), cream, true);
                var model = ObjectiveDressing.FitProp("horse_statue_01", horse, horse.position, 0f, 1.5f);
                if (model != null)
                {
                    body.GetComponent<Renderer>().enabled = false;
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    {
                        renderer.GetPropertyBlock(tintBlock); tintBlock.SetColor("_BaseColor", tints[i]); renderer.SetPropertyBlock(tintBlock);
                        renderer.gameObject.layer = 3;
                    }
                }
                else PrimitiveHorse(horse, ArtLibrary.Lit(tints[i] * 0.95f, 0.6f), red, body);
                // A saddle to sit on, just behind the pole: as high as the model's back is there.
                // The seat lives on the horse's body (its collider is what you aim at), placed in the horse's own space.
                var seatSpot = body.AddComponent<SeatSpot>();
                seatSpot.Style = SeatStyle.Astride; seatSpot.Surface = ride; seatSpot.What = "the horse";
                var saddle = seatSpot.AddSeat(Vector3.zero, Quaternion.identity, PlayerMotor.SaddleDrop);
                saddle.localScale = new Vector3(1f / body.transform.localScale.x, 1f / body.transform.localScale.y, 1f / body.transform.localScale.z);
                saddle.position = horse.TransformPoint(new Vector3(0f, SaddleHeight(model, horse, new Vector3(0f, 0f, -0.24f)), -0.24f));
                saddle.rotation = horse.rotation;
                ride.seats.Add(seatSpot);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole"; Object.Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(horse, false);
                // From the deck up into the canopy (its underside is about 3.5 m at the horses' radius).
                pole.transform.localPosition = new Vector3(0f, (3.6f - PlatformHeight) * 0.5f, 0f);
                pole.transform.localScale = new Vector3(0.05f, (3.6f - PlatformHeight) * 0.5f, 0.05f);
                pole.GetComponent<Renderer>().sharedMaterial = brass;
                pole.layer = 3;
                ride.horses[i] = horse;
            }
            var zone = new GameObject("Carousel ride zone");
            zone.transform.SetParent(spinner, false);
            zone.transform.position = center + Vector3.up * 1.2f;
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(Radius * 1.5f, 2.2f, Radius * 1.5f);
            ride.Ride = zone.AddComponent<HoldSpot>();
            // Music: a small looping waltz figure, synthesised; it slows with the ride.
            ride.music = host.AddComponent<AudioSource>();
            ride.music.clip = Waltz(); ride.music.loop = true; ride.music.spatialBlend = 1f; ride.music.minDistance = 4f; ride.music.maxDistance = 30f;
            ride.music.volume = 0.35f; ride.music.playOnAwake = false;
            ride.music.Play();
            ride.Paint();
            return ride;
        }

        /// <summary>Top of the horse model's back at a point (horse space), from its own vertices; 1.25 m without the model.</summary>
        private static float SaddleHeight(GameObject model, Transform horse, Vector3 at)
        {
            float best = float.MinValue;
            if (model != null)
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var toHorse = horse.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    foreach (var v in mesh.vertices)
                    {
                        Vector3 p = toHorse.MultiplyPoint3x4(v);
                        if ((p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z) < 0.15f * 0.15f && p.y > best) best = p.y;
                    }
                }
            return best > 0.6f && best < 1.8f ? best + 0.02f : 1.25f;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 at, float radius, float height, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            else
            {
                // A mesh collider on the cylinder keeps riders from sliding off a capsule's rounded cap.
                Object.Destroy(go.GetComponent<Collider>());
                var mesh = go.AddComponent<MeshCollider>(); mesh.convex = true;
            }
            return go;
        }

        /// <summary>Without the art pack: a rounded horse from capsules and spheres (not boxes).</summary>
        private static void PrimitiveHorse(Transform horse, Material coat, Material saddle, GameObject body)
        {
            body.GetComponent<Renderer>().enabled = false;
            Round(horse, PrimitiveType.Capsule, "Barrel", new Vector3(0f, 1.0f, 0f), new Vector3(0.42f, 0.55f, 0.42f), Quaternion.Euler(90f, 0f, 0f), coat);
            Round(horse, PrimitiveType.Capsule, "Neck", new Vector3(0f, 1.38f, 0.45f), new Vector3(0.22f, 0.3f, 0.22f), Quaternion.Euler(30f, 0f, 0f), coat);
            Round(horse, PrimitiveType.Capsule, "Head", new Vector3(0f, 1.62f, 0.66f), new Vector3(0.2f, 0.2f, 0.2f), Quaternion.Euler(75f, 0f, 0f), coat);
            Round(horse, PrimitiveType.Sphere, "Saddle", new Vector3(0f, 1.24f, -0.05f), new Vector3(0.44f, 0.1f, 0.5f), Quaternion.identity, saddle);
            foreach (float z in new[] { -0.36f, 0.36f })
                foreach (float x in new[] { -0.12f, 0.12f })
                    Round(horse, PrimitiveType.Capsule, "Leg", new Vector3(x, 0.55f, z + (z > 0 ? 0.08f : -0.08f)), new Vector3(0.09f, 0.3f, 0.09f), Quaternion.Euler(z > 0 ? -25f : 25f, 0f, 0f), coat);
        }

        private static void Round(Transform parent, PrimitiveType type, string name, Vector3 local, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localRotation = rotation; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.layer = 3;
        }

        private static GameObject Part(Transform parent, string name, Vector3 local, Vector3 size, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.layer = 3;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            phaseLeft -= delta;
            if (phaseLeft <= 0f) { Running = !Running; phaseLeft = Running ? RunSeconds : RestSeconds; }
            Speed = Mathf.MoveTowards(Speed, Running ? TopSpeed : 0f, TopSpeed / 2f * delta);
            Angle = Mathf.Repeat(Angle + Speed * delta, 360f);
            if (Spinner != null) Spinner.rotation = Quaternion.Euler(0f, Angle, 0f);
            for (int i = 0; i < horses.Length; i++)
            {
                var p = horses[i].localPosition;
                p.y = PlatformHeight + Mathf.Sin(Angle * Mathf.Deg2Rad * 3f + i * 1.1f) * 0.12f * (Speed / TopSpeed);
                horses[i].localPosition = p;
            }
            if (music != null) { music.pitch = Mathf.Lerp(0.6f, 1f, Speed / TopSpeed); music.volume = 0.08f + 0.3f * (Speed / TopSpeed); }
            blink += delta * (Speed > 0.5f ? 4f : 0.6f);
            if (blink >= 1f) { blink = 0f; Paint(); }
        }

        private int paintPhase;
        private void Paint()
        {
            paintPhase++;
            for (int i = 0; i < bulbs.Length; i++)
                bulbs[i].sharedMaterial = Speed > 0.5f ? ((i + paintPhase) % 2 == 0 ? bulbOn : bulbOff) : (paintPhase % 2 == 0 && i % 4 == 0 ? bulbOn : bulbOff);
        }

        /// <summary>Three bars of an oom-pah-pah waltz on soft tones, long enough that the loop is not obvious.</summary>
        private static AudioClip Waltz()
        {
            const int rate = 22050;
            float beat = 0.42f;
            float[] bass = { 130.8f, 98f, 110f, 123.5f };     // C G A B
            float[][] chords = { new[] { 261.6f, 329.6f, 392f }, new[] { 246.9f, 293.7f, 392f }, new[] { 261.6f, 329.6f, 440f }, new[] { 246.9f, 311.1f, 392f } };
            int bars = bass.Length;
            int count = (int)(rate * beat * 3 * bars);
            var data = new float[count];
            for (int bar = 0; bar < bars; bar++)
                for (int b = 0; b < 3; b++)
                {
                    int start = (int)(rate * beat * (bar * 3 + b));
                    int length = (int)(rate * beat * 0.9f);
                    if (b == 0) Tone(data, start, length, bass[bar], 0.22f);
                    else foreach (var f in chords[bar]) Tone(data, start, (int)(length * 0.6f), f, 0.08f);
                }
            var clip = AudioClip.Create("Carousel waltz", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Tone(float[] data, int start, int length, float frequency, float volume)
        {
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float along = i / (float)length;
                float envelope = Mathf.Sin(Mathf.Clamp01(along / 0.08f) * Mathf.PI * 0.5f) * Mathf.Exp(-along * 3.2f);
                float t = (start + i) / 22050f;
                data[start + i] += (Mathf.Sin(t * frequency * Mathf.PI * 2f) + 0.35f * Mathf.Sin(t * frequency * 2f * Mathf.PI * 2f)) * envelope * volume;
            }
        }
    }
}
