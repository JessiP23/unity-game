using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>Placeholder overlay. Layout can be replaced without changing match rules.</summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public bool Ready { get; private set; }
        private TextMeshProUGUI label;
        private TMP_FontAsset font;
        public void Configure()
        {
            try { CreateOverlay(); }
            catch (Exception exception) { Debug.LogWarning("[UI] TextMeshPro overlay unavailable. " + exception.Message); }
        }
        private void CreateOverlay()
        {
            Font os = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans" }, 32);
            if (os == null) return;
            font = TMP_FontAsset.CreateFontAsset(os);
            if (font == null) return;
            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObject.AddComponent<CanvasScaler>();
            var textObject = new GameObject("Status");
            textObject.transform.SetParent(canvasObject.transform, false);
            label = textObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 18; label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16, 16); rect.offsetMax = new Vector2(-16, -16);
            Ready = true;
        }
        public void Show(string text) { if (label != null) label.text = text; }
        private void OnDestroy() { if (font != null) Destroy(font); }
    }
}
