using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using Random = System.Random;
namespace NightSupermarket.Game
{
    /// <summary>
    /// A shopper archetype. Every customer spawned from it rolls its own values inside these ranges,
    /// so two customers from the same profile still walk, browse, and react differently.
    /// </summary>
    [CreateAssetMenu(menuName = "Night Supermarket/Customer Profile")]
    public sealed class CustomerProfile : ScriptableObject
    {
        [Serializable] public struct Preference { public ZoneType department; [Min(0)] public float weight; }

        [Header("Movement")]
        public Vector2 walkSpeed = new Vector2(1.0f, 1.5f);
        [Header("Shopping")]
        public Vector2 browseSeconds = new Vector2(2, 8);
        public Vector2 waitSeconds = new Vector2(1, 3);
        [Range(0, 1)] public float waitChance = 0.35f;
        [Range(0, 1)] public float glanceChance = 0.35f;
        public Vector2 checkoutSeconds = new Vector2(3, 6);
        [Range(0, 1)] public float checkoutChance = 0.9f;
        public Vector2Int items = new Vector2Int(1, 3);
        [Tooltip("Seconds before the customer abandons the rest of the list and leaves.")]
        [Min(10)] public float maxVisitSeconds = 420;
        [Tooltip("Seconds a leaving customer may fail to reach an exit before being removed.")]
        [Min(5)] public float exitTimeoutSeconds = 90;
        [Tooltip("Pace multiplier when leaving shaken after a report.")]
        [Min(0.5f)] public float hurriedPace = 1.3f;
        public Preference[] preferredDepartments = new Preference[0];
        [Header("Perception (civilian, not security)")]
        [Min(0.5f)] public float visionRange = 7f;
        [Range(10, 170)] public float fieldOfView = 70f;
        [Min(0.05f)] public float perceptionInterval = 0.2f;
        public Vector2 suspicionSensitivity = new Vector2(0.8f, 1.2f);
        [Min(0.01f)] public float suspicionGain = 0.9f;
        [Min(0)] public float suspicionDecay = 0.35f;
        [Min(0.01f)] public float suspicionThreshold = 1f;
        public Vector2 noticeSeconds = new Vector2(0.25f, 0.5f);
        public Vector2 reactionSeconds = new Vector2(0.8f, 1.6f);
        public Vector2 reportDelaySeconds = new Vector2(1.5f, 3f);
        [Range(0, 1)] public float reportingProbability = 0.85f;
        [Range(0, 1)] public float leaveAfterReport = 0.5f;

        public float RollWalkSpeed(Random random) => Roll(walkSpeed, random);

        public CustomerSettings CreateSettings(Random random) => new CustomerSettings
        {
            BrowseMin = browseSeconds.x, BrowseMax = browseSeconds.y,
            WaitMin = waitSeconds.x, WaitMax = waitSeconds.y, WaitChance = waitChance,
            CheckoutMin = checkoutSeconds.x, CheckoutMax = checkoutSeconds.y, CheckoutChance = checkoutChance,
            ReportingProbability = reportingProbability, LeaveAfterReport = leaveAfterReport,
            StareTime = Roll(reactionSeconds, random) + 0.4f, GlanceChance = glanceChance,
            MaxVisitSeconds = maxVisitSeconds, ExitTimeoutSeconds = exitTimeoutSeconds, HurriedPace = hurriedPace,
        };

        public AwarenessSettings CreateAwareness(Random random) => new AwarenessSettings
        {
            SuspicionGain = suspicionGain, SuspicionDecay = suspicionDecay, Threshold = suspicionThreshold,
            Sensitivity = Roll(suspicionSensitivity, random), NoticeTime = Roll(noticeSeconds, random),
            ReactionTime = Roll(reactionSeconds, random), ReportDelay = Roll(reportDelaySeconds, random),
        };

        public Dictionary<ZoneType, float> Preferences()
        {
            var weights = new Dictionary<ZoneType, float>();
            foreach (var preference in preferredDepartments) weights[preference.department] = preference.weight;
            return weights;
        }

        private static float Roll(Vector2 range, Random random) => range.x + (float)random.NextDouble() * Mathf.Max(0, range.y - range.x);

        /// <summary>Built-in archetypes used when no profile assets are assigned.</summary>
        public static CustomerProfile[] Defaults()
        {
            var casual = Create("Casual shopper", new Vector2(1.0f, 1.4f), new Vector2(3, 8), 0.85f, ZoneType.Supermarket, 2f);
            var hurried = Create("In a hurry", new Vector2(1.35f, 1.6f), new Vector2(1.5f, 3.5f), 0.7f, ZoneType.Supermarket, 3f);
            hurried.waitChance = 0.1f; hurried.suspicionSensitivity = new Vector2(0.6f, 0.9f); hurried.reactionSeconds = new Vector2(1.2f, 2.0f);
            var fashion = Create("Fashion browser", new Vector2(0.9f, 1.2f), new Vector2(5, 10), 0.9f, ZoneType.Clothing, 3f);
            fashion.suspicionSensitivity = new Vector2(0.8f, 1.1f); fashion.reactionSeconds = new Vector2(1.0f, 1.8f);
            var gadget = Create("Gadget fan", new Vector2(1.0f, 1.4f), new Vector2(4, 9), 0.8f, ZoneType.Electronics, 3f);
            gadget.suspicionSensitivity = new Vector2(0.7f, 1.0f); gadget.reactionSeconds = new Vector2(1.1f, 2.0f);
            var nosy = Create("Nosy neighbour", new Vector2(0.9f, 1.3f), new Vector2(3, 7), 0.95f, ZoneType.Home, 2f);
            nosy.suspicionSensitivity = new Vector2(1.2f, 1.5f); nosy.fieldOfView = 85; nosy.visionRange = 8.5f; nosy.reactionSeconds = new Vector2(0.5f, 1.0f);
            return new[] { casual, hurried, fashion, gadget, nosy };
        }

        private static CustomerProfile Create(string title, Vector2 speed, Vector2 browse, float report, ZoneType favourite, float weight)
        {
            var profile = CreateInstance<CustomerProfile>();
            profile.name = title; profile.walkSpeed = speed; profile.browseSeconds = browse; profile.reportingProbability = report;
            profile.preferredDepartments = new[] { new Preference { department = favourite, weight = weight } };
            return profile;
        }
    }
}
