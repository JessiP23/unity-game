using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Optional exposed four-second display swap. Leaving or moving cancels progress.</summary>
    public sealed class BonusDisplay : MonoBehaviour, IInteractable
    {
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private PlayerMotor worker;
        private float progress;
        public string Label { get; private set; }
        public bool Complete { get; private set; }
        public string Prompt => Complete ? "Display swapped · bonus banked" : worker != null ?
            "Swapping · stay still " + Mathf.Max(0, 4 - progress).ToString("0.0") + "s" : "E — optional display swap (4s; makes noise)";
        public void Configure(LocalMatchAuthority match, WorldSignals world, string label)
        { authority = match; signals = world; Label = label; }
        public bool TryInteract(PlayerMotor player)
        {
            if (authority == null || authority.Phase != MatchPhase.Night || Complete || worker != null || !InteractionValidation.CanReach(player, transform)) return false;
            worker = player; progress = 0;
            signals.Noise.Publish(new NoiseEvent(transform.position, 0.8f, "display swap"));
            return true;
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            GameRules.RequireDelta(delta);
            if (worker == null) return;
            if (authority.Phase != MatchPhase.Night || !InteractionValidation.CanReach(worker, transform) || worker.ActualSpeed > worker.Rules.movementThreshold)
            { worker = null; progress = 0; return; }
            if (authority.Clock.Paused) return;
            progress += delta;
            if (progress < 4) return;
            Complete = true; worker = null;
            authority.Stats.RecordBonus();
            var surface = GetComponent<Renderer>();
            if (surface != null) surface.material.color = new Color(0.25f, 0.85f, 0.6f);
            authority.Audio.Publish(AudioCue.MissionComplete);
        }
    }
}
