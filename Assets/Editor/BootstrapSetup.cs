using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightSupermarket.Editor
{
    /// <summary>Explicit, repeatable bootstrap scene creation; never runs on import.</summary>
    public static class BootstrapSetup
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap/Bootstrap.unity";

        public static void PopulateScene(Scene scene)
        {
            var camera = new GameObject("Bootstrap Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(camera, scene);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2, -5);
            var light = new GameObject("Bootstrap Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        [MenuItem("Night Supermarket/Setup/Create Bootstrap Scene")]
        public static void SaveBootstrap()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("[SETUP] Bootstrap already exists; preserving it.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save bootstrap scene.");
            AssetDatabase.Refresh();
        }
    }
}
