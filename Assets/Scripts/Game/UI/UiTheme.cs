using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>Shared Night Mart UI look: glass cards, coral accent, keycaps. No gameplay.</summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.96f, 0.94f, 0.90f);
        public static readonly Color Mute = new Color(0.72f, 0.70f, 0.66f);
        public static readonly Color Gold = new Color(0.96f, 0.76f, 0.32f);
        public static readonly Color Coral = new Color(0.91f, 0.29f, 0.18f);
        public static readonly Color Glass = new Color(0.07f, 0.07f, 0.08f, 0.86f);
        public static readonly Color Rail = new Color(0.05f, 0.05f, 0.06f, 0.94f);
        public static readonly Color Chip = new Color(0.10f, 0.10f, 0.11f, 0.82f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Good = new Color(0.49f, 0.89f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.62f, 0.27f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.37f);
        public static Font Font { get; private set; }
        private static Sprite pixel, round, disk;

        public static void Ensure()
        {
            if (Font == null)
                Font = Resources.Load<Font>("Fonts/Oswald-Header")
                    ?? Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica Neue", "Helvetica", "Arial" }, 36)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (pixel == null) pixel = Sprite.Create(Texture(2, 2, Color.white), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100);
            if (round == null) round = Rounded(32, 8);
            if (disk == null) disk = Rounded(32, 16);
        }

        public static string Read(string text) => string.IsNullOrEmpty(text) ? "" : text.ToUpperInvariant().Replace(" ", " · ");

        public static Image Fill(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite ?? pixel;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite != pixel) image.type = Image.Type.Sliced;
            return image;
        }

        public static Text Label(Transform parent, string name, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignByGeometry = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static RectTransform Stretch(Component component, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static RectTransform Pin(Component component, Vector2 corner, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Sprite Disk => disk;
        public static Sprite Round => round;

        public static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out var color);
            return color;
        }

        private static Sprite Rounded(int size, int radius)
        {
            var tex = Texture(size, size, Color.clear);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = x < radius ? radius - x : x >= size - radius ? x - (size - radius - 1) : 0;
                    int dy = y < radius ? radius - y : y >= size - radius ? y - (size - radius - 1) : 0;
                    float d = dx == 0 || dy == 0 ? 0 : Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, d <= radius + 0.2f ? Color.white : Color.clear);
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Texture2D Texture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
