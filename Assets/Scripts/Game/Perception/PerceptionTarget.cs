using System.Collections.Generic;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Something observers can notice (a mannequin player). Observers read only what an onlooker could
    /// see: where the body is and how fast it moved. They do not learn that it is a player.
    /// </summary>
    public sealed class PerceptionTarget : MonoBehaviour
    {
        private static readonly List<PerceptionTarget> active = new List<PerceptionTarget>();
        public static IReadOnlyList<PerceptionTarget> Active => active;
        [Tooltip("Height of the point observers aim their line-of-sight ray at.")]
        public float aimHeight = 1f;
        public string Id => motor != null && motor.Record != null ? motor.Record.Id : name;
        public PlayerMotor Motor => motor;
        /// <summary>Only free mannequins can be noticed; captured ones are in the warehouse.</summary>
        public bool Noticeable => motor == null || motor.Record == null || motor.Record.Free;
        public float Speed => motor != null ? motor.ActualSpeed : 0f;
        /// <summary>Recent peak speed, so a quick step between two perception samples still counts as movement.</summary>
        public float RecentSpeed => Time.time - lastMoving <= MemorySeconds ? Mathf.Max(lastMovingSpeed, Speed) : Speed;
        private const float MemorySeconds = 0.3f;
        private float lastMoving = float.NegativeInfinity, lastMovingSpeed;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        /// <summary>Share of a watcher's suspicion the surrounding crowd absorbs (0–0.75), set by <see cref="CrowdCover"/>.</summary>
        public float Concealment { get; set; }
        /// <summary>Crouched still in a hiding spot (see <see cref="HideSpot"/>): shoppers and staff cannot see you at all.</summary>
        public bool Hidden { get; set; }
        private PlayerMotor motor;
        private void Awake() => motor = GetComponent<PlayerMotor>();
        private void FixedUpdate()
        {
            if (Speed <= 0.05f) return;
            lastMoving = Time.time; lastMovingSpeed = Speed;
        }
        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
    }
}
