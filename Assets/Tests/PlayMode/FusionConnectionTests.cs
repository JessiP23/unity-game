using System.Collections;
using System.IO;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Hits Photon Cloud with the local App Id. Run with python3 Tools/unity.py connect.</summary>
    public sealed class FusionConnectionTests
    {
        [UnityTest] public IEnumerator HostSessionConnectsToPhotonCloud()
        {
            var host = new GameObject("Fusion probe").AddComponent<FusionSession>();
            var task = host.ConnectHost("NightSupermarket-connect-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            string report = "status=" + host.Status + " connected=" + host.Connected +
                            " cloud=" + host.CloudReady + " authority=" + host.IsAuthority + " mode=" + host.Mode + "\n";
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/fusion-connect.txt", report);
            Debug.Log("[FUSION]\n" + report);
            Assert.That(task.IsCompleted, Is.True, "Photon did not answer within 30 seconds");
            Assert.That(task.IsFaulted, Is.False, task.Exception != null ? task.Exception.GetBaseException().Message : "");
            Assert.That(task.Result, Is.True, host.Status);
            Assert.That(host.LastOk, Is.True, host.Status);
            Assert.That(host.Connected, Is.True, host.Status);
            Assert.That(host.IsAuthority, Is.True, "the host must stay the match authority");
            host.Shutdown();
            yield return null;
            Object.Destroy(host.gameObject);
        }
    }
}
