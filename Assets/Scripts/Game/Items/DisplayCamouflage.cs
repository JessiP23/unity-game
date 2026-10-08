using UnityEngine;
namespace NightSupermarket.Game
{
    public sealed class DisplayCamouflage : MonoBehaviour
    {
        private DisplayPose display;
        private PlayerMotor motor;
        private PlayerInventory inventory;
        private CarrySystem carry;
        private Vector3 position;
        private float heading;
        public bool Active
        {
            get
            {
                if (display == null) return false;
                if (!Eligible() || Vector3.Distance(position, transform.position) > 0.08f ||
                    Mathf.Abs(Mathf.DeltaAngle(heading, transform.eulerAngles.y)) > 20f)
                    display = null;
                return display != null;
            }
        }
        private bool Eligible() => motor != null && motor.Record.Free && motor.Grounded &&
            motor.ActualSpeed <= motor.Rules.movementThreshold && inventory != null && inventory.Items.Count("shirt") > 0 &&
            (carry == null || carry.Held == null);
        public bool TryPose(DisplayPose spot)
        {
            motor = GetComponent<PlayerMotor>(); inventory = GetComponent<PlayerInventory>(); carry = GetComponent<CarrySystem>();
            if (spot == null || !Eligible() || !InteractionValidation.CanReach(motor, spot.transform)) return false;
            display = spot; position = transform.position; heading = transform.eulerAngles.y;
            return true;
        }
    }
}
