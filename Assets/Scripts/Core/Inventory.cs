using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace NightSupermarket.Core
{
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly Dictionary<string, int> costs = new Dictionary<string, int>();
        public int Capacity { get; }
        public int UsedSlots { get; private set; }
        public event Action Changed;
        public Inventory(int capacity) { if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity = capacity; }
        public int Count(string id) => id != null && counts.TryGetValue(id, out int count) ? count : 0;
        public bool TryAdd(string id, int quantity, int slotCost)
        {
            if (string.IsNullOrWhiteSpace(id) || quantity <= 0 || slotCost < 0) return false;
            if (costs.TryGetValue(id, out int existing) && existing != slotCost) return false;
            long required = (long)quantity * slotCost;
            if (required > Capacity - UsedSlots || (long)Count(id) + quantity > int.MaxValue) return false;
            counts[id] = Count(id) + quantity; costs[id] = slotCost; UsedSlots += (int)required; Changed?.Invoke(); return true;
        }
        public bool TryRemove(string id, int quantity)
        {
            if (quantity <= 0 || Count(id) < quantity) return false;
            counts[id] -= quantity; UsedSlots -= costs[id] * quantity;
            if (counts[id] == 0) { counts.Remove(id); costs.Remove(id); }
            Changed?.Invoke(); return true;
        }
        public IReadOnlyDictionary<string, int> Snapshot() => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(counts));
    }
}
