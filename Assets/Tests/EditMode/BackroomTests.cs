using NUnit.Framework;
using NightSupermarket.Core;

namespace NightSupermarket.Tests
{
    public sealed class BackroomTests
    {
        [Test]
        public void TheSameSeedRepeatsAndAnotherSeedCanDiffer()
        {
            var course = new BackroomCourse(7);
            var again = new BackroomCourse(7);
            Assert.That(course.Length, Is.EqualTo(3));
            Assert.That(Matches(course), Is.EqualTo(1));
            Assert.That(course.TrialAt(0) != course.TrialAt(1) && course.TrialAt(1) != course.TrialAt(2) && course.TrialAt(0) != course.TrialAt(2), Is.True, "three different rooms");
            Assert.That(course.DoorMark(0) != course.DoorMark(1) && course.DoorMark(1) != course.DoorMark(2) && course.DoorMark(0) != course.DoorMark(2), Is.True, "every door shows a different number");
            Assert.That(again.TrialAt(0), Is.EqualTo(course.TrialAt(0)));
            Assert.That(again.DoorMark(1), Is.EqualTo(course.DoorMark(1)));
            Assert.That(again.EchoAt(0), Is.EqualTo(course.EchoAt(0)));
            bool differed = false;
            for (int seed = 1; seed < 20 && !differed; seed++)
            {
                var other = new BackroomCourse(seed);
                differed = other.TrialAt(0) != course.TrialAt(0) || other.LitCount != course.LitCount || other.EchoAt(0) != course.EchoAt(0);
            }
            Assert.That(differed, Is.True);
        }

        [Test]
        public void AMissStaysInTheRoomAndThreeClearsReturnYou()
        {
            var course = new BackroomCourse(11);
            Assert.That(course.TryPassStill(false), Is.EqualTo(course.Trial == BackroomTrial.StillLight));
            course = new BackroomCourse(11);
            for (int room = 0; room < 3; room++)
            {
                int before = course.Room;
                Clear(course);
                Assert.That(course.Room, Is.EqualTo(before + 1));
            }
            Assert.That(course.Complete, Is.True);
            Assert.That(course.TryDoor(0), Is.False);
        }

        [Test]
        public void FinishingTheBackroomFreesYouAndATeammateStillCan()
        {
            var session = new LocalSession();
            var authority = new LocalMatchAuthority(new GameRules(), session);
            Assert.That(authority.TryBeginNight(), Is.True);
            var rescuer = new PlayerRecord(session.Join());
            var captive = new PlayerRecord(session.Join());
            authority.Register(rescuer);
            authority.Register(captive);
            Assert.That(authority.TryCapture(captive.Id), Is.True);
            captive.SetBackroom(true);
            Assert.That(authority.Flow.Phase, Is.EqualTo(MatchPhase.Night));
            Assert.That(authority.TryFinishBackroom(captive.Id), Is.True);
            Assert.That(captive.Free, Is.True);
            Assert.That(captive.InBackroom, Is.False);
            Assert.That(authority.Warehouse.Contains(captive.Id), Is.False);

            Assert.That(authority.TryCapture(captive.Id), Is.True);
            captive.SetBackroom(true);
            Assert.That(authority.TryRescueGroup(rescuer.Id, rescuer.Id), Is.EqualTo(1));
            Assert.That(captive.State, Is.EqualTo(PlayerState.Normal));
            Assert.That(captive.InBackroom, Is.False);
        }

        [Test]
        public void EverySeedClearsAndVarietyIsReal()
        {
            var orders = new System.Collections.Generic.HashSet<string>();
            for (int seed = 0; seed < 300; seed++)
            {
                var course = new BackroomCourse(seed);
                orders.Add(course.TrialAt(0) + "," + course.TrialAt(1) + "," + course.TrialAt(2));
                for (int room = 0; room < 3; room++) Clear(course);
                Assert.That(course.Complete, Is.True);
                for (int step = 1; step < course.EchoLength; step++)
                    Assert.That(course.EchoAt(step), Is.Not.EqualTo(course.EchoAt(step - 1)), "no pad twice in a row");
            }
            Assert.That(orders.Count > 60, Is.True, "3 of 6 rooms in order gives up to 120 different halls; saw " + orders.Count);
        }

