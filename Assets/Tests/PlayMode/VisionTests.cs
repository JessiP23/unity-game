using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NightSupermarket.Game;
namespace NightSupermarket.Tests
{
    public sealed class VisionTests
    {
        [UnityTest] public IEnumerator VisionFiltersDistanceAngleAndWalls()
        {
            Vector3 eye = new Vector3(100, 100, 100);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = eye + Vector3.forward * 2; wall.SetActive(false);
                var vision = new GuardVisionSystem(10, 90, ~0);
                Assert.That(vision.CanSee(eye, Vector3.forward, eye + Vector3.forward * 5).Visible, Is.True);
                Assert.That(vision.CanSee(eye, Vector3.forward, eye - Vector3.forward * 5).Visible, Is.False);
                Assert.That(vision.CanSee(eye, Vector3.forward, eye + Vector3.forward * 11).Visible, Is.False);
                wall.SetActive(true); Physics.SyncTransforms(); yield return null;
                Assert.That(vision.CanSee(eye, Vector3.forward, eye + Vector3.forward * 5).Visible, Is.False);
                vision.ResetLineTests();
                Assert.That(vision.CanSee(eye, Vector3.forward, eye + Vector3.forward * 30).Visible, Is.False);
                Assert.That(vision.CanSee(eye, Vector3.forward, eye - Vector3.forward * 3).Visible, Is.False);
                Assert.That(vision.LineTests, Is.Zero);
                Assert.That(vision.CanSee(eye, Vector3.forward, eye + Vector3.forward * 4).LineOfSight, Is.False);
                Assert.That(vision.LineTests, Is.EqualTo(1));
            }
            finally { Object.Destroy(wall); }
        }
    }
}
