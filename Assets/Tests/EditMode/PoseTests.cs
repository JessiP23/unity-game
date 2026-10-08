using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class PoseTests
    {
        [Test] public void HoldingIsFreeThenStrainsThenTwitchesOnce()
        {
            var strain = new PoseStrain();
            strain.Begin(Stance.Display);
            Assert.That(strain.Tick(PoseStrain.Comfort), Is.False);
            Assert.That(strain.Strain, Is.Zero);
            Assert.That(strain.ComfortLeft, Is.Zero);
            bool twitched = false;
            for (int i = 0; i < 50 && !twitched; i++) twitched = strain.Tick(0.1);
            Assert.That(twitched, Is.True);
            Assert.That(strain.Twitches, Is.EqualTo(1));
            Assert.That(strain.Strain, Is.EqualTo(0.5));
        }
        [Test] public void LettingGoRecoversTwiceAsFast()
        {
            var strain = new PoseStrain();
            strain.Begin(Stance.Staff);
            strain.Tick(PoseStrain.Comfort + PoseStrain.BuildSeconds * 0.5);
            Assert.That(strain.Strain, Is.EqualTo(0.5).Within(0.001));
            strain.Release();
            strain.Tick(PoseStrain.BuildSeconds * 0.25);
            Assert.That(strain.Strain, Is.EqualTo(0).Within(0.001));
            Assert.That(strain.Holding, Is.False);
        }
        [Test] public void DepartmentsExpectAPose()
        {
            Assert.That(PoseLibrary.ExpectedIn(ZoneType.Clothing), Is.EqualTo(Stance.Display));
            Assert.That(PoseLibrary.ExpectedIn(ZoneType.Home), Is.EqualTo(Stance.Lounging));
            Assert.That(PoseLibrary.ExpectedIn(ZoneType.EntranceExit), Is.EqualTo(Stance.Neutral));
            Assert.That(PoseLibrary.ExpectedIn(null), Is.EqualTo(Stance.Neutral));
        }
    }
}
