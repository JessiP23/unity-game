using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NightSupermarket.Core;
using NightSupermarket.Game;
namespace NightSupermarket.Tests
{
    public sealed class ItemTests
    {
        [UnityTest] public IEnumerator PickupOwnershipDropThrowAndBreak()
        {
            var actor = new GameObject("Item tester"); var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>(); var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                actor.transform.position = new Vector3(100, 100, 100);
                var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, new PlayerRecord("owner"));
                var carry = actor.AddComponent<CarrySystem>(); carry.Configure(motor);
                item.canBreak = true; item.breakSpeed = 2;
                var physical = cube.AddComponent<PhysicalItem>(); physical.Configure(item, new WorldSignals());
                cube.transform.position = actor.transform.position + Vector3.forward * 10; Physics.SyncTransforms();
                Assert.That(physical.TryInteract(motor), Is.False);
                cube.transform.position = actor.transform.position + Vector3.forward; Physics.SyncTransforms();
                var carve = cube.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                Assert.That(carve != null && carve.enabled && carve.carving, Is.True, "resting items carve the NavMesh");
                Assert.That(physical.TryInteract(motor), Is.True);
                Assert.That(physical.TryInteract(motor), Is.False);
                Assert.That(carve.enabled, Is.False, "held items stop carving");
                Assert.That(physical.Holder, Is.EqualTo(carry)); Assert.That(carry.Release(false), Is.True);
                Assert.That(physical.Holder, Is.Null);
                Assert.That(carve.enabled, Is.True);
                cube.transform.position = actor.transform.position + Vector3.forward; Physics.SyncTransforms();
                Assert.That(physical.TryInteract(motor), Is.True);
                Assert.That(carry.Release(true), Is.True);
                yield return new WaitForFixedUpdate();
                Assert.That(cube.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.GreaterThan(1));
                Assert.That(physical.TryBreak(1), Is.False); Assert.That(physical.TryBreak(3), Is.True);
                Assert.That(physical.Broken, Is.True); Assert.That(physical.TryInteract(motor), Is.False);
            }
            finally { Object.Destroy(actor); Object.Destroy(cube); Object.Destroy(rules); Object.Destroy(item); }
        }
        [UnityTest] public IEnumerator PushMovesAReachableCrateAndRejectsInvalidPushes()
        {
            var actor = new GameObject("Push tester");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                actor.transform.position = new Vector3(40, 2, 40);
                var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, new PlayerRecord("pusher"));
                item.canPush = true; item.canPickup = false;
                var physical = cube.AddComponent<PhysicalItem>(); physical.Configure(item, new WorldSignals());
                cube.transform.position = actor.transform.position + Vector3.forward * 8; Physics.SyncTransforms();
                Assert.That(physical.Push(Vector3.forward, motor), Is.False);
                cube.transform.position = actor.transform.position + Vector3.forward; Physics.SyncTransforms();
                var body = cube.GetComponent<Rigidbody>(); body.useGravity = false;
                Assert.That(physical.Push(Vector3.right * 5, motor), Is.True);
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.x, Is.GreaterThan(0.2f));
                body.linearVelocity = Vector3.zero;
                item.canPush = false;
                Assert.That(physical.Push(Vector3.right * 5, motor), Is.False);
                motor.Record.SetState(PlayerState.Captured);
                item.canPush = true;
                Assert.That(physical.Push(Vector3.right * 5, motor), Is.False);
            }
            finally { Object.Destroy(actor); Object.Destroy(cube); Object.Destroy(rules); Object.Destroy(item); }
        }
    }
}