        [Test]
        public void DifficultyLengthensTheEchoAndSpeedsTheLights()
        {
            var easy = new BackroomCourse(5, 0);
            var hard = new BackroomCourse(5, 2);
            Assert.That(easy.EchoLength, Is.EqualTo(4));
            Assert.That(hard.EchoLength, Is.EqualTo(6));
            Assert.That(hard.BeamSeconds < easy.BeamSeconds, Is.True);
            Assert.That(hard.GreenSeconds < easy.GreenSeconds, Is.True);
        }

        [Test]
        public void RedLightStartsGreenAndCycles()
        {
            var course = new BackroomCourse(3);
            Assert.That(course.RedLightIsRed(0), Is.False);
            Assert.That(course.RedLightIsRed(course.GreenSeconds + 0.01), Is.True);
            Assert.That(course.RedLightIsRed(course.GreenSeconds + course.RedSeconds + 0.01), Is.False);
        }

        private static int Matches(BackroomCourse course)
        {
            int matches = 0;
            for (int door = 0; door < 3; door++)
                if (course.DoorMark(door) == course.LitCount) matches++;
            return matches;
        }

        private static void Clear(BackroomCourse course)
        {
            if (course.Trial == BackroomTrial.StillLight)
            {
                int before = course.Room;
                Assert.That(course.TryPassStill(true), Is.False);
                Assert.That(course.Room, Is.EqualTo(before));
                Assert.That(course.TryPassStill(false), Is.True);
                return;
            }
            if (course.Trial == BackroomTrial.LiarDoors)
            {
                int before = course.Room;
                int wrong = course.DoorMark(0) == course.LitCount ? 1 : 0;
                Assert.That(course.TryDoor(wrong), Is.False);
                Assert.That(course.Room, Is.EqualTo(before));
                for (int door = 0; door < 3; door++)
                    if (course.DoorMark(door) == course.LitCount)
                        Assert.That(course.TryDoor(door), Is.True);
                return;
            }
            if (course.Trial == BackroomTrial.RedLight)
            {
                int before = course.Room;
                Assert.That(course.TryPassRedLight(true), Is.False);
                Assert.That(course.Room, Is.EqualTo(before));
                Assert.That(course.TryPassRedLight(false), Is.True);
                return;
            }
            if (course.Trial == BackroomTrial.OddOneOut)
            {
                int before = course.Room;
                Assert.That(course.TryFigure((course.OddFigure + 1) % BackroomCourse.Figures), Is.False);
                Assert.That(course.Room, Is.EqualTo(before));
                Assert.That(course.TryFigure(course.OddFigure), Is.True);
                return;
            }
            if (course.Trial == BackroomTrial.PriceTags)
            {
                int before = course.Room;
                Assert.That(course.TryPrice((course.PriceAnswer + 1) % 3), Is.False);
                Assert.That(course.Room, Is.EqualTo(before));
                Assert.That(course.Price(course.PriceAnswer) > course.Price((course.PriceAnswer + 1) % 3), Is.True);
                Assert.That(course.TryPrice(course.PriceAnswer), Is.True);
                return;
            }
            int room = course.Room;
            int wrongPad = (course.EchoAt(0) + 1) % 4;
            Assert.That(course.TryPad(wrongPad), Is.False);
            Assert.That(course.EchoProgress, Is.EqualTo(0));
            Assert.That(course.Room, Is.EqualTo(room));
            for (int step = 0; step < course.EchoLength; step++)
                Assert.That(course.TryPad(course.EchoAt(step)), Is.True);
        }
    }
}
