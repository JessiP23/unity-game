using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Injectable diagnostic sink; presentation can replace this without changing rules.</summary>
    public sealed class GameLog
    {
        public bool Enabled { get; set; }
        public GameLog(bool enabled) { Enabled = enabled; }
        public void Write(string category, string message)
        { if (Enabled) Debug.Log($"[{category}] {message}"); }
    }
}
