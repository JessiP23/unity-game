using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class GuardTests
    {
        [Test] public void PatrolInvestigateSearchReturn()
        {
            var brain = new GuardBrain(1, 2);
            brain.Tick(true, false, false, 1); Assert.That(brain.AdvancePatrol, Is.True);
            brain.Hear(); Assert.That(brain.State, Is.EqualTo(GuardState.Investigate));
            brain.Tick(true, false, false, 0.1); Assert.That(brain.State, Is.EqualTo(GuardState.Search));
            brain.Tick(false, false, false, 2); Assert.That(brain.State, Is.EqualTo(GuardState.ReturnToPatrol));
            brain.Tick(true, false, false, 0.1); Assert.That(brain.State, Is.EqualTo(GuardState.Patrol));
        }
        [Test] public void ChaseCapturesAndIgnoresNoiseUntilDone()
        {
            var brain = new GuardBrain(1, 2); brain.Tick(false, true, false, 0.1);
            Assert.That(brain.State, Is.EqualTo(GuardState.Chase)); brain.Hear();
            Assert.That(brain.State, Is.EqualTo(GuardState.Chase));
            brain.Tick(false, true, true, 0.1); Assert.That(brain.State, Is.EqualTo(GuardState.Capture));
            brain.Tick(false, false, false, 0.1); Assert.That(brain.State, Is.EqualTo(GuardState.ReturnToPatrol));
        }
        [Test] public void LosingChaseSearchesLastKnownPosition()
        {
            var brain = new GuardBrain(1, 2); brain.Tick(false, true, false, 0.1);
            brain.Tick(false, false, false, 0.1); Assert.That(brain.State, Is.EqualTo(GuardState.Search));
        }
    }
}
