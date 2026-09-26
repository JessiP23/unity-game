using NightSupermarket.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    /// <summary>Semantic area. Restricted zones also stamp a NavMesh area that civilians cannot walk on.</summary>
    public sealed class ZoneVolume : MonoBehaviour
    {
        public ZoneType zone;
        public Vector3 size = Vector3.one;
        public Bounds Bounds => new Bounds(transform.position, size);
        public float Area => size.x * size.z;
        public void Configure(ZoneType type, Vector3 volumeSize)
        {
            zone = type; size = volumeSize;
            if (!ZoneAccess.IsRestricted(type)) return;
            var modifier = gameObject.AddComponent<NavMeshModifierVolume>();
            modifier.size = volumeSize; modifier.center = Vector3.zero; modifier.area = NavigationAreas.Restricted;
        }
    }

}
