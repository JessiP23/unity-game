using UnityEngine;
namespace NightSupermarket.Game
{
    [CreateAssetMenu(menuName = "Night Supermarket/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id = "crate";
        public string displayName = "Crate";
        public string missionTag = "object";
        [Min(0.1f)] public float mass = 1;
        public bool canPickup = true, canThrow = true, canPush = true, canPlace = true, canBreak;
        [Min(0.1f)] public float breakSpeed = 5;
        public bool inventoryOnly;
        [Min(0)] public int slotCost = 1;
    }
}
