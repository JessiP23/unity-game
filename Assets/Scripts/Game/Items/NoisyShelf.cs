using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A toy shelf you can brush past. Walk and nothing happens; run along it and the boxes rattle,
    /// which is a noise the guard hears. The shelf wobbles its stock for a moment so you know it was you.
    /// </summary>
    public sealed class NoisyShelf : MonoBehaviour
    {
        public const float Loudness = 1.1f, Cooldown = 1.6f;
        private WorldSignals signals;
        private Transform[] stock = new Transform[0];
        private Vector3[] rest = new Vector3[0];
        private float wobbleUntil, nextNoise;

        public void Configure(WorldSignals world, Transform[] shaken)
        {
            signals = world; stock = shaken;
            rest = new Vector3[stock.Length];
            for (int i = 0; i < stock.Length; i++) rest[i] = stock[i].localPosition;
        }

        private void OnTriggerStay(Collider other)
        {
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null || signals == null) return;
            if (motor.ActualSpeed < motor.Rules.walkSpeed * 1.15f) return;
            if (Time.time < nextNoise) return;
            nextNoise = Time.time + Cooldown;
            wobbleUntil = Time.time + 0.9f;
            signals.Noise.Publish(new NoiseEvent(transform.position, Loudness, "toy shelf"));
        }

        private void Update()
        {
            if (Time.time > wobbleUntil) { for (int i = 0; i < stock.Length; i++) stock[i].localPosition = rest[i]; return; }
            float left = wobbleUntil - Time.time;
            for (int i = 0; i < stock.Length; i++)
                stock[i].localPosition = rest[i] + new Vector3(Mathf.Sin(Time.time * 28f + i) * 0.03f * left, 0f, 0f);
        }
    }
}
