using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Saves the night's funny moments as photos: a shopper's selfie with you, the instant you go down
    /// like a plank, the escape. Each is written to Pictures/Night Mall as a PNG and slides in as a
    /// polaroid in the corner for a few seconds. One frame is captured with the HUD hidden; nothing
    /// runs between moments, so it costs nothing while you play. Off in batch mode and test runs.
    /// </summary>
    public sealed class PhotoBooth : MonoBehaviour
    {
        public const int MaxPerNight = 8;
        public bool Enabled = true;
        public int Taken { get; private set; }
        public string Folder { get; private set; }
        private Canvas hud;
        private RectTransform card;
        private RawImage picture;
        private Text caption;
        private Texture2D last;
        private float shownAt = -10f;
        private bool busy;

        public static PhotoBooth Create(Transform parent, Canvas hudCanvas)
        {
            var booth = new GameObject("Photo booth").AddComponent<PhotoBooth>();
            booth.transform.SetParent(parent, false);
            booth.hud = hudCanvas;
            string pictures = "";
            try { pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); } catch { }
            booth.Folder = string.IsNullOrEmpty(pictures) ? Path.Combine(Application.persistentDataPath, "Photos") : Path.Combine(pictures, "Night Mall");
            if (hudCanvas != null) booth.BuildCard(hudCanvas.transform);
            return booth;
        }

        private void BuildCard(Transform canvas)
        {
            var frame = UiTheme.Fill(canvas, "Polaroid", new Color(0.97f, 0.96f, 0.93f, 1f));
            card = frame.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(1f, 0f);
            card.pivot = new Vector2(1f, 0f);
            card.sizeDelta = new Vector2(300f, 270f);
            var shot = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            shot.transform.SetParent(card, false);
            picture = shot.GetComponent<RawImage>();
            UiTheme.Stretch(picture, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 58f), new Vector2(-14f, -14f));
            caption = UiTheme.Label(card, "Caption", 20, new Color(0.15f, 0.13f, 0.12f), TextAnchor.MiddleCenter, bold: true, shadow: false);
            UiTheme.Stretch(caption, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 52f));
            card.gameObject.SetActive(false);
        }

        /// <summary>Takes a photo at the end of this frame (HUD hidden), saves it and shows the polaroid.</summary>
        public void Snap(string moment, float delay = 0f)
        {
            if (!Enabled || busy || Taken >= MaxPerNight || Application.isBatchMode) return;
            StartCoroutine(Capture(moment, delay));
        }

        private IEnumerator Capture(string moment, float delay)
        {
            busy = true;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            bool hudWas = hud != null && hud.enabled;
            if (hud != null) hud.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception error) { Debug.LogWarning("[PHOTO] capture failed: " + error.Message); }
            if (hud != null) hud.enabled = hudWas;
            busy = false;
            if (shot == null) yield break;
            Taken++;
            // Encoding a full-resolution PNG takes a moment: copy the pixels now, encode and write on a worker thread.
            string file = Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + " " + moment + ".png");
            var pixels = shot.GetPixelData<byte>(0).ToArray();
            var format = shot.graphicsFormat; uint width = (uint)shot.width, height = (uint)shot.height;
            string folder = Folder;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllBytes(file, ImageConversion.EncodeArrayToPNG(pixels, format, width, height));
                }
                catch (Exception error) { Debug.LogWarning("[PHOTO] could not save: " + error.Message); }
            });
            if (last != null) Destroy(last);
            last = shot;
            if (picture != null)
            {
                picture.texture = shot;
                caption.text = moment;
                card.gameObject.SetActive(true);
                shownAt = Time.unscaledTime;
            }
        }

        private void Update()
        {
            if (card == null || !card.gameObject.activeSelf) return;
            float age = Time.unscaledTime - shownAt;
            if (age > 4.2f) { card.gameObject.SetActive(false); return; }
            // Slide in from the right with a little tilt, hold, slide out.
            float slide = age < 0.35f ? 1f - Mathf.SmoothStep(0f, 1f, age / 0.35f) : age > 3.8f ? Mathf.SmoothStep(0f, 1f, (age - 3.8f) / 0.4f) : 0f;
            card.anchoredPosition = new Vector2(-28f + slide * 360f, 150f);
            card.localRotation = Quaternion.Euler(0f, 0f, 4f - slide * 6f);
        }

        private void OnDestroy() { if (last != null) Destroy(last); }
    }
}
