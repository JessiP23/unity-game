using System;
using System.Collections;
using System.IO;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace NightSupermarket.Tests.PlayMode
{
    /// <summary>Writes review screenshots of the prototype scene. Runs only when NS_CAPTURE is set.</summary>
    public sealed class VisualCaptureTests
    {
        private static readonly (string name, Vector3 position, Vector3 target)[] Views =
        {
            ("display_pose", new Vector3(-4.2f, 1.6f, 10.8f), new Vector3(-4.2f, 1.3f, 12.9f)),
            ("distraction_bell", new Vector3(-8.8f, 1.6f, -1.8f), new Vector3(-8.8f, 1.2f, 1.8f)),
            ("bonus_display", new Vector3(3, 1.6f, -8.5f), new Vector3(3, 1.2f, -5.7f)),
            ("entrance", new Vector3(0, 1.6f, -13.5f), new Vector3(0, 1.2f, 0)),
            ("aisle", new Vector3(-2.5f, 1.6f, -6), new Vector3(-2.5f, 1.2f, 6)),
            ("checkout", new Vector3(8, 1.6f, -6), new Vector3(3, 1f, -12)),
            ("warehouse", new Vector3(-9, 1.7f, 9), new Vector3(-13.5f, 1f, 13.5f)),
            ("guard", new Vector3(3, 1.6f, -9.5f), new Vector3(3, 1.1f, -5)),
            ("mannequins", new Vector3(-4.2f, 1.5f, 9.8f), new Vector3(-4.2f, 1.1f, 13.5f)),
            ("clothing", new Vector3(0.5f, 1.7f, 6.2f), new Vector3(-5f, 0.9f, 11.5f)),
            ("electronics", new Vector3(-0.5f, 1.7f, 6.5f), new Vector3(5f, 1f, 13f)),
            ("home", new Vector3(8.8f, 1.7f, -6.5f), new Vector3(13f, 0.6f, 0.5f)),
            ("entrance_crowd", new Vector3(3.5f, 2.2f, -5f), new Vector3(-1f, 0.8f, -13.5f)),
            ("crowd", new Vector3(-13.5f, 2.8f, -13.5f), new Vector3(3f, 0.5f, 3f)),
            ("overview", new Vector3(13, 2.7f, -14), new Vector3(-4, 0, 6)),
            ("produce", new Vector3(-9.5f, 1.6f, -12.5f), new Vector3(-12.3f, 0.8f, -8f)),
            ("staff", new Vector3(9.5f, 1.6f, 6f), new Vector3(13f, 1f, 12f)),
            ("security", new Vector3(-11.3f, 1.6f, 10.2f), new Vector3(-12f, 1f, 12.8f)),
            ("shelf_closeup", new Vector3(-3.6f, 1.4f, -1.2f), new Vector3(-4.6f, 1f, 0.2f)),
            // The mall, the climb, and the close-ups that were reported as too basic.
            ("atrium", new Vector3(8.5f, 3.2f, -31.5f), new Vector3(-2f, 0.5f, -19f)),
            ("fountain", new Vector3(3.6f, 1.7f, -28.2f), new Vector3(0f, 1.1f, -24f)),
            ("fountain_closeup", new Vector3(1.8f, 1.2f, -26.6f), new Vector3(0f, 0.9f, -24f)),
            ("toy_store", new Vector3(-13.5f, 2.4f, -31.5f), new Vector3(-22f, 1f, -23f)),
            ("carousel", new Vector3(-16.5f, 1.8f, -30.2f), new Vector3(-21f, 1.5f, -25.5f)),
            ("carousel_horse", new Vector3(-18.0f, 1.5f, -27.4f), new Vector3(-20.3f, 1.1f, -25.5f)),
            ("robot_shelf", new Vector3(-16.2f, 1.6f, -27.8f), new Vector3(-16.2f, 2.2f, -31.2f)),
            ("escalator", new Vector3(0.8f, 1.7f, 2.8f), new Vector3(0.8f, 2f, 12f)),
            ("upstairs", new Vector3(2.0f, 4.9f, 13.6f), new Vector3(10f, 3.6f, 9f)),
            ("tv_wall", new Vector3(4.2f, 1.4f, 11.6f), new Vector3(4.2f, 1.2f, 14.2f)),
            ("tv_wall_side", new Vector3(1.2f, 1.3f, 13.0f), new Vector3(4.5f, 1.2f, 13.9f)),
            ("security_monitors", new Vector3(-12f, 1.3f, 11.4f), new Vector3(-12f, 1.0f, 12.7f)),
            ("clothing_racks", new Vector3(-4.2f, 1.6f, 7.6f), new Vector3(-4.2f, 0.9f, 11f)),
        };
        private static readonly (string name, LightingMode mode, Vector3 position, Vector3 target)[] Moods =
        {
            ("mood_dark", LightingMode.Dark, new Vector3(2.5f, 1.6f, -3.8f), new Vector3(2.5f, 1.2f, 6)),
            ("mood_emergency", LightingMode.Emergency, new Vector3(2.5f, 1.6f, -3.8f), new Vector3(2.5f, 1.2f, 6)),
            ("mood_partial", LightingMode.Partial, new Vector3(-7.5f, 1.6f, -9f), new Vector3(-7.5f, 1.2f, 6)),
            ("mood_dawn", LightingMode.Dawn, new Vector3(2.5f, 1.6f, 3), new Vector3(0, 1.2f, -14)),
        };
        [UnityTest] public IEnumerator CaptureReviewShots()
        {
            string folder = Environment.GetEnvironmentVariable("NS_CAPTURE");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Set NS_CAPTURE to a folder to write screenshots.");
            Directory.CreateDirectory(folder);
            yield return SceneManager.LoadSceneAsync("Prototype");
            for (int i = 0; i < 30; i++) yield return null;
            yield return new WaitForSeconds(5f);
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            Assert.That(root, Is.Not.Null);
            foreach (var body in Object.FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None)) body.SetFirstPerson(false);
            var source = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var rig = new GameObject("Capture camera").AddComponent<Camera>();
            if (source != null) rig.CopyFrom(source);
            rig.fieldOfView = 70; rig.nearClipPlane = 0.05f; rig.farClipPlane = 80;
            rig.clearFlags = CameraClearFlags.SolidColor; rig.backgroundColor = Color.black;
            var data = rig.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            rig.targetTexture = texture;
            try
            {
                foreach (var view in Views)
                {
                    rig.transform.position = view.position;
                    rig.transform.LookAt(view.target);
                    yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, view.name + ".png"));
                }
                foreach (var mood in Moods)
                {
                    LightingPresenter.Apply(mood.mode);
                    rig.transform.position = mood.position;
                    rig.transform.LookAt(mood.target);
                    for (int i = 0; i < 12; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, mood.name + ".png"));
                }
                LightingPresenter.Apply(LightingMode.Normal);
                var debug = Object.FindAnyObjectByType<NpcDebugOverlay>();
                if (debug != null)
                {
                    rig.transform.position = new Vector3(-9.5f, 2.6f, -9.5f); rig.transform.LookAt(new Vector3(-1f, 0.4f, 1f));
                    foreach (var (layer, label) in new[] { (0, "labels"), (1, "perception"), (2, "navigation") })
                    {
                        debug.Labels = layer == 0; debug.Perception = layer == 1; debug.Navigation = layer == 2;
                        for (int i = 0; i < 3; i++) yield return null;
                        Save(rig, texture, pixels, Path.Combine(folder, "npc_debug_" + label + ".png"));
                    }
                    debug.Labels = debug.Perception = debug.Navigation = true;
                    rig.transform.position = new Vector3(-3.2f, 2.3f, -7.5f); rig.transform.LookAt(new Vector3(-2.5f, 0.8f, 0f));
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "npc_debug.png"));
                    debug.Labels = debug.Perception = debug.Navigation = false;
                }
                // The crowd's cast, lined up in the atrium: a family, a phone zombie, the influencer, a cleaner by a wet floor.
                root.Crowd.EnableKinds(true);
                var cast = new System.Collections.Generic.List<NpcController>();
                var parent = root.Population.Spawn(NpcRole.Customer, new Vector3(-5f, 0f, -19.5f), 180f, true);
                cast.Add(parent);
                cast.Add(root.Population.Spawn(NpcRole.Customer, new Vector3(-4.3f, 0f, -19.3f), 180f, true, ShopperArchetypes.Kid(), ShopperKind.Kid, (CustomerBehavior)parent.Behavior));
                cast.Add(root.Population.Spawn(NpcRole.Customer, new Vector3(-2.6f, 0f, -19.5f), 180f, true, ShopperArchetypes.PhoneZombie(), ShopperKind.PhoneZombie));
                cast.Add(root.Population.Spawn(NpcRole.Customer, new Vector3(-0.6f, 0f, -19.5f), 180f, true, ShopperArchetypes.Influencer(), ShopperKind.Influencer));
                var cleaner = root.Population.Spawn(NpcRole.Employee, new Vector3(1.6f, 0f, -19.5f), 180f, true);
                CustomerPopulationManager.Dress(cleaner, ShopperKind.Cleaner);
                cast.Add(cleaner);
                WetFloor.Create(root.transform, new Vector3(2.4f, 0f, -18.6f), 180f);
                yield return new WaitForSeconds(1f); // poses blend in over a third of a second
                float timeScale = Time.timeScale;
                Time.timeScale = 0f;
                // Facing the camera, so the props, poses and the influencer's light read.
                foreach (var npc in cast) { npc.transform.position = new Vector3(npc.transform.position.x, 0f, -19.5f); npc.transform.rotation = Quaternion.identity; }
                for (int i = 0; i < 4; i++) yield return null;
                rig.transform.position = new Vector3(-1.6f, 1.5f, -15.6f); rig.transform.LookAt(new Vector3(-1.6f, 0.9f, -19.5f));
                yield return null;
                Save(rig, texture, pixels, Path.Combine(folder, "crowd_cast.png"));
                rig.transform.position = new Vector3(-0.6f, 1.55f, -17.6f); rig.transform.LookAt(new Vector3(-0.6f, 1.3f, -19.5f));
                yield return null;
                Save(rig, texture, pixels, Path.Combine(folder, "crowd_influencer.png"));
                Time.timeScale = timeScale;
                root.Crowd.EnableKinds(false);
                foreach (var npc in new System.Collections.Generic.List<NpcController>(root.Population.Active))
                {
                    var look = npc.GetComponent<ShopperLook>();
                    if (cast.Contains(npc) || (look != null && look.Kind != ShopperKind.Regular)) npc.Depart();
                }
                foreach (var wet in Object.FindObjectsByType<WetFloor>(FindObjectsSortMode.None)) Object.Destroy(wet.gameObject);

                var hud = GameObject.Find("Prototype HUD");
                if (hud != null)
                {
                    var canvas = hud.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = rig; canvas.planeDistance = 0.5f;
                    rig.transform.position = new Vector3(-2.5f, 1.6f, -6); rig.transform.LookAt(new Vector3(-2.5f, 1.4f, 6));
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "hud.png"));
                    var help = typeof(PrototypeRoot).GetField("help", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var page = typeof(PrototypeRoot).GetField("menuPage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    page?.SetValue(root, MenuPage.Controls);
                    help?.SetValue(root, true);
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "hud_controls.png"));
                    page?.SetValue(root, MenuPage.Missions);
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "hud_missions.png"));
                    page?.SetValue(root, MenuPage.Cameras);
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "hud_cameras.png"));
                    help?.SetValue(root, false);
                    for (int i = 0; i < 3; i++) yield return null;
                    var card = ShiftCard.Create(canvas);
                    card.Show("Shift #286  ·  Guard: Doris (eagle-eyed)  ·  Crowd: Busy  ·  Dare: Photobomb", Difficulty.Normal, 1);
                    for (int i = 0; i < 3; i++) yield return null;
                    Save(rig, texture, pixels, Path.Combine(folder, "shift_card.png"));
                    card.Hide();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
            finally
            {
                rig.targetTexture = null;
                Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(rig.gameObject);
                Cursor.lockState = CursorLockMode.None;
            }
        }
        private static void Save(Camera rig, RenderTexture texture, Texture2D pixels, string path)
        {
            rig.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
    }
}
