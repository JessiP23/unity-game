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
            follow = host.GetComponent<MotionInterpolator>();
            if (follow == null) follow = host.gameObject.AddComponent<MotionInterpolator>();
            renderers = GetComponentsInChildren<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == "Bip01") { hips = t; break; }
            if (hips != null) hipsBind = transform.InverseTransformPoint(hips.position);
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

        /// <summary>Hides the body from its owner's camera while keeping its shadow.</summary>
        public void SetFirstPerson(bool firstPerson)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                renderer.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
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
            if (hips == null) return;
            Vector3 local = transform.InverseTransformPoint(hips.position);
            local.x = hipsBind.x; local.z = hipsBind.z;
            hips.position = transform.TransformPoint(local);
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
