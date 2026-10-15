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
    /// <summary>
    /// The close look. A guard who saw you move walks over; standing there like a plank does not fool him up close,
    /// a pose that fits the department does.
    /// </summary>
    public sealed class GuardInspectionTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            float until = Time.time + 5f;
            while (!root.Crowd.Ready && Time.time < until) yield return null;
            root.Population.Budget = 0;
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart();
        }

        /// <summary>Puts the guard 1.6 m in front of the player, both facing each other, in Clothing (where Display fits).</summary>
        private static void FaceOff(PrototypeRoot root, PlayerMotor player)
        {
            Vector3 at = new Vector3(-4.2f, 0.1f, 6.0f); // open floor in Clothing (on the placement rug, which is only a trigger)
            player.Teleport(at); player.transform.rotation = Quaternion.identity;
            var guard = root.Guard;
            var agent = guard.GetComponent<NavMeshAgent>();
            Vector3 spot = at + Vector3.forward * 1.6f;
            if (NavMesh.SamplePosition(spot, out var hit, 1f, NavMesh.AllAreas)) spot = hit.position;
            agent.Warp(spot);
            guard.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms();
        }

        private static IEnumerator Still(PlayerInputReader input, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { input.Submit(new PlayerCommand(Vector2.zero, false, false)); yield return new WaitForFixedUpdate(); }
        }

        [UnityTest] public IEnumerator StandingStillInFrontOfTheGuardGetsYouGrabbed()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.MannequinAt(0);
            FaceOff(root, player);
            yield return Still(root.InputAt(0), 0.3f);
            Assert.That(root.DetectionAt(0).Observed, Is.True, "the guard is looking right at you");
            // Seen moving, now frozen: Red. He is already next to you.
            root.DetectionAt(0).Detection.DebugOverride(DetectionState.Red, 0);
            yield return Still(root.InputAt(0), 1.0f);
            Assert.That(root.Guard.Inspecting, Is.Not.Null, "he looks you over");
            Assert.That(player.Record.Free, Is.True, "not yet");
            yield return Still(root.InputAt(0), PrototypeRoot.InspectSeconds + 0.6f);
            Assert.That(player.Record.Free, Is.False, "a body just standing in the aisle is grabbed");
        }

        [UnityTest] public IEnumerator AFittingPoseFoolsHimUpClose()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.MannequinAt(0);
            var input = root.InputAt(0);
            FaceOff(root, player);
            input.ForcePose = true; // the Display pose, which fits Clothing
            try
            {
                yield return Still(input, 0.3f);
                root.DetectionAt(0).Detection.DebugOverride(DetectionState.Red, 0);
                int closeCalls = root.Score.CloseCalls;
                yield return Still(input, PrototypeRoot.FooledSeconds + 1.0f);
                Assert.That(player.Record.Free, Is.True, "posed as a display mannequin: he buys it");
                Assert.That(root.Score.CloseCalls, Is.EqualTo(closeCalls + 1), "a close call");
                Assert.That(root.Guard.Inspecting, Is.Null);
                Assert.That(root.DetectionAt(0).Detection.Suspicion.Value, Is.Zero);
                // He turns back to his round; once you are out of his eye the sighting fades.
                float until = Time.time + 6f;
                while (Time.time < until && root.DetectionAt(0).Detection.State != DetectionState.Green) yield return Still(input, 0.1f);
                Assert.That(root.DetectionAt(0).Detection.State, Is.EqualTo(DetectionState.Green), "and forgets what he saw");
                Assert.That(player.Record.Free, Is.True, "no second look at a mannequin he just checked");
            }
            finally { input.ForcePose = false; }
        }

        [UnityTest] public IEnumerator SeenMovingFromAfarHeWalksOverToLook()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.MannequinAt(0);
            Vector3 at = new Vector3(-4.2f, 0.1f, 6.0f);
            player.Teleport(at); player.transform.rotation = Quaternion.identity;
            var agent = root.Guard.GetComponent<NavMeshAgent>();
            Vector3 far = at + Vector3.forward * 7f;
            if (NavMesh.SamplePosition(far, out var hit, 1.5f, NavMesh.AllAreas)) far = hit.position;
            agent.Warp(far);
            root.Guard.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms();
            yield return Still(root.InputAt(0), 0.3f);
            // Walk a step in his sight, then freeze: Orange runs out into Red.
            float end = Time.time + 0.5f;
            while (Time.time < end) { root.InputAt(0).Submit(new PlayerCommand(new Vector2(1f, 0f), false, false)); yield return new WaitForFixedUpdate(); }
            float wait = Time.time + 4f;
            while (Time.time < wait && root.DetectionAt(0).Detection.State != DetectionState.Red) yield return Still(root.InputAt(0), 0.1f);
            Assert.That(root.DetectionAt(0).Detection.State, Is.EqualTo(DetectionState.Red), "he is sure he saw something");
            // He walks over and stops at looking distance (he may already have come part of the way during the Orange grace).
            float until = Time.time + 6f;
            while (Time.time < until && root.Guard.Inspecting == null) yield return Still(root.InputAt(0), 0.1f);
            float after = Vector3.Distance(root.Guard.transform.position, player.transform.position);
            Assert.That(root.Guard.Inspecting, Is.Not.Null, "he walks over to look (" + after.ToString("0.0") + " m away)");
            Assert.That(after, Is.LessThan(GuardController.InspectDistance + 0.6f));
        }
    }
}
