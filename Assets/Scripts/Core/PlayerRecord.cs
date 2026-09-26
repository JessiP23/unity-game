using System;
namespace NightSupermarket.Core
{
    public enum PlayerState { Normal, Running, Jumping, Interacting, Carrying, Throwing, Captured, Surveillance, Escaping, Escaped, Inactive }
    public enum PlayerRole { Mannequin, Guard }
    public sealed class PlayerRecord
    {
        public string Id { get; }
        public PlayerRole Role { get; }
        public PlayerState State { get; private set; }
        public bool Free => State != PlayerState.Captured && State != PlayerState.Surveillance && State != PlayerState.Escaping && State != PlayerState.Escaped && State != PlayerState.Inactive;
        public event Action<PlayerState> Changed;
        public PlayerRecord(string id, PlayerRole role = PlayerRole.Mannequin)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            Id = id; Role = role;
        }
        public void SetState(PlayerState state) { if (State == state) return; State = state; Changed?.Invoke(state); }
    }
}
