using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>World-space signs: a coloured board with depth-tested 3D text, optionally hung from the ceiling.</summary>
    public static class SignFactory
    {
        public const float CeilingHeight = 3.0f;
        private static Font font;
        private static Material textMaterial;

        /// <summary>Sign text is unlit; dim it when the lights go out.</summary>
        public static void SetBrightness(float level)
        {
            if (textMaterial != null) textMaterial.SetColor("_Color", new Color(level, level, level, 1));
        }

        /// <summary>Flat sign; readers stand on the side local -Z faces after <paramref name="yaw"/>.</summary>
        public static Transform Sign(Transform parent, string text, Vector3 position, float yaw, float letterHeight, Color panel, Color ink,
            bool doubleSided, float glow = 0, bool hang = false)
        {
            var sign = new GameObject("Sign " + text).transform;
            sign.SetParent(parent, false);
            sign.localPosition = position;
            sign.localRotation = Quaternion.Euler(0, yaw, 0);
            float width = text.Length * letterHeight * 0.68f + letterHeight * 1.6f;
            var board = DressingKit.Panel(sign, "Board", Vector3.zero, new Vector3(width, letterHeight * 2f, 0.04f));
            board.sharedMaterial = glow > 0 ? ArtLibrary.Emissive(panel, glow) : ArtLibrary.Lit(panel, 0.35f);
            Text(sign, text, new Vector3(0, 0, -0.022f), 0, letterHeight, ink);
            if (doubleSided) Text(sign, text, new Vector3(0, 0, 0.022f), 180, letterHeight, ink);
            if (hang)
            {
                float drop = CeilingHeight - position.y - letterHeight;
                var wire = ArtLibrary.Lit(new Color(0.2f, 0.2f, 0.2f), 0.4f, 0.8f);
                foreach (float side in new[] { -1f, 1f })
                    DressingKit.Panel(sign, "Sign wire", new Vector3(side * (width * 0.5f - 0.08f), letterHeight + drop * 0.5f, 0), new Vector3(0.012f, drop, 0.012f)).sharedMaterial = wire;
            }
            return sign;
        }

        private static void Text(Transform sign, string text, Vector3 position, float yaw, float letterHeight, Color ink)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            if (textMaterial == null)
            {
                var template = Resources.Load<Material>("Materials/WorldText");
                textMaterial = template != null ? new Material(template) : new Material(font.material);
                textMaterial.mainTexture = font.material.mainTexture;
                Font.textureRebuilt += rebuilt => { if (rebuilt == font && textMaterial != null) textMaterial.mainTexture = rebuilt.material.mainTexture; };
            }
            var label = new GameObject("Text");
            label.transform.SetParent(sign, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var mesh = label.AddComponent<TextMesh>();
            mesh.font = font; mesh.text = text; mesh.fontSize = 96;
            mesh.characterSize = letterHeight * 10f / 96f * 1.4f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.color = ink;
            label.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
        }
    }
}
