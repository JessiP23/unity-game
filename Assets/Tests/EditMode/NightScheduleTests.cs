using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class NightScheduleTests
    {
        [Test] public void TheNightDarkensInFourSteps()
        {
            Assert.That(NightSchedule.At(360, 360).Phase, Is.EqualTo(NightPhase.Open));
            Assert.That(NightSchedule.At(240, 360).Phase, Is.EqualTo(NightPhase.Closing));
            Assert.That(NightSchedule.At(240, 360).Lighting, Is.EqualTo(LightingMode.Partial));
            Assert.That(NightSchedule.At(120, 360).Phase, Is.EqualTo(NightPhase.Dark));
            Assert.That(NightSchedule.At(120, 360).Flashlight, Is.True);
            Assert.That(NightSchedule.At(60, 360).Phase, Is.EqualTo(NightPhase.Lockdown));
            Assert.That(NightSchedule.At(60, 360).Lighting, Is.EqualTo(LightingMode.Emergency));
            Assert.That(NightSchedule.At(0, 360).Phase, Is.EqualTo(NightPhase.Dawn));
        }
        [Test] public void GuardGetsQuickerAndCrowdThins()
        {
            Assert.That(NightSchedule.At(300, 360).GuardPace, Is.EqualTo(1));
            Assert.That(NightSchedule.At(100, 360).GuardPace > NightSchedule.At(200, 360).GuardPace, Is.True);
            Assert.That(NightSchedule.At(100, 360).Crowd < NightSchedule.At(300, 360).Crowd, Is.True);
        }
        [Test] public void CountdownToTheNextPhase()
        {
            Assert.That(NightSchedule.UntilNextPhase(360, 360), Is.EqualTo(120).Within(0.001));
            Assert.That(NightSchedule.UntilNextPhase(130, 360), Is.EqualTo(10).Within(0.001));
            Assert.That(NightSchedule.UntilNextPhase(30, 360), Is.Zero);
        }
    }
}
