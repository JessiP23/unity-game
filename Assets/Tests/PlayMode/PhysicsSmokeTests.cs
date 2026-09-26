using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightSupermarket.Tests
{
    public sealed class PhysicsSmokeTests
    {
        [UnityTest]
        public IEnumerator PrimitiveColliderBlocksRay()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.transform.position = new Vector3(1000, 1000, 1000);
                Physics.SyncTransforms();
                yield return null;
                var ray = new Ray(cube.transform.position - Vector3.forward * 2, Vector3.forward);
                Assert.That(cube.GetComponent<Collider>().Raycast(ray, out var hit, 3), Is.True);
                Assert.That(hit.distance, Is.EqualTo(1.5f).Within(0.01f));
            }
            finally { Object.Destroy(cube); }
        }
    }
}
