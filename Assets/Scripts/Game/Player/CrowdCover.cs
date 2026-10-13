using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Hiding in the crowd, on one mannequin. Five times a second it counts the shoppers walking your way at your
    /// pace within two metres (<see cref="CoverRules"/>); two or more and watchers' suspicion is cut, and the
    /// guard doubts what he saw. A soft ring at your feet and the HUD badge show when it is working.
    /// </summary>
    public sealed class CrowdCover : MonoBehaviour
    {
        public const float SampleSeconds = 0.2f;
        private PlayerMotor motor;
        private PerceptionTarget target;
        private CustomerPopulationManager population;
        private Transform ring;
        private Renderer ringRenderer;
        private Material on, faint;
        private Vector3 lastPosition;
        private float timer, pulse;
        private Vector3 velocity;

        public int Companions { get; private set; }
        public double Strength { get; private set; }
        public bool Covered => Strength > 0;
        public bool Running { get; private set; }
        /// <summary>Seconds spent covered tonight (night metrics).</summary>
        public double CoveredSeconds { get; private set; }
        /// <summary>Close to people but not with them: the HUD tells you why it isn't working.</summary>
        public string Hint { get; private set; } = "";

        public static CrowdCover Attach(PlayerMotor body, CustomerPopulationManager crowd)
        {
            var cover = body.gameObject.AddComponent<CrowdCover>();
            cover.motor = body; cover.population = crowd;
            cover.target = body.GetComponent<PerceptionTarget>();
            cover.lastPosition = body.transform.position;
            cover.BuildRing();
            return cover;
        }

        private void BuildRing()
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Crowd cover ring";
            Destroy(disc.GetComponent<Collider>());
            disc.layer = 2;
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale = new Vector3(1.3f, 0.004f, 1.3f);
            ringRenderer = disc.GetComponent<Renderer>();
            on = ArtLibrary.Particle(new Color(0.35f, 0.9f, 1f, 0.55f)) ?? ArtLibrary.Emissive(new Color(0.35f, 0.9f, 1f), 1.2f);
            faint = ArtLibrary.Particle(new Color(0.35f, 0.9f, 1f, 0.18f)) ?? ArtLibrary.Emissive(new Color(0.2f, 0.5f, 0.6f), 0.6f);
            ringRenderer.sharedMaterial = on;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            ring = disc.transform;
            disc.SetActive(false);
        }

        private void Update()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            lastPosition = transform.position;
            timer -= dt;
            if (timer <= 0) { timer = SampleSeconds; Sample(); }
            if (Covered) CoveredSeconds += dt;
            if (ring != null)
            {
                bool show = Covered && motor.Record != null && motor.Record.Free;
                if (ring.gameObject.activeSelf != show) ring.gameObject.SetActive(show);
                if (show)
                {
                    pulse += dt;
                    float size = (Strength >= 0.7 ? 1.55f : 1.3f) + Mathf.Sin(pulse * 4f) * 0.05f;
                    ring.localScale = new Vector3(size, 0.004f, size);
                    ringRenderer.sharedMaterial = Mathf.Repeat(pulse, 1.2f) < 0.9f ? on : faint;
                }
            }
        }

        private void Sample()
        {
            Running = motor.Record != null && motor.Record.State == PlayerState.Running;
            velocity = motor.PlanarVelocity;
            int count = 0, near = 0, beside = 0; float pace = 0f;
            Vector3 heading = new Vector3(velocity.x, 0f, velocity.z).normalized;
            Vector3 me = transform.position;
            if (population != null && motor.Record != null && motor.Record.Free)
                foreach (var npc in population.Active)
                {
                    if (npc == null || npc.Role != NpcRole.Customer || npc.Movement == null) continue;
                    Vector3 d = npc.transform.position - me;
                    if (Mathf.Abs(d.y) > 1.2f) continue; // other floor
                    if (d.x * d.x + d.z * d.z <= CoverRules.Radius * CoverRules.Radius) near++;
                    Vector3 w = npc.Movement.Velocity;
                    if (!CoverRules.Companion(d.x, d.z, w.x, w.z, velocity.x, velocity.z)) continue;
                    count++;
                    // Only people walking beside you set your pace: coming up behind a group never pins you there.
                    if (Mathf.Abs(Vector3.Dot(new Vector3(d.x, 0f, d.z), heading)) <= 1.2f) { beside++; pace += new Vector2(w.x, w.z).magnitude; }
                }
            Companions = count;
            // Fall into step: alongside two or more walkers your walk slows to a touch faster than theirs, so you stay
            // in the group for a good while and then drift out ahead of it. Sprint to break away at once.
            motor.PaceCap = beside >= 2 && !Running && velocity.sqrMagnitude > CoverRules.StillSpeed * CoverRules.StillSpeed ? Mathf.Max(1.2f, pace / beside + 0.3f) : float.PositiveInfinity;
            Strength = motor.Record != null && motor.Record.Free ? CoverRules.Strength(count, Running) : 0;
            if (target != null) target.Concealment = (float)Strength;
            Hint = Covered ? "" : Running && near >= 2 ? "running gives you away" : near >= 2 && count < 2 ? "walk their way, at their pace" : "";
        }

        private void OnDisable()
        {
            if (target != null) target.Concealment = 0;
            if (motor != null) motor.PaceCap = float.PositiveInfinity;
        }
    }
}
