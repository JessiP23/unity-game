using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        public Inventory Items { get; private set; }
        public void Configure(int capacity) { Items = new Inventory(capacity); }
    }
}
