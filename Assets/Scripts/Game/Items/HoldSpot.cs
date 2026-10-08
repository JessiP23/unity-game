using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A job that is done by standing still at a place for a few seconds: posing in the window,
    /// swapping a price tag, taking a display mannequin's spot, turning a camera. Press E (or just
    /// stand inside, for a zone) to start; moving, stepping away or being captured cancels; the
    /// window keeps half its progress because being seen there is the point.
    /// </summary>
    public sealed class HoldSpot : MonoBehaviour, IInteractable, IPlayerPrompt
    {
        public string Label = "Hold here";
        public float Seconds = 3f;
        /// <summary>Progress kept on a cancel, 0..1. Zero restarts from scratch.</summary>
        public float KeepOnCancel;
        /// <summary>Standing inside the trigger starts the hold without pressing E.</summary>
        public bool AutoStart;
        /// <summary>Only start when nobody is looking at the player.</summary>
        public Func<PlayerMotor, bool> CanStart;
        public string StartHint = "E — start";
        public string BlockedHint = "Wait until nobody is looking";
        public string DoneHint = "Done";
        public string MovedHint = "You moved — hold still to continue";
        public Action<PlayerMotor> Completed;
        public bool Complete { get; private set; }
        public float Progress { get; private set; }
        public PlayerMotor Worker { get; private set; }
        private LocalMatchAuthority authority;
        private WorldSignals signals;
        private ActionKind kind;
        private string actionTagId, destination;
        private float movedUntil;
        private bool wasMoving;

        public void Configure(LocalMatchAuthority match, WorldSignals world, ActionKind actionKind, string actionTag, string actionDestination = "")
        {
            authority = match; signals = world; kind = actionKind; actionTagId = actionTag; destination = actionDestination;
        }

        public string Prompt => Complete ? DoneHint : Worker != null ? Label + " · hold still " + Mathf.CeilToInt(Seconds - Progress) + " s" : StartHint;

        public string PromptFor(PlayerMotor player)
        {
            if (Complete) return DoneHint;
            if (Worker == player && Time.time < movedUntil) return MovedHint;
            if (Worker == player) return Label + " · hold still " + Mathf.CeilToInt(Seconds - Progress) + " s";
            if (CanStart != null && !CanStart(player)) return BlockedHint;
            return StartHint;
        }

        public bool TryInteract(PlayerMotor player)
        {
            if (AutoStart) return false;
            return Begin(player, true);
        }

        private bool Begin(PlayerMotor player, bool checkReach)
        {
            if (authority == null || authority.Phase != MatchPhase.Night || Complete || Worker != null) return false;
            if (!player.Record.Free) return false;
            if (checkReach && !InteractionValidation.CanReach(player, transform)) return false;
            if (CanStart != null && !CanStart(player)) return false;
            Worker = player;
            return true;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!AutoStart || Complete || Worker != null) return;
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor != null) Begin(motor, false);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (Worker == null || Complete) return;
            if (authority.Phase != MatchPhase.Night || !Worker.Record.Free) { Cancel(); return; }
            bool inside = AutoStart ? Inside(Worker.transform.position) : InteractionValidation.CanReach(Worker, transform);
            if (!inside) { Cancel(); return; }
            if (authority.Clock.Paused) return;
            if (Worker.ActualSpeed > Worker.Rules.movementThreshold)
            {
                // One penalty per burst of movement, not one per frame.
                if (!wasMoving) { Progress *= KeepOnCancel; movedUntil = Time.time + 1.2f; }
                wasMoving = true;
                if (KeepOnCancel <= 0f) Worker = null;
                return;
            }
            wasMoving = false;
            Progress += delta;
            if (Progress < Seconds) return;
            Complete = true;
            var done = Worker; Worker = null;
            signals.Actions.Publish(new ObjectAction(kind, name + GetInstanceID(), actionTagId, done.Record.Id, destination));
            authority.Audio.Publish(AudioCue.MissionComplete);
            Completed?.Invoke(done);
        }

        private void Cancel()
        {
            Progress *= KeepOnCancel;
            Worker = null;
            wasMoving = false;
        }

        private bool Inside(Vector3 position)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return Vector3.Distance(position, transform.position) < 1.2f;
            Vector3 local = transform.InverseTransformPoint(position) - box.center;
            Vector3 half = box.size * 0.5f + Vector3.one * 0.15f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }
    }
}
