using System;
namespace NightSupermarket.Core
{
    public enum MatchPhase { Bootstrap, Lobby, Night, Victory, Defeat }
    public sealed class MatchFlow
    {
        public MatchPhase Phase { get; private set; }
        public bool Ended => Phase == MatchPhase.Victory || Phase == MatchPhase.Defeat;
        public event Action<MatchPhase> Changed;
        public bool TryTransition(MatchPhase next)
        {
            bool valid = (Phase == MatchPhase.Bootstrap && next == MatchPhase.Lobby)
                || (Phase == MatchPhase.Lobby && next == MatchPhase.Night)
                || (Phase == MatchPhase.Night && (next == MatchPhase.Victory || next == MatchPhase.Defeat));
            if (!valid) return false;
            Phase = next; Changed?.Invoke(next); return true;
        }
    }
}
