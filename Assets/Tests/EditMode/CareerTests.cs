using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class CareerTests
    {
        [Test] public void PointsUnlockGadgetsInOrderAndLossesNeverSubtract()
        {
            int career = 0;
            Assert.That(Career.Unlocked(Gadget.WindUpToy, career), Is.False);
            career = Career.Add(career, 1800);
            career = Career.Add(career, -400);
            Assert.That(career, Is.EqualTo(1800));
            Assert.That(Career.Next(career).Value.gadget, Is.EqualTo(Gadget.WindUpToy));
            Assert.That(Career.Next(career).Value.pointsAway, Is.EqualTo(1200));
            career = Career.Add(career, 1500);
            Assert.That(Career.Unlocked(Gadget.WindUpToy, career), Is.True);
            Assert.That(Career.Unlocked(Gadget.PriceGun, career), Is.False);
            career = Career.Add(career, 3000);
            Assert.That(Career.Unlocked(Gadget.PriceGun, career), Is.True);
            Assert.That(Career.Next(career), Is.Null);
        }
        [Test] public void GuardsRotateWithTheShiftAndShiftZeroIsPlain()
        {
            var plain = GuardRoster.For(0);
            Assert.That(plain.Vision, Is.EqualTo(1));
            Assert.That(plain.Pace, Is.EqualTo(1));
            bool differed = false;
            for (int shift = 1; shift < 30 && !differed; shift++) differed = GuardRoster.For(shift).Name != plain.Name;
            Assert.That(differed, Is.True);
            Assert.That(GuardRoster.For(17).Name, Is.EqualTo(GuardRoster.For(17).Name));
            foreach (var guard in GuardRoster.All) Assert.That(guard.Tell.Length > 20, Is.True, guard.Name + " needs a readable tell");
        }
        [Test] public void AdmirationScores()
        {
            var score = new NightScore();
            Assert.That(score.Add(ScoreEvent.Admired), Is.EqualTo(NightScore.AdmiredPoints));
        }
    }
}
