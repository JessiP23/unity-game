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
                Assert.That(physical.TryInteract(motor), Is.True);
                Assert.That(physical.TryInteract(motor), Is.False);
                Assert.That(physical.Holder, Is.EqualTo(carry)); Assert.That(carry.Release(false), Is.True);
                Assert.That(physical.Holder, Is.Null);
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
    }
}
