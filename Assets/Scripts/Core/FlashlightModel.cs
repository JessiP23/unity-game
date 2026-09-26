using System;
namespace NightSupermarket.Core
{
    /// <summary>Gameplay cone. Rendering subscribes separately and does not decide detection.</summary>
    public sealed class FlashlightModel
    {
        public bool Enabled { get; private set; }
        public float Range { get; }
        public float ConeDegrees { get; }
        public FlashlightModel(float range, float coneDegrees, bool enabled = true)
        {
            if (float.IsNaN(range) || float.IsInfinity(range) || range <= 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (float.IsNaN(coneDegrees) || coneDegrees <= 0 || coneDegrees >= 180) throw new ArgumentOutOfRangeException(nameof(coneDegrees));
            Range = range; ConeDegrees = coneDegrees; Enabled = enabled;
        }
        public void SetEnabled(bool enabled) => Enabled = enabled;
        public bool Covers(float distance, float angleDegrees)
        {
            if (!Enabled || float.IsNaN(distance) || float.IsNaN(angleDegrees) || distance < 0) return false;
            return distance <= Range && angleDegrees <= ConeDegrees * 0.5f;
        }
    }
}
