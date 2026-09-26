namespace NightSupermarket.Core
{
    /// <summary>Per-night tallies for the end-of-night grade. Counts facts; never changes match state.</summary>
    public sealed class NightStats
    {
        public int Reports { get; private set; }
        public int Captures { get; private set; }
        public int Rescues { get; private set; }
        public void RecordReport() => Reports++;
        public void RecordCapture() => Captures++;
        public void RecordRescue() => Rescues++;

        /// <summary>S for a clean escape nobody noticed, down to D for a noisy night.</summary>
        public string Grade(bool victory, double secondsLeft)
        {
            if (!victory) return "D";
            int penalty = Reports + Captures * 2;
            if (penalty == 0 && secondsLeft > 60) return "S";
            if (penalty <= 1) return "A";
            if (penalty <= 3) return "B";
            return "C";
        }
    }
}
