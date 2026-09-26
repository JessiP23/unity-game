using System.Text;
using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Placeholder overlay: a compact status box top-left, the interaction prompt bottom-centre,
    /// and a collapsible two-column controls list bottom-right. Uses Unity's built-in font so it
    /// renders in the Editor, builds, and batch runs alike. Layout can change without touching rules.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public bool Ready { get; private set; }
        private Text status, prompt;
        private readonly Text[] keys = new Text[2], actions = new Text[2];
        private GameObject promptPanel;
        private Font font;
        private readonly StringBuilder left = new StringBuilder(), right = new StringBuilder();
        public void Configure()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) { Debug.LogWarning("[UI] Built-in font unavailable; using IMGUI."); return; }
            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var statusPanel = Panel(canvasObject.transform, "Status", new Vector2(0, 1), new Vector2(24, -24), false);
            status = Label(statusPanel, 20, TextAnchor.UpperLeft);
            promptPanel = Panel(canvasObject.transform, "Prompt", new Vector2(0.5f, 0), new Vector2(0, 110), false);
            prompt = Label(promptPanel, 22, TextAnchor.MiddleCenter);
            var controls = Panel(canvasObject.transform, "Controls", new Vector2(1, 0), new Vector2(-24, 24), true);
            for (int i = 0; i < 2; i++)
            {
                keys[i] = Label(controls, 16, TextAnchor.UpperLeft);
                actions[i] = Label(controls, 16, TextAnchor.UpperLeft);
            }
            Crosshair(canvasObject.transform);
            Ready = true;
        }
        /// <summary>Dark box that grows to fit its text, pinned to one corner of the screen.</summary>
        private static GameObject Panel(Transform canvas, string name, Vector2 corner, Vector2 offset, bool columns)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ContentSizeFitter));
            panel.transform.SetParent(canvas, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.62f);
            HorizontalOrVerticalLayoutGroup layout = columns ? panel.AddComponent<HorizontalLayoutGroup>() : panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 18;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }
        private Text Label(GameObject panel, int size, TextAnchor alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(panel.transform, false);
            var label = textObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = new Color(0.93f, 0.95f, 0.97f);
            label.alignment = alignment; label.supportRichText = true; label.lineSpacing = 1.1f;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        private static void Crosshair(Transform canvas)
        {
            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(canvas, false);
            var rect = (RectTransform)dot.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(5, 5);
            dot.GetComponent<Image>().color = new Color(1, 1, 1, 0.7f);
        }
        /// <summary>
        /// <paramref name="helpText"/> lines are "key\taction"; lines without a tab span the key column.
        /// A blank line starts the second side-by-side section.
        /// </summary>
        public void Show(string statusText, string promptText, string helpText)
        {
            if (!Ready) return;
            status.text = statusText;
            prompt.text = promptText;
            promptPanel.SetActive(!string.IsNullOrEmpty(promptText));
            string[] sections = helpText.Split(new[] { "\n\n" }, 2, System.StringSplitOptions.None);
            for (int i = 0; i < 2; i++)
            {
                left.Clear(); right.Clear();
                if (i < sections.Length)
                    foreach (string line in sections[i].Split('\n'))
                    {
                        int tab = line.IndexOf('\t');
                        left.Append(tab < 0 ? line : line.Substring(0, tab)).Append('\n');
                        right.Append(tab < 0 ? "" : line.Substring(tab + 1)).Append('\n');
                    }
                keys[i].text = left.ToString().TrimEnd('\n');
                actions[i].text = right.ToString().TrimEnd('\n');
                keys[i].gameObject.SetActive(left.ToString().Trim().Length > 0);
                actions[i].gameObject.SetActive(right.ToString().Trim().Length > 0);
            }
        }
    }
}
