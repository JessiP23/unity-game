using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Shoppers who get a good look at a mannequin holding still react like people: a double take,
    /// a compliment, a selfie. Reactions are cosmetic plus a small score, never a report. A reaction
    /// needs a calm shopper within a few metres who can see a mannequin that has not moved for a
    /// moment; each shopper reacts at most once every half minute and admires each mannequin once.
    /// </summary>
    public sealed class ShopperReactions : MonoBehaviour
    {
        public const float Reach = 3.2f, StillSeconds = 1.5f, Cooldown = 28f;
        private CustomerPopulationManager population;
        private Func<IReadOnlyList<PlayerMotor>> mannequins;
        private Func<PlayerMotor, float> stillFor;
        private Func<PlayerMotor, bool> posing;
        private Action<PlayerMotor, string> admired;
        private StoreAudio sounds;
        private readonly Dictionary<NpcController, float> nextAllowed = new Dictionary<NpcController, float>();
        private readonly HashSet<(NpcController, PlayerMotor)> admiredOnce = new HashSet<(NpcController, PlayerMotor)>();
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private float nextScan;
        private System.Random rng = new System.Random(11);
        public int Reactions { get; private set; }

        private sealed class Bubble { public Transform Root; public float Until; public Light Flash; }

        private static readonly string[] Compliments = { "So lifelike!", "Is that new?", "Nice display.", "Ooh, I'd wear that.", "Looks almost real…" };
        private static readonly string[] Selfies = { "*click*", "Say cheese!", "One for the group chat." };
        private static readonly string[] Pokes = { "Is this plastic?", "…did it just blink?", "Hm?" };

        public void Configure(CustomerPopulationManager people, Func<IReadOnlyList<PlayerMotor>> bodies, Func<PlayerMotor, float> stillTime,
            Func<PlayerMotor, bool> holdingPose, Action<PlayerMotor, string> onAdmired, StoreAudio sounds, int seed)
        {
            population = people; mannequins = bodies; stillFor = stillTime; posing = holdingPose; admired = onAdmired; this.sounds = sounds;
            rng = new System.Random(seed);
        }

        private void Update()
        {
            TickBubbles();
            if (population == null || Time.time < nextScan) return;
            nextScan = Time.time + 0.4f;
            var bodies = mannequins();
            foreach (var npc in population.Active)
            {
                if (npc == null || npc.Role != NpcRole.Customer || npc.Perception == null) continue;
                if (npc.Awareness == AwarenessState.Suspicious || npc.Awareness == AwarenessState.Reporting) continue;
                if (nextAllowed.TryGetValue(npc, out float allowed) && Time.time < allowed) continue;
                foreach (var body in bodies)
                {
                    if (body == null || !body.Record.Free) continue;
                    var target = body.GetComponent<PerceptionTarget>();
                    if (target == null || !npc.Perception.Trackers.TryGetValue(target, out var tracker) || !tracker.Visible) continue;
                    float distance = Vector3.Distance(npc.transform.position, body.transform.position);
                    if (distance > Reach || stillFor(body) < StillSeconds) continue;
                    React(npc, body, distance);
                    nextAllowed[npc] = Time.time + Cooldown + (float)rng.NextDouble() * 10f;
                    break;
                }
            }
        }

        private void React(NpcController npc, PlayerMotor body, float distance)
        {
            Reactions++;
            bool poseHeld = posing(body);
            int roll = rng.Next(10);
            string line; float pause; bool selfie = false;
            if (distance < 1.4f && roll < 3) { line = Pokes[rng.Next(Pokes.Length)]; pause = 1.6f; }
            else if (poseHeld && roll < 7) { line = Selfies[rng.Next(Selfies.Length)]; pause = 2.6f; selfie = true; }
            else { line = Compliments[rng.Next(Compliments.Length)]; pause = 1.8f; }
            npc.Movement?.Pause(pause, body.transform.position);
            Say(npc.transform, line, pause + 0.8f, selfie);
            if (selfie && this.sounds != null) this.sounds.Shutter();
            if (admiredOnce.Add((npc, body))) admired?.Invoke(body, selfie ? "A shopper took your photo" : "A shopper admired you");
        }

        private void Say(Transform who, string text, float seconds, bool flash)
        {
            var root = new GameObject("Speech").transform;
            root.SetParent(who, false);
            root.localPosition = new Vector3(0f, 2.15f, 0f);
            var board = PrimitiveWorld.Box(root, "Bubble", root.position, new Vector3(text.Length * 0.062f + 0.3f, 0.3f, 0.03f), new Color(0.97f, 0.96f, 0.92f));
            board.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Lit(new Color(0.97f, 0.96f, 0.92f), 0.2f);
            Destroy(board.GetComponent<Collider>());
            var label = SignFactory.Label(root, text, 0.11f, new Color(0.12f, 0.1f, 0.1f));
            if (label != null) label.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            Light flash_ = null;
            if (flash)
            {
                var lamp = new GameObject("Phone flash");
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = new Vector3(0f, -0.5f, 0.3f);
                flash_ = lamp.AddComponent<Light>();
                flash_.type = LightType.Point; flash_.range = 5f; flash_.intensity = 0f; flash_.color = new Color(0.9f, 0.95f, 1f);
                flash_.shadows = LightShadows.None;
            }
            bubbles.Add(new Bubble { Root = root, Until = Time.time + seconds, Flash = flash_ });
        }

        private void TickBubbles()
        {
            var eyes = Camera.main;
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = bubbles[i];
                if (bubble.Root == null || Time.time > bubble.Until) { if (bubble.Root != null) Destroy(bubble.Root.gameObject); bubbles.RemoveAt(i); continue; }
                if (eyes != null)
                {
                    Vector3 toCamera = eyes.transform.position - bubble.Root.position; toCamera.y = 0;
                    if (toCamera.sqrMagnitude > 0.001f) bubble.Root.rotation = Quaternion.LookRotation(-toCamera);
                }
                if (bubble.Flash != null)
                {
                    float left = bubble.Until - Time.time;
                    // Two quick flashes about a second in.
                    float t = (bubble.Until - 0.8f) - Time.time;
                    bubble.Flash.intensity = (t > 1.0f && t < 1.15f) || (t > 0.75f && t < 0.9f) ? 6f : 0f;
                    if (left < 0) bubble.Flash.intensity = 0f;
                }
            }
        }
    }
}
