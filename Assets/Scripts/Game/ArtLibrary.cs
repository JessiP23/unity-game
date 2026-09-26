using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Loads fetched CC0/MIT art from Resources/Art and builds URP materials for it.
    /// Every lookup returns null when a file is missing so the prototype still runs without art.
    /// </summary>
    public static class ArtLibrary
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Object> Loaded = new Dictionary<string, Object>();
        private static Material opaque, transparent;
        public static bool Available => Prop("cardboard_box_01") != null;
        public static GameObject Prop(string id) => Load<GameObject>($"Art/Props/{id}/{id}_1k");
        public static GameObject Character(string name) => Load<GameObject>($"Art/Characters/{name}/{name}");
        public static Texture2D Texture(string path) => Load<Texture2D>(path);
        private static T Load<T>(string path) where T : Object
        {
            if (!Loaded.TryGetValue(path, out var asset)) { asset = Resources.Load<T>(path); Loaded[path] = asset; }
            return asset as T;
        }
        public static Material Lit(Color color, float smoothness = 0.4f, float metallic = 0f)
        {
            string key = $"lit:{color}:{smoothness}:{metallic}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var material = NewOpaque();
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return Materials[key] = material;
        }
        public static Material Emissive(Color color, float intensity)
        {
            string key = $"emit:{color}:{intensity}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var material = NewOpaque();
            material.SetColor("_BaseColor", color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", color * intensity);
            return Materials[key] = material;
        }
        /// <summary>Tiling surface from Resources/Art/Surfaces. Returns null when the texture was not fetched.</summary>
        public static Material Surface(string id, Vector2 tiling, Color tint, float smoothness)
        {
            string key = $"surface:{id}:{tiling}:{tint}:{smoothness}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var diffuse = Texture($"Art/Surfaces/{id}/{id}_diff");
            if (diffuse == null) return null;
            var material = NewOpaque();
            material.SetTexture("_BaseMap", diffuse);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            var normal = Texture($"Art/Surfaces/{id}/{id}_nor_gl");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            return Materials[key] = material;
        }
        /// <summary>Instantiates a Poly Haven prop under a holder so the importer's root rotation and scale survive.</summary>
        public static GameObject Spawn(string id, Transform parent, Vector3 position, float yaw, float scale = 1f)
        {
            var prefab = Prop(id);
            if (prefab == null) return null;
            var holder = new GameObject(id);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            holder.transform.localScale = Vector3.one * scale;
            var visual = Object.Instantiate(prefab, holder.transform, false);
            Strip(visual);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = PropMaterial(id, shared[i] != null ? shared[i].name : id);
                renderer.sharedMaterials = shared;
            }
            return holder;
        }
        public static void Strip(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            foreach (var animation in visual.GetComponentsInChildren<Animation>()) Object.Destroy(animation);
        }
        public static Bounds BoundsOf(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(visual.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        private static Material PropMaterial(string id, string materialName)
        {
            string key = $"prop:{id}:{materialName}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            string folder = $"Art/Props/{id}/textures/";
            var diffuse = Texture(folder + materialName + "_diff_1k") ?? Texture(folder + id + "_diff_1k");
            var normal = Texture(folder + materialName + "_nor_gl_1k") ?? Texture(folder + id + "_nor_gl_1k");
            string lower = materialName.ToLowerInvariant();
            bool glass = lower.Contains("glass") || lower.Contains("clearwater");
            var material = glass ? NewTransparent(0.35f) : NewOpaque();
            if (diffuse != null) material.SetTexture("_BaseMap", diffuse);
            material.SetColor("_BaseColor", glass ? new Color(1, 1, 1, 0.3f) : Color.white);
            material.SetFloat("_Smoothness", glass ? 0.92f : 0.35f);
            if (normal != null && !glass)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (id == "mounted_fluorescent_lights" && glass) MakeGlow(material, new Color(0.9f, 0.95f, 1f), 3.5f);
            if (id == "caged_hanging_light" && lower.Contains("caged")) material.SetFloat("_Metallic", 0.6f);
            return Materials[key] = material;
        }
        public static void MakeGlow(Material material, Color color, float intensity)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", color * intensity);
        }
        /// <summary>Character material for Rocketbox avatars. <paramref name="plastic"/> turns the part into a mannequin shell.</summary>
        public static Material CharacterMaterial(string character, string materialName, bool plastic, bool hidden)
        {
            string key = $"char:{character}:{materialName}:{plastic}:{hidden}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            Material material;
            if (hidden)
            {
                material = NewTransparent(0f);
                material.SetColor("_BaseColor", new Color(1, 1, 1, 0));
                return Materials[key] = material;
            }
            material = NewOpaque();
            string folder = $"Art/Characters/{character}/textures/{materialName}";
            var normal = Texture(folder + "_normal");
            if (plastic)
            {
                material.SetColor("_BaseColor", new Color(0.86f, 0.84f, 0.8f));
                material.SetFloat("_Smoothness", 0.72f);
            }
            else
            {
                var diffuse = Texture(folder + "_color");
                if (diffuse != null) material.SetTexture("_BaseMap", diffuse);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.3f);
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            return Materials[key] = material;
        }
        private static Material NewOpaque()
        {
            if (opaque == null) opaque = Resources.Load<Material>("Materials/LitOpaque");
            var material = opaque != null ? new Material(opaque) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.DisableKeyword("_EMISSION");
            return material;
        }
        private static Material NewTransparent(float alpha)
        {
            if (transparent == null) transparent = Resources.Load<Material>("Materials/LitTransparent");
            Material material;
            if (transparent != null) material = new Material(transparent);
            else
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            material.SetColor("_BaseColor", new Color(1, 1, 1, alpha));
            return material;
        }
    }
}
