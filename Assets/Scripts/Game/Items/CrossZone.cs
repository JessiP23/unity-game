using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>A trigger that counts once when a player walks through it while <see cref="Gate"/> allows it.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CrossZone : MonoBehaviour
    {
        public Func<bool> Gate = () => true;
        public string BlockedToast = "";
        public bool Complete { get; private set; }
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private ActionKind kind;
        private string actionTagId, destination;
        private float lastBlocked;
        public Action<string> Say;

        public void Configure(LocalMatchAuthority match, WorldSignals world, ActionKind actionKind, string actionTag, string actionDestination = "")
        {
            authority = match; signals = world; kind = actionKind; actionTagId = actionTag; destination = actionDestination;
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Complete || authority == null || authority.Phase != MatchPhase.Night) return;
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null || !motor.Record.Free) return;
            if (!Gate())
            {
                if (Time.time - lastBlocked > 4f && !string.IsNullOrEmpty(BlockedToast)) { Say?.Invoke(BlockedToast); lastBlocked = Time.time; }
                return;
            }
            Complete = true;
            signals.Actions.Publish(new ObjectAction(kind, name + GetInstanceID(), actionTagId, motor.Record.Id, destination));
            authority.Audio.Publish(AudioCue.MissionComplete);
        }
    }
}
