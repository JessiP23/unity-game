using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.TestTools;
using NightSupermarket.Game;
namespace NightSupermarket.Tests
{
    public sealed class GuardNavigationTests
    {
        [UnityTest] public IEnumerator GuardUsesBakedNavMeshAndHearsNoise()
        {
            var world = new GameObject("Navigation test"); var guardObject = new GameObject("Guard");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(world.transform);
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                floor.transform.position = new Vector3(100, -0.5f, 100); floor.transform.localScale = new Vector3(20, 1, 20);
                var surface = world.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
                guardObject.transform.position = new Vector3(100, 0, 100);
                var guard = guardObject.AddComponent<GuardController>(); var signals = new WorldSignals();
                guard.Configure(rules, signals, new[] { new Vector3(105, 0, 100) });
                var agent = guardObject.GetComponent<NavMeshAgent>(); Assert.That(agent.isOnNavMesh, Is.True);
                agent.SetDestination(new Vector3(105, 0, 100)); var start = guardObject.transform.position;
                for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(start, guardObject.transform.position), Is.GreaterThan(0.1f));
                signals.Noise.Publish(new NoiseEvent(guardObject.transform.position, 1, "test"));
                Assert.That(guard.Brain.State, Is.EqualTo(NightSupermarket.Core.GuardState.Investigate));
                guard.PlayerDriven = true; guardObject.transform.rotation = Quaternion.LookRotation(Vector3.right);
                Vector3 driven = guardObject.transform.position;
                for (int i = 0; i < 20; i++) guard.Drive(Vector2.up, 3f, 0.05f);
                Assert.That(Vector3.Distance(driven, guardObject.transform.position), Is.GreaterThan(0.2f));
                var eyes = new GuardVisionSystem(12, 90, ~0);
                Assert.That(eyes.CanSee(guardObject.transform.position + Vector3.up * 0.6f, guardObject.transform.forward,
                    guardObject.transform.position + guardObject.transform.forward * 3).Visible, Is.True);
            }
            finally { Object.Destroy(guardObject); Object.Destroy(world); Object.Destroy(rules); }
        }
    }
}
