using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using NightSupermarket.Editor;

namespace NightSupermarket.Tests
{
    public sealed class BootstrapTests
    {
        [Test]
        public void BootstrapContainsCameraAndLight()
        {
            var scene = BootstrapSetup.CreateScene();
            try
            {
                Assert.That(scene.IsValid(), Is.True);
                Assert.That(scene.GetRootGameObjects().Length, Is.EqualTo(2));
                var roots = scene.GetRootGameObjects();
                Assert.That(System.Array.Exists(roots, x => x.GetComponent<Camera>() != null), Is.True);
                Assert.That(System.Array.Exists(roots, x => x.GetComponent<Light>() != null), Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
