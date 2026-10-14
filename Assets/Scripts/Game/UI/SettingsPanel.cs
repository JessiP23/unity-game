using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    public enum GraphicsLevel { Low, Medium, High }

    /// <summary>
    /// The player's settings, saved between sessions: volume, mouse sensitivity, invert mouse, graphics and
    /// full screen. Graphics only touches per-camera switches (shadows, anti-aliasing, post-processing) and the
    /// crowd's size, never the shared render-pipeline asset, so nothing in the project changes on disk.
    /// </summary>
    public static class GameSettings
    {
        public static float Volume = 0.8f, Sensitivity = 1f;
        public static bool InvertY, Fullscreen = true;
        public static GraphicsLevel Graphics = GraphicsLevel.High;
        private static bool loaded;

        /// <summary>Most shoppers this graphics level will draw (the frame-rate budget may hold it lower still).</summary>
        public static int CrowdCeiling => Graphics == GraphicsLevel.Low ? 24 : Graphics == GraphicsLevel.Medium ? 32 : 40;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Volume = PlayerPrefs.GetFloat("ns-set-volume", 0.8f);
            Sensitivity = PlayerPrefs.GetFloat("ns-set-mouse", 1f);
            InvertY = PlayerPrefs.GetInt("ns-set-invert", 0) == 1;
            Fullscreen = PlayerPrefs.GetInt("ns-set-fullscreen", 1) == 1;
            Graphics = (GraphicsLevel)Mathf.Clamp(PlayerPrefs.GetInt("ns-set-graphics", 2), 0, 2);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("ns-set-volume", Volume); PlayerPrefs.SetFloat("ns-set-mouse", Sensitivity);
            PlayerPrefs.SetInt("ns-set-invert", InvertY ? 1 : 0); PlayerPrefs.SetInt("ns-set-fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("ns-set-graphics", (int)Graphics);
            PlayerPrefs.Save();
        }

        /// <summary>Applies everything now. <paramref name="window"/> is false in tests (no full-screen changes in the editor).</summary>
        public static void Apply(bool window)
        {
            AudioListener.volume = Mathf.Clamp01(Volume);
            PlayerView.SensitivityScale = Mathf.Clamp(Sensitivity, 0.2f, 3f);
            PlayerView.InvertY = InvertY;
            if (window && !Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) continue;
                data.renderShadows = Graphics != GraphicsLevel.Low;
                data.antialiasing = Graphics == GraphicsLevel.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : Graphics == GraphicsLevel.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                data.renderPostProcessing = Graphics != GraphicsLevel.Low;
            }
        }
    }

    /// <summary>The settings screen: one row per setting, ◀ value ▶, saved as you change them.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private GameObject root;
        private readonly Text[] values = new Text[5];
        public bool Visible => root != null && root.activeSelf;
        public Action Closed;

        public static SettingsPanel Create(Canvas canvas)
        {
            var panel = canvas.gameObject.AddComponent<SettingsPanel>();
            panel.Build(canvas.transform);
            return panel;
        }

        private void Build(Transform canvas)
        {
            var dim = UiTheme.Fill(canvas, "Settings", new Color(0.02f, 0.02f, 0.03f, 0.86f));
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var card = UiTheme.Fill(root.transform, "Card", new Color(0.09f, 0.09f, 0.1f, 0.98f), UiTheme.Round);
            UiTheme.Pin(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 470));
            var title = UiTheme.Label(card.transform, "Title", 40, UiTheme.Gold, TextAnchor.MiddleCenter, header: true);
            UiTheme.Pin(title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(600, 56));
            title.text = "SETTINGS";
            string[] names = { "Volume", "Mouse sensitivity", "Invert mouse", "Graphics", "Full screen" };
            for (int i = 0; i < names.Length; i++)
            {
                int row = i;
                float y = -100 - i * 62;
                var label = UiTheme.Label(card.transform, "Label", 24, UiTheme.Ink, TextAnchor.MiddleLeft);
                UiTheme.Pin(label, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, y), new Vector2(300, 44));
                label.text = names[i];
                Arrow(card.transform, "◄", new Vector2(380, y), () => Change(row, -1));
                values[i] = UiTheme.Label(card.transform, "Value", 24, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true);
                UiTheme.Pin(values[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, y), new Vector2(180, 44));
                Arrow(card.transform, "►", new Vector2(620, y), () => Change(row, +1));
            }
            var done = Arrow(card.transform, "DONE", new Vector2(260, -410), Hide, new Vector2(200, 50));
            done.color = new Color(0.91f, 0.29f, 0.18f, 0.95f);
            root.SetActive(false);
        }

        private Image Arrow(Transform parent, string text, Vector2 at, Action click, Vector2? size = null)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UiTheme.Round; image.type = Image.Type.Sliced; image.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            var button = go.GetComponent<Button>();
            var nav = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            UiTheme.Pin(image, new Vector2(0, 1), new Vector2(0, 1), at, size ?? new Vector2(44, 44));
            var label = UiTheme.Label(go.transform, "Label", 22, Color.white, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Stretch(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = text;
            return image;
        }

        private void Change(int row, int step)
        {
            switch (row)
            {
                case 0: GameSettings.Volume = Mathf.Clamp01(Mathf.Round((GameSettings.Volume + step * 0.1f) * 10f) / 10f); break;
                case 1: GameSettings.Sensitivity = Mathf.Clamp(Mathf.Round((GameSettings.Sensitivity + step * 0.1f) * 10f) / 10f, 0.2f, 3f); break;
                case 2: GameSettings.InvertY = !GameSettings.InvertY; break;
                case 3: GameSettings.Graphics = (GraphicsLevel)(((int)GameSettings.Graphics + step + 3) % 3); break;
                case 4: GameSettings.Fullscreen = !GameSettings.Fullscreen; break;
            }
            GameSettings.Save();
            GameSettings.Apply(true);
            Paint();
        }

        private void Paint()
        {
            values[0].text = Mathf.RoundToInt(GameSettings.Volume * 100) + "%";
            values[1].text = GameSettings.Sensitivity.ToString("0.0") + "×";
            values[2].text = GameSettings.InvertY ? "On" : "Off";
            values[3].text = GameSettings.Graphics.ToString();
            values[4].text = GameSettings.Fullscreen ? "On" : "Off";
        }

        public void Show() { GameSettings.Load(); Paint(); root.SetActive(true); root.transform.SetAsLastSibling(); }
        public void Hide() { if (root == null || !root.activeSelf) return; root.SetActive(false); Closed?.Invoke(); }
    }
}
