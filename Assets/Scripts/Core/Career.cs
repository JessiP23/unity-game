using System;
namespace NightSupermarket.Core
{
    public enum Gadget { None, WindUpToy, PriceGun }

    /// <summary>
    /// Progress between nights: points earned on winning nights add up, and thresholds unlock gadgets
    /// that change tactics rather than power. Pure arithmetic; the game stores the total.
    /// </summary>
    public static class Career
    {
        public const int WindUpToyAt = 3000, PriceGunAt = 6000;

        public static bool Unlocked(Gadget gadget, int careerPoints) => gadget switch
        {
            Gadget.WindUpToy => careerPoints >= WindUpToyAt,
            Gadget.PriceGun => careerPoints >= PriceGunAt,
            _ => true
        };

        /// <summary>The next thing to earn and how far away it is; null when everything is unlocked.</summary>
        public static (Gadget gadget, int pointsAway)? Next(int careerPoints)
        {
            if (careerPoints < WindUpToyAt) return (Gadget.WindUpToy, WindUpToyAt - careerPoints);
            if (careerPoints < PriceGunAt) return (Gadget.PriceGun, PriceGunAt - careerPoints);
            return null;
        }

        /// <summary>Only a night's positive total counts; a bad night never takes progress away.</summary>
        public static int Add(int careerPoints, int nightTotal) => careerPoints + Math.Max(0, nightTotal);

        public static string Name(Gadget gadget) => gadget switch
        {
            Gadget.WindUpToy => "Wind-up toy",
            Gadget.PriceGun => "Squeaky price gun",
            _ => "Nothing"
        };

        public static string Describe(Gadget gadget) => gadget switch
        {
            Gadget.WindUpToy => "X drops a toy that walks off clattering. The guard follows it, not you. Once a night.",
            Gadget.PriceGun => "Z squeaks at the spot you aim at, up to 14 m away. The guard goes to look. Twice a night.",
            _ => ""
        };

        public static int Uses(Gadget gadget) => gadget == Gadget.PriceGun ? 2 : gadget == Gadget.WindUpToy ? 1 : 0;
    }
}
