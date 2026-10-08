using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>One reusable gold marker. It moves to the current job. No extra lights.</summary>
    public sealed class GuideBeacon : MonoBehaviour
    {
        private Transform arrow;
        private float bob;

        public static GuideBeacon Create(Transform parent)
        {
            var host = new GameObject("Guide beacon");
            host.transform.SetParent(parent, false);
            var beacon = host.AddComponent<GuideBeacon>();
            beacon.arrow = new GameObject("Arrow").transform;
            beacon.arrow.SetParent(host.transform, false);
            var gold = ArtLibrary.Emissive(new Color(1f, 0.78f, 0.2f), 2.2f);
            // A stationary downward pointer identifies the destination; it never implies a route.
            Panel(beacon.arrow, new Vector3(0, 0.48f, 0), new Vector3(0.09f, 0.4f, 0.09f), gold);
            var left = Panel(beacon.arrow, new Vector3(-0.1f, 0.27f, 0), new Vector3(0.09f, 0.3f, 0.09f), gold);
            left.localRotation = Quaternion.Euler(0, 0, 45);
            var right = Panel(beacon.arrow, new Vector3(0.1f, 0.27f, 0), new Vector3(0.09f, 0.3f, 0.09f), gold);
            right.localRotation = Quaternion.Euler(0, 0, -45);
            host.SetActive(false);
            return beacon;
        }

        public void Show(bool on, Vector3 world, Camera viewer = null)
        {
            gameObject.SetActive(on);
            if (!on) return;
            transform.position = world + Vector3.up * 1.55f;
            if (viewer != null)
            {
                Vector3 toward = viewer.transform.position - transform.position; toward.y = 0;
                if (toward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toward);
            }
        }

        private void Update()
        {
            bob += Time.deltaTime;
            arrow.localPosition = new Vector3(0, Mathf.Sin(bob * 3f) * 0.12f, 0);
        }

        private static Transform Panel(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Mark";
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            DestroyImmediate(box.GetComponent<Collider>());
            return box.transform;
        }
    }
}
