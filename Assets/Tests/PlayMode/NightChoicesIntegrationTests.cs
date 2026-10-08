using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    public sealed class NightChoicesIntegrationTests
    {
        private static IEnumerator Load()
        {
            yield return CoopTestScene.Load(); yield return null;
            Object.FindAnyObjectByType<PrototypeRoot>().Guard.PlayerDriven = true;
        }
        private static void Approach(PlayerMotor player, Transform target)
        {
            player.Teleport(new Vector3(target.position.x, 0.1f, target.position.z - 1.1f));
            Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator BellDelaysGuardInvestigationAndEnforcesSharedCooldown()
        {
            yield return Load(); var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var bell = Object.FindAnyObjectByType<DistractionBell>(); var player = root.MannequinAt(0);
            Approach(player, bell.transform);
            var before = root.Guard.LastKnownPosition;
            Assert.That(bell.TryInteract(player), Is.True);
            Assert.That(bell.TryInteract(player), Is.False);
            bell.Tick(2.9f); Assert.That(root.Guard.LastKnownPosition, Is.EqualTo(before));
            root.Authority.Clock.Paused = true; bell.Tick(10); Assert.That(bell.Armed, Is.True);
            root.Authority.Clock.Paused = false; bell.Tick(0.11f);
            Assert.That(root.Guard.LastKnownPosition, Is.EqualTo(bell.transform.position));
            Assert.That(bell.TryInteract(player), Is.False);
            bell.Tick(15); Assert.That(bell.TryInteract(player), Is.True);
        }
        [UnityTest] public IEnumerator OptionalSwapCancelsAtDistancePaysOnceAndDoesNotGateEscape()
        {
            yield return Load(); var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Authority.Rules.Duration, Is.EqualTo(360));
            var display = Object.FindAnyObjectByType<BonusDisplay>(); var player = root.MannequinAt(0);
            Approach(player, display.transform); Assert.That(display.TryInteract(player), Is.True);
            display.Tick(2); player.Teleport(new Vector3(0, 0.1f, -12)); display.Tick(3);
            Assert.That(display.Complete, Is.False);
            Approach(player, display.transform); Assert.That(display.TryInteract(player), Is.True);
            root.Authority.Clock.Paused = true; display.Tick(10); Assert.That(display.Complete, Is.False);
            root.Authority.Clock.Paused = false; display.Tick(4); display.Tick(4);
            Assert.That(root.Authority.Stats.Bonuses, Is.EqualTo(1));
            Assert.That(display.TryInteract(player), Is.False);
            foreach (var mission in root.Missions.Missions) mission.DebugComplete();
            var exit = Object.FindAnyObjectByType<EscapeInteractable>();
            player.Teleport(new Vector3(exit.transform.position.x, 0.1f, exit.transform.position.z + 1.1f)); Physics.SyncTransforms();
            Assert.That(exit.TryInteract(player), Is.True, "remaining optional stand must not gate escape");
        }
        [UnityTest] public IEnumerator CamouflageNeedsShirtStillnessAndCancelsWhenYouLeave()
        {
            yield return Load(); var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.MannequinAt(0); var display = Object.FindAnyObjectByType<DisplayPose>();
            // Approach from the side, avoiding the clothing table in front of this stand.
            player.Teleport(new Vector3(display.transform.position.x + 1.1f, 0.1f, display.transform.position.z));
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Assert.That(display.TryInteract(player), Is.False);
            Assert.That(player.Grounded, Is.True, "standing on store floor");
            Assert.That(player.ActualSpeed, Is.LessThanOrEqualTo(player.Rules.movementThreshold), "settled pose speed");
            Assert.That(InteractionValidation.CanReach(player, display.transform), Is.True, "model stand reach");
            Assert.That(player.GetComponent<PlayerInventory>().Items.TryAdd("shirt", 1, 1), Is.True);
            Assert.That(display.TryInteract(player), Is.True);
            var pose = player.GetComponent<DisplayCamouflage>(); Assert.That(pose.Active, Is.True);
            player.transform.Rotate(0, 30, 0); Assert.That(pose.Active, Is.False);
            Assert.That(display.TryInteract(player), Is.True);
            player.Teleport(player.transform.position + Vector3.right); Assert.That(pose.Active, Is.False);
        }
        [UnityTest] public IEnumerator NewChoicesHaveReachableApproachesAndRescueStillWorks()
        {
            yield return Load(); var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.MannequinAt(0);
            var choices = new List<Transform>();
            foreach (var bonus in Object.FindObjectsByType<BonusDisplay>(FindObjectsSortMode.None)) choices.Add(bonus.transform);
            choices.Add(Object.FindAnyObjectByType<DisplayPose>().transform);
            choices.Add(Object.FindAnyObjectByType<DistractionBell>().transform);
            foreach (var stand in choices)
            {
                var destination = stand.position + Vector3.back * 1.1f;
                Assert.That(NavMesh.SamplePosition(destination, out var end, 1.5f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(player.transform.position, out var start, 2, NavMesh.AllAreas), Is.True);
                var path = new NavMeshPath(); Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
            var captive = root.MannequinAt(1);
            Assert.That(root.Authority.TryCapture(captive.Record.Id), Is.True);
            var rescue = Object.FindAnyObjectByType<RescueInteractable>(); Approach(player, rescue.transform);
            Assert.That(rescue.TryInteract(player), Is.True);
            Assert.That(captive.Record.Free, Is.True);
        }
    }
}
