using NightSupermarket.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
namespace NightSupermarket.Game
{
    public enum PointKind { Browse, Checkout, Entrance, Spawn, Exit, Work }

    /// <summary>NavMesh area used to keep civilians out of staff-only zones.</summary>
    public static class NavigationAreas
    {
        public const int Restricted = 3;
        public static int CivilianMask => NavMesh.AllAreas & ~(1 << Restricted);
    }

    /// <summary>A place an NPC can go to and what they do there. Forward is the direction they face on arrival.</summary>
    public class NavigationPoint : MonoBehaviour
    {
        public ZoneType zone;
        public PointKind kind;
        /// <summary>Id of the NPC currently using the point, or 0.</summary>
        public int Occupant { get; private set; }
        public bool Free => Occupant == 0;
        public Vector3 Position => transform.position;
        public bool TryReserve(int npc)
        {
            if (Occupant != 0 && Occupant != npc) return false;
            Occupant = npc; return true;
        }
        public void Release(int npc) { if (Occupant == npc) Occupant = 0; }
    }

}
