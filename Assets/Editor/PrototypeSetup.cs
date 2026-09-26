using System.IO;
using NightSupermarket.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace NightSupermarket.Editor
{
    public static class PrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/Prototype/Prototype.unity";
        [MenuItem("Night Supermarket/Setup/Create Prototype")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes/Prototype");
            var rules = AssetDatabase.LoadAssetAtPath<GameRulesAsset>("Assets/Settings/GameRules.asset");
            if (rules == null) { rules = ScriptableObject.CreateInstance<GameRulesAsset>(); AssetDatabase.CreateAsset(rules, "Assets/Settings/GameRules.asset"); }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype").AddComponent<PrototypeRoot>(); root.rules = rules;
                var light = new GameObject("Night lighting").AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50, -30, 0); light.intensity = 0.7f;
                RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
    }
}
