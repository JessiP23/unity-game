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
    /// <summary>The mall around the supermarket: walkable, zoned, with a carousel that carries riders and a climb with two ways up.</summary>
    public sealed class MallTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
        }

        private static NavMeshPath PathBetween(Vector3 a, Vector3 b)
        {
            Assert.That(NavMesh.SamplePosition(a, out var start, 1.2f, NavMesh.AllAreas), Is.True, "no floor at " + a);
            Assert.That(NavMesh.SamplePosition(b, out var end, 1.2f, NavMesh.AllAreas), Is.True, "no floor at " + b);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
            return path;
        }

        [UnityTest] public IEnumerator ShoppersCanWalkFromTheMallDoorsToEveryDepartment()
        {
            yield return Load();
            Vector3 doors = new Vector3(0f, 0f, -31.9f);
            foreach (var (name, at) in new[]
            {
                ("the supermarket aisles", new Vector3(-2.5f, 0f, 0f)),
                ("the toy store carousel", PrimitiveWorld.CarouselCentre + new Vector3(0f, 0f, -4.2f)),
                ("the robot shelf stool", PrimitiveWorld.RobotShelf + new Vector3(0f, 0f, 1.9f)),
                ("the escape door", PrimitiveWorld.EscapeDoor + new Vector3(0f, -1.3f, 1.2f)),
                ("upstairs at the top of the stairs", new Vector3(2.0f, PrimitiveWorld.UpstairsY, 13.6f)),
                ("upstairs by the fire exit", PrimitiveWorld.FireExit + new Vector3(0f, -1.2f, -1.0f)),
            })
                Assert.That(PathBetween(doors, at).status, Is.EqualTo(NavMeshPathStatus.PathComplete), "no complete route from the mall doors to " + name);
        }

        [UnityTest] public IEnumerator TheMallHasItsOwnZones()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Directory.ZoneAt(new Vector3(0f, 0.1f, -24f)), Is.EqualTo(ZoneType.Atrium));
            Assert.That(root.Directory.ZoneAt(new Vector3(-21f, 0.1f, -25f)), Is.EqualTo(ZoneType.ToyStore));
            Assert.That(root.Directory.ZoneAt(new Vector3(0f, 0.1f, -32.5f)), Is.EqualTo(ZoneType.EntranceExit));
            Assert.That(root.Directory.ZoneAt(new Vector3(0f, 0.1f, -13f)), Is.EqualTo(ZoneType.EntranceExit));
        }

        [UnityTest] public IEnumerator TheStairsClimbBesideTheEscalatorWithoutGaps()
        {
            yield return Load();
            Physics.SyncTransforms();
            float x = (PrimitiveWorld.StairsMinX + PrimitiveWorld.StairsMaxX) * 0.5f;
            float last = 0f;
            for (float z = PrimitiveWorld.RampStartZ + 0.2f; z < PrimitiveWorld.RampEndZ; z += 0.3f)
            {
                Assert.That(Physics.Raycast(new Vector3(x, 6f, z), Vector3.down, out var hit, 7f, 1, QueryTriggerInteraction.Ignore), Is.True, "nothing under the stairs at z=" + z);
                Assert.That(hit.collider.name, Is.EqualTo("Stair"), "at z=" + z + " the stairs are covered by " + hit.collider.name);
                Assert.That(hit.point.y, Is.GreaterThanOrEqualTo(last - 0.01f), "the stairs go down at z=" + z);
                Assert.That(hit.point.y - last, Is.LessThan(0.2f), "a step taller than a shin at z=" + z);
                last = hit.point.y;
            }
            Assert.That(last, Is.GreaterThan(PrimitiveWorld.UpstairsY - 0.2f), "the top step does not reach the mezzanine");
            Assert.That(GameObject.Find("Escalator").GetComponent<EscalatorBelt>(), Is.Not.Null, "the escalator has no belt");
        }

        [UnityTest] public IEnumerator TheCarouselCarriesARiderWithoutCountingAsMovement()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var carousel = Object.FindAnyObjectByType<CarouselRide>();
            Assert.That(carousel, Is.Not.Null);
            Assert.That(carousel.Speed, Is.GreaterThan(1f), "the carousel starts turning");
            var player = root.Player;
            // Between two horses (they sit every 60°), feet on the platform; settle before measuring.
            Vector3 start = PrimitiveWorld.CarouselCentre + new Vector3(2.3f * Mathf.Cos(30f * Mathf.Deg2Rad), CarouselRide.PlatformHeight, 2.3f * Mathf.Sin(30f * Mathf.Deg2Rad));
            player.Teleport(start);
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            start = player.transform.position;
            float topSpeed = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForFixedUpdate();
                topSpeed = Mathf.Max(topSpeed, player.ActualSpeed);
            }
            Vector3 moved = player.transform.position - start;
            Assert.That(moved.magnitude, Is.GreaterThan(0.6f), "the rider was not carried (moved " + moved.magnitude.ToString("0.00") + " m)");
            Assert.That(Vector3.Distance(new Vector3(player.transform.position.x, 0, player.transform.position.z), new Vector3(PrimitiveWorld.CarouselCentre.x, 0, PrimitiveWorld.CarouselCentre.z)), Is.LessThan(3.0f), "the rider slid off the platform");
            Assert.That(topSpeed, Is.LessThan(player.Rules.movementThreshold), "carried motion counted as moving (" + topSpeed.ToString("0.00") + " m/s)");
            Assert.That(player.Surface, Is.SameAs(carousel));
        }

        [UnityTest] public IEnumerator GemsInTheMallSitOnOpenFloor()
        {
            yield return Load();
            foreach (var (at, _) in JobBuilder.GemSpots)
            {
                if (at.z > -15f) continue; // the supermarket's own spots are covered by MezzanineTests
                if (Vector3.Distance(new Vector3(at.x, 0, at.z), PrimitiveWorld.CarouselCentre) < CarouselRide.Radius) continue; // reached by riding, not walking
                Assert.That(NavMesh.SamplePosition(at, out _, 1.6f, NavMesh.AllAreas), Is.True, "mall gem spot " + at + " is not near walkable floor");
                var overlaps = Physics.OverlapSphere(at, 0.12f, 1, QueryTriggerInteraction.Ignore);
                Assert.That(overlaps.Length, Is.Zero, "mall gem spot " + at + " is inside " + (overlaps.Length > 0 ? overlaps[0].name : ""));
            }
        }
    }
}
