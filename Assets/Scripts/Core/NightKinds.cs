using System;
namespace NightSupermarket.Core
{
    /// <summary>Which night you clock in for: tonight's ordinary shift or one of the special nights.</summary>
    public enum NightKind { Regular, BlackFriday, Blackout, Heist, Birthday }

    /// <summary>
    /// The special nights, as rules. Each changes one big thing and gives you something to go for:
    /// Black Friday drowns the mall in shoppers and doorbuster sales; a blackout leaves only phone torches and the
    /// guard's flashlight; the heist puts a diamond in a glass case while a fashion show pulls every eye; the birthday
    /// party fills the toy store with kids who only look away when the cake comes out.
    /// </summary>
    public static class NightKinds
    {
        public static readonly NightKind[] Specials = { NightKind.Blackout, NightKind.Birthday, NightKind.BlackFriday, NightKind.Heist };

        /// <summary>Nights you need behind you before a special night opens (the ordinary shift is always open).</summary>
        public static int UnlocksAfter(NightKind kind) => kind switch
        {
            NightKind.Blackout => 1,
            NightKind.Birthday => 2,
            NightKind.BlackFriday => 3,
            NightKind.Heist => 4,
            _ => 0
        };

        public static bool Unlocked(NightKind kind, int nightsPlayed) => nightsPlayed >= UnlocksAfter(kind);

        /// <summary>One special night is featured each day (same for everyone) and pays a bonus.</summary>
        public static NightKind Featured(int dailyShift) => Specials[((dailyShift % Specials.Length) + Specials.Length) % Specials.Length];
        public const double FeaturedBonus = 1.25;

        public static string Name(NightKind kind) => kind switch
        {
            NightKind.BlackFriday => "Black Friday",
            NightKind.Blackout => "Blackout",
            NightKind.Heist => "The Heist",
            NightKind.Birthday => "Birthday Party",
            _ => "Tonight's shift"
        };

        public static string Describe(NightKind kind) => kind switch
        {
            NightKind.BlackFriday => "Doors open early. The biggest crowd ever and four doorbuster sales. Hide in the stampede.",
            NightKind.Blackout => "The power's out. Shoppers see only by phone torch, the guard's flashlight never goes off. Move between the beams.",
            NightKind.Heist => "A diamond in a glass case. Lift it while the fashion show has every eye, then get out with it.",
            NightKind.Birthday => "Kids have taken over the toy store and see everything. Grab the present when the cake comes out.",
            _ => "The mall as it is tonight: today's store, guard and dare."
        };

        /// <summary>Share of the usual crowd.</summary>
        public static double CrowdFactor(NightKind kind) => kind switch
        {
            NightKind.BlackFriday => 1.7,
            NightKind.Blackout => 0.6,
            _ => 1
        };

        public static double ScoreFactor(NightKind kind) => kind switch
        {
            NightKind.BlackFriday => 1.2,
            NightKind.Blackout => 1.15,
            NightKind.Birthday => 1.1,
            _ => 1
        };

        /// <summary>Flash sales tonight (Black Friday's are short doorbusters).</summary>
        public static int Sales(NightKind kind, int difficultySales) => kind == NightKind.BlackFriday ? 4 : kind == NightKind.Blackout ? 1 : difficultySales;

        /// <summary>The mall's lights for a phase of the night: a blackout keeps them off until the emergency lights.</summary>
        public static LightingMode Lighting(NightKind kind, LightingMode scheduled) =>
            kind == NightKind.Blackout && (scheduled == LightingMode.Normal || scheduled == LightingMode.Partial) ? LightingMode.Dark : scheduled;

        public static bool GuardTorchAllNight(NightKind kind) => kind == NightKind.Blackout;

        // ---- the special objectives ----
        public const int DiamondPoints = 1000, PresentPoints = 600;
        /// <summary>The fashion show and the cake: a 20 s distraction about once a minute, starting after a short wait.</summary>
        public const double ShowEvery = 60, ShowSeconds = 20, ShowWarning = 6, FirstShowAt = 35;

        /// <summary>Is the night's big distraction on (or about to be)? <paramref name="untilChange"/> counts down to the next boundary.</summary>
        public static SaleStage ShowAt(double elapsed, out double untilChange)
        {
            if (elapsed < FirstShowAt - ShowWarning) { untilChange = FirstShowAt - elapsed; return SaleStage.None; }
            double t = (elapsed - FirstShowAt) % ShowEvery;
            if (elapsed < FirstShowAt) { untilChange = FirstShowAt - elapsed; return SaleStage.Announced; }
            if (t < ShowSeconds) { untilChange = ShowSeconds - t; return SaleStage.Open; }
            double next = ShowEvery - t;
            if (next <= ShowWarning) { untilChange = next; return SaleStage.Announced; }
            untilChange = next; return SaleStage.None;
        }

        /// <summary>Blackout sight: a shopper with a phone torch sees down its beam; one without sees only what is close.</summary>
        public static (double range, double fieldOfView) BlackoutSight(bool torch) => torch ? (8.0, 38.0) : (3.0, 70.0);
        public const double TorchShare = 0.45;
    }
}
