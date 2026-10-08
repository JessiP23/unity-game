using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Hold right mouse to lock the body into the selected pose; scroll to pick one of four poses.
    /// A pose that fits the department you stand in counts as display camouflage (extra guard doubt).
    /// Holding too long builds strain until the body twitches once. Presentation reads
    /// <see cref="Strain"/>; rules live in <see cref="PoseStrain"/>.
    /// </summary>
    public sealed class MannequinPose : MonoBehaviour
    {
        public PoseStrain Strain { get; } = new PoseStrain();
        public Stance Selected { get; private set; } = Stance.Display;
        public bool Holding => Strain.Holding;
        public ZoneType? Zone { get; private set; }
        public bool Matches => Holding && PoseLibrary.ExpectedIn(Zone) == Strain.Current && Strain.Current != Stance.Neutral;
        /// <summary>Set for one tick when strain forces a twitch.</summary>
        public bool TwitchedThisTick { get; private set; }
        private PlayerMotor motor;
        private StoreDirectory directory;
        private CarrySystem carry;
        private float tilt;
        private Transform visual;
        private Quaternion visualRest = Quaternion.identity;

        public void Configure(PlayerMotor body, StoreDirectory store)
        {
            motor = body; directory = store; carry = GetComponent<CarrySystem>();
        }

        public void Cycle(int steps)
        {
            if (steps == 0) return;
            int index = System.Array.IndexOf(PoseLibrary.Selectable, Selected);
            int count = PoseLibrary.Selectable.Length;
            index = ((index + steps) % count + count) % count;
            Selected = PoseLibrary.Selectable[index];
            if (Holding) Strain.Begin(Selected);
        }

        /// <summary>Authoritative step. Returns true if the body may not move this tick.</summary>
        public bool Tick(bool held, float delta)
        {
            TwitchedThisTick = false;
            if (motor == null || motor.Record == null) return false;
            bool allowed = held && motor.Record.Free && motor.Grounded && (carry == null || carry.Held == null);
            if (allowed) Strain.Begin(Selected); else Strain.Release();
            Zone = directory != null ? directory.ZoneAt(transform.position) : null;
            TwitchedThisTick = Strain.Tick(delta);
            return Holding;
        }

        /// <summary>Where the strain bar should sit for the HUD: 0 fresh, 1 twitching.</summary>
        public float Wobble => (float)Strain.Strain;

        private void Update()
        {
            // A small lean that grows with strain so the player can feel the wobble without a bar.
            float target = Holding ? Mathf.Sin(Time.time * 9f) * Wobble * 1.6f : 0f;
            tilt = Mathf.Lerp(tilt, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (visual == null)
            {
                var body = GetComponentInChildren<CharacterVisual>();
                visual = body != null ? body.transform : null;
                if (visual != null) visualRest = visual.localRotation;
            }
            if (visual != null) visual.localRotation = visualRest * Quaternion.Euler(0f, 0f, tilt);
        }
    }
}
