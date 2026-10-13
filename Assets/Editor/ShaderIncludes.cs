using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightSupermarket.Editor
{
    /// <summary>
    /// Shaders the game finds by name at runtime (fountain spray, splashes) must survive build stripping.
    /// Adds them to Graphics Settings ▸ Always Included Shaders once, when the editor loads.
    /// </summary>
    [InitializeOnLoad]
    public static class ShaderIncludes
    {
        private static readonly string[] Needed = { "Universal Render Pipeline/Particles/Unlit" };

        static ShaderIncludes() => EditorApplication.delayCall += Ensure;

        public static void Ensure()
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            var list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            foreach (var name in Needed)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                changed = true;
            }
            if (changed) { serialized.ApplyModifiedProperties(); AssetDatabase.SaveAssets(); }
        }
    }
}
