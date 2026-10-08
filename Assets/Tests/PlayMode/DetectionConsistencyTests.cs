using System.Collections;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    public sealed class DetectionConsistencyTests
    {
        [TestCase(0.3f)] [TestCase(1f)] [TestCase(3f)] [TestCase(8f)]
        public void GuardNoticesTargetAtEveryFrontDistance(float distance)
        {
            var sensor = new GuardVisionSystem(12, 90, ~0);
            Vector3 eye = new Vector3(100, 101.6f, 100);
            Assert.That(sensor.CanSee(eye, Vector3.forward, new Vector3(100, 101, 100 + distance)).Visible, Is.True);
        }
        [UnityTest] public IEnumerator PartialCoverFullCoverAndOpeningAgreeForBothObserverTypes()
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Vector3 eye = new Vector3(100, 101.6f, 100), chest = new Vector3(100, 101, 104);
                obstacle.transform.position = new Vector3(100, 100.65f, 102);
                obstacle.transform.localScale = new Vector3(2, 1.3f, 0.3f);
                Physics.SyncTransforms(); yield return null;
                foreach (var sensor in new VisionSensor[] {new GuardVisionSystem(12, 90, ~0), new VisionSensor(12, 90, ~0)})
                {
                    Assert.That(sensor.CanSee(eye, Vector3.forward, chest).Visible, Is.True, "head visible above low cover");
                    obstacle.transform.localScale = new Vector3(2, 4, 0.3f); Physics.SyncTransforms();
                    Assert.That(sensor.CanSee(eye, Vector3.forward, chest).Visible, Is.False, "full wall blocks both samples");
                    obstacle.SetActive(false); Physics.SyncTransforms();
                    Assert.That(sensor.CanSee(eye, Vector3.forward, chest).Visible, Is.True, "opening restores sight");
                    obstacle.SetActive(true); obstacle.transform.localScale = new Vector3(2, 1.3f, 0.3f); Physics.SyncTransforms();
                }
            }
            finally { Object.Destroy(obstacle); }
        }
        [UnityTest] public IEnumerator GuardUsesRealSecondsAndImmediatelyLosesSightBehindCover()
        {
            var actor = new GameObject("target"); var guard = new GameObject("guard");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                guard.transform.position = new Vector3(100, 101, 100);
                actor.transform.position = new Vector3(100, 100, 102);
                var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, new PlayerRecord("test"));
                actor.layer = 2; wall.SetActive(false); Physics.SyncTransforms();
                var coordinator = new DetectionCoordinator(motor, guard.transform, rules);
                for (int i = 0; i < 10; i++) { motor.Simulate(new PlayerCommand(Vector2.right, false, false), 0.02f); coordinator.Tick(0.02f); }
                Assert.That(coordinator.Observed, Is.True);
                Assert.That(coordinator.Detection.GraceRemaining, Is.EqualTo(rules.orangeDuration - 0.2f).Within(0.001));
                wall.transform.position = new Vector3(100, 101, 101); wall.transform.localScale = new Vector3(5, 5, 0.3f);
                wall.SetActive(true); Physics.SyncTransforms(); coordinator.Tick(0.02f);
                Assert.That(coordinator.Observed, Is.False);
                Assert.That(coordinator.Detection.State, Is.EqualTo(DetectionState.Green));
                yield return null;
            }
            finally { Object.Destroy(actor); Object.Destroy(guard); Object.Destroy(wall); Object.Destroy(rules); }
        }
    }
}
