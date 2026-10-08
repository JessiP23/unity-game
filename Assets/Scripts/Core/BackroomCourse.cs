using System;

namespace NightSupermarket.Core
{
    /// <summary>
    /// StillLight: cross while the sweeping beam is off you. LiarDoors: open the door whose number
    /// matches the lit lamps. EchoTiles: repeat the flashed pad order. RedLight: a camera cycles
    /// green and red; move only on green. OddOneOut: five mannequins, one faces the wrong way.
    /// PriceTags: three prices show briefly; pick the dearest.
    /// </summary>
    public enum BackroomTrial { StillLight, LiarDoors, EchoTiles, RedLight, OddOneOut, PriceTags }

    /// <summary>
    /// One seeded trip through the back hall. Three of six rooms, in a random order, with random
    /// answers, so no two captures play the same. Failing a room does not erase rooms already
    /// cleared. Difficulty (captures so far tonight) lengthens the echo and speeds the lights.
    /// </summary>
    public sealed class BackroomCourse
    {
        public const int Figures = 5, Rooms = 3;
        private readonly BackroomTrial[] trials;
        private readonly int[] doorMarks;
        private readonly int[] echo;
        private readonly int[] prices;
        public int Seed { get; }
        public int Difficulty { get; }
        public int Room { get; private set; }
        public int LitCount { get; }
        public double BeamSeconds { get; }
        public double GreenSeconds { get; }
        public double RedSeconds { get; }
        public int OddFigure { get; }
        public int EchoProgress { get; private set; }
        public int Attempts { get; private set; }
        public bool Complete => Room >= trials.Length;
        public int Length => trials.Length;
        public int EchoLength => echo.Length;
        public int PriceAnswer { get; }

        public BackroomCourse(int seed, int difficulty = 0)
        {
            Seed = seed;
            Difficulty = Math.Max(0, difficulty);
            var rng = new Random(seed);
            var pool = (BackroomTrial[])Enum.GetValues(typeof(BackroomTrial));
            Shuffle(pool, rng);
            trials = new BackroomTrial[Rooms];
            Array.Copy(pool, trials, Rooms);

            LitCount = rng.Next(1, 4);
            int truth = rng.Next(0, 3);
            doorMarks = new int[3];
            var decoys = new[] { 0, 1, 2, 3, 4 };
            int decoy = 0;
            for (int i = 0; i < decoys.Length; i++)
                if (decoys[i] != LitCount) decoys[decoy++] = decoys[i];
            // Every door shows a different number, so the room is a counting puzzle, not a coin toss.
            Shuffle(decoys, decoy, rng);
            int used = 0;
            for (int i = 0; i < doorMarks.Length; i++)
                doorMarks[i] = i == truth ? LitCount : decoys[used++];

            echo = new int[4 + Math.Min(Difficulty, 2)];
            for (int i = 0; i < echo.Length; i++)
            {
                int pad = rng.Next(4);
                if (i > 0 && pad == echo[i - 1]) pad = (pad + 1 + rng.Next(3)) % 4;
                echo[i] = pad;
            }
            double quicker = 1 - Math.Min(Difficulty, 3) * 0.1;
            BeamSeconds = (0.85 + rng.NextDouble() * 0.7) * quicker;
            GreenSeconds = (1.6 + rng.NextDouble() * 0.8) * quicker;
            RedSeconds = 1.2 + rng.NextDouble() * 0.6;
            OddFigure = rng.Next(Figures);
            prices = new int[3];
            for (int i = 0; i < prices.Length; i++)
            {
                int price;
                do price = 3 + rng.Next(40); while (Array.IndexOf(prices, price, 0, i) >= 0);
                prices[i] = price;
            }
            PriceAnswer = Array.IndexOf(prices, Math.Max(prices[0], Math.Max(prices[1], prices[2])));
        }

        public BackroomTrial TrialAt(int room) => trials[room];
        public BackroomTrial Trial => Complete ? trials[trials.Length - 1] : trials[Room];
        public int DoorMark(int door) => doorMarks[door];
        public int EchoAt(int step) => echo[step];
        public int Price(int tag) => prices[tag];
        /// <summary>Which way figure i faces: true = toward the player, except the odd one.</summary>
        public bool FigureFacesYou(int figure) => figure != OddFigure;

        /// <summary>Red/green camera state at a hall-local time. Starts green so the room reads before it bites.</summary>
        public bool RedLightIsRed(double time)
        {
            double cycle = GreenSeconds + RedSeconds;
            double phase = time % cycle;
            if (phase < 0) phase += cycle;
            return phase >= GreenSeconds;
        }

        public bool TryPassStill(bool insideBeam) => TryGate(BackroomTrial.StillLight, !insideBeam);
        public bool TryPassRedLight(bool isRed) => TryGate(BackroomTrial.RedLight, !isRed);

        public bool TryDoor(int door)
        {
            if (Complete || Trial != BackroomTrial.LiarDoors || door < 0 || door >= doorMarks.Length) return false;
            return TryGate(BackroomTrial.LiarDoors, doorMarks[door] == LitCount);
        }

        public bool TryFigure(int figure)
        {
            if (Complete || Trial != BackroomTrial.OddOneOut || figure < 0 || figure >= Figures) return false;
            return TryGate(BackroomTrial.OddOneOut, figure == OddFigure);
        }

        public bool TryPrice(int tag)
        {
            if (Complete || Trial != BackroomTrial.PriceTags || tag < 0 || tag >= prices.Length) return false;
            return TryGate(BackroomTrial.PriceTags, tag == PriceAnswer);
        }

        /// <summary>Wrong pads restart the echo. The room stays.</summary>
        public bool TryPad(int pad)
        {
            if (Complete || Trial != BackroomTrial.EchoTiles) return false;
            if (pad != echo[EchoProgress])
            {
                EchoProgress = 0;
                Attempts++;
                return false;
            }
            EchoProgress++;
            if (EchoProgress >= echo.Length) Advance();
            return true;
        }

        private bool TryGate(BackroomTrial trial, bool correct)
        {
            if (Complete || Trial != trial) return false;
            if (!correct) { Attempts++; return false; }
            Advance();
            return true;
        }

        private void Advance()
        {
            Room++;
            EchoProgress = 0;
        }

        private static void Shuffle<T>(T[] items, Random rng) => Shuffle(items, items.Length, rng);
        private static void Shuffle<T>(T[] items, int count, Random rng)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                var held = items[i];
                items[i] = items[swap];
                items[swap] = held;
            }
        }
    }
}
