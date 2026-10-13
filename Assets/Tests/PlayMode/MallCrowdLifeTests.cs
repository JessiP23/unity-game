using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Hiding in the crowd, crowds at the jobs with quiet windows, the heat map, new kinds of shoppers and shifts.</summary>
    public sealed class MallCrowdLifeTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var crowd = Object.FindAnyObjectByType<PrototypeRoot>().Crowd;
            float until = Time.time + 5f;
            while (!crowd.Ready && Time.time < until) yield return null;
            Assert.That(crowd.Ready, Is.True);
        }

        private static void ClearCrowd(PrototypeRoot root)
        {
            root.Population.Budget = 0;
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart();
        }

        private static IEnumerator Hold(PlayerInputReader input, float seconds, Vector2 move, bool sprint = false)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { input.Submit(new PlayerCommand(move, sprint, false)); yield return new WaitForFixedUpdate(); }
        }

        [UnityTest] public IEnumerator WalkingWithAGroupHidesYouAndRunningGivesYouAway()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            ClearCrowd(root);
            yield return null;
            var player = root.MannequinAt(0);
            var cover = player.GetComponent<CrowdCover>();
            Assert.That(cover, Is.Not.Null, "every mannequin can hide in the crowd");
            Vector3 start = new Vector3(-2.5f, 0.1f, -4.6f);
            player.Teleport(start);
            player.transform.rotation = Quaternion.identity; // up the aisle (+z)
            Physics.SyncTransforms();
            var walkers = new List<NpcController>();
            foreach (var offset in new[] { new Vector3(-0.7f, 0, 0.2f), new Vector3(0.7f, 0, 0.1f), new Vector3(0f, 0, -0.9f) })
            {
                var npc = root.Population.Spawn(NpcRole.Customer, start + offset - Vector3.up * 0.1f, 0f, true);
                walkers.Add(npc);
            }
            yield return null;
            foreach (var npc in walkers) npc.Movement.GoTo(npc.transform.position + Vector3.forward * 9f);
            int best = 0; bool covered = false, slowed = false;
            float end = Time.time + 2.5f;
            while (Time.time < end)
            {
                root.InputAt(0).Submit(new PlayerCommand(new Vector2(0, 1), false, false));
                yield return new WaitForFixedUpdate();
                best = Mathf.Max(best, cover.Companions);
                covered |= cover.Covered;
                slowed |= !float.IsPositiveInfinity(player.PaceCap);
            }
            Assert.That(best, Is.GreaterThanOrEqualTo(2), "the three shoppers walking up the aisle count as company");
            Assert.That(covered, Is.True, "walking with them hides you");
            Assert.That(slowed, Is.True, "you fall into step with the group");
            yield return Hold(root.InputAt(0), 0.8f, new Vector2(0, 1), sprint: true);
            Assert.That(cover.Strength, Is.Zero, "running gives you away");
        }

        [UnityTest] public IEnumerator ShoppersGatherAtTheJobsAndDriftOffInTheQuietWindow()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var crowd = root.Crowd;
            crowd.PressureEnabled = true;
            yield return new WaitForSeconds(1.2f);
            int job = -1;
            for (int i = 0; i < root.Missions.Missions.Count && job < 0; i++)
                if (crowd.LingerSpots(i) >= 2 && crowd.JobZone(i).HasValue && ZoneAccess.Customer.Allows(crowd.JobZone(i).Value)) job = i;
            Assert.That(job, Is.GreaterThanOrEqualTo(0), "at least one job tonight has room for a crowd");
            Vector3 spot = crowd.JobSpot(job).Value;
            foreach (var point in root.Directory.Points)
            {
                if (point.kind != PointKind.Linger || point.group != job) continue;
                Vector3 d = point.Position - spot; d.y = 0;
                Assert.That(d.magnitude, Is.GreaterThanOrEqualTo((float)JobPressure.KeepClear - 0.01f), "never standing on the job itself");
                Assert.That(d.magnitude, Is.LessThanOrEqualTo(3.5f), "close enough to be in the way");
                Assert.That(NavMesh.SamplePosition(point.Position, out _, 0.3f, NavigationAreas.CivilianMask), Is.True);
            }
            // Busy stretch: someone wanders over.
            float scale = Time.timeScale; Time.timeScale = 4f;
            float end = Time.time + 45f; int peak = 0;
            while (Time.time < end && peak == 0)
            {
                yield return null;
                if (crowd.StageOf(job) != PressureStage.Quiet) peak = Mathf.Max(peak, crowd.LingerersAt(job));
            }
            Time.timeScale = scale;
            Assert.That(peak, Is.GreaterThanOrEqualTo(1), "people hang around the job");
            // Fast-forward to the job's quiet window: everyone moves on.
            for (int step = 0; step < 40 && crowd.StageOf(job) != PressureStage.Quiet; step++)
            {
                root.Authority.DebugAdvance(2.5);
                yield return new WaitForSeconds(0.55f);
            }
            Assert.That(crowd.StageOf(job), Is.EqualTo(PressureStage.Quiet));
            yield return new WaitForSeconds(2f); // anyone mid-purchase finishes it first
            Assert.That(crowd.LingerersAt(job), Is.Zero, "the quiet window empties the job");
            Assert.That(crowd.JobLine(job, out var tint), Does.StartWith("QUIET NOW"));
            Assert.That(tint, Is.EqualTo("#40DF80"));
            Assert.That(crowd.TryQuietMark(job, out _), Is.True, "and the map marks it green");
        }

        [UnityTest] public IEnumerator TheHeatMapCountsTheShoppersInEachDepartment()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            yield return new WaitForSeconds(1.2f);
            int total = 0;
            foreach (var h in root.Crowd.Heat) { total += h.People; Assert.That(h.Level, Is.EqualTo(CrowdHeat.Level(h.People))); }
            Assert.That(total, Is.GreaterThan(0));
            Assert.That(total, Is.LessThanOrEqualTo(root.Population.Customers));
        }

        [UnityTest] public IEnumerator FamiliesPhoneZombiesTheInfluencerAndTheCrewAreWhoTheySayTheyAre()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var population = root.Population;
            root.Crowd.EnableKinds(true);
            var at = new Vector3(-2.5f, 0f, -4f);
            var parent = population.Spawn(NpcRole.Customer, at, 0f, true);
            var kid = population.Spawn(NpcRole.Customer, at + Vector3.right * 0.7f, 0f, true, ShopperArchetypes.Kid(), ShopperKind.Kid, (CustomerBehavior)parent.Behavior);
            var phone = population.Spawn(NpcRole.Customer, at + Vector3.forward * 2f, 0f, true, ShopperArchetypes.PhoneZombie(), ShopperKind.PhoneZombie);
            var star = population.Spawn(NpcRole.Customer, at + Vector3.forward * 4f, 0f, true, ShopperArchetypes.Influencer(), ShopperKind.Influencer);
            yield return null;
            var kidBehavior = (CustomerBehavior)kid.Behavior;
            Assert.That(kidBehavior.Following, Is.True);
            Assert.That(kidBehavior.Brain, Is.Null, "kids have no list of their own");
            Assert.That(kid.GetComponent<CapsuleCollider>().height, Is.LessThan(1.4f), "kids are small");
            Assert.That(kid.GetComponent<ShopperLook>().Kind, Is.EqualTo(ShopperKind.Kid));
            Assert.That(phone.transform.Find("PhoneZombie props"), Is.Not.Null, "a phone in hand");
            Assert.That(star.GetComponent<ShopperLook>().CameraLight, Is.Not.Null, "the influencer's red light");
            Assert.That(star.Perception.CrowdBlind, Is.Zero, "the camera sees through crowds");
            float scale = Time.timeScale; Time.timeScale = 3f;
            yield return new WaitForSeconds(8f);
            Time.timeScale = scale;
            if (parent.isActiveAndEnabled && kid.isActiveAndEnabled)
                Assert.That(Vector3.Distance(kid.transform.position, parent.transform.position), Is.LessThan(3.5f), "the kid sticks with their grown-up");
            Assert.That(root.Crowd.Crew, Is.GreaterThanOrEqualTo(1), "a cleaner is on shift");
            Assert.That(GameObject.Find("Cleaner props"), Is.Not.Null, "with a mop");
            if (parent.isActiveAndEnabled) parent.Depart();
            yield return null; yield return null;
            Assert.That(kid.isActiveAndEnabled, Is.False, "no kid wanders the mall alone");
            root.Crowd.EnableKinds(false);
        }

        [UnityTest] public IEnumerator AWetFloorSqueaksOnlyWhenYouRun()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            ClearCrowd(root);
            var signals = (WorldSignals)typeof(PrototypeRoot).GetField("signals", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(root);
            var player = root.MannequinAt(0);
            int squeaks = 0;
            using (signals.Noise.Subscribe(n => { if (n.Source == "squeaky wet floor") squeaks++; }))
            {
                player.Teleport(new Vector3(-2.5f, 0.1f, -4.6f)); player.transform.rotation = Quaternion.identity;
                WetFloor.Create(root.transform, new Vector3(-2.5f, 0f, -3.4f), 0f);
                yield return Hold(root.InputAt(0), 1.0f, new Vector2(0, 1));
                Assert.That(squeaks, Is.Zero, "walking across is fine");
                player.Teleport(new Vector3(2.5f, 0.1f, -4.6f)); player.transform.rotation = Quaternion.identity;
                WetFloor.Create(root.transform, new Vector3(2.5f, 0f, -2.9f), 0f);
                yield return Hold(root.InputAt(0), 1.0f, new Vector2(0, 1), sprint: true);
                Assert.That(squeaks, Is.GreaterThanOrEqualTo(1), "running across squeaks for the guard");
            }
        }

        [UnityTest] public IEnumerator ShiftsChangeTheCrowdAndTheSales()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.Difficulty, Is.EqualTo(Difficulty.Normal), "test runs clock in on Normal, with no card");
            Assert.That(GameObject.Find("Shift card"), Is.Null);
            root.Crowd.SetDifficulty(Difficulty.RushHour);
            Assert.That(root.Crowd.Sales.Count, Is.EqualTo(3));
            Assert.That(root.Population.DifficultyFactor, Is.EqualTo(1.3f).Within(1e-4));
            root.Crowd.SetDifficulty(Difficulty.Relaxed);
            Assert.That(root.Crowd.Sales.Count, Is.EqualTo(2));
            Assert.That(root.Population.DifficultyFactor, Is.EqualTo(0.7f).Within(1e-4));
            root.Crowd.SetDifficulty(Difficulty.Normal);
        }
    }
}
