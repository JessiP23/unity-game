using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
namespace NightSupermarket.Tests
{
    public sealed class ReadabilityTests
    {
        [TestCase(0, "AHEAD · USE THE AISLES")]
        [TestCase(-90, "TURN LEFT")]
        [TestCase(90, "TURN RIGHT")]
        [TestCase(180, "TURN AROUND")]
        [TestCase(-180, "TURN AROUND")]
        public void ObjectiveBearingIsExplicit(float angle, string label)
        { Assert.That(GuideDirection.Label(angle, 5), Is.EqualTo(label)); }
        [Test] public void NearObjectiveDoesNotDemandTurning()
        { Assert.That(GuideDirection.Label(180, 1), Does.StartWith("HERE")); }
        [Test] public void MemoryIsNotPresentedAsCurrentSight()
        {
            Assert.That(HudText.CrowdPlain(AwarenessState.Observing, false), Is.EqualTo("LOST SIGHT"));
            Assert.That(HudText.CrowdPlain(AwarenessState.Suspicious, false), Is.EqualTo("REMEMBERS MOVEMENT"));
            Assert.That(HudText.CrowdPlain(AwarenessState.Reporting, false), Is.EqualTo("CALLING GUARD"));
        }
    }
}
