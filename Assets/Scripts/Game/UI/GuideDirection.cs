using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>A direct objective bearing, not a navigation path through shelves.</summary>
    public static class GuideDirection
    {
        public static string Label(float yaw, float distance)
        {
            if (distance <= 2) return "HERE · LOOK FOR THE GOLD MARK";
            if (Mathf.Abs(yaw) >= 135) return "TURN AROUND";
            if (yaw < -20) return "TURN LEFT";
            if (yaw > 20) return "TURN RIGHT";
            return "AHEAD · USE THE AISLES";
        }
    }
}
