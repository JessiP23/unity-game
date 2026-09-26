namespace NightSupermarket.Core
{
    public enum NpcRole { Customer, Employee, Security }

    /// <summary>
    /// Everything a remote client needs to draw an NPC. Built by the state authority and applied by proxies.
    /// Perception, suspicion values, paths, and shopping lists stay on the authority.
    /// </summary>
    public readonly struct NpcSnapshot
    {
        public readonly int Id;
        public readonly NpcRole Role;
        public readonly MapPoint Position;
        public readonly float Yaw;
        public readonly float Speed;
        public readonly byte Behavior;
        /// <summary>Visible reaction only (unaware, watching, suspicious, reporting), never the suspicion number.</summary>
        public readonly AwarenessState Awareness;
        public NpcSnapshot(int id, NpcRole role, MapPoint position, float yaw, float speed, byte behavior, AwarenessState awareness)
        {
            Id = id; Role = role; Position = position; Yaw = yaw; Speed = speed; Behavior = behavior; Awareness = awareness;
        }
    }
}
