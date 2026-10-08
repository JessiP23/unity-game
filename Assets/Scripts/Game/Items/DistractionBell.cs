using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Arms a delayed noise. Shared cooldown prevents a team from spamming it.</summary>
    public sealed class DistractionBell : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private float pending, cooldown;
        private AudioSource audioSource;
        private AudioClip bellClip;
        public bool Armed => pending > 0;
        public string Prompt => Armed ? "Bell rings in " + pending.ToString("0.0") + "s — move away" :
            cooldown > 0 ? "Bell resetting · " + Mathf.CeilToInt(cooldown) + "s" : "E — bell in 3s; draw guard away for a rescue";
        public void Configure(LocalMatchAuthority match, WorldSignals world)
        {
            authority = match; signals = world;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1; audioSource.maxDistance = 25; audioSource.playOnAwake = false;
            var samples = new float[22050];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(2 * Mathf.PI * 880 * i / 22050f) * Mathf.Exp(-5f * i / 22050f) * 0.5f;
            bellClip = AudioClip.Create("Service bell", samples.Length, 1, 22050, false); bellClip.SetData(samples, 0);
        }
        private void OnDestroy() { if (bellClip != null) Destroy(bellClip); }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || authority.Phase != MatchPhase.Night || cooldown > 0 || !InteractionValidation.CanReach(player, transform)) return false;
            pending = 3; cooldown = 18; return true;
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            GameRules.RequireDelta(delta);
            if (authority == null || authority.Phase != MatchPhase.Night || authority.Clock.Paused) return;
            cooldown = Mathf.Max(0, cooldown - delta);
            if (pending <= 0) return;
            pending = Mathf.Max(0, pending - delta);
            if (pending == 0)
            {
                signals.Noise.Publish(new NoiseEvent(transform.position, 2, "distraction bell"));
                if (audioSource != null) audioSource.PlayOneShot(bellClip);
            }
        }
    }
}
