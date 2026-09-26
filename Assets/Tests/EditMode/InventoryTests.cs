using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class InventoryTests
    {
        [Test] public void CapacityQuantitiesAndInsufficientRemoval()
        {
            var inventory = new Inventory(3);
            Assert.That(inventory.TryAdd("shirt", 2, 1), Is.True);
            Assert.That(inventory.TryAdd("box", 1, 2), Is.False);
            Assert.That(inventory.TryRemove("shirt", 3), Is.False);
            Assert.That(inventory.Count("shirt"), Is.EqualTo(2));
            Assert.That(inventory.TryRemove("shirt", 1), Is.True);
            Assert.That(inventory.TryAdd("box", 1, 2), Is.True);
            Assert.That(inventory.UsedSlots, Is.EqualTo(3));
        }
        [Test] public void KeysHaveNoSlotCostAndInvalidValuesAreRejected()
        {
            var inventory = new Inventory(0);
            Assert.That(inventory.TryAdd("key", 1, 0), Is.True);
            Assert.That(inventory.TryAdd("key", -1, 0), Is.False);
            Assert.That(inventory.TryAdd("key", 1, 1), Is.False);
            Assert.That(inventory.TryRemove("key", 0), Is.False);
            Assert.That(inventory.Count("key"), Is.EqualTo(1));
        }
        [Test] public void SnapshotCannotMutateInventory()
        {
            var inventory = new Inventory(2); inventory.TryAdd("item", 1, 1);
            var snapshot = inventory.Snapshot(); inventory.TryRemove("item", 1);
            Assert.That(snapshot["item"], Is.EqualTo(1)); Assert.That(inventory.Count("item"), Is.Zero);
        }
    }
}
