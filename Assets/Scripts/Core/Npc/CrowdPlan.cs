using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>One flash sale: when it is called (as a share of the night still to go), how long it runs, and which candidate zone it uses.</summary>
    public readonly struct FlashSale
    {
        /// <summary>Share of the night remaining when the sale opens (1 = start, 0 = dawn).</summary>
        public readonly double OpensAt;
        public readonly double Seconds;
        /// <summary>Seeded roll used to choose among tonight's candidate zones.</summary>
        public readonly int Pick;
        public FlashSale(double opensAt, double seconds, int pick) { OpensAt = opensAt; Seconds = seconds; Pick = pick; }
    }

    /// <summary>What a flash sale is doing right now, for HUD copy and the population.</summary>
    public enum SaleStage { None, Announced, Open }

    /// <summary>
    /// How busy the mall is. The crowd ebbs and flows on a slow wave, grows as the player gets more nights
    /// behind them, thins as the store closes, and surges into one department during a flash sale.
    /// The frame-rate budget always has the last word.
    /// </summary>
    public static class CrowdPlan
    {
        public const int MaxLevel = 8;
        /// <summary>Peak crowd at the top level, relative to level 0.</summary>
        public const double LevelStep = 0.1;
        public const double SaleBoost = 1.3;
        /// <summary>Chance a shopper choosing where to go next takes the detour to the sale.</summary>
        public const double SaleLure = 0.7;
        /// <summary>Chance a shopper already walking or browsing drops it and heads for the sale when it opens.</summary>
        public const double SaleRedirect = 0.45;
        public const double AnnounceSeconds = 10, SaleSeconds = 50;

        /// <summary>The first two nights are level 0; each night after adds one, up to <see cref="MaxLevel"/>.</summary>
        public static int Level(int nightsPlayed) => Math.Max(0, Math.Min(MaxLevel, nightsPlayed - 1));

        public static double LevelFactor(int level) => 1 + LevelStep * Math.Max(0, Math.Min(MaxLevel, level));

        public static string LevelName(int level) => level <= 0 ? "Steady" : level <= 2 ? "Busy" : level <= 4 ? "Bustling" : level <= 6 ? "Packed" : "Heaving";

        /// <summary>
        /// Shoppers who should be on the floor. <paramref name="phaseCrowd"/> is the night schedule's share
        /// (1 open, 0.5 closing, …); <paramref name="cap"/> is the live performance budget.
        /// </summary>
        public static int Target(int minimum, int maximum, double elapsed, double cycleSeconds, double phaseCrowd, int level, bool saleOpen, int cap, double difficulty = 1)
        {
            double wave = 0.5 + 0.5 * Math.Sin(elapsed / Math.Max(1, cycleSeconds) * Math.PI * 2);
            double people = (minimum + (maximum - minimum) * wave) * LevelFactor(level) * Math.Max(0, difficulty) * Math.Max(0, Math.Min(1, phaseCrowd));
            if (saleOpen && phaseCrowd > 0) people = Math.Max(people * SaleBoost, minimum * 0.75);
            return Math.Max(0, Math.Min(cap, (int)Math.Round(people)));
        }

        /// <summary>People arrive in ones, couples and small groups; a big shortfall brings bigger groups.</summary>
        public static int Arrivals(int shortfall, double roll)
        {
            if (shortfall <= 0) return 0;
            int group = roll < 0.45 ? 1 : roll < 0.8 ? 2 : roll < 0.95 ? 3 : 4;
            if (shortfall > 8) group++;
            return Math.Min(shortfall, group);
        }

        /// <summary>Seconds until the next arrival: quicker while the mall is far below its target.</summary>
        public static double ArrivalGap(double baseInterval, int shortfall, double roll)
        {
            double urgency = shortfall > 8 ? 0.35 : shortfall > 4 ? 0.6 : 1;
            return baseInterval * urgency * (0.6 + 0.8 * Math.Max(0, Math.Min(1, roll)));
        }

        /// <summary>
        /// Two sales a night: one while the store is open, one at closing time. Rush hour squeezes in a third
        /// (each a little shorter, so they never overlap). Same shift, same sales.
        /// </summary>
        public static IReadOnlyList<FlashSale> SalesFor(int shift, int count = 2)
        {
            var random = new Random(shift * 6007 + 17);
            if (count >= 3)
                return new[]
                {
                    new FlashSale(0.9 - random.NextDouble() * 0.02, RushSaleSeconds, random.Next(1000)),
                    new FlashSale(0.7 - random.NextDouble() * 0.02, RushSaleSeconds, random.Next(1000)),
                    new FlashSale(0.52 - random.NextDouble() * 0.02, RushSaleSeconds, random.Next(1000)),
                };
            return new[]
            {
                new FlashSale(0.88 - random.NextDouble() * 0.1, SaleSeconds, random.Next(1000)),
                new FlashSale(0.6 - random.NextDouble() * 0.08, SaleSeconds, random.Next(1000)),
            };
        }

        public const double RushSaleSeconds = 40;

        /// <summary>The sale in progress or about to be called, if any, at this point of the night.</summary>
        public static SaleStage StageAt(IReadOnlyList<FlashSale> sales, double remaining, double duration, out int index, out double secondsLeft)
        {
            index = -1; secondsLeft = 0;
            if (sales == null || duration <= 0) return SaleStage.None;
            for (int i = 0; i < sales.Count; i++)
            {
                double opens = sales[i].OpensAt * duration;
                double untilOpen = remaining - opens;
                if (untilOpen > 0 && untilOpen <= AnnounceSeconds) { index = i; secondsLeft = untilOpen; return SaleStage.Announced; }
                double runFor = sales[i].Seconds - (opens - remaining);
                if (untilOpen <= 0 && runFor > 0) { index = i; secondsLeft = runFor; return SaleStage.Open; }
            }
            return SaleStage.None;
        }
    }

    /// <summary>
    /// The mall's live performance budget: how many shoppers this machine can show smoothly.
    /// Fed a smoothed frame time every couple of seconds; drops quickly when frames are slow, grows back slowly.
    /// </summary>
    public static class CrowdBudget
    {
        public static int Next(int current, double frameMs, double targetMs, int floor, int ceiling)
        {
            int next = current;
            if (frameMs > targetMs * 1.25) next = current - 3;
            else if (frameMs > targetMs * 1.1) next = current - 1;
            else if (frameMs < targetMs * 0.85) next = current + 1;
            return Math.Max(floor, Math.Min(ceiling, next));
        }
    }

    /// <summary>Where the crowd is being pulled right now. Shared by every shopper's brain; changed by the sale director.</summary>
    public sealed class CrowdPull
    {
        public ZoneType? Zone { get; private set; }
        /// <summary>Bumped for each new sale, so a shopper takes each sale's detour at most once.</summary>
        public int Version { get; private set; }
        public double Lure { get; set; } = CrowdPlan.SaleLure;
        public bool Active => Zone.HasValue;
        public void Open(ZoneType zone) { Zone = zone; Version++; }
        public void Close() => Zone = null;
    }
}
