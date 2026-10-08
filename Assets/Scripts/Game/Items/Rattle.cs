using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>A carried fragile thing clinks every few seconds: a small noise the guard can hear.</summary>
    [RequireComponent(typeof(PhysicalItem))]
    public sealed class Rattle : MonoBehaviour
    {
        public float Every = 2.5f;
        public float Loudness = 0.5f;
        private WorldSignals signals;
        private PhysicalItem item;
        private float next;
        public void Configure(WorldSignals world) { signals = world; item = GetComponent<PhysicalItem>(); }
        private void Update()
        {
            if (signals == null || item == null || item.Holder == null || item.Broken) { next = Time.time + Every * 0.5f; return; }
            if (Time.time < next) return;
            next = Time.time + Every;
            signals.Noise.Publish(new NoiseEvent(transform.position, Loudness, "rattle"));
        }
    }
}
