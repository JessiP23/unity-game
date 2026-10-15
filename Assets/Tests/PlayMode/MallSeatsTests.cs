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
    /// <summary>Seats (carousel horses, sofas, benches), the gallery's jobs upstairs, speech bubbles and the live stream.</summary>
    public sealed class MallSeatsTests
    {
        /// <summary>The first shift whose draw includes this job, so its set piece gets built.</summary>
        private static int ShiftWith(string job)
        {
            for (int shift = 1; shift < 2000; shift++)
                foreach (var j in JobCatalog.Draw(shift)) if (j.Id == job) return shift;
            Assert.Fail("no shift draws " + job);
            return 0;
        }

        private static IEnumerator Load(int shift)
        {
            PrototypeRoot.NextShift = shift;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            float until = Time.time + 5f;
            while (!root.Crowd.Ready && Time.time < until) yield return null;
            PrototypeRoot.NextShift = 0;
        }

        private static IEnumerator Still(PlayerInputReader input, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { input.Submit(new PlayerCommand(Vector2.zero, false, false)); yield return new WaitForFixedUpdate(); }
        }

        private static void ClearCrowd(PrototypeRoot root)
        {
            root.Population.Budget = 0;
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart();
        }

        [UnityTest] public IEnumerator AHorseCarriesItsRiderAndTheRideCountsAWholeTurn()
        {
            yield return Load(ShiftWith("carousel-ride"));
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            ClearCrowd(root);
            var carousel = root.Jobs.Carousel;
            var ride = root.Jobs.RideSpot;
            Assert.That(ride, Is.Not.Null);
            Assert.That(carousel.Seats.Count, Is.EqualTo(6), "six horses, six saddles");
            var player = root.MannequinAt(0);
            var horse = carousel.Seats[0];
            Assert.That(horse.TryInteract(player), Is.True, "E on a horse climbs on");
            Assert.That(player.Mounted && player.SeatOwner == horse, Is.True);
            Assert.That(horse.TryInteract(player), Is.True, "E again climbs down");
            Assert.That(player.Mounted, Is.False);
            Assert.That(horse.TryInteract(player), Is.True);
            var saddle = player.Seat;
            Vector3 start = player.transform.position;
            float fastest = 0f;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                root.InputAt(0).Submit(new PlayerCommand(Vector2.zero, false, false));
                yield return new WaitForFixedUpdate();
                fastest = Mathf.Max(fastest, player.ActualSpeed);
                Assert.That(Vector3.Distance(player.transform.position + Vector3.up * PlayerMotor.SaddleDrop, saddle.position), Is.LessThan(0.25f), "the rider stays in the saddle");
            }
            Assert.That(Vector3.Distance(player.transform.position, start), Is.GreaterThan(0.5f), "the horse carried the rider round");
            Assert.That(fastest, Is.LessThan(player.Rules.movementThreshold), "riding is not moving");
            Assert.That(player.Surface, Is.SameAs(carousel));
            Assert.That(ride.Worker, Is.SameAs(player), "the ride job starts by itself for a rider");
            Assert.That(ride.Progress, Is.GreaterThan(20f), "degrees of the turn are counted (" + ride.Progress.ToString("0") + ")");
            Assert.That(ride.PromptFor(player), Does.Contain("°"));
            // A move key climbs down onto the deck, on the ride and standing.
            root.InputAt(0).Submit(new PlayerCommand(new Vector2(0f, 1f), false, false));
            yield return new WaitForFixedUpdate();
            Assert.That(player.Mounted, Is.False, "a move key climbs down");
            yield return Still(root.InputAt(0), 0.3f);
            Vector3 flat = player.transform.position - carousel.transform.position; flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(CarouselRide.Radius), "you climb down onto the deck");
            Assert.That(player.Surface, Is.SameAs(carousel));
        }

        [UnityTest] public IEnumerator SofasArmchairsAndBenchesCanBeSatOn()
        {
            yield return Load(ShiftWith("sofa-shoot"));
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            ClearCrowd(root);
            int benches = 0, upstairs = 0;
            foreach (var spot in SeatSpot.All)
            {
                if (spot == null || spot.Style != SeatStyle.Chair) continue;
                if (spot.What == "the bench") benches++;
                if (spot.transform.position.y > PrimitiveWorld.UpstairsY - 0.5f) upstairs++;
                foreach (var seat in spot.Seats)
                {
                    Vector3 stand = spot.StandUpPoint(seat);
                    // Room for a standing body (the NavMesh is eroded by the agent radius, so tight gaps between
                    // furniture have none), and walkable floor close by.
                    Assert.That(Physics.CheckCapsule(stand + Vector3.up * 0.35f, stand + Vector3.up * 1.5f, 0.28f, ~(1 << 2), QueryTriggerInteraction.Ignore), Is.False, spot.name + ": getting up lands in furniture (" + stand + ")");
                    Assert.That(NavMesh.SamplePosition(stand, out _, 1.2f, NavMesh.AllAreas), Is.True, spot.name + ": getting up lands far from walkable floor (" + stand + ")");
                }
            }
            Assert.That(benches, Is.EqualTo(4), "the atrium benches");
            Assert.That(upstairs, Is.GreaterThanOrEqualTo(1), "the gallery has seats");
            var shoot = root.Jobs.Shoot;
            Assert.That(shoot, Is.Not.Null);
            SeatSpot sofa = null;
            foreach (var s in SeatSpot.All) if (s != null && s.What == "the sofa" && s.transform.position.y > PrimitiveWorld.UpstairsY - 0.5f) sofa = s;
            Assert.That(sofa, Is.Not.Null);
            var player = root.MannequinAt(0);
            Assert.That(sofa.TryInteract(player), Is.True, "E on the sofa sits down");
            yield return Still(root.InputAt(0), 1.2f);
            Assert.That(player.Mounted, Is.True);
            Assert.That(Mathf.Abs(player.transform.position.y - PrimitiveWorld.UpstairsY), Is.LessThan(0.15f), "seated, the body's feet are on the gallery floor");
            Assert.That(player.GetComponent<PlayerView>().Crouching, Is.True, "seated eyes are low");
            Assert.That(shoot.Worker, Is.SameAs(player), "the photo starts when you sit");
            Assert.That(shoot.Progress, Is.GreaterThan(0.5f));
            Assert.That(sofa.TryInteract(player), Is.True, "E again stands up");
            Assert.That(player.Mounted, Is.False);
            yield return Still(root.InputAt(0), 0.3f);
            Assert.That(NavMesh.SamplePosition(player.transform.position, out _, 0.6f, NavMesh.AllAreas), Is.True, "standing on open floor after getting up");
        }

        [UnityTest] public IEnumerator TheGalleryTvWallSwitchesToTheMannequinChannel()
        {
            yield return Load(ShiftWith("tv-channel"));
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            ClearCrowd(root);
            var channels = new List<HoldSpot>();
            foreach (var spot in Object.FindObjectsByType<HoldSpot>(FindObjectsSortMode.None)) if (spot.name == "TV channel") channels.Add(spot);
            Assert.That(channels.Count, Is.EqualTo(3));
            MissionTracker job = null;
            foreach (var m in root.Missions.Missions) if (m.Rule.Id == "tv-channel") job = m;
            Assert.That(job, Is.Not.Null);
            var player = root.MannequinAt(0);
            var tv = channels[0];
            Vector3 at = new Vector3(tv.transform.position.x, PrimitiveWorld.UpstairsY + 0.1f, 13.25f);
            player.Teleport(at);
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            yield return Still(root.InputAt(0), 0.2f);
            Assert.That(tv.TryInteract(player), Is.True, "E on the marked TV");
            yield return Still(root.InputAt(0), 2.6f);
            Assert.That(tv.Complete, Is.True, "held still 2 s: switched");
            Assert.That(job.Progress, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ShopperBubblesFaceTheViewAndThePlayerCameraIsMain()
        {
            yield return Load(0);
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(Camera.main, Is.Not.Null, "the player's view is the main camera");
            Assert.That(Camera.main, Is.SameAs(root.MannequinAt(0).GetComponent<PlayerView>().View));
            Assert.That(ViewCameras.Current, Is.SameAs(Camera.main));
        }

        [UnityTest] public IEnumerator TheLiveStreamShowsItsShotAndGivesAShoutOutForAHeldPose()
        {
            yield return Load(0);
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart();
            yield return null;
            root.Population.Budget = 12;
            // In the atrium, facing open floor between the benches and the fountain.
            var star = root.Crowd.StartStream(new Vector3(-3f, 0f, -18.5f), 90f, true);
            Assert.That(star, Is.Not.Null);
            yield return new WaitForSeconds(0.3f);
            var stream = root.Crowd.Stream;
            Assert.That(stream, Is.Not.Null, "the influencer is live");
            Assert.That(stream.TagText, Does.StartWith("● LIVE"));
            int fans = 0;
            foreach (var npc in root.Population.Active)
                if (npc.Behavior is CustomerBehavior c && c.Following && c.Leader != null && c.Leader.gameObject == star.gameObject) fans++;
            Assert.That(fans, Is.EqualTo(2), "two fans tag along");
            Assert.That(star.Perception.Sensor.Range, Is.EqualTo(LiveStream.Range).Within(0.01f), "what they see is the shot");
            var player = root.MannequinAt(0);
            var input = root.InputAt(0);
            input.ForcePose = true;
            int before = 0; foreach (var l in root.Score.Lines) if (l.Event == ScoreEvent.Shoutout) before++;
            try
            {
                float end = Time.time + 7f;
                while (Time.time < end)
                {
                    // Keep the streamer standing and filming this way, and the player in the middle of the shot.
                    star.Movement.Pause(0.5f, player.transform.position);
                    Vector3 lens = stream.Lens;
                    Vector3 want = new Vector3(lens.x, 0.02f, lens.z) + stream.Facing * 4f;
                    if (Vector3.Distance(player.transform.position, want) > 1.5f) { player.Teleport(want); Physics.SyncTransforms(); }
                    input.Submit(new PlayerCommand(Vector2.zero, false, false));
                    yield return new WaitForFixedUpdate();
                    int got = 0; foreach (var l in root.Score.Lines) if (l.Event == ScoreEvent.Shoutout) got++;
                    if (got > before) break;
                }
                Vector3 behind = new Vector3(stream.Lens.x, 1.2f, stream.Lens.z) - stream.Facing * 3f;
                Assert.That(stream.Sees(behind, null), Is.False, "behind the camera is not in the shot");
            }
            finally { input.ForcePose = false; }
            int after = 0; foreach (var l in root.Score.Lines) if (l.Event == ScoreEvent.Shoutout) after++;
            Assert.That(after, Is.EqualTo(before + 1), "a held pose in the shot earns one shout-out");
        }
    }
}
