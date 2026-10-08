using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Puts real props on the prototype's coloured boxes. The box keeps its collider (so interaction
    /// and physics are unchanged) and loses its renderer; the prop is fitted to a target height and
    /// stood on the box's base. If the art pack is missing the box stays visible in a quieter colour.
    /// </summary>
    public static class ObjectiveDressing
    {
        /// <summary>Spawns a prop, scales it to <paramref name="height"/> metres and rests its base at <paramref name="floor"/>.</summary>
        public static GameObject FitProp(string id, Transform parent, Vector3 floor, float yaw, float height)
        {
            var holder = ArtLibrary.Spawn(id, parent, Vector3.zero, yaw, 1f);
            if (holder == null) return null;
            holder.transform.position = floor;
            var bounds = ArtLibrary.BoundsOf(holder);
            if (bounds.size.y > 0.001f) holder.transform.localScale = Vector3.one * (height / bounds.size.y);
            bounds = ArtLibrary.BoundsOf(holder);
            holder.transform.position += new Vector3(floor.x - bounds.center.x, floor.y - bounds.min.y, floor.z - bounds.center.z);
            return holder;
        }

        /// <summary>
        /// Dresses a primitive box: the prop travels with the box (so carried crates keep their look)
        /// through an unscaled child, and the box's own renderer is hidden. Returns false without art.
        /// </summary>
        public static bool Dress(GameObject box, string prop, float height, float yaw = 0f, Color? fallback = null)
        {
            var renderer = box.GetComponent<Renderer>();
            var size = box.transform.localScale;
            Vector3 floor = box.transform.position - Vector3.up * size.y * 0.5f;
            var carrier = new GameObject("Dress").transform;
            carrier.position = floor;
            carrier.rotation = Quaternion.identity;
            carrier.SetParent(box.transform, true); // world scale stays 1 under the scaled box
            var visual = FitProp(prop, carrier, floor, yaw, height);
            if (visual == null)
            {
                Object.Destroy(carrier.gameObject);
                if (fallback.HasValue && renderer != null) renderer.sharedMaterial = ArtLibrary.Lit(fallback.Value, 0.35f);
                return false;
            }
            if (renderer != null) renderer.enabled = false;
            return true;
        }

        public static void Recolor(GameObject box, Color color, float smoothness = 0.35f)
        {
            var renderer = box.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = ArtLibrary.Lit(color, smoothness);
        }

        public static void Surface(GameObject box, string surface, Vector2 tiling, Color tint, Color fallback)
        {
            var renderer = box.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = ArtLibrary.Surface(surface, tiling, tint, 0.3f) ?? ArtLibrary.Lit(fallback, 0.3f);
        }

        /// <summary>A small hanging sign above a thing, readable from the store side.</summary>
        public static Transform Sign(Transform parent, string text, Vector3 position, float yaw, Color panel, Color ink, float glow = 0.4f, float letter = 0.12f) =>
            SignFactory.Sign(parent, text, position, yaw, letter, panel, ink, true, glow);
    }
}
