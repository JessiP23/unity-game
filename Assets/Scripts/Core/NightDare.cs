using System;
namespace NightSupermarket.Core
{
    /// <summary>One small, funny challenge per night, the same for everyone on the daily shift.</summary>
    public enum Dare { Selfie, CarouselRide, Wish, Unreported, AllGems, NeverRun, FireExit }

    /// <summary>
    /// The nightly dare: a seeded side goal with a fixed reward (points and one gem) and a daily
    /// streak. It is the "come back tomorrow" hook without timers that punish you for missing a
    /// day: a broken streak just starts again at 1, nothing is taken away.
    /// </summary>
    public static class NightDare
    {
        public const int Points = 300;
        public const float CarouselSeconds = 20f;
        public static readonly Dare[] All = { Dare.Selfie, Dare.CarouselRide, Dare.Wish, Dare.Unreported, Dare.AllGems, Dare.NeverRun, Dare.FireExit };

        /// <summary>Shift 0 (tests, the classic layout) always dares a selfie; any other shift draws from the list.</summary>
        public static Dare For(int shift)
        {
            if (shift == 0) return Dare.Selfie;
            var rng = new Random(shift * 7907 + 3);
            return All[rng.Next(All.Length)];
        }

        public static string Title(Dare dare) => dare switch
        {
            Dare.Selfie => "Photobomb",
            Dare.CarouselRide => "Merry-go-round",
            Dare.Wish => "Make a wish",
            Dare.Unreported => "Nobody saw a thing",
            Dare.AllGems => "Treasure hunter",
            Dare.NeverRun => "Slow and steady",
            _ => "Fire drill"
        };

        public static string Describe(Dare dare) => dare switch
        {
            Dare.Selfie => "Get a shopper to take your photo: hold a pose near one.",
            Dare.CarouselRide => "Ride the toy-store carousel for " + (int)CarouselSeconds + " seconds while it turns (E on a horse to climb on).",
            Dare.Wish => "Toss a coin in the atrium fountain, then escape.",
            Dare.Unreported => "Escape without a single report from a shopper or a camera.",
            Dare.AllGems => "Find every hidden gem tonight (they glint after lights out).",
            Dare.NeverRun => "Escape without ever running. Not once.",
            _ => "Escape through the upstairs fire exit."
        };

        /// <summary>
        /// The streak after tonight. Days are whole days since an epoch (the daily shift number).
        /// Same day twice keeps the streak; the next day adds one; a gap starts again at one.
        /// </summary>
        public static int NextStreak(int lastDay, int streak, int today)
        {
            if (streak <= 0 || lastDay <= 0) return 1;
            if (today == lastDay) return streak;
            if (today == lastDay + 1) return streak + 1;
            return 1;
        }
    }

    /// <summary>What the fountain grants when you toss a coin. One wish a night, the same for everyone that day.</summary>
    public enum Wish { Fortune, Gem, Hush }

    public static class FountainWish
    {
        public const int FortunePoints = 150;
        public const float HushSeconds = 45f;
        /// <summary>How loud the splash is: about a dropped crate. It brings the guard to look.</summary>
        public const float SplashLoudness = 1.3f;

        public static Wish For(int shift)
        {
            var rng = new Random(shift * 3571 + 11);
            int roll = rng.Next(10);
            return roll < 5 ? Wish.Fortune : roll < 8 ? Wish.Hush : Wish.Gem;
        }

        public static string Describe(Wish wish) => wish switch
        {
            Wish.Fortune => "Fortune: +" + FortunePoints + " points.",
            Wish.Gem => "A gem washes up: +1 gem for the wardrobe.",
            _ => "Hush: for " + (int)HushSeconds + " seconds no shopper will report you."
        };
    }
}
