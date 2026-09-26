using System;
using NightSupermarket.Core;
using Unity.Profiling;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Composition root for one NPC. On the state authority it ticks perception and behaviour; on a remote
    /// client it only applies replicated <see cref="NpcSnapshot"/>s. No gameplay decisions live here.
    /// </summary>
    public sealed class NpcController : MonoBehaviour
    {
        public int Id { get; private set; }
        public NpcRole Role { get; private set; }
        public bool Authority { get; private set; }
        public NpcMovement Movement { get; private set; }
        public NpcNavigation Navigation { get; private set; }
        public NpcPerception Perception { get; private set; }
        public NpcBehavior Behavior { get; private set; }
        public NpcAnimationHooks Animation { get; private set; }
        /// <summary>Raised when the NPC has left the store and can go back to the pool.</summary>
        public event Action<NpcController> Departed;
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("NPC.Update");
        private byte replicatedBehavior;
        private AwarenessState replicatedAwareness;

        public void Configure(int id, NpcRole role, bool authority)
        {
            Id = id; Role = role; Authority = authority;
            Movement = GetComponent<NpcMovement>();
            Navigation = GetComponent<NpcNavigation>();
            Perception = GetComponent<NpcPerception>();
            Behavior = GetComponent<NpcBehavior>();
            Animation = GetComponent<NpcAnimationHooks>();
            if (Movement != null) Movement.SetSimulated(authority);
        }

        private void Update()
        {
            if (!Authority) return;
            using (UpdateMarker.Auto())
            {
                float delta = Time.deltaTime;
                if (Perception != null) Perception.Tick(delta);
                if (Behavior != null) Behavior.Tick(delta);
            }
        }

        public void Depart() => Departed?.Invoke(this);

        public byte BehaviorCode => Authority ? (Behavior != null ? Behavior.StateCode : (byte)0) : replicatedBehavior;
        public AwarenessState Awareness => Authority ? (Perception != null ? Perception.Strongest : AwarenessState.Unaware) : replicatedAwareness;
        public string StateLabel => Behavior != null ? Behavior.StateLabel(BehaviorCode) : BehaviorCode.ToString();

        /// <summary>What the authority sends each network tick.</summary>
        public NpcSnapshot Capture()
        {
            Vector3 p = transform.position;
            float speed = Movement != null ? Movement.Speed : 0f;
            return new NpcSnapshot(Id, Role, new MapPoint(p.x, p.y, p.z), transform.eulerAngles.y, speed, BehaviorCode, Awareness);
        }

        /// <summary>Remote clients: adopt the authority's state. Never runs AI.</summary>
        public void Apply(NpcSnapshot snapshot)
        {
            if (Authority) return;
            transform.SetPositionAndRotation(new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z), Quaternion.Euler(0, snapshot.Yaw, 0));
            replicatedBehavior = snapshot.Behavior;
            replicatedAwareness = snapshot.Awareness;
        }
    }
}
