using System.Collections;
using NightSupermarket.Game;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    public sealed class GuardMovementTests
    {
        [UnityTest] public IEnumerator GuardWalksAroundACrateInItsPath()
        {
            var world = new GameObject("Guard path test");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var guardObject = new GameObject("Guard");
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                floor.transform.SetParent(world.transform);
                floor.transform.position = new Vector3(200, -0.5f, 200); floor.transform.localScale = new Vector3(20, 1, 20);
                var surface = world.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
                crate.transform.position = new Vector3(200, 0.3f, 200); crate.transform.localScale = Vector3.one * 0.6f;
                crate.AddComponent<Rigidbody>().isKinematic = true;
                crate.AddComponent<PhysicalItem>().Configure(definition, new WorldSignals());
                guardObject.transform.position = new Vector3(200, 0, 196);
                var guard = guardObject.AddComponent<GuardController>();
                guard.Configure(rules, new WorldSignals(), new[] { new Vector3(200, 0, 204) });
                var agent = guardObject.GetComponent<NavMeshAgent>();
                for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
                agent.SetDestination(new Vector3(200, 0, 204));
                float closest = float.PositiveInfinity;
                float deadline = Time.time + 10f;
                while (Time.time < deadline && guardObject.transform.position.z < 203.5f)
                {
                    yield return null;
                    Vector3 offset = guardObject.transform.position - crate.transform.position; offset.y = 0;
                    closest = Mathf.Min(closest, offset.magnitude);
                }
                Assert.That(guardObject.transform.position.z, Is.GreaterThan(202f), "the guard reaches the far side");
                Assert.That(closest, Is.GreaterThan(0.55f), "and its body never overlaps the crate on the way");
            }
            finally
            {
                Object.Destroy(guardObject); Object.Destroy(crate); Object.Destroy(world);
                Object.Destroy(rules); Object.Destroy(definition);
            }
        }
    }
}
