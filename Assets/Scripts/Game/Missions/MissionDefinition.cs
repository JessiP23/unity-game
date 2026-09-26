using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    [CreateAssetMenu(menuName = "Night Supermarket/Mission")]
    public sealed class MissionDefinition : ScriptableObject
    {
        public string id, title, targetTag, destination, requiredItem;
        public ActionKind kind;
        [Min(1)] public int quantity = 1;
        public bool required = true;
        public MissionRule CreateRule() => new MissionRule(id, title, kind, targetTag, quantity, destination);
    }
}
