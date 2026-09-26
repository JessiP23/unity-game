using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Placeholder spot light. Detection reads <see cref="Model"/> and does not depend on the Light component.</summary>
    public sealed class GuardFlashlight : MonoBehaviour
    {
        public FlashlightModel Model { get; private set; }
        private Light beam;
        public void Configure(float range, float coneDegrees)
        {
            Model = new FlashlightModel(range, coneDegrees, true);
            var lamp = new GameObject("Flashlight");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0.22f, 0.4f, 0.35f);
            beam = lamp.AddComponent<Light>();
            beam.type = LightType.Spot; beam.range = range; beam.spotAngle = coneDegrees; beam.intensity = 6;
            beam.innerSpotAngle = coneDegrees * 0.55f; beam.color = new Color(1f, 0.95f, 0.85f);
            beam.shadows = LightShadows.Soft; beam.shadowStrength = 0.9f;
        }
        public void SetEnabled(bool enabled)
        {
            if (Model == null) return;
            Model.SetEnabled(enabled);
            if (beam != null) beam.enabled = enabled;
        }
    }
}
