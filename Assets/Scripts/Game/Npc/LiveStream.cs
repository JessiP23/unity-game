using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The influencer's live stream, made readable: a "● LIVE" tag with a viewer count over their head, likes
    /// floating up from the phone, and a faint red fan on the floor that is exactly what the camera sees (the same
    /// range and angle as their eyes). Inside the fan, moving goes viral; holding a pose there for a moment gets
    /// you a shout-out instead. Everything here is presentation and a geometry test; the root keeps the score.
    /// </summary>
    public sealed class LiveStream : MonoBehaviour
    {
        /// <summary>What the camera sees: the influencer's eyes use these numbers too.</summary>
        public const float Range = 10f, Angle = 70f;
        /// <summary>Seconds of a held pose in the shot that earns a shout-out.</summary>
        public const float ShoutoutSeconds = 2.2f;
        private Transform tag_, fan;
        private TextMesh front, back;
        private Renderer fanRenderer;
        private Material calm, hot;
        private Light cameraLight;
        private Transform phone;
        private readonly Transform[] likes = new Transform[6];
        private readonly float[] likeAge = new float[6];
        private float viewers, likeTimer, textTimer, hotUntil;
        private System.Random random;
        public int Viewers => Mathf.RoundToInt(viewers);
        private static readonly RaycastHit[] hits = new RaycastHit[12];
        public int StreamId { get; private set; }

        public string TagText => front != null ? front.text : "";

        public static LiveStream Attach(NpcController npc, Light cameraLight)
        {
            var stream = npc.GetComponent<LiveStream>();
            if (stream == null) stream = npc.gameObject.AddComponent<LiveStream>();
            stream.Begin(npc.Id, cameraLight);
            return stream;
        }

        private void Begin(int id, Light light)
        {
            StreamId = id; cameraLight = light;
            phone = light != null ? light.transform.parent : null;
            random = new System.Random(id * 7919 + 3);
            viewers = 800 + random.Next(1600);
            enabled = true;
            if (tag_ == null) Build();
            tag_.gameObject.SetActive(true); fan.gameObject.SetActive(true);
            Paint();
        }

        private void OnDisable()
        {
            if (tag_ != null) tag_.gameObject.SetActive(false);
            if (fan != null) fan.gameObject.SetActive(false);
            foreach (var like in likes) if (like != null) like.gameObject.SetActive(false);
        }

        private void Build()
        {
            // The tag: a red pill with white text, printed on both faces, turned to the camera.
            tag_ = new GameObject("Live tag").transform;
            tag_.SetParent(transform, false);
            tag_.localPosition = new Vector3(0f, 2.3f, 0f);
            var pill = DressingKit.Panel(tag_, "Live pill", Vector3.zero, new Vector3(0.78f, 0.22f, 0.02f));
            pill.sharedMaterial = ArtLibrary.Emissive(new Color(0.9f, 0.1f, 0.12f), 1.4f);
            pill.shadowCastingMode = ShadowCastingMode.Off;
            front = SignFactory.Label(tag_, "", 0.09f, Color.white);
            if (front != null) front.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            back = SignFactory.Label(tag_, "", 0.09f, Color.white);
            if (back != null) { back.transform.localPosition = new Vector3(0f, 0f, 0.015f); back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); }
            // The shot on the floor: a fan mesh, bright near the camera and fading out at the edge of its range.
            fan = new GameObject("Live shot").transform;
            fan.SetParent(transform, false);
            var filter = fan.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = FanMesh(Range, Angle, 18);
            fanRenderer = fan.gameObject.AddComponent<MeshRenderer>();
            calm = ArtLibrary.Particle(new Color(1f, 0.25f, 0.2f, 0.16f)) ?? ArtLibrary.Emissive(new Color(0.5f, 0.08f, 0.06f), 0.4f);
            hot = ArtLibrary.Particle(new Color(1f, 0.2f, 0.15f, 0.34f)) ?? ArtLibrary.Emissive(new Color(0.9f, 0.1f, 0.08f), 1f);
            fanRenderer.sharedMaterial = calm;
            fanRenderer.shadowCastingMode = ShadowCastingMode.Off; fanRenderer.receiveShadows = false;
            // Likes: soft pink dots that drift up from the phone.
            var pink = ArtLibrary.Particle(new Color(1f, 0.45f, 0.7f, 0.9f)) ?? ArtLibrary.Emissive(new Color(1f, 0.4f, 0.65f), 2f);
            for (int i = 0; i < likes.Length; i++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                dot.name = "Like";
                Destroy(dot.GetComponent<Collider>());
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * 0.09f;
                var r = dot.GetComponent<Renderer>();
                r.sharedMaterial = pink; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                dot.SetActive(false);
                likes[i] = dot.transform;
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.transform.IsChildOf(tag_) || r.transform == fan || r.name == "Like") r.gameObject.layer = 2;
        }

        private static Mesh FanMesh(float radius, float degrees, int segments)
        {
            var mesh = new Mesh { name = "Live shot fan" };
            var vertices = new Vector3[segments + 2];
            var colors = new Color[segments + 2];
            var uv = new Vector2[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero; colors[0] = Color.white; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= segments; i++)
            {
                float a = (-degrees * 0.5f + degrees * i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                colors[i + 1] = new Color(1f, 1f, 1f, 0f); // fades out at the far edge
                uv[i + 1] = new Vector2(0.5f, 0.5f);
            }
            for (int i = 0; i < segments; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Where the camera points, flattened onto the floor.</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 f = cameraLight != null ? cameraLight.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
            }
        }

        public Vector3 Lens => phone != null ? phone.position : transform.position + Vector3.up * 1.5f;

        /// <summary>Is this point in the shot right now: in range, inside the angle, and nothing solid in between?</summary>
        public bool Sees(Vector3 point, Transform body)
        {
            if (!isActiveAndEnabled) return false;
            Vector3 from = Lens;
            Vector3 to = point - from;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.magnitude > Range || flat.sqrMagnitude < 0.01f) return false;
            if (Vector3.Angle(Facing, flat) > Angle * 0.5f) return false;
            // Walls and shelves block the shot; people do not (a phone camera held up sees over and between them).
            int count = Physics.RaycastNonAlloc(from, to.normalized, hits, to.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].transform;
                if (body != null && (t == body || t.IsChildOf(body))) continue;
                if (t.IsChildOf(transform) || t.GetComponentInParent<NpcController>() != null || t.GetComponentInParent<PlayerMotor>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>The chat goes wild: a burst of likes and viewers (a shout-out, or a mannequin caught moving).</summary>
        public void Spike(int extraViewers)
        {
            viewers += extraViewers;
            hotUntil = Time.time + 2.5f;
            for (int i = 0; i < likes.Length; i++) if (!likes[i].gameObject.activeSelf) Launch(i, i * 0.12f);
            Paint();
        }

        private void Launch(int i, float delay)
        {
            likes[i].gameObject.SetActive(true);
            likeAge[i] = -delay;
            likes[i].position = Lens + new Vector3((float)random.NextDouble() * 0.2f - 0.1f, 0.05f, (float)random.NextDouble() * 0.2f - 0.1f);
        }

        private void Paint()
        {
            string text = "● LIVE  " + (viewers >= 1000 ? (viewers / 1000f).ToString("0.0") + "k" : Viewers.ToString());
            if (front != null && front.text != text) { front.text = text; back.text = text; }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var view = ViewCameras.Current;
            if (view != null && tag_ != null)
            {
                Vector3 toCamera = view.transform.position - tag_.position; toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.001f) tag_.rotation = Quaternion.LookRotation(-toCamera);
            }
            if (fan != null)
            {
                fan.position = new Vector3(transform.position.x, transform.position.y + 0.03f, transform.position.z);
                fan.rotation = Quaternion.LookRotation(Facing, Vector3.up);
                fanRenderer.sharedMaterial = Time.time < hotUntil ? hot : calm;
            }
            // The audience drifts up while they film; the count ticks once a second.
            viewers += dt * (3f + (float)random.NextDouble() * 6f);
            textTimer -= dt;
            if (textTimer <= 0f) { textTimer = 1f; Paint(); }
            likeTimer -= dt;
            if (likeTimer <= 0f)
            {
                likeTimer = 0.7f + (float)random.NextDouble() * 0.9f;
                for (int i = 0; i < likes.Length; i++) if (!likes[i].gameObject.activeSelf) { Launch(i, 0f); break; }
            }
            for (int i = 0; i < likes.Length; i++)
            {
                if (!likes[i].gameObject.activeSelf) continue;
                likeAge[i] += dt;
                if (likeAge[i] < 0f) continue;
                if (likeAge[i] > 1.6f) { likes[i].gameObject.SetActive(false); continue; }
                likes[i].position += new Vector3(Mathf.Sin(likeAge[i] * 5f + i) * 0.12f * dt, 0.5f * dt, 0f);
                likes[i].localScale = Vector3.one * (0.09f * (1f - likeAge[i] / 1.6f) + 0.02f);
                if (view != null) likes[i].rotation = Quaternion.LookRotation(likes[i].position - view.transform.position);
            }
        }
    }
}
