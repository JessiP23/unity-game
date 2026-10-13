using System.Collections.Generic;
using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class DareTests
    {
        [Test] public void DaresAreSeededAndAllAppear()
        {
            Assert.That(NightDare.For(0), Is.EqualTo(Dare.Selfie));
            var seen = new HashSet<Dare>();
            for (int shift = 1; shift < 200; shift++)
            {
                Assert.That(NightDare.For(shift), Is.EqualTo(NightDare.For(shift)));
                seen.Add(NightDare.For(shift));
            }
            Assert.That(seen.Count, Is.EqualTo(NightDare.All.Length));
            foreach (var dare in NightDare.All)
            {
                Assert.That(NightDare.Title(dare), Is.Not.Empty);
                Assert.That(NightDare.Describe(dare), Is.Not.Empty);
            }
        }
        [Test] public void StreakGrowsDayByDayAndRestartsAfterAGap()
        {
            Assert.That(NightDare.NextStreak(0, 0, 100), Is.EqualTo(1));
            Assert.That(NightDare.NextStreak(100, 1, 101), Is.EqualTo(2));
            Assert.That(NightDare.NextStreak(101, 2, 101), Is.EqualTo(2), "two dares the same day count once");
            Assert.That(NightDare.NextStreak(101, 5, 103), Is.EqualTo(1), "a missed day starts again, nothing is taken");
        }
        [Test] public void WishesAreSeededAndScore()
        {
            var seen = new HashSet<Wish>();
            for (int shift = 0; shift < 100; shift++) { Assert.That(FountainWish.For(shift), Is.EqualTo(FountainWish.For(shift))); seen.Add(FountainWish.For(shift)); }
            Assert.That(seen.Count, Is.EqualTo(3));
            Assert.That(new NightScore().Add(ScoreEvent.Wish), Is.EqualTo(FountainWish.FortunePoints));
            Assert.That(new NightScore().Add(ScoreEvent.Dare), Is.EqualTo(NightDare.Points));
            foreach (Wish wish in System.Enum.GetValues(typeof(Wish))) Assert.That(FountainWish.Describe(wish), Is.Not.Empty);
        }
    }
}
