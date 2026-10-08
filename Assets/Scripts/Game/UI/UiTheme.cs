using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Shared Night Mart UI look: dark glass cards, one coral accent, gold for "look here".
    /// Two fonts: Oswald for the clock and titles, Inter for everything a player must read.
    /// Every label gets a soft shadow so it stays legible over the bright ceiling. No gameplay.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.97f, 0.95f, 0.91f);
        public static readonly Color Mute = new Color(0.74f, 0.72f, 0.68f);
        public static readonly Color Gold = new Color(0.98f, 0.78f, 0.30f);
        public static readonly Color Coral = new Color(0.91f, 0.29f, 0.18f);
        public static readonly Color Glass = new Color(0.06f, 0.06f, 0.07f, 0.90f);
        public static readonly Color Rail = new Color(0.04f, 0.04f, 0.05f, 0.96f);
        public static readonly Color Chip = new Color(0.08f, 0.08f, 0.09f, 0.86f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Good = new Color(0.49f, 0.89f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.64f, 0.27f);
        public static readonly Color Bad = new Color(1f, 0.40f, 0.35f);
        public static readonly Color Pose = new Color(0.45f, 0.72f, 1f);
        /// <summary>Headline font: clock, card titles, the big banner.</summary>
        public static Font Font { get; private set; }
        /// <summary>Reading font, regular weight.</summary>
        public static Font Body { get; private set; }
        /// <summary>Reading font, semi-bold. Real weight; no faked bold.</summary>
        public static Font BodyBold { get; private set; }
        /// <summary>Nothing a player reads goes below this size at the 1600x900 reference.</summary>
        public const int MinimumSize = 18;
        private static Sprite pixel, round, disk;

        public static void Ensure()
        {
            if (Font == null)
                Font = Resources.Load<Font>("Fonts/Oswald-Header")
                    ?? Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Helvetica Neue", "Arial" }, 48)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Body == null)
                Body = Resources.Load<Font>("Fonts/Inter-Body")
                    ?? Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Segoe UI", "Arial" }, 32)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (BodyBold == null)
                BodyBold = Resources.Load<Font>("Fonts/Inter-Bold") ?? Body;
            if (pixel == null) pixel = Sprite.Create(Texture(2, 2, Color.white), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100);
            if (round == null) round = Rounded(32, 8);
            if (disk == null) disk = Rounded(32, 16);
        }

        /// <summary>Text is shown as written. Earlier builds spaced every word with dots; that killed word shapes.</summary>
        public static string Read(string text) => text ?? "";

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

        /// <summary>A reading label. <paramref name="bold"/> picks the semi-bold face; <paramref name="header"/> the headline font.</summary>
        public static Text Label(Transform parent, string name, int size, Color color, TextAnchor align, bool bold = false, bool header = false, bool shadow = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = header ? Font : bold ? BodyBold : Body;
            text.fontSize = header ? size : Mathf.Max(size, MinimumSize);
            text.color = color;
            text.alignment = align;
            text.fontStyle = FontStyle.Normal;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignByGeometry = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var drop = go.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.85f);
                drop.effectDistance = new Vector2(1.5f, -1.5f);
                drop.useGraphicAlpha = true;
            }
            return text;
        }

        /// <summary>Legacy signature kept for callers that passed a FontStyle; bold maps to the semi-bold face.</summary>
        public static Text Label(Transform parent, string name, int size, Color color, TextAnchor align, FontStyle style) =>
            Label(parent, name, size, color, align, style == FontStyle.Bold || style == FontStyle.BoldAndItalic);

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
