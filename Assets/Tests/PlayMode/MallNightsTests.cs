using System.Collections;
using System.Collections.Generic;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Crouching and hiding spots, settings, and the four special nights.</summary>
    public sealed class MallNightsTests
    {
        private static IEnumerator Load()
        {
            PrototypeRoot.NextShift = 0;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            float until = Time.time + 5f;
            while ((!root.Crowd.Ready || !root.Hiding.Ready) && Time.time < until) yield return null;
            Assert.That(root.Crowd.Ready && root.Hiding.Ready, Is.True);
        }

        private static IEnumerator Hold(PlayerInputReader input, float seconds, Vector2 move, bool crouch)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { input.Submit(new PlayerCommand(move, false, false, crouch)); yield return new WaitForFixedUpdate(); }
        }

        [UnityTest] public IEnumerator CrouchingMakesYouLowSlowAndQuiet()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            root.Population.Budget = 0;
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart(); // nobody to bump into
            var player = root.MannequinAt(0);
            player.Teleport(new Vector3(-2.5f, 0.1f, -4.6f)); player.transform.rotation = Quaternion.identity;
            yield return Hold(root.InputAt(0), 0.6f, new Vector2(0, 1), true);
            Assert.That(player.Crouched, Is.True);
            // Walking pace measured on the floor plane over a stretch, not one physics step.
            Vector3 from = player.transform.position;
            yield return Hold(root.InputAt(0), 0.5f, new Vector2(0, 1), true);
            Vector3 travelled = player.transform.position - from; travelled.y = 0f;
            float pace = travelled.magnitude / 0.5f;
            Assert.That(player.GetComponent<PerceptionTarget>().aimHeight, Is.LessThan(0.7f), "watchers aim lower, so counters block them");
            Assert.That(player.GetComponent<CharacterController>().height, Is.LessThan(1.3f));
            Assert.That(pace, Is.LessThan(player.Rules.walkSpeed * PlayerMotor.CrouchPace + 0.2f), "crouched walking is slow (" + pace.ToString("0.00") + " m/s)");
            Assert.That(pace, Is.GreaterThan(0.3f), "but you still move");
            yield return Hold(root.InputAt(0), 0.3f, Vector2.zero, false);
            Assert.That(player.Crouched, Is.False, "standing up in the open");
        }

        [UnityTest] public IEnumerator HidingSpotsSitByLowCoverAndHideYouWhenYouCrouchStill()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var spots = root.Hiding.Spots;
            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(4), "the mall has places to hide");
            foreach (var s in spots)
                Assert.That(NavMesh.SamplePosition(s.transform.position, out _, 0.3f, NavigationAreas.CivilianMask), Is.True, s.Label + " is on open floor");
            var spot = spots[0];
            var player = root.MannequinAt(0);
            var target = player.GetComponent<PerceptionTarget>();
            player.Teleport(spot.transform.position + Vector3.up * 0.05f);
            yield return Hold(root.InputAt(0), 0.2f, Vector2.zero, false);
            Assert.That(target.Hidden, Is.False, "standing is not hiding");
            yield return Hold(root.InputAt(0), 0.8f, Vector2.zero, true);
            Assert.That(target.Hidden, Is.True, "crouched still in the spot: hidden");
            yield return Hold(root.InputAt(0), 0.5f, Vector2.zero, false);
            Assert.That(target.Hidden, Is.False, "standing up gives you away again");
        }

        [UnityTest] public IEnumerator SettingsApplyToTheGame()
        {
            yield return Load();
            float sensitivity = GameSettings.Sensitivity; var graphics = GameSettings.Graphics; float volume = GameSettings.Volume;
            try
            {
                GameSettings.Sensitivity = 2f; GameSettings.Graphics = GraphicsLevel.Low; GameSettings.Volume = 0.3f;
                GameSettings.Apply(false);
                Assert.That(PlayerView.SensitivityScale, Is.EqualTo(2f).Within(1e-4));
                Assert.That(AudioListener.volume, Is.EqualTo(0.3f).Within(1e-4));
                Assert.That(GameSettings.CrowdCeiling, Is.LessThan(40));
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) Assert.That(data.renderShadows, Is.False);
            }
            finally
            {
                GameSettings.Sensitivity = sensitivity; GameSettings.Graphics = graphics; GameSettings.Volume = volume;
                GameSettings.Apply(false);
            }
        }

        [UnityTest] public IEnumerator BlackFridayFloodsTheMallWithDoorbusters()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            int before = root.Population.Customers;
            root.BeginScenario(NightKind.BlackFriday);
            yield return null;
            Assert.That(root.Night, Is.EqualTo(NightKind.BlackFriday));
            Assert.That(root.Crowd.Sales.Count, Is.EqualTo(4), "four doorbusters");
            Assert.That(root.Population.DifficultyFactor, Is.GreaterThan(1.5f));
            Assert.That(root.Population.Customers, Is.GreaterThan(before), "the early crowd walks in");
            Assert.That(root.Crowd.SalesEnabled, Is.True);
        }

        [UnityTest] public IEnumerator ABlackoutKillsTheLightsAndShrinksWhatShoppersSee()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            root.BeginScenario(NightKind.Blackout);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(root.Authority.Lighting.Mode, Is.EqualTo(LightingMode.Dark));
            Assert.That(root.Scenario.Kind, Is.EqualTo(NightKind.Blackout));
            foreach (var npc in root.Population.Active)
                if (npc.Role == NpcRole.Customer && npc.Perception != null && npc.Perception.Sensor != null)
                    Assert.That(npc.Perception.Sensor.Range, Is.LessThanOrEqualTo(8.01f), npc.name + " sees too far in the dark");
            Assert.That(root.Scenario.Torches, Is.GreaterThan(0), "some shoppers light the way with their phones");
        }

        [UnityTest] public IEnumerator TheHeistsDiamondIsWorthOnlyWhatYouCarryOut()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            root.Population.Budget = 0;
            foreach (var npc in new List<NpcController>(root.Population.Active)) npc.Depart();
            root.BeginScenario(NightKind.Heist);
            yield return null;
            Assert.That(GameObject.Find("Fashion runway"), Is.Not.Null);
            var caseHost = GameObject.Find("Diamond case");
            Assert.That(caseHost, Is.Not.Null);
            var prize = caseHost.GetComponent<HoldSpot>();
            var player = root.MannequinAt(0);
            Vector3 at = caseHost.transform.position;
            Vector3 floor = new Vector3(at.x, 0f, at.z);
            Assert.That(NavMesh.SamplePosition(floor + Vector3.back * 1.1f, out var stand, 1.0f, NavMesh.AllAreas), Is.True, "you can walk up to the case");
            player.Teleport(stand.position + Vector3.up * 0.1f);
            player.transform.rotation = Quaternion.LookRotation(new Vector3(at.x - stand.position.x, 0f, at.z - stand.position.z));
            Physics.SyncTransforms();
            Assert.That(prize.TryInteract(player), Is.True, "E on the case starts lifting the diamond");
            float end = Time.time + 5f;
            while (Time.time < end && !root.Scenario.Carrying(player)) { root.InputAt(0).Submit(new PlayerCommand(Vector2.zero, false, false)); yield return new WaitForFixedUpdate(); }
            Assert.That(root.Scenario.Carrying(player), Is.True, "held still: you have the diamond");
            Assert.That(root.Scenario.PaysOnEscape, Is.True);
            root.Authority.TryCapture(player.Record.Id);
            yield return null; yield return null;
            Assert.That(root.Scenario.Carrying(player), Is.False, "caught: it goes back in the case");
            Assert.That(GameObject.Find("Diamond case").GetComponent<HoldSpot>().Complete, Is.False, "and can be stolen again");
        }

        [UnityTest] public IEnumerator TheBirthdayPartyFillsTheToyStoreAndCakeTimeEmptiesIt()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            root.BeginScenario(NightKind.Birthday);
            yield return null;
            Assert.That(root.Scenario.PartyKids, Is.EqualTo(8));
            Assert.That(GameObject.Find("Birthday present"), Is.Not.Null);
            Assert.That(GameObject.Find("Party table"), Is.Not.Null);
            // Fast-forward to cake time: the kids go to the table.
            root.Authority.DebugAdvance(NightKinds.FirstShowAt + 1);
            yield return new WaitForSeconds(0.5f);
            Assert.That(root.Scenario.Show, Is.EqualTo(SaleStage.Open));
            Assert.That(root.Crowd.Watching(ScenarioDirector.CakeGroup), Is.GreaterThan(0), "kids head for the cake");
            Assert.That(root.Scenario.EventLine(out bool hot), Does.StartWith("CAKE TIME"));
            Assert.That(hot, Is.True);
        }
    }
}
