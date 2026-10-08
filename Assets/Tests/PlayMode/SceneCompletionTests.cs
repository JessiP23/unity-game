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
    /// <summary>End-to-end checks in the real prototype store with four mannequins, live customers, and the guard.</summary>
    public sealed class SceneCompletionTests
    {
        private float scale;
        [SetUp] public void Remember() => scale = Time.timeScale;
        [TearDown] public void Restore() { Time.timeScale = scale; Cursor.lockState = CursorLockMode.None; }

        private static IEnumerator Load()
        {
            yield return CoopTestScene.Load();
            yield return null;
            yield return new WaitForFixedUpdate();
        }
        private static PrototypeRoot Root => Object.FindAnyObjectByType<PrototypeRoot>();
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        private static MissionTracker Mission(PrototypeRoot root, string id)
        {
            foreach (var mission in root.Missions.Missions) if (mission.Rule.Id == id) return mission;
            throw new AssertionException("missing mission " + id);
        }

        [UnityTest] public IEnumerator FourMannequinsStartAmongDisplaysWhileCustomersShopOutsideStaffAreas()
        {
            yield return Load();
            var root = Root;
            Assert.That(root.MannequinCount, Is.EqualTo(4));
            var displays = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name.StartsWith("Display mannequin")) displays.Add(t);
            Assert.That(displays.Count, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < root.MannequinCount; i++)
            {
                float nearest = float.PositiveInfinity;
                foreach (var d in displays) nearest = Mathf.Min(nearest, Vector3.Distance(Flat(d.position), Flat(root.MannequinAt(i).transform.position)));
                Assert.That(nearest, Is.LessThan(2.2f), "player " + (i + 1) + " starts among the display mannequins");
            }
            Time.timeScale = 4;
            var shopped = new HashSet<int>();
            float end = Time.time + 90;
            while (Time.time < end)
            {
                yield return null;
                var population = root.Population;
                Assert.That(population.Customers, Is.LessThanOrEqualTo(population.Settings.maxActiveCustomers));
                foreach (var npc in population.Active)
                {
                    if (npc.Role != NpcRole.Customer) continue;
                    var zone = root.Directory.ZoneAt(npc.transform.position);
                    Assert.That(zone.HasValue && ZoneAccess.IsRestricted(zone.Value), Is.False, npc.name + " walked into " + zone);
                    if (npc.Behavior is CustomerBehavior c && c.Brain != null && (c.Brain.State == CustomerState.Browsing || c.Brain.State == CustomerState.Shopping)) shopped.Add(npc.Id);
                }
            }
            Assert.That(shopped.Count, Is.GreaterThanOrEqualTo(3), "customers reach shelves and shop");
            Assert.That(root.Population.Customers, Is.GreaterThanOrEqualTo(1));
            var reader = root.MannequinAt(1).Record;
            Assert.That(root.Authority.TryCapture(reader.Id), Is.True);
            root.Authority.TryEnterSurveillance(reader.Id, reader.Id);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(root.Authority.TryReadSurveillance(reader.Id, reader.Id, out var view), Is.True);
            Assert.That(view.Players.Count, Is.EqualTo(4), "customers never take player slots");
            int mannequins = 0, customers = 0, guards = 0;
            foreach (var entity in view.Entities)
            {
                if (entity.Kind == EntityKind.Mannequin) mannequins++;
                if (entity.Kind == EntityKind.Customer) customers++;
                if (entity.Kind == EntityKind.Guard) guards++;
            }
            Assert.That(mannequins, Is.EqualTo(4)); Assert.That(customers, Is.GreaterThan(0)); Assert.That(guards, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ShirtAndCrateMissionsCompleteAndThePlayersEscape()
        {
            yield return Load();
            var root = Root;
            var shirtObject = GameObject.Find("Shirt");
            Vector3 shirtStart = shirtObject.transform.position;
            Time.timeScale = 4;
            yield return new WaitForSeconds(40f);
            Time.timeScale = 1;
            Assert.That(shirtObject.activeInHierarchy, Is.True, "customers leave the shirt alone");
            Assert.That(Vector3.Distance(shirtObject.transform.position, shirtStart), Is.LessThan(0.2f));
            var thief = root.MannequinAt(0);
            thief.Teleport(new Vector3(shirtObject.transform.position.x, 0.1f, shirtObject.transform.position.z - 0.9f));
            Physics.SyncTransforms();
            Assert.That(shirtObject.GetComponent<PhysicalItem>().TryInteract(thief), Is.True);
            Assert.That(thief.GetComponent<PlayerInventory>().Items.Count("shirt"), Is.EqualTo(1));
            Assert.That(Mission(root, "steal-shirt").Complete, Is.True, "Steal a shirt from Clothing");

            var zone = GameObject.Find("Clothing placement zone").transform.position;
            var crates = new List<PhysicalItem>();
            foreach (var item in Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None))
                if (item.name == "Collectible crate") crates.Add(item);
            Assert.That(crates.Count, Is.EqualTo(3));
            var carry = thief.GetComponent<CarrySystem>();
            for (int i = 0; i < crates.Count; i++)
            {
                var crate = crates[i];
                thief.Teleport(crate.transform.position + new Vector3(0, -crate.transform.position.y + 0.1f, 0.9f));
                Physics.SyncTransforms();
                Assert.That(crate.TryInteract(thief), Is.True, "picked up crate " + (i + 1));
                if (i == 0)
                {
                    thief.Teleport(new Vector3(zone.x, 0.4f, zone.z + 1.1f));
                    Physics.SyncTransforms();
                }
                Assert.That(carry.Release(false), Is.True);
                for (int f = 0; f < 90; f++) yield return new WaitForFixedUpdate();
            }
            Assert.That(Mission(root, "collect-crates").Complete, Is.True, "collect three crates");
            Assert.That(Mission(root, "place-crate").Complete, Is.True, "crate placed in the clothing drop zone");
            Assert.That(root.Missions.Complete, Is.True);

            var exit = GameObject.Find("Escape door").transform.position;
            thief.Teleport(new Vector3(exit.x, 0.1f, exit.z + 1.2f));
            Physics.SyncTransforms();
            Assert.That(GameObject.Find("Escape door").GetComponent<EscapeInteractable>().TryInteract(thief), Is.True);
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Victory), "objectives done and a mannequin escaped");
        }

        [UnityTest] public IEnumerator CaptureRescueAndDawnStillDecideTheNight()
        {
            yield return Load();
            var root = Root;
            var captive = root.MannequinAt(2);
            Assert.That(root.Authority.TryCapture(captive.Record.Id), Is.True);
            Assert.That(root.Authority.Warehouse.Count, Is.EqualTo(1));
            var rescuer = root.MannequinAt(3);
            var console = GameObject.Find("Rescue console");
            rescuer.Teleport(new Vector3(console.transform.position.x, 0.1f, console.transform.position.z - 1.0f));
            Physics.SyncTransforms();
            Assert.That(console.GetComponent<RescueInteractable>().TryInteract(rescuer), Is.True);
            Assert.That(captive.Record.State, Is.EqualTo(PlayerState.Normal), "rescued");
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Night));
            root.Authority.DebugAdvance(root.Authority.Clock.Remaining + 1);
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Defeat), "dawn before escape loses");
        }

        [UnityTest] public IEnumerator AllMannequinsCapturedLosesEvenWithCustomersAround()
        {
            yield return Load();
            var root = Root;
            for (int i = 0; i < root.MannequinCount; i++) root.Authority.TryCapture(root.MannequinAt(i).Record.Id);
            Assert.That(root.Population.Customers, Is.GreaterThan(0));
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Night));
            Assert.That(root.MannequinAt(0).Record.InBackroom, Is.True);
        }

        /// <summary>
        /// The design walkthrough in the live store. A player (driven through the same input seam a network
        /// client will use) hides behind a customer, freezes in view, then moves; the customer reports the
        /// last-known spot; the guard walks there; the report never follows the player.
        /// </summary>
        [UnityTest] public IEnumerator LiveStoreCustomerReportsMovingPlayerAndGuardInvestigatesLastKnownSpot()
        {
            yield return Load();
            var root = Root;
            var player = root.MannequinAt(0);
            var input = root.InputAt(0);
            var profile = Arena.Watcher();
            var display = new Vector3(-1.2f, 0, 13.2f);
            var held = new List<NavigationPoint>();
            foreach (var point in root.Directory.Points)
            {
                if (point.kind != PointKind.Browse || point.zone != ZoneType.Clothing) continue;
                if (Vector3.Distance(Flat(point.Position), display) <= 0.5f) point.Release(point.Occupant);
                else if (point.TryReserve(-99)) held.Add(point);
            }
            var watcher = root.Population.Spawn(NpcRole.Customer, new Vector3(-1.2f, 0, 10.5f), 0, true, profile);
            var brain = ((CustomerBehavior)watcher.Behavior).Brain;
            float wait = Time.time + 20;
            while (Time.time < wait && brain.State != CustomerState.Browsing) yield return null;
            foreach (var point in held) point.Release(-99);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing), "the customer looks at the display mannequins");
            yield return new WaitForSeconds(0.5f);

            Vector3 eye = watcher.Perception.Eye;
            Vector3 behind = watcher.transform.position - watcher.transform.forward * 1.6f;
            Assert.That(NavMesh.SamplePosition(behind, out var behindHit, 1f, NavMesh.AllAreas), Is.True);
            player.transform.rotation = Quaternion.LookRotation(-watcher.transform.right);
            player.Teleport(behindHit.position + Vector3.up * 0.1f);
            var target = player.GetComponent<PerceptionTarget>();
            yield return Hold(input, 3.5f, Vector2.zero);
            Assert.That(watcher.Perception.StateFor(target), Is.EqualTo(AwarenessState.Unaware), "out of sight, the customer forgets the display it glanced at");
            yield return Hold(input, 1.0f, new Vector2(0, 1));
            Assert.That(watcher.Perception.StateFor(target), Is.EqualTo(AwarenessState.Unaware), "moving behind a customer is safe");

            Vector3 spot = Vector3.zero; bool found = false;
            for (int angle = 0; angle <= 40 && !found; angle += 10)
                foreach (int sign in new[] { 1, -1 })
                {
                    Vector3 dir = Quaternion.Euler(0, angle * sign, 0) * watcher.transform.forward;
                    for (float d = 3.5f; d >= 1.2f && !found; d -= 0.4f)
                    {
                        if (!NavMesh.SamplePosition(watcher.transform.position + dir * d, out var hit, 0.4f, NavMesh.AllAreas)) continue;
                        if (!watcher.Perception.Sensor.CanSee(eye, watcher.transform.forward, hit.position + Vector3.up, null).Visible) continue;
                        spot = hit.position; found = true;
                    }
                    if (found) break;
                }
            Assert.That(found, Is.True, "found a clear spot in the customer's view");
            player.transform.rotation = Quaternion.LookRotation(watcher.transform.right);
            player.Teleport(spot + Vector3.up * 0.1f);
            yield return Hold(input, 2.0f, Vector2.zero);
            Assert.That(watcher.Perception.StateFor(target), Is.EqualTo(AwarenessState.Observing), "freeze: just a mannequin");
            int reportsBefore = root.Authority.Stats.Reports;

            float giveUp = Time.time + 6f;
            for (int step = 0; Time.time < giveUp && watcher.Perception.StateFor(target) < AwarenessState.Suspicious; step++)
                yield return Hold(input, 0.4f, new Vector2(0, step % 2 == 0 ? 1 : -1));
            Assert.That(watcher.Perception.StateFor(target), Is.GreaterThanOrEqualTo(AwarenessState.Suspicious), "wait... that mannequin moved");
            Vector3 lastSeen = player.transform.position;

            player.Teleport(new Vector3(-13.2f, 0.1f, 9.4f));
            Physics.SyncTransforms();
            wait = Time.time + 8;
            while (Time.time < wait && root.Authority.Stats.Reports == reportsBefore) yield return Hold(input, 0.1f, Vector2.zero);
            Assert.That(root.Authority.Stats.Reports, Is.EqualTo(reportsBefore + 1), "the customer reported");
            var report = root.Guard.LastReport;
            Assert.That(report.HasValue, Is.True, "security received it");
            var reported = new Vector3(report.Value.LastKnownPosition.X, 0, report.Value.LastKnownPosition.Z);
            Assert.That(Vector3.Distance(reported, Flat(lastSeen)), Is.LessThan(1.2f), "the report is the last-known location");
            Assert.That(Vector3.Distance(reported, Flat(player.transform.position)), Is.GreaterThan(4f), "not the hiding place");
            Assert.That(report.Value.Zone, Is.EqualTo(ZoneType.Clothing));

            var guard = root.Guard.transform;
            float before = Vector3.Distance(Flat(guard.position), reported);
            Time.timeScale = 3;
            wait = Time.time + 30;
            bool reached = false;
            while (Time.time < wait && !reached)
            {
                yield return null;
                reached = Vector3.Distance(Flat(guard.position), reported) < 2.5f || root.Guard.Brain.State == GuardState.Search;
            }
            Assert.That(reached || Vector3.Distance(Flat(guard.position), reported) < before - 4f, Is.True, "the guard walks to the reported area");
            Assert.That(Vector3.Distance(Flat(root.Guard.LastKnownPosition), Flat(player.transform.position)), Is.GreaterThan(4f), "and still does not know where the player went");
            Assert.That(brain.State, Is.Not.EqualTo(CustomerState.Reporting).And.Not.EqualTo(CustomerState.Alerted), "the customer is back to shopping");
            Assert.That(player.Record.Free, Is.True, "customers never capture");
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Night), "the night goes on");
        }

        [UnityTest] public IEnumerator DebugOverlayShowsRealNpcStateAndNeverTouchesGameplay()
        {
            yield return Load();
            var root = Root;
            var overlay = Object.FindAnyObjectByType<NpcDebugOverlay>();
            yield return new WaitForSeconds(1f);
            int colliders = WorldColliders();
            overlay.Labels = overlay.Perception = overlay.Navigation = true;
            yield return null; yield return null;
            var labels = overlay.GetComponentsInChildren<TextMesh>(false);
            var lines = System.Array.FindAll(overlay.GetComponentsInChildren<LineRenderer>(false), l => l.enabled);
            Assert.That(labels.Length, Is.GreaterThan(0), "Z shows labels");
            Assert.That(lines.Length, Is.GreaterThan(0), "X and Y draw cones and paths");
            var npc = root.Population.Active[0];
            bool labelled = false;
            foreach (var label in labels) if (label.text.StartsWith(npc.Role + " " + npc.Id) && label.text.Contains(npc.StateLabel)) labelled = true;
            Assert.That(labelled, Is.True, "labels show the NPC's actual state");
            Assert.That(WorldColliders(), Is.EqualTo(colliders), "debug drawing adds nothing physical");
            overlay.Labels = overlay.Perception = overlay.Navigation = false;
            yield return null; yield return null;
            Assert.That(overlay.GetComponentsInChildren<TextMesh>(false).Length, Is.EqualTo(0), "Z off hides labels");
            Assert.That(System.Array.FindAll(overlay.GetComponentsInChildren<LineRenderer>(false), l => l.enabled).Length, Is.EqualTo(0), "X/Y off hides cones and paths");
        }

        private static int WorldColliders()
        {
            int count = 0;
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.GetComponentInParent<NpcController>() == null) count++;
            return count;
        }

        private static IEnumerator Hold(PlayerInputReader input, float seconds, Vector2 move)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                input.Submit(new PlayerCommand(move, false, false));
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
