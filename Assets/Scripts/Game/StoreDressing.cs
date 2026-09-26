using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Swaps primitive renderers for CC0 Poly Haven models. Colliders stay on the gameplay objects.
    /// Missing imports are skipped so the prototype still runs.
    /// </summary>
    public static class StoreDressing
    {
        public static void Apply(Transform root)
        {
            var floor = Material("brown_floor_tiles_diff_1k", new Vector2(10, 10));
            var wall = Material("beige_wall_001_diff_1k", new Vector2(4, 2));
            var boxTexture = Resources.Load<Texture2D>("Store/textures/cardboard_box_01_diff_1k");
            var shelfTexture = Resources.Load<Texture2D>("Store/textures/steel_frame_shelves_01_diff_1k");
            var cameraTexture = Resources.Load<Texture2D>("Store/textures/security_camera_01_diff_1k");
            var lightTexture = Resources.Load<Texture2D>("Store/textures/mounted_fluorescent_lights_diff_1k");
            var box = Resources.Load<GameObject>("Store/cardboard_box_01_1k");
            var shelf = Resources.Load<GameObject>("Store/steel_frame_shelves_01_1k");
            var camera = Resources.Load<GameObject>("Store/security_camera_01_1k");
            var lamp = Resources.Load<GameObject>("Store/mounted_fluorescent_lights_1k");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.gameObject.name;
                if (name == "Floor" || name == "Entrance") { if (floor != null) renderer.sharedMaterial = floor; }
                else if (name.Contains("wall") || name.Contains("Wall") || name.Contains("partition")) { if (wall != null) renderer.sharedMaterial = wall; }
                else if (name == "Shelf") PlaceRow(root, shelf, shelfTexture, renderer.transform, 2.1f, 2.4f);
                else if (name.Contains("crate")) PlaceOn(renderer.transform, box, boxTexture, 0.55f);
            }
            Place(root, camera, cameraTexture, new Vector3(-11.2f, 2.5f, 13.5f), Quaternion.Euler(20, 180, 0), 0.35f);
            Place(root, lamp, lightTexture, new Vector3(0, 2.75f, -6), Quaternion.identity, 1.6f);
            Place(root, lamp, lightTexture, new Vector3(0, 2.75f, 0), Quaternion.identity, 1.6f);
            Place(root, lamp, lightTexture, new Vector3(0, 2.75f, 6), Quaternion.identity, 1.6f);
        }
        private static void PlaceRow(Transform root, GameObject prefab, Texture2D texture, Transform host, float height, float spacing)
        {
            if (prefab == null) return;
            host.GetComponent<MeshRenderer>().enabled = false;
            for (int i = -1; i <= 1; i++)
                Place(root, prefab, texture, host.position + new Vector3(0, 0, i * spacing), host.rotation, height);
        }
        private static void PlaceOn(Transform host, GameObject prefab, Texture2D texture, float size)
        {
            if (prefab == null) return;
            var renderer = host.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            var visual = Spawn(prefab, texture, host.position, host.rotation, size);
            if (visual != null) visual.transform.SetParent(host, true);
        }
        private static void Place(Transform root, GameObject prefab, Texture2D texture, Vector3 position, Quaternion rotation, float size)
        {
            var visual = Spawn(prefab, texture, position, rotation, size);
            if (visual != null) visual.transform.SetParent(root, true);
        }
        private static GameObject Spawn(GameObject prefab, Texture2D texture, Vector3 position, Quaternion rotation, float size)
        {
            if (prefab == null) return null;
            var visual = Object.Instantiate(prefab);
            visual.name = prefab.name;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            visual.transform.SetPositionAndRotation(position, rotation);
            visual.transform.localScale = Vector3.one;
            Paint(visual, texture);
            float extent = Mathf.Max(0.001f, BoundsOf(visual).size.y);
            visual.transform.localScale = Vector3.one * (size / extent);
            return visual;
        }
        private static Bounds BoundsOf(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(visual.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        private static void Paint(GameObject visual, Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || texture == null) return;
            var material = new Material(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
        }
        private static Material Material(string textureName, Vector2 scale)
        {
            var texture = Resources.Load<Texture2D>("Store/" + textureName);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (texture == null || shader == null) return null;
            var material = new Material(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
            material.SetColor("_BaseColor", Color.white);
            return material;
        }
    }
}
