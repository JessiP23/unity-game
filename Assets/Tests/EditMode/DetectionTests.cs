using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class DetectionTests
    {
        [Test] public void OrangeExpiresAndLosingVisionResetsGrace()
        {
            var d = new DetectionSystem(new GameRules(orangeDuration: 1));
            d.Tick(true, 0, 0.4); Assert.That(d.State, Is.EqualTo(DetectionState.Orange));
            d.Tick(false, 0, 0.1); Assert.That(d.State, Is.EqualTo(DetectionState.Green));
            d.Tick(true, 0, 0.8); Assert.That(d.State, Is.EqualTo(DetectionState.Orange));
            d.Tick(true, 0, 0.2); Assert.That(d.State, Is.EqualTo(DetectionState.Red));
        }
        [Test] public void StillnessAndJitterDoNotIncreaseSuspicion()
        {
            var d = new DetectionSystem(new GameRules()); d.Tick(true, 0, 2);
            d.Tick(true, 0, 20); d.Tick(true, 0.02, 20);
            Assert.That(d.Suspicion.Value, Is.Zero);
        }
        [Test] public void MovementHasIntegerCadenceAndContinuesPastThreshold()
        {
            var d = new DetectionSystem(new GameRules(discoveryThreshold: 2)); d.Tick(true, 0, 2);
            d.Tick(true, 1, 0.1); Assert.That(d.Suspicion.Value, Is.EqualTo(1));
            d.Tick(true, 1, 0.9); Assert.That(d.Suspicion.Value, Is.EqualTo(2));
            Assert.That(d.State, Is.EqualTo(DetectionState.Red));
            d.Tick(true, 1, 1); Assert.That(d.State, Is.EqualTo(DetectionState.Discovered));
            d.Reset(); Assert.That(d.State, Is.EqualTo(DetectionState.Green)); Assert.That(d.Suspicion.Value, Is.Zero);
        }
        [Test] public void ImmediateDiscoveryOption()
        {
            var d = new DetectionSystem(new GameRules(discoveryThreshold: 1, continuedMovementToDiscover: false));
            d.Tick(true, 0, 2); d.Tick(true, 1, 0.1);
            Assert.That(d.State, Is.EqualTo(DetectionState.Discovered));
        }
        [Test] public void DiscoveryPersistsUntilExplicitReset()
        {
            var d = new DetectionSystem(new GameRules(discoveryThreshold: 1, continuedMovementToDiscover: false));
            d.Tick(true, 0, 2); d.Tick(true, 1, 1); d.Tick(false, 0, 10);
            Assert.That(d.State, Is.EqualTo(DetectionState.Discovered));
        }
    }
}
