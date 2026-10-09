using System;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Unlockable tools that change tactics, not power. The wind-up toy walks off clattering so the
    /// guard follows it; the price gun squeaks at a spot you aim at. Both are noise the existing
    /// hearing system already understands. Uses are per night.
    /// </summary>
    public sealed class GadgetKit : MonoBehaviour
    {
        private Transform root;
        private WorldSignals signals;
        private LocalMatchAuthority authority;
        private int career;
        private Action<string> toast;
        public int ToyUsesLeft { get; private set; }
        public int GunUsesLeft { get; private set; }

        public void Configure(Transform parent, WorldSignals world, LocalMatchAuthority match, int careerPoints, Action<string> say)
        {
            root = parent; signals = world; authority = match; career = careerPoints; toast = say;
            ToyUsesLeft = Career.Unlocked(Gadget.WindUpToy, career) ? Career.Uses(Gadget.WindUpToy) : 0;
            GunUsesLeft = Career.Unlocked(Gadget.PriceGun, career) ? Career.Uses(Gadget.PriceGun) : 0;
        }

        /// <summary>One line for the HUD: what you carry and how many uses remain.</summary>
        public string Status
        {
            get
            {
                string toy = Career.Unlocked(Gadget.WindUpToy, career) ? "X  Wind-up toy " + ToyUsesLeft : "";
                string gun = Career.Unlocked(Gadget.PriceGun, career) ? "Z  Price gun " + GunUsesLeft : "";
                if (toy.Length == 0 && gun.Length == 0) return "";
                return toy + (toy.Length > 0 && gun.Length > 0 ? "   " : "") + gun;
            }
        }

        public bool TryToy(PlayerMotor player)
        {
            if (player == null || !player.Record.Free || authority.Phase != MatchPhase.Night) return false;
            if (!Career.Unlocked(Gadget.WindUpToy, career)) { toast(Career.Name(Gadget.WindUpToy) + " unlocks at " + Career.WindUpToyAt.ToString("N0") + " career points (you have " + career.ToString("N0") + ")"); return false; }
            if (ToyUsesLeft <= 0) { toast("No wind-up toys left tonight"); return false; }
            ToyUsesLeft--;
            Vector3 at = player.transform.position + player.transform.forward * 0.7f + Vector3.up * 0.1f;
            var body = PrimitiveWorld.Box(root, "Wind-up toy", at, new Vector3(0.22f, 0.14f, 0.26f), new Color(0.95f, 0.75f, 0.2f));
            body.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0);
            Destroy(body.GetComponent<Collider>());
            ObjectiveDressing.Dress(body, "gamepad", 0.14f, player.transform.eulerAngles.y, new Color(0.95f, 0.75f, 0.2f));
            body.AddComponent<WindUpToy>().Configure(signals);
            toast("The toy clatters off. Move the other way.");
            return true;
        }

        public bool TryGun(PlayerMotor player, Camera eyes)
        {
            if (player == null || !player.Record.Free || authority.Phase != MatchPhase.Night) return false;
            if (!Career.Unlocked(Gadget.PriceGun, career)) { toast(Career.Name(Gadget.PriceGun) + " unlocks at " + Career.PriceGunAt.ToString("N0") + " career points (you have " + career.ToString("N0") + ")"); return false; }
            if (GunUsesLeft <= 0) { toast("The price gun is out of squeaks"); return false; }
            GunUsesLeft--;
            Vector3 origin = eyes != null ? eyes.transform.position : player.transform.position + Vector3.up * 1.5f;
            Vector3 forward = eyes != null ? eyes.transform.forward : player.transform.forward;
            Vector3 point = Physics.Raycast(origin, forward, out var hit, 14f, ~(1 << 2), QueryTriggerInteraction.Ignore) ? hit.point : origin + forward * 14f;
            signals.Noise.Publish(new NoiseEvent(point, 1.6f, "price gun"));
            var mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "Squeak"; mark.transform.SetParent(root, false); mark.transform.position = point; mark.transform.localScale = Vector3.one * 0.25f;
            mark.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Emissive(new Color(1f, 0.85f, 0.2f), 2.5f);
            Destroy(mark.GetComponent<Collider>());
            Destroy(mark, 1.2f);
            toast("Squeak! The guard will check over there.");
            return true;
        }
    }

    /// <summary>Walks straight ahead for a few seconds making a racket, then winds down where it stops.</summary>
    public sealed class WindUpToy : MonoBehaviour
    {
        private WorldSignals signals;
        private float life = 5.5f, nextClatter;
        public void Configure(WorldSignals world) { signals = world; }
        private void Update()
        {
            if (signals == null) return;
            life -= Time.deltaTime;
            if (life <= 0f) return;
            // Tiny waddle, and stop at the first obstacle.
            if (!Physics.Raycast(transform.position + Vector3.up * 0.1f, transform.forward, 0.35f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position += transform.forward * 1.1f * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, Mathf.Sin(Time.time * 18f) * 8f);
            if (Time.time >= nextClatter)
            {
                nextClatter = Time.time + 0.7f;
                signals.Noise.Publish(new NoiseEvent(transform.position, 0.95f, "wind-up toy"));
            }
        }
    }
}
