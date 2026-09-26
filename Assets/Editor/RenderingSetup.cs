using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace NightSupermarket.Editor
{
    /// <summary>Night store rendering: Forward+, post-processing, SSAO, soft flashlight shadows, material templates.</summary>
    public static class RenderingSetup
    {
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        private const string TemplateFolder = "Assets/Resources/Materials";

        [MenuItem("Night Supermarket/Setup/Configure Rendering")]
        public static void Configure()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ProjectSetup.RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetup.PipelinePath);
            if (renderer == null || pipeline == null) throw new System.InvalidOperationException("Run Configure Project first.");
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            renderer.renderingMode = RenderingMode.ForwardPlus;
            if (!renderer.rendererFeatures.Exists(feature => feature is ScreenSpaceAmbientOcclusion))
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                renderer.rendererFeatures.Add(ssao);
            }
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            var serializedRenderer = new SerializedObject(renderer);
            var map = serializedRenderer.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long id))
                    map.GetArrayElementAtIndex(i).longValue = id;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 6;
            settings.FindProperty("m_ShadowDistance").floatValue = 22;
            settings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 1024;
            var any = settings.FindProperty("m_AnyShadowsSupported");
            if (any != null) any.boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            CreateTemplates();
            AssetDatabase.SaveAssets();
            Debug.Log("[SETUP] Rendering configured: Forward+, post-processing, SSAO, soft additional shadows.");
        }

        private static void CreateTemplates()
        {
            Directory.CreateDirectory(TemplateFolder);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var opaque = new Material(lit) { name = "LitOpaque" };
            opaque.EnableKeyword("_NORMALMAP");
            opaque.EnableKeyword("_EMISSION");
            opaque.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            opaque.SetColor("_EmissionColor", Color.black);
            Save(opaque, TemplateFolder + "/LitOpaque.mat");
            var transparent = new Material(lit) { name = "LitTransparent" };
            transparent.SetFloat("_Surface", 1);
            transparent.SetFloat("_Blend", 0);
            transparent.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            transparent.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            transparent.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            transparent.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            transparent.SetFloat("_ZWrite", 0);
            transparent.SetOverrideTag("RenderType", "Transparent");
            transparent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            transparent.renderQueue = (int)RenderQueue.Transparent;
            Save(transparent, TemplateFolder + "/LitTransparent.mat");
            var cutout = new Material(lit) { name = "LitCutout" };
            cutout.SetFloat("_AlphaClip", 1);
            cutout.SetFloat("_Cutoff", 0.5f);
            cutout.SetFloat("_Cull", 0);
            cutout.EnableKeyword("_ALPHATEST_ON");
            cutout.SetOverrideTag("RenderType", "TransparentCutout");
            cutout.renderQueue = (int)RenderQueue.AlphaTest;
            Save(cutout, TemplateFolder + "/LitCutout.mat");
            var text = Shader.Find("NightSupermarket/WorldText");
            if (text != null) Save(new Material(text) { name = "WorldText" }, TemplateFolder + "/WorldText.mat");
        }

        private static void Save(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(material, existing); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(material, path);
        }
    }
}
