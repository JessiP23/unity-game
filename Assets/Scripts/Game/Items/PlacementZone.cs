using UnityEngine;
namespace NightSupermarket.Game
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlacementZone : MonoBehaviour
    {
        public string destinationId = "clothing";
        private void Awake() { GetComponent<BoxCollider>().isTrigger = true; }
        private void OnTriggerStay(Collider other)
        {
            var item = other.GetComponent<PhysicalItem>();
            if (item != null && item.Body.linearVelocity.sqrMagnitude < 0.04f && item.Holder == null)
                item.ReportPlacement(destinationId);
        }
    }
}
