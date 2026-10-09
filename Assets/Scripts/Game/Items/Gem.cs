using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A hidden gem. By day it is a dull stone nobody looks at twice; after lights out it glints, pulses
    /// and shows on the store map. Some can only be reached while holding a pose (the one the
    /// department expects), so taking them means standing exposed in the dark. Gems are the only
    /// currency for outfits, which is why finding them is worth the risk.
    /// </summary>
    public sealed class Gem : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        public static readonly Color Glow = new Color(0.35f, 0.9f, 1f);
        public Stance Required { get; private set; } = Stance.Neutral;
        public bool Taken { get; private set; }
        public int Index { get; private set; }
        private bool lit;
        private Renderer[] shards = Array.Empty<Renderer>();
        private Light lamp;
        private SphereCollider reach;
        private Material dark, bright;
        private Func<PlayerMotor, Stance?> poseOf;
        private Action<Gem, PlayerMotor> collected;
        private float phase;

        /// <summary>True after lights out. A dark gem cannot be seen or taken.</summary>
        public bool Lit
        {
            get => lit;
            set
            {
                if (lit == value) return;
                lit = value;
                Refresh();
            }
        }

        public static Gem Create(Transform parent, int index, Vector3 at, Stance required, Func<PlayerMotor, Stance?> heldPose, Action<Gem, PlayerMotor> onCollected)
        {
            var host = new GameObject("Gem " + (index + 1));
            host.transform.SetParent(parent, false);
            host.transform.position = at;
            host.layer = 3; // off the NavMesh bake, like every loose item
            var gem = host.AddComponent<Gem>();
            gem.Index = index; gem.Required = required; gem.poseOf = heldPose; gem.collected = onCollected;
            gem.dark = ArtLibrary.Lit(new Color(0.16f, 0.17f, 0.19f), 0.85f, 0.2f);
            gem.bright = ArtLibrary.Emissive(Glow, 2.2f);
            // Two crossed, tilted cubes read as a cut stone from every side without a model.
            gem.shards = new[] { Shard(host.transform, Quaternion.Euler(45f, 0f, 45f)), Shard(host.transform, Quaternion.Euler(45f, 90f, 45f)) };
            gem.reach = host.AddComponent<SphereCollider>();
            gem.reach.radius = 0.14f;
            var lamp = new GameObject("Glint").AddComponent<Light>();
            lamp.transform.SetParent(host.transform, false);
            lamp.transform.localPosition = Vector3.up * 0.25f;
            lamp.type = LightType.Point; lamp.range = 2.6f; lamp.intensity = 0f; lamp.color = Glow; lamp.shadows = LightShadows.None;
            gem.lamp = lamp;
            gem.phase = index * 1.7f;
            gem.Refresh();
            return gem;
        }

        private static Renderer Shard(Transform parent, Quaternion tilt)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Shard";
            cube.transform.SetParent(parent, false);
            cube.transform.localRotation = tilt;
            cube.transform.localScale = new Vector3(0.13f, 0.2f, 0.13f);
            cube.layer = 3;
            UnityEngine.Object.Destroy(cube.GetComponent<Collider>());
            return cube.GetComponent<Renderer>();
        }

        private void Refresh()
        {
            bool live = lit && !Taken;
            foreach (var shard in shards) shard.sharedMaterial = live ? bright : dark;
            if (lamp != null) lamp.enabled = live;
            // A dark or taken gem is not an obstacle and cannot be aimed at.
            if (reach != null) reach.enabled = live;
        }

        private void Update()
        {
            if (!lit || Taken) return;
            float t = Time.time + phase;
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
            if (lamp != null) lamp.intensity = 1.2f + pulse * 1.6f;
            float bob = Mathf.Sin(t * 1.6f) * 0.03f;
            foreach (var shard in shards) shard.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        private bool PoseOk(PlayerMotor player)
        {
            if (Required == Stance.Neutral) return true;
            var held = poseOf != null ? poseOf(player) : null;
            return held == Required;
        }

        public string Prompt => PromptFor(null);
        public string PromptFor(PlayerMotor player)
        {
            if (!lit || Taken) return "";
            if (player != null && !PoseOk(player))
                return "A gem — hold the " + PoseLibrary.Name(Required) + " pose to reach it (right mouse, scroll to pick)";
            return "E — take the gem";
        }

        public bool TryInteract(PlayerMotor player)
        {
            if (!lit || Taken || player == null || !PoseOk(player) || !InteractionValidation.CanReach(player, transform)) return false;
            Taken = true;
            Refresh();
            collected?.Invoke(this, player);
            return true;
        }
    }
}
