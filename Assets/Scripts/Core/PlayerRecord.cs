using System;
namespace NightSupermarket.Core
{
    public enum PlayerState { Normal, Running, Jumping, Interacting, Carrying, Throwing, Captured, Surveillance, Escaping, Inactive }
    public sealed class PlayerRecord
    {
        public string Id { get; }
        public PlayerState State { get; private set; }
        public bool Free => State != PlayerState.Captured && State != PlayerState.Surveillance && State != PlayerState.Escaping && State != PlayerState.Inactive;
        public event Action<PlayerState> Changed;
        public PlayerRecord(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id)); Id = id; }
        public void SetState(PlayerState state) { if (State == state) return; State = state; Changed?.Invoke(state); }
    }
}
