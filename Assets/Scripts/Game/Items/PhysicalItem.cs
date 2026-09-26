using NightSupermarket.Core;
using System;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicalItem : MonoBehaviour, IInteractable
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public ItemDefinition Definition { get; private set; }
        /// <summary>While held the item travels with its carrier, so it stops carving the NavMesh.</summary>
        public CarrySystem Holder
        {
            get => holder;
            internal set { holder = value; if (obstacle != null) obstacle.enabled = value == null; }
        }
        public bool Broken { get; private set; }
        public Rigidbody Body { get; private set; }
        public string LastActor { get; private set; } = "";
        public string Prompt => Broken ? "Broken" : "E — pick up " + Definition.displayName;
        private WorldSignals signals;
        private CarrySystem holder;
        private NavMeshObstacle obstacle;
        public void Configure(ItemDefinition definition, WorldSignals world)
        {
            Definition = definition; signals = world; Body = GetComponent<Rigidbody>(); Body.mass = definition.mass;
            if (!definition.inventoryOnly) Carve();
        }
        /// <summary>Resting world items cut a hole in the NavMesh so the guard walks around them.</summary>
        private void Carve()
        {
            if (!TryGetComponent(out obstacle)) obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            if (TryGetComponent(out BoxCollider box)) { obstacle.center = box.center; obstacle.size = box.size; }
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingMoveThreshold = 0.1f;
        }
        public bool TryInteract(PlayerMotor player)
        {
            if (!gameObject.activeInHierarchy || Broken || Holder != null || !Definition.canPickup || !InteractionValidation.CanReach(player, transform)) return false;
            if (Definition.inventoryOnly)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                if (inventory == null || !inventory.Items.TryAdd(Definition.id, 1, Definition.slotCost)) return false;
                LastActor = player.Record.Id;
                signals.Actions.Publish(new ObjectAction(ActionKind.Collect, Id, Definition.missionTag, LastActor));
                gameObject.SetActive(false); return true;
            }
            var carry = player.GetComponent<CarrySystem>();
            if (carry == null || !carry.TryHold(this)) return false;
            LastActor = player.Record.Id;
            signals.Actions.Publish(new ObjectAction(ActionKind.Collect, Id, Definition.missionTag, LastActor));
            return true;
        }
        public bool Push(Vector3 impulse, PlayerMotor player)
        {
            if (Broken || Holder != null || !Definition.canPush || !InteractionValidation.CanReach(player, transform)) return false;
            LastActor = player.Record.Id; Body.AddForce(Vector3.ClampMagnitude(impulse, player.Rules.throwForce), ForceMode.Impulse); return true;
        }
        public bool TryBreak(float impactSpeed)
        {
            if (Broken || !Definition.canBreak || impactSpeed < Definition.breakSpeed || Holder != null) return false;
            Broken = true; signals.Actions.Publish(new ObjectAction(ActionKind.Break, Id, Definition.missionTag, LastActor));
            signals.Noise.Publish(new NoiseEvent(transform.position, 2, Id));
            gameObject.SetActive(false); return true;
        }
        public void ReportPlacement(string destination)
        {
            if (Broken || Holder != null || !Definition.canPlace || string.IsNullOrEmpty(LastActor)) return;
            signals.Actions.Publish(new ObjectAction(ActionKind.Move, Id, Definition.missionTag, LastActor, destination));
            signals.Actions.Publish(new ObjectAction(ActionKind.Place, Id, Definition.missionTag, LastActor, destination));
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (signals == null || Holder != null) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.5f) signals.Noise.Publish(new NoiseEvent(transform.position, Mathf.Clamp(speed / 5, 0.1f, 2), Id));
            TryBreak(speed);
        }
    }
}
