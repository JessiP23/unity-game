using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>How busy the store is. Loaded from Resources/Npc/NpcPopulation, with code defaults as a fallback.</summary>
    [CreateAssetMenu(menuName = "Night Supermarket/NPC Population")]
    public sealed class NpcPopulationSettings : ScriptableObject
    {
        [Tooltip("Shoppers at the quiet end of the crowd wave, on a player's first nights.")]
        [Min(0)] public int minimumCustomers = 10;
        [Tooltip("Shoppers at the busy end of the wave on a first night; later nights multiply this (CrowdPlan.LevelFactor).")]
        [Min(0)] public int maximumCustomers = 20;
        [Tooltip("Hard cap regardless of the target, to protect frame rate. The live budget may hold the crowd lower.")]
        [Min(0)] public int maxActiveCustomers = 40;
        [Tooltip("Where the live frame-rate budget starts on a real night (tests start at the hard cap).")]
        [Min(0)] public int startingBudget = 30;
        [Tooltip("The live budget never drops below this, however slow the machine.")]
        [Min(0)] public int budgetFloor = 16;
        [Tooltip("Customers already shopping when the night starts.")]
        [Min(0)] public int initialCustomers = 12;
        [Tooltip("Seconds between arrivals while below the target (shorter while far below).")]
        [Min(0.1f)] public float spawnInterval = 4f;
        [Tooltip("Seconds for the target population to drift between minimum and maximum.")]
        [Min(1)] public float crowdCycleSeconds = 180f;
        [Min(0)] public int employees = 3;
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
