namespace NightSupermarket.Core
{
    public sealed class DoorLock
    {
        public string RequiredKey { get; }
        public bool Locked { get; private set; }
        public bool Open { get; private set; }
        public DoorLock(string requiredKey) { RequiredKey = requiredKey; Locked = !string.IsNullOrEmpty(requiredKey); }
        public bool TryUnlock(Inventory inventory, bool consume)
        {
            if (!Locked || inventory == null || inventory.Count(RequiredKey) == 0) return false;
            if (consume && !inventory.TryRemove(RequiredKey, 1)) return false;
            Locked = false; return true;
        }
        public bool TryToggle() { if (Locked) return false; Open = !Open; return true; }
    }
}
