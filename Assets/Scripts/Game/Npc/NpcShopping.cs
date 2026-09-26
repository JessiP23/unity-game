using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// What a customer is carrying. Picking an item shows a basket at their side; paying swaps it for a bag.
    /// Visual only: stock on shelves is dressing, so nothing in the level is removed.
    /// </summary>
    public sealed class NpcShopping : MonoBehaviour
    {
        private readonly List<ShoppingItem> basket = new List<ShoppingItem>();
        private GameObject carried;
        private bool bagged;
        public IReadOnlyList<ShoppingItem> Basket => basket;
        public bool Paid => bagged;

        public void Pick(ShoppingItem item)
        {
            basket.Add(item);
            if (carried == null) Show(false);
        }

        public void PayAndBag()
        {
            bagged = true;
            if (carried != null) Destroy(carried);
            Show(true);
        }

        public void Clear()
        {
            basket.Clear(); bagged = false;
            if (carried != null) Destroy(carried);
            carried = null;
        }

        private void Show(bool bag)
        {
            carried = new GameObject(bag ? "Shopping bag" : "Basket");
            carried.transform.SetParent(transform, false);
            carried.transform.localPosition = new Vector3(0.3f, 0.62f, 0.08f);
            var prop = bag ? null : ArtLibrary.Spawn("plastic_crate_01", carried.transform, new Vector3(0, -0.12f, 0), 90, 0.75f);
            if (prop != null) return;
            var box = DressingKit.Panel(carried.transform, "Placeholder", Vector3.zero, bag ? new Vector3(0.12f, 0.32f, 0.26f) : new Vector3(0.24f, 0.18f, 0.34f));
            box.sharedMaterial = ArtLibrary.Lit(bag ? new Color(0.9f, 0.88f, 0.8f) : new Color(0.75f, 0.12f, 0.1f), 0.3f);
        }
    }
}
