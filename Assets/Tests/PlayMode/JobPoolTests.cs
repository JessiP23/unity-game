using System.Collections;
using System.Reflection;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Every drawn job must have something in the store to point at, and the player must be able to walk there.</summary>
    public sealed class JobPoolTests
    {
        [UnityTest] public IEnumerator DrawnJobsAreBuiltAndReachableOnSeveralShifts()
        {
            foreach (int shift in new[] { 1, 2, 3, 5, 8, 13 })
            {
                PrototypeRoot.NextShift = shift;
                yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
                var root = Object.FindAnyObjectByType<PrototypeRoot>();
                Assert.That(root.Shift, Is.EqualTo(shift));
                Assert.That(root.Missions.Missions.Count, Is.EqualTo(JobCatalog.PerNight), "shift " + shift);
                var builder = (JobBuilder)typeof(PrototypeRoot).GetField("jobs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(root);
                var player = root.Player;
                Assert.That(NavMesh.SamplePosition(player.transform.position, out var start, 2f, NavMesh.AllAreas), Is.True);
                foreach (var job in root.Missions.Missions)
                {
                    Assert.That(builder.Guide(job, player, out var at, out var label), Is.True, "shift " + shift + ": " + job.Rule.Id + " has nothing to point at");
                    Assert.That(label, Is.Not.Empty);
                    Assert.That(NavMesh.SamplePosition(at, out var end, 2.0f, NavMesh.AllAreas), Is.True, "shift " + shift + ": " + job.Rule.Id + " target " + at + " is not near walkable floor");
                    float offFloor = Vector3.Distance(new Vector3(at.x, 0, at.z), new Vector3(end.position.x, 0, end.position.z));
                    Assert.That(offFloor, Is.LessThan(0.9f), "shift " + shift + ": " + job.Rule.Id + " target " + at + " is " + offFloor.ToString("0.0") + " m from walkable floor — inside furniture?");
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "shift " + shift + ": no walkable path to " + job.Rule.Id);
                    Assert.That(builder.How(job.Rule.Id), Is.Not.Empty);
                }
                foreach (var pink in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!pink.enabled || pink.sharedMaterial == null || !pink.sharedMaterial.HasProperty("_BaseColor")) continue;
                    var c = pink.sharedMaterial.GetColor("_BaseColor");
                    bool magenta = c.r > 0.6f && c.b > 0.6f && c.g < 0.4f;
                    Assert.That(magenta, Is.False, "a magenta placeholder is still visible: " + pink.name);
                }
            }
            PrototypeRoot.NextShift = 0;
        }

        /// <summary>The furniture snap must leave items that sit ON a table alone; shift 0 always draws the shirt job.</summary>
        [UnityTest] public IEnumerator ShirtsStayOnTheClothingTables()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            yield return new WaitForSeconds(0.5f); // let physics settle
            int shirts = 0;
            foreach (var item in Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None))
            {
                if (item.Definition == null || item.Definition.missionTag != "shirt") continue;
                shirts++;
                var at = item.transform.position;
                Assert.That(at.y, Is.GreaterThan(0.85f), "shirt fell off its table to " + at);
                Assert.That(Mathf.Abs(at.z - 9.0f), Is.LessThan(0.5f), "shirt left the table row: " + at);
            }
            Assert.That(shirts, Is.EqualTo(2));
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.Player;
            // Stand in front of the first table, look at the shirt: the probe must offer it.
            player.transform.position = new Vector3(-6.2f, 0.1f, 7.9f);
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            var probe = player.GetComponent<InteractionProbe>();
            var view = player.GetComponent<PlayerView>();
            var target = probe.FindTarget(view.View.transform.position, (new Vector3(-6.2f, 0.94f, 9.0f) - view.View.transform.position).normalized);
            Assert.That(target, Is.Not.Null, "looking at the shirt from the aisle offers nothing");
            Assert.That(target is PhysicalItem p && p.Definition.missionTag == "shirt", Is.True, "offered " + target);
        }

        [UnityTest] public IEnumerator HoldSpotCountsOnlyAfterStandingStill()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var signals = (WorldSignals)typeof(PrototypeRoot).GetField("signals", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(root);
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.name = "Test hold"; host.transform.position = root.Player.transform.position + root.Player.transform.forward * 1.2f + Vector3.up;
            var spot = host.AddComponent<HoldSpot>();
            spot.Configure(root.Authority, signals, ActionKind.Steal, "test-hold");
            spot.Seconds = 1f;
            int counted = 0;
            using (signals.Actions.Subscribe(a => { if (a.Tag == "test-hold") counted++; }))
            {
                Physics.SyncTransforms();
                Assert.That(spot.TryInteract(root.Player), Is.True);
                spot.Tick(0.6f);
                Assert.That(spot.Complete, Is.False);
                spot.Tick(0.5f);
                Assert.That(spot.Complete, Is.True);
                Assert.That(counted, Is.EqualTo(1));
                Assert.That(spot.TryInteract(root.Player), Is.False, "a finished spot cannot be used again");
            }
            Object.Destroy(host);
        }
    }
}
