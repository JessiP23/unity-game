using System.Collections;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>The second floor: reachable, zoned by height, with gems hidden on it and the only exit after lockdown.</summary>
    public sealed class MezzanineTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
        }

        [UnityTest] public IEnumerator UpstairsIsItsOwnZoneAboveElectronics()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Directory.ZoneAt(new Vector3(4f, 0.1f, 10f)), Is.EqualTo(ZoneType.Electronics));
            Assert.That(root.Directory.ZoneAt(new Vector3(4f, PrimitiveWorld.UpstairsY + 0.1f, 10f)), Is.EqualTo(ZoneType.Mezzanine));
            Assert.That(root.Directory.ZoneAt(new Vector3(12f, PrimitiveWorld.UpstairsY + 0.1f, 10f)), Is.EqualTo(ZoneType.Mezzanine), "over the staff room too");
            Assert.That(root.Directory.ZoneAt(new Vector3(12f, 0.1f, 10f)), Is.EqualTo(ZoneType.Employee));
        }

        [UnityTest] public IEnumerator TheEscalatorLeadsFromTheEntranceToTheFireExit()
        {
            yield return Load();
            var fire = GameObject.Find("Fire exit");
            Assert.That(fire, Is.Not.Null);
            Assert.That(fire.GetComponent<EscapeInteractable>(), Is.Not.Null, "the fire exit can be used");
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 0f, -12f), out var start, 1f, NavMesh.AllAreas), Is.True);
            Vector3 infront = PrimitiveWorld.FireExit + new Vector3(0f, -1.2f, -1.0f);
            Assert.That(NavMesh.SamplePosition(infront, out var end, 1.0f, NavMesh.AllAreas), Is.True, "floor in front of the fire exit");
            Assert.That(end.position.y, Is.GreaterThan(PrimitiveWorld.UpstairsY - 0.5f), "that floor is upstairs");
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "no walkable route up the escalator");
            // The ramp surface rises steadily: a point halfway up must have floor under it at the expected height.
            float midZ = (PrimitiveWorld.RampStartZ + PrimitiveWorld.RampEndZ) * 0.5f;
            Assert.That(Physics.Raycast(new Vector3(0f, 6f, midZ), Vector3.down, out var hit, 7f, 1, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(Mathf.Abs(hit.point.y - PrimitiveWorld.RampHeightAt(midZ)), Is.LessThan(0.2f), "ramp height at z=" + midZ + " is " + hit.point.y);
        }

        [UnityTest] public IEnumerator GemsAreHiddenOnWalkableFloorAndOnlyGlintAfterLightsOut()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Gems.Count, Is.EqualTo(GemPlan.PerNight));
            foreach (var gem in root.Gems)
            {
                Assert.That(gem.Lit, Is.False, "store is open: " + gem.name + " must not glint yet");
                Assert.That(string.IsNullOrEmpty(gem.PromptFor(root.Player)), Is.True, "no prompt while dark");
                Assert.That(NavMesh.SamplePosition(gem.transform.position, out var near, 1.6f, NavMesh.AllAreas), Is.True, gem.name + " is not near walkable floor at " + gem.transform.position);
                var overlaps = Physics.OverlapSphere(gem.transform.position, 0.12f, 1, QueryTriggerInteraction.Ignore);
                Assert.That(overlaps.Length, Is.Zero, gem.name + " is inside " + (overlaps.Length > 0 ? overlaps[0].name : ""));
            }
            // Advance to lights out: the clock's last third.
            double duration = root.Authority.Clock.Remaining;
            root.Authority.DebugAdvance(duration * (1 - NightSchedule.DarkAt) + 1);
            yield return null; yield return null;
            foreach (var gem in root.Gems) Assert.That(gem.Lit, Is.True, gem.name + " should glint after lights out");
        }

        [UnityTest] public IEnumerator LockdownDropsTheFrontShutterAndPointsUpstairs()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var door = GameObject.Find("Escape door").GetComponent<EscapeInteractable>();
            Assert.That(door.Blocked, Is.False);
            double duration = root.Authority.Clock.Remaining;
            root.Authority.DebugAdvance(duration * (1 - NightSchedule.LockdownAt) + 1);
            yield return null; yield return null;
            Assert.That(door.Blocked, Is.True, "front door still opens after lockdown");
            Assert.That(door.Prompt, Does.Contain("upstairs"));
            var shutter = GameObject.Find("Front shutter");
            Assert.That(shutter, Is.Not.Null, "shutter never appeared");
            Assert.That(shutter.activeInHierarchy, Is.True);
            yield return new WaitForSeconds(FrontShutter.Seconds + 0.2f);
            Assert.That(shutter.transform.position.y, Is.LessThan(1.5f), "shutter came down");
            Assert.That(GameObject.Find("Fire exit").GetComponent<EscapeInteractable>().Blocked, Is.False);
        }
    }
}
