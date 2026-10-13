using System;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Clothing you can actually take: a rack of jackets or a table of folded shirts. E puts something on
    /// (a borrowed piece that disguises you in Clothing for the rest of the night, or until you are
    /// caught); E again puts it back. The rule this enforces: in Clothing, anything that looks like
    /// clothes can be used, so pressing E is never met with silence.
    /// </summary>
    public sealed class FittingRack : MonoBehaviour, IInteractable, IPlayerPrompt, ILowPriorityInteractable
    {
        public string Piece = "a jacket";
        public Func<PlayerMotor, bool> Wearing = _ => false;
        public Func<PlayerMotor, bool> Toggle = _ => false;
        public string Prompt => "E — try on " + Piece;
        public string PromptFor(PlayerMotor player) =>
            player != null && Wearing(player) ? "E — put the borrowed clothes back" : "E — try on " + Piece + " (disguise in Clothing)";
        public bool TryInteract(PlayerMotor player)
        {
            if (player == null || !player.Record.Free || !InteractionValidation.CanReach(player, transform)) return false;
            return Toggle(player);
        }
    }

    /// <summary>A small "E" that floats over whatever the player can use, so every usable thing announces itself.</summary>
    public sealed class InteractBadge : MonoBehaviour
    {
        private Transform plate;
        private Renderer plateRenderer;
        private float bob;

        public static InteractBadge Create(Transform parent)
        {
            var host = new GameObject("Interact badge");
            host.transform.SetParent(parent, false);
            var badge = host.AddComponent<InteractBadge>();
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "Badge plate";
            UnityEngine.Object.Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(host.transform, false);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.2f, 0.01f, 0.2f);
            plate.layer = 2;
            badge.plateRenderer = plate.GetComponent<Renderer>();
            badge.plateRenderer.sharedMaterial = ArtLibrary.Emissive(new Color(0.95f, 0.72f, 0.25f), 1.4f);
            badge.plateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            badge.plate = plate.transform;
            var letter = SignFactory.Label(host.transform, "E", 0.11f, new Color(0.1f, 0.08f, 0.05f));
            if (letter != null) { letter.transform.localPosition = new Vector3(0f, 0f, -0.012f); letter.gameObject.layer = 2; }
            host.SetActive(false);
            return badge;
        }

        /// <summary>Shows the badge above a target's top, facing the camera, or hides it when target is null.</summary>
        public void Show(Component target, Camera eyes)
        {
            bool on = target != null && eyes != null;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) return;
            var bounds = Bounds(target);
            bob += Time.unscaledDeltaTime;
            transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.22f + Mathf.Sin(bob * 3f) * 0.025f, bounds.center.z);
            Vector3 toCamera = eyes.transform.position - transform.position;
            if (toCamera.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            float pulse = 1f + Mathf.Sin(bob * 6f) * 0.06f;
            transform.localScale = Vector3.one * pulse * Mathf.Clamp(toCamera.magnitude / 2.5f, 0.8f, 1.6f);
        }

        private static Bounds Bounds(Component target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null && collider.enabled) return collider.bounds;
            var renderer = target.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds : new Bounds(target.transform.position, Vector3.one * 0.2f);
        }
    }
}
