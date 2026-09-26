using UnityEditor;
using UnityEngine;
namespace NightSupermarket.Editor
{
    /// <summary>Import settings for downloaded art under Resources/Art so fetched files need no hand-made metas.</summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Art/";
        private static readonly Color Plastic = new Color(0.86f, 0.84f, 0.8f);
        public override uint GetVersion() => 5;
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = assetPath.Contains("/Characters/") || assetPath.Contains("/Surfaces/") ? 2048 : 1024;
            bool normal = assetPath.Contains("_nor_gl") || assetPath.Contains("_normal");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = assetPath.Contains("/Surfaces/") ? 8 : 2;
            importer.alphaIsTransparency = assetPath.Contains("_opacity");
            importer.alphaSource = assetPath.Contains("_opacity") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        }
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(Root + "Characters/Mannequin") || !assetPath.EndsWith("_body_color.jpg")) return;
            var pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                if (IsSkin(pixels[i]))
                {
                    float shade = Mathf.Clamp01(pixels[i].grayscale * 1.6f);
                    pixels[i] = Color.Lerp(Plastic * 0.7f, Plastic, shade);
                }
            texture.SetPixels(pixels);
            texture.Apply();
        }
        private static bool IsSkin(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max < 0.3f || !(c.r >= c.g && c.g >= c.b)) return false;
            float saturation = (max - min) / max;
            float redGreen = c.r - c.g;
            return saturation > 0.12f && saturation < 0.7f && redGreen > 0.03f && redGreen < 0.3f && c.r - c.b > 0.1f;
        }
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.isReadable = !assetPath.Contains("/Characters/");
            if (assetPath.Contains("/Characters/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = assetPath.Contains("/Animations/");
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }
        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Characters/Animations/")) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
            importer.clipAnimations = clips;
        }
    }
}
