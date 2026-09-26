using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>Ordered warehouse occupants. Identity is retained; nothing is destroyed.</summary>
    public sealed class WarehouseRoster
    {
        private readonly List<string> order = new List<string>();
        public IReadOnlyList<string> Occupants => order;
        public int Count => order.Count;
        public bool Contains(string id) => !string.IsNullOrEmpty(id) && order.Contains(id);
        public bool Admit(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || order.Contains(id)) return false;
            order.Add(id); return true;
        }
        public bool Release(string id) => !string.IsNullOrEmpty(id) && order.Remove(id);
    }
}
