using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>Records mannequins who have left the supermarket. Identity remains in the match.</summary>
    public sealed class EscapeLedger
    {
        private readonly HashSet<string> ids = new HashSet<string>();
        public int Count => ids.Count;
        public bool Contains(string id) => ids.Contains(id);
        public bool TryMark(PlayerRecord player)
        {
            if (player == null || player.Role != PlayerRole.Mannequin || !player.Free) return false;
            if (!ids.Add(player.Id)) return false;
            player.SetState(PlayerState.Escaped);
            return true;
        }
    }
}
