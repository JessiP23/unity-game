using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class DoorTests
    {
        [Test] public void CorrectKeyUnlocksAndWrongKeyCannotOpen()
        {
            var door = new DoorLock("employee-key"); var items = new Inventory(0);
            Assert.That(door.TryToggle(), Is.False); items.TryAdd("wrong", 1, 0);
            Assert.That(door.TryUnlock(items, false), Is.False);
            items.TryAdd("employee-key", 1, 0); Assert.That(door.TryUnlock(items, false), Is.True);
            Assert.That(items.Count("employee-key"), Is.EqualTo(1)); Assert.That(door.TryToggle(), Is.True);
            Assert.That(door.Open, Is.True); door.TryToggle(); Assert.That(door.Open, Is.False);
        }
        [Test] public void ConsumableKeyIsRemovedOnlyOnce()
        {
            var door = new DoorLock("key"); var items = new Inventory(0); items.TryAdd("key", 1, 0);
            Assert.That(door.TryUnlock(items, true), Is.True); Assert.That(items.Count("key"), Is.Zero);
            Assert.That(door.TryUnlock(items, true), Is.False);
        }
    }
}
