using System.Collections;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The roller shutter over the front door. It stays rolled up (inactive) until lockdown, then slides
    /// down over 2.4 s with its own collider, and the door behind it refuses to open. From then on the
    /// fire exit upstairs is the way out, which is what gives the second floor its reason to exist.
    /// </summary>
    public sealed class FrontShutter : MonoBehaviour
    {
        public const float Seconds = 2.4f;
        private EscapeInteractable door;
        private float top, bottom;
        public bool Down { get; private set; }

        public static FrontShutter Create(Transform parent, EscapeInteractable frontDoor, Vector3 floorPoint)
        {
            var slab = PrimitiveWorld.Box(parent, "Front shutter", floorPoint + new Vector3(0, 1.35f + 2.8f, 0), new Vector3(1.7f, 2.7f, 0.12f), new Color(0.45f, 0.47f, 0.5f));
            slab.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(new Color(0.5f, 0.52f, 0.55f), 0.6f, 0.9f);
            var shutter = slab.AddComponent<FrontShutter>();
            shutter.door = frontDoor;
            shutter.top = floorPoint.y + 1.35f + 2.8f;
            shutter.bottom = floorPoint.y + 1.35f;
            slab.SetActive(false);
            return shutter;
        }

        public void Drop()
        {
            if (Down) return;
            Down = true;
            gameObject.SetActive(true);
            if (door != null) door.Blocked = true;
            StartCoroutine(Slide());
        }

        private IEnumerator Slide()
        {
            float t = 0f;
            while (t < Seconds)
            {
                t += Time.deltaTime;
                float along = Mathf.SmoothStep(0f, 1f, t / Seconds);
                var p = transform.position; p.y = Mathf.Lerp(top, bottom, along); transform.position = p;
                yield return null;
            }
        }
    }
}
