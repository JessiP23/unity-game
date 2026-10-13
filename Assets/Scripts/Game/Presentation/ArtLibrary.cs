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
        public static Material Glass(Color tint)
        {
            string key = $"glass:{tint}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var material = NewTransparent(tint.a);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.95f);
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
            string folder = $"Art/Characters/{character}/textures/{materialName}";
            if (materialName.EndsWith("_opacity"))
            {
                material = NewCutout();
                var hair = Texture(folder + "_color");
                if (hair != null) material.SetTexture("_BaseMap", hair);
                material.SetFloat("_Smoothness", 0.25f);
                return Materials[key] = material;
            }
            material = NewOpaque();
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
        private static Texture2D waterNormal, softDot;

        /// <summary>
        /// Clear, glossy water: a transparent smooth surface with a generated ripple normal map. Scroll the
        /// map (see <see cref="WaterRipple"/>) and it moves without a custom shader.
        /// </summary>
        public static Material Water(Color tint)
        {
            string key = $"water:{tint}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var material = NewTransparent(tint.a);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.97f);
            material.SetFloat("_Metallic", 0.1f);
            waterNormal ??= RippleNormals(128);
            material.SetTexture("_BumpMap", waterNormal);
            material.SetFloat("_BumpScale", 0.6f);
            material.SetTextureScale("_BaseMap", new Vector2(3f, 3f)); // URP Lit tiles every map with _BaseMap_ST
            material.EnableKeyword("_NORMALMAP");
            return Materials[key] = material;
        }

        public enum ScreenShow { Broadcast, Cctv }

        /// <summary>
        /// A television picture rather than a glowing slab: dark glass with the picture in its emission map,
        /// scanlines and a darker rim like a real tube. Broadcast is a little sunset scene with a news bar;
        /// CCTV is a green, grainy view of an aisle with a REC dot.
        /// </summary>
        public static Material Screen(Color tint, ScreenShow show, int seed)
        {
            string key = $"screen:{tint}:{show}:{seed % 4}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var material = NewOpaque();
            material.SetColor("_BaseColor", new Color(0.04f, 0.045f, 0.05f));
            material.SetFloat("_Smoothness", 0.92f);
            var picture = Picture(tint, show, seed % 4);
            material.SetTexture("_BaseMap", picture);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetTexture("_EmissionMap", picture);
            // The panel faces the viewer turned half round: show the picture the right way up.
            material.SetTextureScale("_BaseMap", new Vector2(-1f, -1f));
            material.SetTextureOffset("_BaseMap", new Vector2(1f, 1f));
            material.SetColor("_EmissionColor", Color.white * 1.15f);
            return Materials[key] = material;
        }

        private static Texture2D Picture(Color tint, ScreenShow show, int seed)
        {
            const int w = 96, h = 72;
            var pixels = new Color[w * h];
            var random = new System.Random(seed * 31 + (int)show);
            float sunX = 0.3f + (float)random.NextDouble() * 0.4f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), v = y / (float)(h - 1);
                    Color c;
                    if (show == ScreenShow.Cctv)
                    {
                        // An aisle in perspective: floor, two shelf runs, a ceiling strip.
                        float lane = Mathf.Abs(u - 0.5f) * 2f;
                        bool shelf = lane > 0.35f + (1f - v) * 0.25f && v > 0.18f && v < 0.85f;
                        float g = shelf ? 0.45f + 0.15f * Mathf.Sin(v * 60f) : v < 0.18f ? 0.25f : 0.6f;
                        g += ((float)random.NextDouble() - 0.5f) * 0.12f; // grain
                        c = new Color(g * 0.55f, g, g * 0.6f);
                        if (u > 0.86f && u < 0.94f && v > 0.84f && v < 0.94f) c = new Color(1f, 0.15f, 0.1f);
                        if (v > 0.88f && u < 0.45f) c *= 1.3f; // the timestamp strip
                    }
                    else
                    {
                        // Sky to tint, a low sun, dark hills and a news bar along the bottom.
                        Color sky = Color.Lerp(tint * 0.9f + new Color(0.15f, 0.1f, 0.05f), new Color(0.08f, 0.1f, 0.25f), v);
                        float sun = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v * 0.75f), new Vector2(sunX, 0.38f)) * 7f);
                        c = sky + new Color(1f, 0.85f, 0.55f) * sun;
                        float hill = 0.32f + 0.06f * Mathf.Sin(u * 9f + seed) + 0.03f * Mathf.Sin(u * 23f);
                        if (v < hill) c = Color.Lerp(new Color(0.05f, 0.08f, 0.1f), tint * 0.3f, v / hill);
                        if (v < 0.14f) c = v > 0.11f ? new Color(0.9f, 0.25f, 0.15f) : new Color(0.12f, 0.12f, 0.16f) + (((x / 3) % 5 == 0) ? new Color(0.5f, 0.5f, 0.5f) : Color.clear) * (v > 0.04f && v < 0.08f ? 1f : 0f);
                    }
                    if (y % 2 == 0) c *= 0.78f;                      // scanlines
                    float rim = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 9f);
                    c *= Mathf.Lerp(0.35f, 1f, rim);                  // darker towards the tube's edge
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Screen picture", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Soft additive-looking particle for spray, splashes and sparkles. Null when URP particle shaders are absent.</summary>
        public static Material Particle(Color tint)
        {
            string key = $"particle:{tint}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            var material = new Material(shader);
            softDot ??= SoftDot(32);
            material.SetTexture("_BaseMap", softDot);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return Materials[key] = material;
        }

        /// <summary>A lit material with vertical stripes (u repeats <paramref name="count"/> times): canopies, awnings.</summary>
        public static Material Stripes(Color a, Color b, int count, float smoothness = 0.35f)
        {
            string key = $"stripes:{a}:{b}:{count}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var texture = new Texture2D(count * 8, 4, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Stripes" };
            for (int x = 0; x < texture.width; x++)
                for (int y = 0; y < texture.height; y++)
                    texture.SetPixel(x, y, (x / 8) % 2 == 0 ? a : b);
            texture.Apply();
            var material = NewOpaque();
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            return Materials[key] = material;
        }

        private static Texture2D RippleNormals(int size)
        {
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                    heights[y * size + x] = Mathf.Sin(u * 3f + v) * 0.5f + Mathf.Sin(v * 4f - u * 2f) * 0.35f + Mathf.Sin((u + v) * 7f) * 0.15f;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, name = "Water normals" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                    float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                    var n = new Vector3(-dx * 1.5f, -dy * 1.5f, 1f).normalized;
                    texture.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            texture.Apply();
            return texture;
        }

        private static Texture2D SoftDot(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Soft dot" };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texture.Apply();
            return texture;
        }

        private static Material NewOpaque()
        {
            if (opaque == null) opaque = Resources.Load<Material>("Materials/LitOpaque");
            var material = opaque != null ? new Material(opaque) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.DisableKeyword("_EMISSION");
            return material;
        }
        private static Material cutout;
        private static Material NewCutout()
        {
            if (cutout == null) cutout = Resources.Load<Material>("Materials/LitCutout");
            if (cutout != null) return new Material(cutout);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", 0.5f); material.SetFloat("_Cull", 0);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
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
