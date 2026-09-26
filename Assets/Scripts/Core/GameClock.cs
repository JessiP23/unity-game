using System;
namespace NightSupermarket.Core
{
    public sealed class GameClock
    {
        public double Remaining { get; private set; }
        public bool Paused { get; set; }
        public event Action Dawn;
        public GameClock(double duration) { GameRules.RequirePositive(duration, nameof(duration)); Remaining = duration; }
        public void Tick(double delta)
        {
            GameRules.RequireDelta(delta);
            if (Paused || Remaining == 0) return;
            Remaining = Math.Max(0, Remaining - delta);
            if (Remaining == 0) Dawn?.Invoke();
        }
    }
}
