using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NightSupermarket.Core;
using NightSupermarket.Game;
namespace NightSupermarket.Tests
{
    public sealed class CaptureTests
    {
        [UnityTest] public IEnumerator CaptureBlocksMovementUntilRescue()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            var session = new LocalSession();
            var authority = new LocalMatchAuthority(rules.CreateRules(), session);
            GameObject rescuerObject = null, captiveObject = null;
            try
            {
                floor.transform.position = new Vector3(100, -0.5f, 100);
                floor.transform.localScale = new Vector3(50, 1, 50);
                authority.TryBeginNight();
                string rescuerId = session.Join();
                string captiveId = session.Join();
                var rescuer = Create(rescuerId, rules, new Vector3(100, 0.1f, 100));
                var captive = Create(captiveId, rules, new Vector3(104, 0.1f, 100));
                rescuerObject = rescuer.gameObject; captiveObject = captive.gameObject;
                authority.Register(rescuer.Record); authority.Register(captive.Record);
                Physics.SyncTransforms(); yield return null;
                Assert.That(authority.TryCapture(captiveId), Is.True);
                Vector3 held = captive.transform.position;
                captive.Simulate(new PlayerCommand(Vector2.up, true, true), 0.2f);
                Assert.That(captive.transform.position, Is.EqualTo(held));
                Assert.That(captive.Record.State, Is.EqualTo(PlayerState.Captured));
                Assert.That(authority.TryRescueGroup(rescuerId, rescuerId), Is.EqualTo(1));
                Assert.That(captive.Record.Free, Is.True);
                captive.Teleport(new Vector3(100, 0.1f, 100));
                yield return null;
                for (int i = 0; i < 20; i++) captive.Simulate(default, 0.02f);
                Vector3 start = captive.transform.position;
                for (int i = 0; i < 25; i++) captive.Simulate(new PlayerCommand(Vector2.up, false, false), 0.02f);
                Assert.That(captive.transform.position.z, Is.GreaterThan(start.z + 0.2f));
            }
            finally
            {
                if (rescuerObject != null) Object.Destroy(rescuerObject);
                if (captiveObject != null) Object.Destroy(captiveObject);
                Object.Destroy(floor); Object.Destroy(rules);
            }
        }
        [UnityTest] public IEnumerator PrototypeSceneStartsNightWithGuardRescueAndEscape()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            yield return new WaitForFixedUpdate();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Player, Is.Not.Null);
            Assert.That(root.Player.Record.Free, Is.True);
            Assert.That(GameObject.Find("Guard"), Is.Not.Null);
            Assert.That(GameObject.Find("Rescue console"), Is.Not.Null);
            Assert.That(GameObject.Find("Escape door"), Is.Not.Null);
            Assert.That(GameObject.Find("Employee door"), Is.Not.Null);
            Assert.That(GameObject.Find("Surveillance terminal"), Is.Not.Null);
            Cursor.lockState = CursorLockMode.None;
        }
        private static PlayerMotor Create(string id, GameRulesAsset rules, Vector3 position)
        {
            var actor = new GameObject("Capture actor");
            actor.transform.position = position;
            var motor = actor.AddComponent<PlayerMotor>();
            motor.Configure(rules, new PlayerRecord(id));
            return motor;
        }
    }
}
