using System.Collections;
using System.Reflection;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    public sealed class SoloUsabilityTests
    {
        [UnityTest] public IEnumerator SoloCaptureDropsYouInTheBackroomWithoutFakeTeammates()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root.MannequinCount, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<RescueInteractable>(), Is.Null);
            Assert.That(root.Authority.TryCapture(root.Player.Record.Id), Is.True);
            Assert.That(root.Authority.Phase, Is.EqualTo(MatchPhase.Night));
            Assert.That(root.Player.Record.InBackroom, Is.True);
            Cursor.lockState = CursorLockMode.Locked;
            var prompt = (string)typeof(PrototypeRoot).GetMethod("BuildPrompt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root, null);
            Assert.That(prompt, Does.Contain("Room"));
            Assert.That(prompt, Does.Not.Contain("teammate"));
            Cursor.lockState = CursorLockMode.None;
        }
        [UnityTest] public IEnumerator SoundCuesReachSourceAndExactlyOneListenerIsActive()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var audio = Object.FindAnyObjectByType<StoreAudio>(); int before = audio.PlayedCues;
            root.Authority.Audio.Publish(AudioCue.DetectionWarning);
            root.Authority.Audio.Publish(AudioCue.Capture);
            audio.TestSound();
            Assert.That(audio.PlayedCues, Is.EqualTo(before + 3));
            int listeners = 0;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) listeners++;
            Assert.That(listeners, Is.EqualTo(1));
            Assert.That(AudioListener.volume, Is.GreaterThan(0));
            Assert.That(AudioListener.pause, Is.False);
        }
        [UnityTest] public IEnumerator ClothingPromptExplainsMissingShirtAndInteractionCannotPassThroughWall()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>(); root.Guard.PlayerDriven = true;
            var player = root.Player; var display = Object.FindAnyObjectByType<DisplayPose>();
            Assert.That(display.PromptFor(player), Does.Contain("Need a shirt"));
            player.GetComponent<PlayerInventory>().Items.TryAdd("shirt", 1, 1);
            player.Teleport(new Vector3(display.transform.position.x + 1.1f, .1f, display.transform.position.z));
            Physics.SyncTransforms(); for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(display.PromptFor(player), Does.Contain("E — blend"));
            var probe = player.GetComponent<InteractionProbe>();
            Vector3 origin = player.transform.position + Vector3.up * 1.6f;
            Vector3 direction = (display.transform.position - origin).normalized;
            Assert.That(probe.FindTarget(origin, direction), Is.SameAs(display));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (origin + display.transform.position) / 2;
            wall.transform.localScale = new Vector3(.12f, 3, 2);
            Physics.SyncTransforms();
            Assert.That(probe.FindTarget(origin, direction), Is.Null);
            Object.Destroy(wall);
        }
    }
}
