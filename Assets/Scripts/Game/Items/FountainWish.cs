using System;
using System.Collections;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>The fountain rim: E tosses a coin (once a night). The splash is loud; the wish is worth it.</summary>
    public sealed class FountainCoin : MonoBehaviour, IInteractable, IPlayerPrompt, ILowPriorityInteractable
    {
        public Func<PlayerMotor, bool> Toss = _ => false;
        public Func<bool> Used = () => false;
        public string Prompt => Used() ? "You made tonight's wish." : "E — toss a coin and make a wish (the splash is loud)";
        public string PromptFor(PlayerMotor player) => Prompt;
        public bool TryInteract(PlayerMotor player)
        {
            if (player == null || !player.Record.Free || Used() || !InteractionValidation.CanReach(player, transform)) return false;
            return Toss(player);
        }
    }

    /// <summary>
    /// The pool itself: a mannequin that moves in the water splashes, and splashing is noise the guard
    /// hears. Standing still in a fountain is allowed (and very funny).
    /// </summary>
    public sealed class FountainWater : MonoBehaviour
    {
        public const float Loudness = 1.0f, Interval = 0.8f;
        private WorldSignals signals;
        private Action<Vector3> splashed;
        private float next;
        public void Configure(WorldSignals world, Action<Vector3> onSplash) { signals = world; splashed = onSplash; }
        private void OnTriggerStay(Collider other)
        {
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null || signals == null || !motor.Record.Free) return;
            if (motor.ActualSpeed <= motor.Rules.movementThreshold || Time.time < next) return;
            next = Time.time + Interval;
            var at = motor.transform.position;
            signals.Noise.Publish(new NoiseEvent(at, Loudness, "splashing in the fountain"));
            splashed?.Invoke(new Vector3(at.x, FountainDressing.WaterY, at.z));
        }
    }

    /// <summary>One-shot splash bursts and a thrown coin's arc. Pure looks.</summary>
    public sealed class FountainEffects : MonoBehaviour
    {
        private ParticleSystem splash;

        public static FountainEffects Create(Transform parent)
        {
            var host = new GameObject("Fountain effects");
            host.transform.SetParent(parent, false);
            var effects = host.AddComponent<FountainEffects>();
            var material = ArtLibrary.Particle(new Color(0.9f, 0.96f, 1f, 0.7f));
            if (material == null) return effects;
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.gravityModifier = 1.2f; main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.rateOverTime = 0;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.12f;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effects.splash = system;
            return effects;
        }

        public void Splash(Vector3 at, int count = 24)
        {
            if (splash == null) return;
            var settings = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            transform.position = at;
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            splash.Emit(settings, count);
        }

        /// <summary>Throws a small gold coin along an arc from <paramref name="from"/> into the water, then calls back.</summary>
        public void ThrowCoin(Vector3 from, Vector3 to, Action landed) => StartCoroutine(Arc(from, to, landed));

        private IEnumerator Arc(Vector3 from, Vector3 to, Action landed)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Wish coin";
            Destroy(coin.GetComponent<Collider>());
            coin.transform.localScale = new Vector3(0.06f, 0.005f, 0.06f);
            coin.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.82f, 0.3f), 0.6f);
            const float seconds = 0.7f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                float k = Mathf.Clamp01(t);
                coin.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
                coin.transform.rotation = Quaternion.Euler(k * 900f, k * 300f, 0f);
                yield return null;
            }
            Destroy(coin);
            Splash(to, 30);
            landed?.Invoke();
        }
    }
}
