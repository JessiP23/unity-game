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
            ("entrance", new Vector3(0, 1.6f, -13.5f), new Vector3(0, 1.2f, 0)),
            ("aisle", new Vector3(-2.5f, 1.6f, -6), new Vector3(-2.5f, 1.2f, 6)),
            ("checkout", new Vector3(8, 1.6f, -6), new Vector3(3, 1f, -12)),
            ("warehouse", new Vector3(-9, 1.7f, 9), new Vector3(-13.5f, 1f, 13.5f)),
            ("guard", new Vector3(3, 1.6f, -9.5f), new Vector3(3, 1.1f, -5)),
            ("mannequins", new Vector3(0.8f, 1.5f, -8.5f), new Vector3(0.8f, 1f, -11)),
            ("overview", new Vector3(13, 2.7f, -14), new Vector3(-4, 0, 6)),
            ("produce", new Vector3(-9.5f, 1.6f, -12.5f), new Vector3(-12.3f, 0.8f, -8f)),
            ("staff", new Vector3(9.5f, 1.6f, 6f), new Vector3(13f, 1f, 12f)),
            ("security", new Vector3(-11.3f, 1.6f, 10.2f), new Vector3(-12f, 1f, 12.8f)),
            ("shelf_closeup", new Vector3(-3.6f, 1.4f, -1.2f), new Vector3(-4.6f, 1f, 0.2f)),
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
