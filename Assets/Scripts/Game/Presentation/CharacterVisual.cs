using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Animated body that follows a gameplay host. Speed and turn rate come from how the (interpolated)
    /// host moved, so visuals never feed back into movement or detection. Stride playback matches ground
    /// speed, turning in place steps the feet, and mannequins hold their current frame when they stop.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        private const float StopSpeed = 0.08f;
        private const float TurnStepRate = 50f;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable idle, walk, run;
        private float walkSpeed = 1.4f, runSpeed = 3.4f;
        private Transform host, hips;
        private MotionInterpolator follow;
        private CharacterProfile profile;
        private Vector3 hipsBind, lastPosition;
        private float lastYaw, smoothedSpeed, smoothedTurn;
        private Renderer[] renderers;
        public float Speed => smoothedSpeed;
        private Transform upperArmL, upperArmR, forearmL, forearmR, head, spine;
        private Stance stance = Stance.Neutral;
        private float stanceWeight;
        /// <summary>
        /// Procedural upper-body poses layered over the frozen idle frame, since the art pack ships no
        /// pose clips. Poses are written as directions in the character's own space (right, up, forward),
        /// not bone-local angles, so they come out the same on any rig: each arm bone is turned so that
        /// it points where the pose says. Head and spine tilt about the character's right axis.
        /// </summary>
        private readonly struct StancePose
        {
            public readonly Vector3 UpperL, UpperR, ForeL, ForeR;
            public readonly float HeadPitch, SpinePitch;
            public StancePose(Vector3 upperL, Vector3 upperR, Vector3 foreL, Vector3 foreR, float headPitch, float spinePitch)
            { UpperL = upperL; UpperR = upperR; ForeL = foreL; ForeR = foreR; HeadPitch = headPitch; SpinePitch = spinePitch; }
        }
        private static readonly StancePose[] StancePoses =
        {
            new StancePose(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0, 0), // Neutral: animation as is
            // Display: arms out to the sides and a little up, forearms carrying on outward, chin up.
            new StancePose(new Vector3(-1f, 0.45f, 0.05f), new Vector3(1f, 0.45f, 0.05f), new Vector3(-1f, 0.3f, 0.1f), new Vector3(1f, 0.3f, 0.1f), -14f, 0f),
            // Browsing: right hand reaching to a shelf ahead, left arm down, looking down at the goods.
            new StancePose(new Vector3(-0.15f, -1f, 0.05f), new Vector3(0.25f, -0.1f, 1f), new Vector3(-0.1f, -1f, 0.1f), new Vector3(0.1f, 0.15f, 1f), 12f, 4f),
            // Lounging: arms folded across the chest, leaning back.
            new StancePose(new Vector3(-0.25f, -0.75f, 0.45f), new Vector3(0.25f, -0.75f, 0.45f), new Vector3(1f, 0.05f, 0.25f), new Vector3(-1f, 0.05f, 0.25f), -5f, -10f),
            // Staff: hands clasped behind the back, upright.
            new StancePose(new Vector3(-0.2f, -0.9f, -0.4f), new Vector3(0.2f, -0.9f, -0.4f), new Vector3(1f, 0.1f, -0.2f), new Vector3(-1f, 0.1f, -0.2f), 0f, 3f),
            // Phone: right hand up in front of the chest, head bowed over the screen.
            new StancePose(new Vector3(-0.1f, -1f, 0.05f), new Vector3(0.12f, -0.85f, 0.4f), new Vector3(-0.05f, -1f, 0.1f), new Vector3(-0.3f, 0.5f, 0.8f), 28f, 6f),
            // Filming: phone held out at eye height, sweeping the room.
            new StancePose(new Vector3(-0.1f, -1f, 0.05f), new Vector3(0.15f, 0.08f, 1f), new Vector3(-0.05f, -1f, 0.1f), new Vector3(0f, 0.18f, 1f), -2f, -2f),
            // Pointing: a kid's arm straight out at what they saw.
            new StancePose(new Vector3(-0.1f, -1f, 0.05f), new Vector3(0.1f, 0.35f, 1f), new Vector3(-0.05f, -1f, 0.1f), new Vector3(0.05f, 0.4f, 1f), -10f, -4f),
            // Mopping: both hands low and forward on the handle, looking at the floor.
            new StancePose(new Vector3(0.05f, -0.6f, 0.8f), new Vector3(0.15f, -0.7f, 0.7f), new Vector3(0.1f, -0.5f, 0.85f), new Vector3(-0.05f, -0.6f, 0.8f), 16f, 8f),
            // Carrying: a box held out in front at waist height.
            new StancePose(new Vector3(-0.15f, -0.4f, 0.9f), new Vector3(0.15f, -0.4f, 0.9f), new Vector3(0.2f, 0.05f, 1f), new Vector3(-0.2f, 0.05f, 1f), 4f, -3f)
        };
        private Transform handL, handR, thighL, thighR, calfL, calfR, footL, footR;
        private float crouchWeight;
        private bool crouching;
        /// <summary>Squat down (the player's crouch): hips drop, knees bend forward, back leans in.</summary>
        public void SetCrouch(bool down) => crouching = down;
        private Transform rideSeat;
        private Vector3 lastSeat;
        private Quaternion seatYaw = Quaternion.identity, restLocal = Quaternion.identity, frame = Quaternion.identity;
        private float rideWeight;
        private bool rideRotated;
        private SeatStyle rideStyle;
        /// <summary>Sit (null to stand): astride a carousel saddle, legs either side and hands on the pole; or on a chair, knees forward.</summary>
        public void SetRide(Transform seat, SeatStyle style = SeatStyle.Astride)
        {
            // Getting up teleports the body: snap out of the seated pose rather than stretching back to the old seat.
            if (seat == null && rideSeat != null) { rideWeight = 0f; if (rideRotated && host != null) { transform.localRotation = restLocal; rideRotated = false; } }
            rideSeat = seat; if (seat != null) rideStyle = style;
        }
        /// <summary>Bones props hang from (null on rigs without Biped names).</summary>
        public Transform HandRight => handR;
        public Transform HandLeft => handL;
        public Transform HeadBone => head;

        public static CharacterVisual Attach(Transform host, CharacterProfile profile)
        {
            var prefab = ArtLibrary.Character(profile.Model);
            if (prefab == null) return null;
            var body = Instantiate(prefab, host, false);
            body.name = profile.Model + " visual";
            body.transform.localPosition = profile.Offset;
            ArtLibrary.Strip(body);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string name = shared[i] != null ? shared[i].name : "";
                    bool head = name.EndsWith("_head"), opacity = name.EndsWith("_opacity");
                    shared[i] = ArtLibrary.CharacterMaterial(profile.Model, name, profile.Mannequin && head, profile.Mannequin && opacity);
                }
                renderer.sharedMaterials = shared;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
            var visual = body.AddComponent<CharacterVisual>();
            visual.Configure(host, profile);
            return visual;
        }

        private void Configure(Transform owner, CharacterProfile character)
        {
            host = owner; profile = character;
            restLocal = transform.localRotation; frame = host.rotation;
            follow = host.GetComponent<MotionInterpolator>();
            if (follow == null) follow = host.gameObject.AddComponent<MotionInterpolator>();
            renderers = GetComponentsInChildren<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "Bip01") hips = t;
                else if (t.name.EndsWith("L UpperArm")) upperArmL = t;
                else if (t.name.EndsWith("R UpperArm")) upperArmR = t;
                else if (t.name.EndsWith("L Forearm")) forearmL = t;
                else if (t.name.EndsWith("R Forearm")) forearmR = t;
                else if (t.name.EndsWith(" Head")) head = t;
                else if (t.name.EndsWith("Spine1")) spine = t;
                else if (t.name.EndsWith("L Hand")) handL = t;
                else if (t.name.EndsWith("R Hand")) handR = t;
                else if (t.name.EndsWith("L Thigh")) thighL = t;
                else if (t.name.EndsWith("R Thigh")) thighR = t;
                else if (t.name.EndsWith("L Calf")) calfL = t;
                else if (t.name.EndsWith("R Calf")) calfR = t;
                else if (t.name.EndsWith("L Foot")) footL = t;
                else if (t.name.EndsWith("R Foot")) footR = t;
            }
            if (hips != null) hipsBind = transform.InverseTransformPoint(hips.position);
            if (upperArmL == null || upperArmR == null || forearmL == null || forearmR == null || head == null)
                Debug.LogWarning("CharacterVisual: some pose bones were not found on '" + name + "' (expects Biped names: 'L UpperArm', 'R Forearm', 'Head'…). Those parts keep the animation pose.", this);
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var idleAsset = Clip(profile.Idle); var walkAsset = Clip(profile.Walk); var runAsset = Clip(profile.Run);
            if (idleAsset == null || walkAsset == null || runAsset == null) return;
            walkSpeed = Mathf.Max(0.5f, TravelSpeed(walkAsset));
            runSpeed = Mathf.Max(walkSpeed + 0.5f, TravelSpeed(runAsset));
            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            idle = AnimationClipPlayable.Create(graph, idleAsset);
            walk = AnimationClipPlayable.Create(graph, walkAsset);
            run = AnimationClipPlayable.Create(graph, runAsset);
            graph.Connect(idle, 0, mixer, 0); graph.Connect(walk, 0, mixer, 1); graph.Connect(run, 0, mixer, 2);
            mixer.SetInputWeight(0, 1);
            if (profile.Mannequin) { idle.SetTime(2.0); idle.SetSpeed(0); }
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(mixer);
            graph.Play();
            lastPosition = follow.Position; lastYaw = host.eulerAngles.y;
        }

        private static AnimationClip Clip(string name)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>("Art/Characters/Animations/" + name))
                if (!clip.name.StartsWith("__preview")) return clip;
            return null;
        }

        /// <summary>Root travel per second of a Rocketbox xy clip, used to match stride to movement.</summary>
        private float TravelSpeed(AnimationClip clip)
        {
            if (hips == null || clip.length <= 0) return 0;
            clip.SampleAnimation(gameObject, 0);
            Vector3 start = transform.InverseTransformPoint(hips.position);
            clip.SampleAnimation(gameObject, clip.length);
            Vector3 end = transform.InverseTransformPoint(hips.position);
            end.y = start.y = 0;
            return Vector3.Distance(start, end) / clip.length;
        }

        /// <summary>Freezes a mannequin on a chosen frame of its idle clip, so display poses differ.</summary>
        public void HoldPose(double seconds)
        {
            if (!graph.IsValid()) return;
            idle.SetTime(seconds); idle.SetSpeed(0);
        }

        /// <summary>Which pose the upper body holds. Neutral lets the animation through.</summary>
        public void SetStance(Stance pose) => stance = pose;
        public bool HasPoseBones => upperArmL != null && upperArmR != null;

        private void ApplyStance(float delta)
        {
            float target = stance == Stance.Neutral ? 0f : 1f;
            stanceWeight = Mathf.MoveTowards(stanceWeight, target, delta / 0.35f);
            if (stanceWeight <= 0f || host == null) return;
            var pose = StancePoses[Mathf.Clamp((int)stance, 0, StancePoses.Length - 1)];
            // Spine and head first: the arms hang off the spine and are aimed afterwards.
            Tilt(spine, pose.SpinePitch);
            Tilt(head, pose.HeadPitch);
            Aim(upperArmL, forearmL, pose.UpperL);
            Aim(upperArmR, forearmR, pose.UpperR);
            Aim(forearmL, handL, pose.ForeL);
            Aim(forearmR, handR, pose.ForeR);
        }

        /// <summary>Turns <paramref name="bone"/> so the segment toward <paramref name="child"/> points along a character-space direction.</summary>
        private void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null || direction == Vector3.zero) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-6f) return;
            Vector3 wanted = frame * direction.normalized;
            var turn = Quaternion.FromToRotation(current.normalized, wanted);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, stanceWeight) * bone.rotation;
        }

        /// <summary>Pitches a bone about the character's right axis: positive looks down, negative up.</summary>
        private void Tilt(Transform bone, float degrees)
        {
            if (bone == null || Mathf.Approximately(degrees, 0f)) return;
            bone.rotation = Quaternion.AngleAxis(degrees * stanceWeight, frame * Vector3.right) * bone.rotation;
        }

        private void ApplyCrouch(float delta)
        {
            crouchWeight = Mathf.MoveTowards(crouchWeight, crouching ? 1f : 0f, delta / 0.2f);
            if (crouchWeight <= 0f || hips == null || host == null) return;
            float w = Mathf.SmoothStep(0f, 1f, crouchWeight);
            hips.position -= host.up * 0.36f * w;
            Bend(thighL, calfL, new Vector3(-0.05f, -0.45f, 0.9f), w);
            Bend(thighR, calfR, new Vector3(0.05f, -0.45f, 0.9f), w);
            Bend(calfL, footL, new Vector3(0f, -1f, -0.15f), w);
            Bend(calfR, footR, new Vector3(0f, -1f, -0.15f), w);
            // Keep the soles on the floor whatever the rig's proportions: lift the hips by however far the feet sank.
            if (footL != null && footR != null)
            {
                float sink = host.position.y + 0.08f - Mathf.Min(footL.position.y, footR.position.y);
                if (sink > 0f) hips.position += host.up * sink;
            }
            if (spine != null) spine.rotation = Quaternion.AngleAxis(18f * w, host.right) * spine.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(-12f * w, host.right) * head.rotation;
        }

        /// <summary>Like <see cref="Aim"/>, with its own weight (crouching is independent of the upper-body pose).</summary>
        private void Bend(Transform bone, Transform child, Vector3 direction, float weight)
        {
            if (bone == null || child == null) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-6f) return;
            var turn = Quaternion.FromToRotation(current.normalized, frame * direction.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
        }

        /// <summary>
        /// Riding faces the body along the horse whatever way the camera (and so the host) turns, so it never sits
        /// sideways through the saddle. Runs first: the poses below are aimed in this frame.
        /// </summary>
        private void UpdateRideFrame(float delta)
        {
            rideWeight = Mathf.MoveTowards(rideWeight, rideSeat != null ? 1f : 0f, delta / 0.25f);
            if (rideSeat != null)
            {
                lastSeat = rideSeat.position;
                Vector3 f = rideSeat.forward; f.y = 0f;
                if (f.sqrMagnitude > 0.001f) seatYaw = Quaternion.LookRotation(f, Vector3.up);
            }
            float w = Mathf.SmoothStep(0f, 1f, rideWeight);
            frame = w > 0f ? Quaternion.Slerp(host.rotation, seatYaw, w) : host.rotation;
            if (w > 0f)
            {
                transform.localRotation = Quaternion.Inverse(host.rotation) * frame * restLocal;
                rideRotated = true;
            }
            else if (rideRotated) { transform.localRotation = restLocal; rideRotated = false; }
        }

        private void ApplyRide()
        {
            if (rideWeight <= 0f || hips == null || host == null) return;
            float w = Mathf.SmoothStep(0f, 1f, rideWeight);
            hips.position = Vector3.Lerp(hips.position, lastSeat + Vector3.up * (rideStyle == SeatStyle.Chair ? 0.1f : 0.06f), w);
            if (rideStyle == SeatStyle.Chair)
            {
                // Seated: thighs level and forward, shins down to the floor, hands resting on the knees.
                Bend(thighL, calfL, new Vector3(-0.1f, -0.08f, 1f), w);
                Bend(thighR, calfR, new Vector3(0.1f, -0.08f, 1f), w);
                Bend(calfL, footL, new Vector3(-0.03f, -1f, 0.12f), w);
                Bend(calfR, footR, new Vector3(0.03f, -1f, 0.12f), w);
                if (stance != Stance.Neutral) return;
                Bend(upperArmL, forearmL, new Vector3(-0.12f, -0.9f, 0.35f), w);
                Bend(upperArmR, forearmR, new Vector3(0.12f, -0.9f, 0.35f), w);
                Bend(forearmL, handL, new Vector3(0.05f, -0.2f, 1f), w);
                Bend(forearmR, handR, new Vector3(-0.05f, -0.2f, 1f), w);
                return;
            }
            // Astride: thighs forward and out round the horse's barrel, shins hanging down by its sides.
            Bend(thighL, calfL, new Vector3(-0.42f, -0.5f, 0.75f), w);
            Bend(thighR, calfR, new Vector3(0.42f, -0.5f, 0.75f), w);
            Bend(calfL, footL, new Vector3(-0.1f, -1f, -0.08f), w);
            Bend(calfR, footR, new Vector3(0.1f, -1f, -0.08f), w);
            if (stance != Stance.Neutral) return; // a held pose keeps its arms
            // Both hands on the brass pole in front.
            Bend(upperArmL, forearmL, new Vector3(-0.12f, -0.35f, 0.92f), w);
            Bend(upperArmR, forearmR, new Vector3(0.12f, -0.35f, 0.92f), w);
            Bend(forearmL, handL, new Vector3(0.25f, 0.45f, 0.85f), w);
            Bend(forearmR, handR, new Vector3(-0.25f, 0.45f, 0.85f), w);
        }

        private int detail = -1;

        /// <summary>
        /// Crowd level of detail for shoppers and staff (never the player): 0 near, 1 mid, 2 far. Only near bodies
        /// cast shadows; mid and far ones skin with two bones. Off screen, nobody's skeleton or skin is updated:
        /// a fixed, generous box stands in for the body when culling (the hips are pinned, so the body never leaves it).
        /// </summary>
        public void SetDetail(int level)
        {
            if (renderers == null || level == detail) return;
            if (detail < 0)
            {
                var animator = GetComponent<Animator>();
                if (animator != null) animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (var renderer in renderers)
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        skinned.updateWhenOffscreen = false;
                        skinned.localBounds = new Bounds(Vector3.zero, Vector3.one * 3.6f);
                    }
            }
            detail = level;
            var shadows = level == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.shadowCastingMode = shadows;
                if (renderer is SkinnedMeshRenderer skinned) skinned.quality = level == 0 ? SkinQuality.Auto : SkinQuality.Bone2;
            }
            if (garment != null) garment.shadowCastingMode = shadows;
        }

        /// <summary>Hides the body from its owner's camera while keeping its shadow.</summary>
        public void SetFirstPerson(bool firstPerson)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                renderer.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (garment != null) garment.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        private Renderer garment;
        private Outfit outfit = Outfit.Plain;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock tintBlock;

        /// <summary>Outfit colour of the garment panel worn over the torso; Plain has none.</summary>
        public static Color OutfitColor(Outfit outfit) => outfit switch
        {
            Outfit.ShopperCoat => new Color(0.78f, 0.7f, 0.55f),
            Outfit.BoutiqueBlack => new Color(0.08f, 0.08f, 0.09f),
            Outfit.TechTee => new Color(0.25f, 0.45f, 0.75f),
            Outfit.Loungewear => new Color(0.55f, 0.2f, 0.28f),
            Outfit.StaffApron => new Color(0.16f, 0.42f, 0.3f),
            _ => Color.white
        };

        /// <summary>
        /// Dresses the body: a torso garment in the outfit's colour plus a light tint on the skin
        /// materials, so a mannequin in Boutique black reads as such from across the store. Rig-agnostic:
        /// the garment follows the spine bone in LateUpdate.
        /// </summary>
        public void SetOutfit(Outfit worn)
        {
            outfit = worn;
            if (garment == null)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Garment";
                panel.transform.SetParent(transform, false);
                panel.transform.localScale = new Vector3(0.36f, 0.46f, 0.2f);
                panel.layer = gameObject.layer;
                Destroy(panel.GetComponent<Collider>());
                garment = panel.GetComponent<Renderer>();
            }
            garment.enabled = worn != Outfit.Plain;
            garment.sharedMaterial = ArtLibrary.Surface("cotton_jersey", Vector2.one, OutfitColor(worn), 0.1f) ?? ArtLibrary.Lit(OutfitColor(worn), 0.15f);
            tintBlock ??= new MaterialPropertyBlock();
            Color tint = Color.Lerp(Color.white, OutfitColor(worn), 0.35f);
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == garment) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (worn == Outfit.Plain) { renderer.SetPropertyBlock(null, i); continue; }
                    var material = materials[i];
                    if (material == null || !material.HasProperty(BaseColor)) continue;
                    var own = material.GetColor(BaseColor);
                    if (own.a < 0.5f) continue; // hidden parts (hair cards etc.) keep their transparency
                    tintBlock.Clear();
                    tintBlock.SetColor(BaseColor, new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a));
                    renderer.SetPropertyBlock(tintBlock, i);
                }
            }
        }

        private void PlaceGarment()
        {
            if (garment == null || !garment.enabled) return;
            Vector3 anchor = spine != null ? spine.position : transform.position + Vector3.up * 1.25f;
            garment.transform.SetPositionAndRotation(anchor + frame * Vector3.forward * 0.08f + Vector3.up * 0.06f, frame);
        }

        private void Update()
        {
            if (!graph.IsValid() || host == null) return;
            float delta = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 position = follow.Position;
            Vector3 moved = position - lastPosition; moved.y = 0;
            lastPosition = position;
            float yaw = host.eulerAngles.y;
            float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / delta;
            lastYaw = yaw;
            float speed = moved.magnitude > 2f ? 0f : moved.magnitude / delta;
            float smoothing = 1f - Mathf.Exp(-10f * delta);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, smoothing);
            smoothedTurn = Mathf.Lerp(smoothedTurn, turn, smoothing);
            if (smoothedSpeed < StopSpeed)
            {
                if (profile.Mannequin) { Pause(); return; }
                float step = Mathf.Clamp01((smoothedTurn - TurnStepRate) / 90f) * 0.45f;
                Blend(1 - step, step, 0, delta);
                walk.SetSpeed(step > 0 ? 0.7f : 1f);
                idle.SetSpeed(1);
                return;
            }
            float walkWeight = Mathf.Clamp01(smoothedSpeed / (walkSpeed * 0.8f));
            float runWeight = Mathf.Clamp01((smoothedSpeed - walkSpeed) / Mathf.Max(0.1f, runSpeed - walkSpeed));
            Blend(1 - walkWeight, walkWeight * (1 - runWeight), walkWeight * runWeight, delta);
            walk.SetSpeed(Mathf.Clamp(smoothedSpeed / walkSpeed, 0.5f, 1.8f));
            run.SetSpeed(Mathf.Clamp(smoothedSpeed / runSpeed, 0.6f, 1.5f));
            idle.SetSpeed(profile.Mannequin ? 0 : 1);
        }

        private void Pause() { walk.SetSpeed(0); run.SetSpeed(0); idle.SetSpeed(0); }

        private void Blend(float idleWeight, float walkWeight, float runWeight, float delta)
        {
            float rate = 1f - Mathf.Exp(-8f * delta);
            mixer.SetInputWeight(0, Mathf.Lerp(mixer.GetInputWeight(0), idleWeight, rate));
            mixer.SetInputWeight(1, Mathf.Lerp(mixer.GetInputWeight(1), walkWeight, rate));
            mixer.SetInputWeight(2, Mathf.Lerp(mixer.GetInputWeight(2), runWeight, rate));
        }

        private void LateUpdate()
        {
            if (host == null) return;
            transform.position = follow.Position + host.rotation * profile.Offset;
            UpdateRideFrame(Time.deltaTime);
            ApplyStance(Time.deltaTime);
            if (hips != null)
            {
                Vector3 local = transform.InverseTransformPoint(hips.position);
                local.x = hipsBind.x; local.z = hipsBind.z;
                hips.position = transform.TransformPoint(local);
            }
            ApplyRide();
            ApplyCrouch(Time.deltaTime);
            PlaceGarment();
            ApplyTopple(Time.deltaTime);
        }

        private float topple, toppleTarget, toppleVelocity;
        /// <summary>True while the body is falling or lying flat.</summary>
        public bool Toppled => toppleTarget > 0.5f;
        /// <summary>Falls over stiff as a plank (true) or stands back up (false). Pivots at the feet, with one small bounce.</summary>
        public void Topple(bool down)
        {
            if (down) { rideSeat = null; rideWeight = 0f; if (rideRotated) { transform.localRotation = restLocal; rideRotated = false; } frame = host != null ? host.rotation : frame; }
            if (down && toppleTarget <= 0f && topple <= 0f) toppleRest = transform.localRotation;
            toppleTarget = down ? 1f : 0f;
            if (!down) { topple = 0f; toppleVelocity = 0f; transform.localRotation = toppleRest; }
        }
        private Quaternion toppleRest = Quaternion.identity;

        private void ApplyTopple(float delta)
        {
            if (toppleTarget <= 0f && topple <= 0f) return;
            if (toppleTarget >= 1f && topple >= 1f && toppleVelocity == 0f) return; // lying flat, at rest
            // A stiff body accelerates as it tips (gravity on a plank), hits the floor, bounces once.
            toppleVelocity += (toppleTarget >= topple ? 9f : -9f) * delta * (0.4f + topple);
            topple += toppleVelocity * delta;
            if (topple >= 1f) { topple = 1f; toppleVelocity = -toppleVelocity * 0.25f; if (Mathf.Abs(toppleVelocity) < 0.15f) toppleVelocity = 0f; }
            if (topple <= 0f && toppleTarget <= 0f) { topple = 0f; toppleVelocity = 0f; }
            // Rotate about the body's own left-right axis (in the parent's space), so any import rotation survives.
            transform.localRotation = Quaternion.AngleAxis(-90f * topple, Vector3.right) * toppleRest;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
