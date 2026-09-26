using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Rocketbox body that follows a gameplay host. Speed comes from how far the host moved, so it
    /// never feeds back into movement or detection. Mannequins hold their current frame when they stop.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        private const float WalkBlendSpeed = 1.2f;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable idle, walk, run;
        private float walkSpeed = 1.4f, runSpeed = 3.4f;
        private Transform host, hips;
        private Vector3 hipsBind, lastPosition;
        private float smoothedSpeed;
        private bool holdWhenStopped;
        private Renderer[] renderers;
        public float Speed => smoothedSpeed;
        public static CharacterVisual Attach(Transform host, string character, Vector3 offset, bool mannequin, string idleClip, string walkClip, string runClip)
        {
            var prefab = ArtLibrary.Character(character);
            if (prefab == null) return null;
            var body = Instantiate(prefab, host, false);
            body.name = character + " visual";
            body.transform.localPosition = offset;
            ArtLibrary.Strip(body);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string name = shared[i] != null ? shared[i].name : "";
                    bool head = name.EndsWith("_head"), opacity = name.EndsWith("_opacity");
                    shared[i] = ArtLibrary.CharacterMaterial(character, name, mannequin && head, mannequin && opacity);
                }
                renderer.sharedMaterials = shared;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
            var visual = body.AddComponent<CharacterVisual>();
            visual.Configure(host, mannequin, idleClip, walkClip, runClip);
            return visual;
        }
        private void Configure(Transform follow, bool mannequin, string idleClip, string walkClip, string runClip)
        {
            host = follow;
            holdWhenStopped = mannequin;
            renderers = GetComponentsInChildren<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == "Bip01") { hips = t; break; }
            if (hips != null) hipsBind = transform.InverseTransformPoint(hips.position);
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var idleAsset = Clip(idleClip); var walkAsset = Clip(walkClip); var runAsset = Clip(runClip);
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
            if (mannequin) { idle.SetTime(2.0); idle.SetSpeed(0); }
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(mixer);
            graph.Play();
            lastPosition = host.position;
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
            Vector3 moved = host.position - lastPosition; moved.y = 0;
            lastPosition = host.position;
            float speed = moved.magnitude > 2f ? 0f : moved.magnitude / delta;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * delta));
            if (smoothedSpeed < 0.08f)
            {
                if (holdWhenStopped) { walk.SetSpeed(0); run.SetSpeed(0); idle.SetSpeed(0); return; }
                Blend(1, 0, 0, delta);
                return;
            }
            float walkWeight = Mathf.Clamp01(smoothedSpeed / WalkBlendSpeed);
            float runWeight = Mathf.Clamp01((smoothedSpeed - walkSpeed) / Mathf.Max(0.1f, runSpeed - walkSpeed));
            Blend(1 - walkWeight, walkWeight * (1 - runWeight), walkWeight * runWeight, delta);
            walk.SetSpeed(Mathf.Clamp(smoothedSpeed / walkSpeed, 0.6f, 1.6f));
            run.SetSpeed(Mathf.Clamp(smoothedSpeed / runSpeed, 0.6f, 1.5f));
            if (!holdWhenStopped) idle.SetSpeed(1);
        }
        private void Blend(float idleWeight, float walkWeight, float runWeight, float delta)
        {
            float rate = 1f - Mathf.Exp(-8f * delta);
            mixer.SetInputWeight(0, Mathf.Lerp(mixer.GetInputWeight(0), idleWeight, rate));
            mixer.SetInputWeight(1, Mathf.Lerp(mixer.GetInputWeight(1), walkWeight, rate));
            mixer.SetInputWeight(2, Mathf.Lerp(mixer.GetInputWeight(2), runWeight, rate));
            if (!holdWhenStopped) { walk.SetSpeed(1); run.SetSpeed(1); idle.SetSpeed(1); }
        }
        private void LateUpdate()
        {
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
