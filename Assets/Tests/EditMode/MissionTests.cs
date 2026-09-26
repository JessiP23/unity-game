using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class MissionTests
    {
        [TestCase(ActionKind.Collect)] [TestCase(ActionKind.Move)] [TestCase(ActionKind.Place)]
        [TestCase(ActionKind.Break)] [TestCase(ActionKind.Steal)] [TestCase(ActionKind.Unlock)] [TestCase(ActionKind.Escape)]
        public void EachTypeCountsOnlyMatchingUniqueObjects(ActionKind kind)
        {
            var mission = new MissionTracker(new MissionRule("id", "Test", kind, "shirt", 2, "zone"));
            var action = new ObjectAction(kind, "one", "shirt", "player", "zone");
            mission.Apply(new ObjectAction(kind, "wrong", "box", "player", "zone"));
            mission.Apply(new ObjectAction(kind, "wrongzone", "shirt", "player", "other"));
            Assert.That(mission.Progress, Is.Zero);
            mission.Apply(action); mission.Apply(action);
            mission.Apply(new ObjectAction(kind, "one", "shirt", "player", "zone"));
            Assert.That(mission.Progress, Is.EqualTo(1));
            mission.Apply(new ObjectAction(kind, "two", "shirt", "player", "zone"));
            Assert.That(mission.Complete, Is.True);
        }
        [Test] public void FailureStopsProgressAndPersonalOwnerIsChecked()
        {
            var mission = new MissionTracker(new MissionRule("id", "Test", ActionKind.Collect, "", 1, owner: "owner"));
            mission.Apply(new ObjectAction(ActionKind.Collect, "1", "", "other")); Assert.That(mission.Progress, Is.Zero);
            mission.Fail(); mission.Apply(new ObjectAction(ActionKind.Collect, "1", "", "owner"));
            Assert.That(mission.Complete, Is.False); Assert.That(mission.Failed, Is.True);
        }
    }
}
