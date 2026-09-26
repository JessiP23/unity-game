using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The only bridge between an NPC and how it looks. Uses a real body when the art is present,
    /// otherwise a tinted capsule, so the AI never depends on models. Swapping to final customer models
    /// means adding a <see cref="CharacterProfile"/>, not touching behaviour code.
    /// </summary>
    public sealed class NpcAnimationHooks : MonoBehaviour
    {
        private GameObject placeholder;
        public CharacterVisual Visual { get; private set; }
        public bool UsesPlaceholder => Visual == null;

        public void Dress(CharacterProfile profile, Color placeholderTint)
        {
            if (Visual != null) return;
            if (!TryGetComponent(out MotionInterpolator interpolator)) interpolator = gameObject.AddComponent<MotionInterpolator>();
            interpolator.Interpolating = false;
            if (profile != null) Visual = CharacterVisual.Attach(transform, profile);
            if (Visual != null) return;
            placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = "Placeholder body";
            Destroy(placeholder.GetComponent<Collider>());
            placeholder.transform.SetParent(transform, false);
            placeholder.transform.localPosition = new Vector3(0, 0.9f, 0);
            placeholder.transform.localScale = new Vector3(0.55f, 0.9f, 0.45f);
            placeholder.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(placeholderTint, 0.35f);
            var nose = DressingKit.Panel(placeholder.transform, "Facing", new Vector3(0, 0.62f, 0.45f), new Vector3(0.35f, 0.08f, 0.25f));
            nose.sharedMaterial = ArtLibrary.Lit(Color.Lerp(placeholderTint, Color.black, 0.5f), 0.3f);
        }
    }
}
