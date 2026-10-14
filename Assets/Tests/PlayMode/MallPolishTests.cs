using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>The playtest reports: Clothing E did nothing, the fountain was a box, TV screens floated off their TVs.</summary>
    public sealed class MallPolishTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
        }

        [UnityTest] public IEnumerator EveryClothingRackAndTableLendsClothes()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var racks = Object.FindObjectsByType<FittingRack>(FindObjectsSortMode.None);
            Assert.That(racks.Length, Is.EqualTo(4), "two racks and two tables in Clothing");
            var player = root.Player;
            var rack = racks[0];
            var at = rack.GetComponent<Collider>().bounds;
            player.Teleport(new Vector3(at.center.x, 0.1f, at.min.z - 0.9f));
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Assert.That(rack.TryInteract(player), Is.True, "E on a rack does nothing");
            Assert.That(rack.Wearing(player), Is.True);
            Assert.That(rack.PromptFor(player), Does.Contain("put"));
            Assert.That(rack.TryInteract(player), Is.True, "E again puts it back");
            Assert.That(rack.Wearing(player), Is.False);
            // The probe offers it, so the E badge can float over it.
            var probe = player.GetComponent<InteractionProbe>();
            var view = player.GetComponent<PlayerView>();
            var found = probe.FindTarget(view.View.transform.position, (at.center - view.View.transform.position).normalized);
            Assert.That(found, Is.Not.Null, "looking at the rack offers nothing");
        }

        [UnityTest] public IEnumerator TheFountainTakesOneCoinAndTheSplashIsHeard()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var signals = (WorldSignals)typeof(PrototypeRoot).GetField("signals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(root);
            var rims = Object.FindObjectsByType<FountainCoin>(FindObjectsSortMode.None);
            Assert.That(rims.Length, Is.EqualTo(4), "a coin can be tossed from any side of the basin");
            Assert.That(Object.FindAnyObjectByType<FountainWater>(), Is.Not.Null, "the pool splashes");
            var rim = rims[0];
            var player = root.Player;
            var bounds = rim.GetComponent<Collider>().bounds;
            Vector3 outward = (bounds.center - FountainDressing.Centre); outward.y = 0f;
            player.Teleport(new Vector3(bounds.center.x, 0.1f, bounds.center.z) + outward.normalized * 0.9f);
            Physics.SyncTransforms();
            var heard = new List<NoiseEvent>();
            using (signals.Noise.Subscribe(heard.Add))
            {
                Assert.That(rim.TryInteract(player), Is.True, "E on the fountain rim does nothing");
                Assert.That(rim.TryInteract(player), Is.False, "one wish a night");
                Assert.That(rim.Prompt, Does.Contain("tonight"));
                yield return new WaitForSeconds(1.0f);
            }
            Assert.That(heard.Exists(n => n.Source.Contains("fountain")), Is.True, "the splash is a noise the guard can hear");
        }

        /// <summary>
        /// Fits a screen to a fresh, unbatched copy of each TV model, then measures independently: a ray
        /// fired at the middle of the panel must hit the TV's glass within 1.5 cm behind it.
        /// </summary>
        [UnityTest] public IEnumerator ScreensSitOnTheirTelevisions()
        {
            yield return Load();
            if (!ArtLibrary.Available) Assert.Ignore("Run Tools/fetch_art.py first: screens are only fitted to real TV models.");
            var bench = new GameObject("Screen bench").transform;
            bench.position = new Vector3(200f, 0f, 200f);
            try
            {
                var ids = new[] { "Television_01", "television_02" };
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    // Each TV on its own spot: a ray at the second TV must not meet the first one.
                    var tv = ArtLibrary.Spawn(id, bench, new Vector3(i * 4f, 0f, 0f), 180f);
                    Assert.That(tv, Is.Not.Null, id + " is missing from the art pack");
                    foreach (var filter in tv.GetComponentsInChildren<MeshFilter>())
                        if (filter.sharedMesh != null && filter.sharedMesh.isReadable) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    var panel = DressingKit.FitScreen(bench, tv, Vector3.back, 0.56f, ArtLibrary.Emissive(Color.cyan, 1f), "Bench screen");
                    Assert.That(panel, Is.Not.Null, "could not fit a screen to " + id);
                    yield return null; // the panel's own collider is destroyed at the end of the frame
                    Physics.SyncTransforms();
                    Vector3 middle = panel.bounds.center;
                    Assert.That(Physics.Raycast(middle + Vector3.back * 0.3f, Vector3.forward, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore), Is.True, "nothing behind the screen of " + id);
                    float gap = hit.point.z - middle.z;
                    Assert.That(gap, Is.InRange(-0.002f, 0.015f), id + ": the glow sits " + (gap * 100f).ToString("0.0") + " cm in front of the glass");
                }
            }
            finally { Object.Destroy(bench.gameObject); }
        }

        [UnityTest] public IEnumerator TheNightHasADare()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var dare = (Dare)typeof(PrototypeRoot).GetField("dare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(root);
            Assert.That(dare, Is.EqualTo(NightDare.For(0)));
            Assert.That(Object.FindAnyObjectByType<PhotoBooth>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PhotoBooth>().Enabled, Is.False, "test runs never write photos");
        }
    }
}
