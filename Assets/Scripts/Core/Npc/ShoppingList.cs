using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public readonly struct ShoppingItem
    {
        public readonly ZoneType Department;
        public readonly string Name;
        public readonly int Quantity;
        public ShoppingItem(ZoneType department, string name, int quantity)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            Department = department; Name = name; Quantity = quantity;
        }
        public override string ToString() => Quantity > 1 ? $"{Name} x{Quantity}" : Name;
    }

    /// <summary>What a customer came for. It exists to give each shopper a believable route, not an economy.</summary>
    public sealed class ShoppingList
    {
        private readonly List<ShoppingItem> items;
        public IReadOnlyList<ShoppingItem> Items => items;
        public int Picked { get; private set; }
        public bool Done => Picked >= items.Count;
        public ShoppingItem? Current => Done ? (ShoppingItem?)null : items[Picked];
        public ShoppingList(IEnumerable<ShoppingItem> entries) { items = new List<ShoppingItem>(entries ?? throw new ArgumentNullException(nameof(entries))); }
        public void MarkPicked() { if (!Done) Picked++; }
        /// <summary>Drops the current item, e.g. when its department is unreachable.</summary>
        public void Skip() => MarkPicked();
    }

    /// <summary>Items each department sells, used to build shopping lists.</summary>
    public sealed class ShoppingCatalog
    {
        private readonly Dictionary<ZoneType, string[]> stock = new Dictionary<ZoneType, string[]>();
        public ShoppingCatalog Add(ZoneType department, params string[] items)
        {
            if (items == null || items.Length == 0) throw new ArgumentException(nameof(items));
            stock[department] = items;
            return this;
        }
        public IEnumerable<ZoneType> Departments => stock.Keys;
        public bool Sells(ZoneType department) => stock.ContainsKey(department);
        public string Pick(ZoneType department, Random random) => stock[department][random.Next(stock[department].Length)];

        public static ShoppingCatalog Default { get; } = new ShoppingCatalog()
            .Add(ZoneType.Supermarket, "Milk", "Bread", "Canned beans", "Apples", "Bananas", "Detergent", "Wine", "Croissants", "Onions")
            .Add(ZoneType.Clothing, "Shirt", "Jacket", "Shoes", "Jeans", "Hat")
            .Add(ZoneType.Electronics, "TV", "Phone charger", "Headphones", "Game console", "Radio")
            .Add(ZoneType.Home, "Lamp", "Cushion", "Vase", "Armchair", "Picture frame");
    }

    /// <summary>
    /// Builds varied lists: each customer weights departments by preference, visits them in a shuffled
    /// order, and sometimes buys two of something. Seeded so tests and replays are deterministic.
    /// </summary>
    public static class ShoppingListGenerator
    {
        public static ShoppingList Create(ShoppingCatalog catalog, IReadOnlyDictionary<ZoneType, float> preferences, int minItems, int maxItems, Random random)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (minItems < 1 || maxItems < minItems) throw new ArgumentOutOfRangeException(nameof(maxItems));
            var departments = new List<ZoneType>();
            foreach (var department in catalog.Departments) departments.Add(department);
            int count = random.Next(minItems, maxItems + 1);
            var items = new List<ShoppingItem>(count);
            var visited = new List<ZoneType>();
            for (int i = 0; i < count; i++)
            {
                var department = Weighted(departments, preferences, random);
                if (!visited.Contains(department)) visited.Add(department);
                items.Add(new ShoppingItem(department, catalog.Pick(department, random), random.NextDouble() < 0.2 ? 2 : 1));
            }
            Shuffle(visited, random);
            items.Sort((a, b) => visited.IndexOf(a.Department).CompareTo(visited.IndexOf(b.Department)));
            return new ShoppingList(items);
        }

        private static ZoneType Weighted(List<ZoneType> departments, IReadOnlyDictionary<ZoneType, float> preferences, Random random)
        {
            double total = 0;
            foreach (var d in departments) total += Weight(d, preferences);
            double roll = random.NextDouble() * total;
            foreach (var d in departments)
            {
                roll -= Weight(d, preferences);
                if (roll <= 0) return d;
            }
            return departments[departments.Count - 1];
        }

        private static double Weight(ZoneType department, IReadOnlyDictionary<ZoneType, float> preferences) =>
            preferences != null && preferences.TryGetValue(department, out float weight) ? Math.Max(0, weight) : 1;

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
