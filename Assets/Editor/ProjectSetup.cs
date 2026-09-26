using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightSupermarket.Editor
{
    /// <summary>Creates the project's replaceable rendering and bootstrap assets.</summary>
    public static class ProjectSetup
    {
        public const string PipelinePath = "Assets/Settings/NightSupermarketPipeline.asset";
        public const string RendererPath = "Assets/Settings/NightSupermarketRenderer.asset";

        [MenuItem("Night Supermarket/Setup/Configure Project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int quality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(quality, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "Night Supermarket";
            PlayerSettings.productName = "Night Supermarket";
            EditorSettings.serializationMode = SerializationMode.ForceText;
            UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
            ConfigureInput();
            BootstrapSetup.SaveBootstrap();
            // Preserve any additional scenes as the prototype grows.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == BootstrapSetup.ScenePath))
                scenes.Insert(0, new EditorBuildSettingsScene(BootstrapSetup.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            RenderingSetup.Configure();
            Debug.Log("[SETUP] Configuration saved. Restart the Editor before testing input.");
        }

        private static void ConfigureInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0) throw new InvalidOperationException("Player settings unavailable.");
            var settings = new SerializedObject(assets[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input == null) throw new InvalidOperationException("Unity input setting schema changed.");
            input.intValue = 1; // Unity PlayerSettings: Input System only.
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Builds a development Windows player; throws on any build failure.</summary>
        public static void BuildWindows()
        {
            var scenes = Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled);
            if (scenes.Length == 0) throw new InvalidOperationException("Run Configure first.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, scene => scene.path),
                locationPathName = "Builds/Windows/NightSupermarket.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        }
    }
}
