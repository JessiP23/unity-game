using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>How busy the store is. Loaded from Resources/Npc/NpcPopulation, with code defaults as a fallback.</summary>
    [CreateAssetMenu(menuName = "Night Supermarket/NPC Population")]
    public sealed class NpcPopulationSettings : ScriptableObject
    {
        [Min(0)] public int minimumCustomers = 6;
        [Min(0)] public int maximumCustomers = 12;
        [Tooltip("Hard cap regardless of the target, to protect frame rate.")]
        [Min(0)] public int maxActiveCustomers = 16;
        [Tooltip("Customers already shopping when the night starts.")]
        [Min(0)] public int initialCustomers = 7;
        [Tooltip("Seconds between arrivals while below the target.")]
        [Min(0.1f)] public float spawnInterval = 6f;
        [Tooltip("Seconds for the target population to drift between minimum and maximum.")]
        [Min(1)] public float crowdCycleSeconds = 180f;
        [Min(0)] public int employees = 2;
        public CustomerProfile[] profiles = new CustomerProfile[0];
        [Header("Employees")]
        public Vector2 employeeWalkSpeed = new Vector2(1.25f, 1.45f);
        public Vector2 employeeWorkSeconds = new Vector2(6, 14);
        [Min(0.5f)] public float employeeVisionRange = 9f;
        [Range(10, 170)] public float employeeFieldOfView = 90f;
        [Min(0.1f)] public float employeeSensitivity = 1.3f;
        [Min(0)] public float employeeNoticeSeconds = 0.25f;
        [Min(0)] public float employeeReactionSeconds = 0.8f;
        [Min(0)] public float employeeReportDelaySeconds = 1.2f;

        public static NpcPopulationSettings Load()
        {
            var settings = Resources.Load<NpcPopulationSettings>("Npc/NpcPopulation");
            if (settings == null) settings = CreateInstance<NpcPopulationSettings>();
            if (settings.profiles == null || settings.profiles.Length == 0) settings.profiles = CustomerProfile.Defaults();
            return settings;
        }
    }
}
