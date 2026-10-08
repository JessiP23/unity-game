using NUnit.Framework;
using System;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class DetectionTests
    {
        [Test] public void OrangeExpiresAndAShortBreakInSightResumesTheSameGrace()
        {
            var d = new DetectionSystem(new GameRules(orangeDuration: 1));
            d.Tick(true, 0, 0.4); Assert.That(d.State, Is.EqualTo(DetectionState.Orange));
            Assert.That(d.FreshSighting, Is.True);
            d.Tick(false, 0, 0.1); Assert.That(d.State, Is.EqualTo(DetectionState.Green), "live sight is gone at once");
            Assert.That(d.AttentionLingering, Is.True, "the guard remembers for a moment");
            d.Tick(true, 0, 0.5); Assert.That(d.State, Is.EqualTo(DetectionState.Orange));
            Assert.That(d.FreshSighting, Is.False, "a resumed sighting is not a new one");
            Assert.That(d.GraceRemaining, Is.EqualTo(0.3).Within(0.001), "0.4 seen, 0.2 drained, 0.5 seen: 0.7 of 1.0 used");
            d.Tick(true, 0, 0.3); Assert.That(d.State, Is.EqualTo(DetectionState.Red));
        }
        [Test] public void LongEnoughOutOfSightGivesAFreshGrace()
        {
            var d = new DetectionSystem(new GameRules(orangeDuration: 1));
            d.Tick(true, 0, 0.9);
            d.Tick(false, 0, 0.45); Assert.That(d.AttentionLingering, Is.False, "0.9 of attention drains in 0.45 s at drain 2");
            d.Tick(true, 0, 0.5); Assert.That(d.State, Is.EqualTo(DetectionState.Orange));
            Assert.That(d.FreshSighting, Is.True);
            Assert.That(d.GraceRemaining, Is.EqualTo(0.5).Within(0.001));
        }
        [Test] public void FlickeringSightCannotBeWalkedThrough()
        {
            var d = new DetectionSystem(new GameRules(orangeDuration: 1.5));
            double t = 0;
            while (t < 30 && d.State != DetectionState.Discovered)
            { bool seen = (t % 1.42) > 0.02; d.Tick(seen, 3.0, 0.02); t += 0.02; }
            Assert.That(d.State, Is.EqualTo(DetectionState.Discovered), "one-frame breaks in line of sight no longer reset the guard");
            Assert.That(t, Is.LessThan(10));
        }
        [Test] public void SuspicionIsForgivenSlowlyWhileUnseen()
        {
            var d = new DetectionSystem(new GameRules(suspicionDecay: 30));
            d.Tick(true, 0, 2); d.Tick(true, 1, 0.1); d.Tick(true, 1, 1);
            Assert.That(d.Suspicion.Value, Is.EqualTo(2));
            d.Tick(false, 2, 29.9);
            Assert.That(d.Suspicion.Value, Is.EqualTo(2), "not yet");
            Assert.That(d.Suspicion.NextForgiveness, Is.EqualTo(0.1).Within(0.01));
            d.Tick(false, 2, 0.2);
            Assert.That(d.Suspicion.Value, Is.EqualTo(1));
            d.Tick(true, 0, 5);
            Assert.That(d.Suspicion.Value, Is.EqualTo(1), "being watched pauses forgetting");
            d.Tick(false, 0, 30);
            Assert.That(d.Suspicion.Value, Is.Zero);
            Assert.That(d.Suspicion.NextForgiveness, Is.Zero);
        }
        [Test] public void ZeroDecayKeepsTheOldPermanentMemory()
        {
            var d = new DetectionSystem(new GameRules(suspicionDecay: 0));
            d.Tick(true, 0, 2); d.Tick(true, 1, 0.1);
            d.Tick(false, 0, 600);
            Assert.That(d.Suspicion.Value, Is.EqualTo(1));
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
