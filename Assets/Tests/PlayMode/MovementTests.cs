using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NightSupermarket.Game;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class MovementTests
    {
        [UnityTest] public IEnumerator WalkRunJumpAndCapturedMovement()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actor = new GameObject("Test Actor");
            var rules = ScriptableObject.CreateInstance<GameRulesAsset>();
            try
            {
                floor.transform.position = new Vector3(100, -0.5f, 100);
                floor.transform.localScale = new Vector3(50, 1, 50);
                actor.transform.position = new Vector3(100, 0.1f, 100);
                var motor = actor.AddComponent<PlayerMotor>(); motor.Configure(rules, new PlayerRecord("test"));
                Physics.SyncTransforms(); yield return null;
                for (int i = 0; i < 20; i++) motor.Simulate(default, 0.02f);
                Vector3 start = actor.transform.position;
                for (int i = 0; i < 25; i++) motor.Simulate(new PlayerCommand(Vector2.up, false, false), 0.02f);
                float rampLoss = rules.walkSpeed * rules.walkSpeed / (2 * rules.acceleration);
                Assert.That(actor.transform.position.z - start.z, Is.EqualTo(rules.walkSpeed * 0.5f - rampLoss).Within(0.06f), "eases up to walking speed");
                start = actor.transform.position;
                for (int i = 0; i < 25; i++) motor.Simulate(new PlayerCommand(Vector2.up, false, false), 0.02f);
                Assert.That(actor.transform.position.z - start.z, Is.EqualTo(rules.walkSpeed * 0.5f).Within(0.05f), "steady walk");
                for (int i = 0; i < 25; i++) motor.Simulate(new PlayerCommand(Vector2.up, true, false), 0.02f);
                start = actor.transform.position;
                for (int i = 0; i < 25; i++) motor.Simulate(new PlayerCommand(Vector2.up, true, false), 0.02f);
                Assert.That(actor.transform.position.z - start.z, Is.EqualTo(rules.sprintSpeed * 0.5f).Within(0.05f), "steady sprint");
                Assert.That(motor.Record.State, Is.EqualTo(PlayerState.Running));
                float stopTime = rules.sprintSpeed / rules.deceleration;
                for (int i = 0; i < Mathf.CeilToInt(stopTime / 0.02f) + 1; i++) motor.Simulate(default, 0.02f);
                Assert.That(motor.ActualSpeed, Is.LessThan(rules.movementThreshold), "a released mannequin stops quickly enough to freeze");
                float height = actor.transform.position.y;
                motor.Simulate(new PlayerCommand(Vector2.zero, false, true), 0.02f);
                Assert.That(actor.transform.position.y, Is.GreaterThan(height));
                motor.Record.SetState(PlayerState.Captured); start = actor.transform.position;
                motor.Simulate(new PlayerCommand(Vector2.one, true, true), 0.2f);
                Assert.That(actor.transform.position, Is.EqualTo(start));
            }
            finally { Object.Destroy(actor); Object.Destroy(floor); Object.Destroy(rules); }
        }

        [UnityTest] public IEnumerator InterpolatorBlendsFixedStepsAndSnapsTeleports()
        {
            var body = new GameObject("Interpolated body");
            try
            {
                var interpolator = body.AddComponent<MotionInterpolator>();
                yield return new WaitForFixedUpdate();
                body.transform.position = new Vector3(0.5f, 0, 0);
                yield return new WaitForFixedUpdate();
                Vector3 blended = interpolator.Position;
                Assert.That(blended.x, Is.InRange(0f, 0.5f), "renders between the last two simulated poses");
                body.transform.position = new Vector3(40, 0, 0);
                yield return new WaitForFixedUpdate();
                Assert.That(interpolator.Position.x, Is.EqualTo(40).Within(0.001f), "large jumps are teleports, not slides");
                interpolator.Interpolating = false;
                body.transform.position = new Vector3(41, 0, 0);
                Assert.That(interpolator.Position.x, Is.EqualTo(41).Within(0.001f));
            }
            finally { Object.Destroy(body); }
        }
    }
}
