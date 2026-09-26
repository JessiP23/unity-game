using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public interface IPlayerSession
    {
        bool CanControl(string connection, string player);
    }
    /// <summary>Offline ownership boundary. Connection IDs must come from transport, never client payloads.</summary>
    public sealed class LocalSession : IPlayerSession
    {
        private readonly HashSet<string> players = new HashSet<string>();
        public string Join() { string id = Guid.NewGuid().ToString("N"); players.Add(id); return id; }
        public bool CanControl(string connection, string player) => players.Contains(connection) && connection == player;
    }
}
