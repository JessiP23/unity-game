using System.Collections;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>The hall must be walkable from the first start strip to the exit landing without a gap in the floor.</summary>
    public sealed class BackroomWalkTests
    {
        [UnityTest] public IEnumerator TheHallFloorIsContinuousAndSignsFaceThePlayer()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Authority.TryCapture(root.Player.Record.Id), Is.True);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Vector3 origin = BackroomPocket.Origin;
            Assert.That(Vector3.Distance(new Vector3(root.Player.transform.position.x, 0, root.Player.transform.position.z), new Vector3(origin.x, 0, origin.z + 1.2f)), Is.LessThan(0.5f), "admitted at the first start strip");
            Assert.That(Vector3.Dot(root.Player.transform.forward, Vector3.forward), Is.GreaterThan(0.9f), "facing down the hall");
            // Walk a line near the right wall: clear of doors, figures, stands and pads, which sit nearer the centre.
            for (float z = 0.6f; z < BackroomCourse.Rooms * 10f + 3.5f; z += 0.5f)
            {
                float inRoom = z % 10f;
                if (inRoom > 9.6f || inRoom < 0.4f) continue; // the gate plane itself
                bool floor = Physics.Raycast(new Vector3(origin.x + 3.4f, origin.y + 2.5f, origin.z + z), Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore);
                Assert.That(floor, Is.True, "floor under z=" + z);
                Assert.That(hit.point.y, Is.LessThan(origin.y + 0.4f), "nothing but floor at z=" + z + " (hit " + hit.collider.name + ")");
            }
            // Between rooms only the gate (a BackroomUse-free wall) may block; after the gate opens the way is clear.
            foreach (var wall in GameObject.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (wall.name != "Backroom wall") continue;
                float localZ = wall.transform.position.z - origin.z;
                bool rear = Mathf.Abs(localZ - 0.2f) < 0.01f;
                bool side = Mathf.Abs(Mathf.Abs(wall.transform.position.x - origin.x) - 4f) < 0.01f;
                Assert.That(rear || side, Is.True, "a full-width wall inside the hall at z=" + localZ);
            }
            var marks = GameObject.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            int facing = 0, total = 0;
            foreach (var mark in marks)
            {
                if (mark.transform.position.x < origin.x - 10f) continue;
                total++;
                if (Vector3.Dot(mark.transform.forward, Vector3.forward) > 0.9f) facing++;
            }
            Assert.That(total, Is.GreaterThan(0));
            Assert.That(facing, Is.EqualTo(total), "every hall label reads for a player walking toward +Z");
        }

        /// <summary>A captive is not "Free", yet must be able to use the hall's doors, gaps, figures and tags.</summary>
        [UnityTest] public IEnumerator ACaptiveCanUseTheFirstRoomsStation()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var player = root.Player;
            Assert.That(root.Authority.TryCapture(player.Record.Id), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(player.Record.InBackroom, Is.True);
            BackroomUse station = null;
            foreach (var use in Object.FindObjectsByType<BackroomUse>(FindObjectsSortMode.None))
                if (use.Room == 0 && use.gameObject.activeInHierarchy && (station == null || use.transform.position.z < station.transform.position.z)) station = use;
            Assert.That(station, Is.Not.Null, "room 1 has no station to use");
            Vector3 target = station.GetComponent<Collider>().bounds.center;
            player.Teleport(new Vector3(target.x, 0.1f, target.z - 1.6f));
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            var probe = player.GetComponent<InteractionProbe>();
            var view = player.GetComponent<PlayerView>();
            var found = probe.FindTarget(view.View.transform.position, (target - view.View.transform.position).normalized);
            Assert.That(found, Is.SameAs(station), "looking at " + station.name + " from 1.6 m offers nothing");
            Assert.That(string.IsNullOrEmpty(station.PromptFor(player)), Is.False, "the station has no prompt for the captive");
        }
    }
}
