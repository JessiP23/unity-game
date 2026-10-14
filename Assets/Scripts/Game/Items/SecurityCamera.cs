using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A ceiling camera that sweeps a red pool of light across the floor. The rule is one sentence:
    /// do not move in the red light. Stand still inside it and nothing happens; move for more than a
    /// moment and the camera reports you to the guard like a shopper would (same report, same
    /// consequences). The light is the whole interface: no icon, no meter.
    /// </summary>
    public sealed class SecurityCamera : MonoBehaviour
    {
        public const float ConeAngle = 44f, Range = 15f, SweepDegrees = 38f, SweepSeconds = 7f, MovingGrace = 0.45f;
        public string Id { get; private set; }
        private Transform head;
        private Light cone;
        private Renderer led;
        private Material ledIdle, ledAlert;
        private Func<System.Collections.Generic.IReadOnlyList<PlayerMotor>> bodies;
        private Func<PlayerMotor, bool> report;
        private Action<PlayerMotor> firstSeen;
        private float baseYaw, timer, alertUntil;
        private readonly System.Collections.Generic.Dictionary<PlayerMotor, float> movingSince = new System.Collections.Generic.Dictionary<PlayerMotor, float>();
        private readonly System.Collections.Generic.Dictionary<PlayerMotor, float> cooldown = new System.Collections.Generic.Dictionary<PlayerMotor, float>();
        private readonly System.Collections.Generic.HashSet<PlayerMotor> warned = new System.Collections.Generic.HashSet<PlayerMotor>();

        public static SecurityCamera Create(Transform parent, string id, Vector3 mount, Vector3 lookAt,
            Func<System.Collections.Generic.IReadOnlyList<PlayerMotor>> mannequins, Func<PlayerMotor, bool> reportSink, Action<PlayerMotor> onFirstSeen, Vector3? wallPoint = null)
        {
            var host = new GameObject("Security camera " + id);
            host.transform.SetParent(parent, false);
            host.transform.position = mount;
            var camera = host.AddComponent<SecurityCamera>();
            camera.Id = id; camera.bodies = mannequins; camera.report = reportSink; camera.firstSeen = onFirstSeen;
            var dark = ArtLibrary.Lit(new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.6f);
            // A bracket from the wall (or the ceiling) to the head.
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube); arm.name = "Camera arm";
            arm.transform.SetParent(host.transform, false);
            Vector3 anchor = wallPoint ?? mount + Vector3.up * 0.5f;
            arm.transform.position = (anchor + mount) * 0.5f;
            arm.transform.rotation = Quaternion.LookRotation((mount - anchor).normalized == Vector3.zero ? Vector3.forward : (mount - anchor).normalized);
            arm.transform.localScale = new Vector3(0.08f, 0.08f, Mathf.Max(0.1f, Vector3.Distance(anchor, mount)));
            arm.GetComponent<Renderer>().sharedMaterial = dark; UnityEngine.Object.Destroy(arm.GetComponent<Collider>());
            camera.head = new GameObject("Camera head").transform;
            camera.head.SetParent(host.transform, false);
            camera.head.localPosition = new Vector3(0f, -0.5f, 0f);
            camera.head.LookAt(lookAt);
            camera.baseYaw = camera.head.eulerAngles.y;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "Camera body";
            body.transform.SetParent(camera.head, false); body.transform.localPosition = new Vector3(0f, 0f, 0.1f); body.transform.localScale = new Vector3(0.18f, 0.18f, 0.42f);
            body.GetComponent<Renderer>().sharedMaterial = dark; UnityEngine.Object.Destroy(body.GetComponent<Collider>());
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere); lens.name = "Camera led";
            lens.transform.SetParent(camera.head, false); lens.transform.localPosition = new Vector3(0f, 0.12f, 0.2f); lens.transform.localScale = Vector3.one * 0.06f;
            UnityEngine.Object.Destroy(lens.GetComponent<Collider>());
            camera.led = lens.GetComponent<Renderer>();
            camera.ledIdle = ArtLibrary.Emissive(new Color(1f, 0.2f, 0.15f), 1.5f);
            camera.ledAlert = ArtLibrary.Emissive(new Color(1f, 0.9f, 0.3f), 3f);
            camera.led.sharedMaterial = camera.ledIdle;
            camera.cone = new GameObject("Camera cone").AddComponent<Light>();
            camera.cone.transform.SetParent(camera.head, false);
            camera.cone.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            camera.cone.type = LightType.Spot; camera.cone.spotAngle = ConeAngle; camera.cone.innerSpotAngle = ConeAngle * 0.7f;
            camera.cone.range = Range; camera.cone.intensity = 14f; camera.cone.color = new Color(1f, 0.1f, 0.08f);
            camera.cone.shadows = LightShadows.None; camera.cone.renderMode = LightRenderMode.ForcePixel;
            return camera;
        }

        /// <summary>True when the point is inside the red light: in the cone and nothing solid between.</summary>
        public bool Covers(Vector3 point)
        {
            Vector3 origin = cone.transform.position;
            Vector3 to = point + Vector3.up * 0.9f - origin;
            if (to.magnitude > Range) return false;
            if (Vector3.Angle(cone.transform.forward, to) > ConeAngle * 0.5f) return false;
            return !Physics.Raycast(origin, to.normalized, to.magnitude - 0.4f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float yaw = baseYaw + Mathf.Sin(timer / SweepSeconds * Mathf.PI * 2f) * SweepDegrees;
            var euler = head.eulerAngles; euler.y = yaw; head.eulerAngles = euler;
            bool alert = Time.time < alertUntil;
            led.sharedMaterial = alert ? ledAlert : ledIdle;
            cone.color = alert ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.1f, 0.08f);
            if (bodies == null) return;
            var list = bodies();
            for (int i = 0; i < list.Count; i++)
            {
                var body = list[i];
                if (body == null || !body.Record.Free) continue;
                // Crouched, the camera aims at a lower body (low cover blocks it); hidden in a spot, it sees nothing.
                var seen = body.GetComponent<PerceptionTarget>();
                bool inside = (seen == null || !seen.Hidden) && Covers(body.transform.position - (body.Crouched ? Vector3.up * 0.45f : Vector3.zero));
                if (!inside) { movingSince.Remove(body); continue; }
                if (warned.Add(body)) firstSeen?.Invoke(body);
                bool moving = body.ActualSpeed > body.Rules.movementThreshold;
                if (!moving) { movingSince.Remove(body); continue; }
                if (!movingSince.ContainsKey(body)) movingSince[body] = Time.time;
                if (Time.time - movingSince[body] < MovingGrace) continue;
                if (cooldown.TryGetValue(body, out float until) && Time.time < until) continue;
                cooldown[body] = Time.time + 12f;
                alertUntil = Time.time + 2.5f;
                report?.Invoke(body);
            }
        }
    }
}
