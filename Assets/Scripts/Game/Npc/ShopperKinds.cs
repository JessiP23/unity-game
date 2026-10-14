using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>Who someone is in the crowd. Each kind reads at a glance: a shape, a pose, a prop.</summary>
    public enum ShopperKind { Regular, Kid, PhoneZombie, Influencer, Cleaner, Restocker }

    /// <summary>
    /// The people who make the mall feel full, as data. Kids see everything but take a moment to tug a parent;
    /// phone zombies see almost nothing; the influencer's camera sees everything, crowd or no crowd.
    /// </summary>
    public static class ShopperArchetypes
    {
        private static CustomerProfile kid, phone, influencer;

        public static CustomerProfile Kid()
        {
            if (kid != null) return kid;
            kid = ScriptableObject.CreateInstance<CustomerProfile>();
            kid.name = "Kid";
            kid.walkSpeed = new Vector2(1.1f, 1.5f);
            kid.visionRange = 7f; kid.fieldOfView = 200f; kid.perceptionInterval = 0.2f;
            kid.suspicionSensitivity = new Vector2(1.3f, 1.6f); kid.noticeSeconds = new Vector2(0.15f, 0.25f);
            // The "tug": a kid who saw you move takes a couple of seconds to get a grown-up to look. Freeze and it fizzles.
            kid.reactionSeconds = new Vector2(2.0f, 2.6f); kid.reportDelaySeconds = new Vector2(0.6f, 1.0f);
            kid.reportingProbability = 1f; kid.leaveAfterReport = 0f;
            return kid;
        }

        private static CustomerProfile partyKid;

        /// <summary>A kid at the birthday party: stays in the toy store for the whole night and sees everything.</summary>
        public static CustomerProfile PartyKid()
        {
            if (partyKid != null) return partyKid;
            partyKid = Object.Instantiate(Kid());
            partyKid.name = "Party kid";
            partyKid.browseSeconds = new Vector2(6, 14); partyKid.waitChance = 0.5f; partyKid.waitSeconds = new Vector2(2, 5);
            partyKid.items = new Vector2Int(8, 12); partyKid.checkoutChance = 0f; partyKid.maxVisitSeconds = 900f;
            partyKid.preferredDepartments = new[] { new CustomerProfile.Preference { department = ZoneType.ToyStore, weight = 100f } };
            return partyKid;
        }

        public static CustomerProfile PhoneZombie()
        {
            if (phone != null) return phone;
            phone = ScriptableObject.CreateInstance<CustomerProfile>();
            phone.name = "Phone zombie";
            phone.walkSpeed = new Vector2(0.75f, 1.05f);
            phone.browseSeconds = new Vector2(4, 10);
            phone.visionRange = 3.5f; phone.fieldOfView = 40f; phone.perceptionInterval = 0.3f;
            phone.suspicionSensitivity = new Vector2(0.4f, 0.6f); phone.noticeSeconds = new Vector2(0.8f, 1.2f);
            phone.reactionSeconds = new Vector2(1.5f, 2.5f); phone.reportingProbability = 0.5f;
            return phone;
        }

        public static CustomerProfile Influencer()
        {
            if (influencer != null) return influencer;
            influencer = ScriptableObject.CreateInstance<CustomerProfile>();
            influencer.name = "Influencer";
            influencer.walkSpeed = new Vector2(0.8f, 1.0f);
            influencer.browseSeconds = new Vector2(9, 14); influencer.waitChance = 0.6f; influencer.waitSeconds = new Vector2(4, 7);
            influencer.items = new Vector2Int(2, 3);
            // The camera is the eyes: what the red fan on the floor shows is exactly what they see.
            influencer.visionRange = LiveStream.Range; influencer.fieldOfView = LiveStream.Angle; influencer.perceptionInterval = 0.15f;
            // On camera there is no "maybe": the clip is posted before anyone thinks twice.
            influencer.suspicionSensitivity = new Vector2(2.2f, 2.4f); influencer.noticeSeconds = new Vector2(0.15f, 0.2f);
            influencer.reactionSeconds = new Vector2(0.25f, 0.35f); influencer.reportDelaySeconds = new Vector2(0.25f, 0.35f);
            influencer.reportingProbability = 1f; influencer.leaveAfterReport = 0f;
            return influencer;
        }
    }

    /// <summary>
    /// Dresses a pooled body for its kind: pose, size, props and the influencer's red camera light. Called on
    /// every spawn, so a body reused from the pool never keeps the last person's mop.
    /// </summary>
    public sealed class ShopperLook : MonoBehaviour
    {
        private GameObject props;
        private CapsuleCollider body;
        private Transform visualRoot;
        private Vector3 baseScale = Vector3.one;
        private float baseY;
        private bool sized;
        public ShopperKind Kind { get; private set; }
        /// <summary>The influencer's camera light: the same "don't move in the red light" rule as the mall's cameras.</summary>
        public Light CameraLight { get; private set; }

        public void Apply(ShopperKind kind, NpcAnimationHooks hooks)
        {
            Kind = kind;
            var stream = GetComponent<LiveStream>();
            if (stream != null && kind != ShopperKind.Influencer) stream.enabled = false; // a pooled body that streamed last time
            if (props != null) Destroy(props);
            props = null; CameraLight = null;
            if (body == null) body = GetComponent<CapsuleCollider>();
            var visual = hooks != null ? hooks.Visual : null;
            visualRoot = visual != null ? visual.transform : transform.Find("Placeholder body");
            float scale = kind == ShopperKind.Kid ? 0.7f : 1f;
            if (visualRoot != null)
            {
                // Remember the body's own size and height once, then scale from those (a kid is 70% of a grown-up, feet on the floor).
                if (!sized) { baseScale = visualRoot.localScale; baseY = visualRoot.localPosition.y; sized = true; }
                visualRoot.localScale = baseScale * scale;
                if (visual == null) visualRoot.localPosition = new Vector3(visualRoot.localPosition.x, baseY * scale, visualRoot.localPosition.z);
            }
            if (body != null) { body.height = 1.8f * scale; body.center = new Vector3(0, 0.9f * scale, 0); }
            var perception = GetComponent<NpcPerception>();
            if (perception != null) perception.eyeHeight = 1.62f * scale;
            if (visual != null) visual.SetStance(kind switch
            {
                ShopperKind.PhoneZombie => Stance.Phone,
                ShopperKind.Influencer => Stance.Filming,
                ShopperKind.Cleaner => Stance.Mopping,
                ShopperKind.Restocker => Stance.Carrying,
                _ => Stance.Neutral
            });
            if (kind == ShopperKind.Regular || kind == ShopperKind.Kid) return;
            props = new GameObject(kind + " props");
            props.transform.SetParent(transform, false);
            switch (kind)
            {
                case ShopperKind.PhoneZombie: Phone(visual, new Vector3(0f, 0.04f, 0.06f), 0.9f, false); break;
                case ShopperKind.Influencer: Influencer(visual); break;
                case ShopperKind.Cleaner: Mop(visual); break;
                case ShopperKind.Restocker: Box(visual); break;
            }
            foreach (var r in props.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.gameObject.layer = 2; }
            foreach (var c in props.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        /// <summary>A phone held up as a torch: a narrow white beam that is exactly what this shopper can see (blackout).</summary>
        public void AddTorch()
        {
            var hooks = GetComponent<NpcAnimationHooks>();
            var visual = hooks != null ? hooks.Visual : null;
            if (props == null) { props = new GameObject("Torch props"); props.transform.SetParent(transform, false); }
            var hand = Follow(visual, new Vector3(0.2f, 1.25f, 0.35f), new Vector3(0f, 0.04f, 0.06f), new Vector3(8f, 0f, 0f));
            var phone = DressingKit.Panel(hand, "Phone", Vector3.zero, new Vector3(0.075f, 0.15f, 0.012f));
            phone.sharedMaterial = ArtLibrary.Lit(new Color(0.08f, 0.08f, 0.1f), 0.8f);
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Torch LED"; Destroy(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(hand, false); lamp.transform.localPosition = new Vector3(0f, 0.06f, 0.008f); lamp.transform.localScale = Vector3.one * 0.02f;
            lamp.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(Color.white, 4f);
            var beam = new GameObject("Torch beam").AddComponent<Light>();
            beam.transform.SetParent(hand, false);
            beam.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            beam.type = LightType.Spot; beam.spotAngle = 38f; beam.range = 8.5f; beam.intensity = 2.4f;
            beam.color = new Color(1f, 0.97f, 0.9f); beam.shadows = LightShadows.None;
            if (visual != null && Kind == ShopperKind.Regular) visual.SetStance(Stance.Filming);
            foreach (var r in props.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.gameObject.layer = 2; }
        }

        /// <summary>A kid pointing at what they saw (and back to normal once they calm down).</summary>
        public void Point(bool on)
        {
            var hooks = GetComponent<NpcAnimationHooks>();
            var visual = hooks != null ? hooks.Visual : null;
            if (visual != null && Kind == ShopperKind.Kid) visual.SetStance(on ? Stance.Pointing : Stance.Neutral);
        }

        private Transform Follow(CharacterVisual visual, Vector3 fallback, Vector3 offset, Vector3 euler)
        {
            var anchor = new GameObject("Held").transform;
            anchor.SetParent(props.transform, false);
            var follower = anchor.gameObject.AddComponent<PropFollower>();
            follower.Configure(transform, visual != null ? visual.HandRight : null, fallback, offset, Quaternion.Euler(euler));
            return anchor;
        }

        private void Phone(CharacterVisual visual, Vector3 offset, float glow, bool outward)
        {
            var hand = Follow(visual, new Vector3(0.18f, 1.3f, 0.3f), offset, outward ? Vector3.zero : new Vector3(-55f, 0f, 0f));
            var case_ = DressingKit.Panel(hand, "Phone", Vector3.zero, new Vector3(0.075f, 0.15f, 0.012f));
            case_.sharedMaterial = ArtLibrary.Lit(new Color(0.08f, 0.08f, 0.1f), 0.8f);
            var screen = DressingKit.Panel(hand, "Phone screen", new Vector3(0f, 0f, -0.007f), new Vector3(0.066f, 0.135f, 0.002f));
            screen.sharedMaterial = ArtLibrary.Emissive(new Color(0.55f, 0.75f, 1f), glow);
        }

        private void Influencer(CharacterVisual visual)
        {
            Phone(visual, new Vector3(0f, 0.05f, 0.08f), 1.2f, true);
            var hand = props.transform.GetChild(0);
            var rec = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rec.name = "REC light";
            rec.transform.SetParent(hand, false);
            rec.transform.localPosition = new Vector3(0.025f, 0.06f, 0.012f);
            rec.transform.localScale = Vector3.one * 0.025f;
            rec.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.1f, 0.08f), 3f);
            // A selfie ring light on a little stick, so the influencer reads from across the atrium.
            var ringHost = new GameObject("Ring light");
            ringHost.transform.SetParent(hand, false);
            ringHost.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(ringHost.transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.16f, 0.004f, 0.16f);
            ring.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.92f, 0.85f), 1.6f);
            var cone = new GameObject("Camera light");
            cone.transform.SetParent(hand, false);
            cone.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            CameraLight = cone.AddComponent<Light>();
            CameraLight.type = LightType.Spot; CameraLight.spotAngle = LiveStream.Angle; CameraLight.range = LiveStream.Range;
            CameraLight.color = new Color(1f, 0.2f, 0.15f); CameraLight.intensity = 2.2f; CameraLight.shadows = LightShadows.None;
        }

        private void Mop(CharacterVisual visual)
        {
            var hand = Follow(visual, new Vector3(0.15f, 0.95f, 0.35f), Vector3.zero, new Vector3(35f, 0f, 0f));
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Mop handle";
            handle.transform.SetParent(hand, false);
            handle.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            handle.transform.localScale = new Vector3(0.025f, 0.65f, 0.025f);
            handle.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(new Color(0.85f, 0.75f, 0.2f), 0.4f);
            var head = DressingKit.Panel(hand, "Mop head", new Vector3(0f, -1.15f, 0f), new Vector3(0.36f, 0.06f, 0.14f));
            head.sharedMaterial = ArtLibrary.Lit(new Color(0.82f, 0.82f, 0.78f), 0.1f);
        }

        private void Box(CharacterVisual visual)
        {
            var hand = Follow(visual, new Vector3(0f, 1.05f, 0.42f), new Vector3(-0.15f, 0.05f, 0.12f), Vector3.zero);
            var box = DressingKit.Panel(hand, "Stock box", Vector3.zero, new Vector3(0.48f, 0.32f, 0.36f));
            box.sharedMaterial = ArtLibrary.Lit(new Color(0.66f, 0.5f, 0.32f), 0.2f);
            var tape = DressingKit.Panel(hand, "Tape", new Vector3(0f, 0.161f, 0f), new Vector3(0.06f, 0.004f, 0.37f));
            tape.sharedMaterial = ArtLibrary.Lit(new Color(0.75f, 0.62f, 0.4f), 0.6f);
        }
    }

    /// <summary>Keeps a prop in a hand after the animation and poses have moved the bones this frame.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PropFollower : MonoBehaviour
    {
        private Transform owner, hand;
        private Vector3 fallback, offset;
        private Quaternion tilt;

        public void Configure(Transform host, Transform handBone, Vector3 hostFallback, Vector3 handOffset, Quaternion rotation)
        { owner = host; hand = handBone; fallback = hostFallback; offset = handOffset; tilt = rotation; LateUpdate(); }

        private void LateUpdate()
        {
            if (owner == null) return;
            Vector3 at = hand != null ? hand.position + owner.rotation * offset : owner.TransformPoint(fallback);
            transform.SetPositionAndRotation(at, owner.rotation * tilt);
        }
    }
}
