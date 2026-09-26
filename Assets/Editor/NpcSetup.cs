using System.IO;
using NightSupermarket.Game;
using UnityEditor;
using UnityEngine;
namespace NightSupermarket.Editor
{
    /// <summary>Writes editable population and customer profile assets from the built-in defaults.</summary>
    public static class NpcSetup
    {
        public const string Folder = "Assets/Resources/Npc";

        [MenuItem("Night Supermarket/Setup/Create NPC Profiles")]
        public static void CreateDefaults()
        {
            Directory.CreateDirectory(Folder);
            string populationPath = Folder + "/NpcPopulation.asset";
            var population = AssetDatabase.LoadAssetAtPath<NpcPopulationSettings>(populationPath);
            if (population == null)
            {
                population = ScriptableObject.CreateInstance<NpcPopulationSettings>();
                AssetDatabase.CreateAsset(population, populationPath);
            }
            if (population.profiles == null || population.profiles.Length == 0)
            {
                var defaults = CustomerProfile.Defaults();
                for (int i = 0; i < defaults.Length; i++)
                {
                    string path = $"{Folder}/Customer {defaults[i].name}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<CustomerProfile>(path);
                    if (existing == null) AssetDatabase.CreateAsset(defaults[i], path); else defaults[i] = existing;
                }
                population.profiles = defaults;
                EditorUtility.SetDirty(population);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[SETUP] NPC population and customer profiles ready in " + Folder);
        }
    }
}
