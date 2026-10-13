using System;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// "Clock in": the card before the night where you pick your shift. Three choices side by side, the one you
    /// played last time already selected, so Enter is all a returning player needs. Rush hour is locked until
    /// you have a few nights behind you.
    /// </summary>
    public sealed class ShiftCard : MonoBehaviour
    {
        private static readonly Difficulty[] Choices = { Difficulty.Relaxed, Difficulty.Normal, Difficulty.RushHour };
        private GameObject root;
        private readonly Image[] cards = new Image[3];
        private readonly Text[] names = new Text[3], details = new Text[3], locks = new Text[3];
        private Text title, subtitle, footer;
        private bool[] locked = new bool[3];
        public int Selected { get; private set; } = 1;
        public bool Visible => root != null && root.activeSelf;
        public Difficulty Choice => Choices[Selected];
        /// <summary>Clicked "clock in", or clicked the already selected card.</summary>
        public Action Confirmed;

        public static ShiftCard Create(Canvas canvas)
        {
            var card = canvas.gameObject.AddComponent<ShiftCard>();
            card.Build(canvas.transform);
            return card;
        }

        private void Build(Transform canvas)
        {
            var dim = UiTheme.Fill(canvas, "Shift card", new Color(0.02f, 0.02f, 0.03f, 0.78f));
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            title = UiTheme.Label(root.transform, "Title", 54, UiTheme.Gold, TextAnchor.MiddleCenter, header: true);
            UiTheme.Pin(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1000, 70));
            title.text = "CLOCK IN";
            subtitle = UiTheme.Label(root.transform, "Subtitle", 22, UiTheme.Mute, TextAnchor.MiddleCenter);
            UiTheme.Pin(subtitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 196), new Vector2(1100, 34));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = new GameObject("Shift " + DifficultyRules.Name(Choices[i]), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                button.transform.SetParent(root.transform, false);
                var image = button.GetComponent<Image>();
                image.sprite = UiTheme.Round; image.type = Image.Type.Sliced;
                var btn = button.GetComponent<Button>();
                var nav = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
                btn.targetGraphic = image;
                btn.onClick.AddListener(() => Pick(index, true));
                UiTheme.Pin(image, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 340, 10), new Vector2(310, 250));
                cards[i] = image;
                var key = UiTheme.Fill(button.transform, "Key", UiTheme.Chip, UiTheme.Round);
                UiTheme.Pin(key, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(36, 36));
                var keyText = UiTheme.Label(key.transform, "N", 20, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true, shadow: false);
                UiTheme.Stretch(keyText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                keyText.text = (i + 1).ToString();
                names[i] = UiTheme.Label(button.transform, "Name", 32, UiTheme.Ink, TextAnchor.UpperLeft, bold: true);
                UiTheme.Pin(names[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -14), new Vector2(232, 44));
                names[i].text = DifficultyRules.Name(Choices[i]).ToUpperInvariant();
                details[i] = UiTheme.Label(button.transform, "Detail", 20, UiTheme.Mute, TextAnchor.UpperLeft);
                UiTheme.Pin(details[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -72), new Vector2(276, 160));
                details[i].text = DifficultyRules.Describe(Choices[i]);
                details[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                details[i].verticalOverflow = VerticalWrapMode.Truncate;
                locks[i] = UiTheme.Label(button.transform, "Lock", 18, UiTheme.Warn, TextAnchor.LowerCenter, bold: true);
                UiTheme.Pin(locks[i], new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(290, 30));
            }
            var go = new GameObject("Clock in", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(root.transform, false);
            var goImage = go.GetComponent<Image>();
            goImage.sprite = UiTheme.Round; goImage.type = Image.Type.Sliced; goImage.color = new Color(0.91f, 0.29f, 0.18f, 0.95f);
            var goButton = go.GetComponent<Button>();
            var goNav = Navigation.defaultNavigation; goNav.mode = Navigation.Mode.None; goButton.navigation = goNav;
            goButton.targetGraphic = goImage;
            goButton.onClick.AddListener(() => Confirmed?.Invoke());
            UiTheme.Pin(goImage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(300, 60));
            var goText = UiTheme.Label(go.transform, "Label", 26, Color.white, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Stretch(goText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            goText.text = "CLOCK IN  ⏎";
            footer = UiTheme.Label(root.transform, "Footer", 18, UiTheme.Mute, TextAnchor.MiddleCenter);
            UiTheme.Pin(footer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -226), new Vector2(1100, 30));
            footer.text = "1 · 2 · 3 to choose     ENTER to clock in     The mall also gets busier every night you play.";
            root.SetActive(false);
        }

        public void Show(string shiftLine, Difficulty remembered, int nightsPlayed)
        {
            subtitle.text = shiftLine;
            for (int i = 0; i < 3; i++)
            {
                locked[i] = !DifficultyRules.Unlocked(Choices[i], nightsPlayed);
                int left = DifficultyRules.RushHourUnlocksAfterNights - nightsPlayed;
                locks[i].text = locked[i] ? "LOCKED · play " + left + " more night" + (left == 1 ? "" : "s") : "";
            }
            int start = Array.IndexOf(Choices, remembered);
            Selected = start >= 0 && !locked[start] ? start : 1;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Paint();
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        /// <summary>Select a card; a second click (or <paramref name="confirmIfSame"/> on the selected one) clocks in.</summary>
        public void Pick(int index, bool confirmIfSame = false)
        {
            if (index < 0 || index > 2 || locked[index]) return;
            bool same = index == Selected;
            Selected = index;
            Paint();
            if (same && confirmIfSame) Confirmed?.Invoke();
        }

        private void Paint()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == Selected;
                cards[i].color = locked[i] ? new Color(0.09f, 0.09f, 0.1f, 0.85f) : on ? new Color(0.22f, 0.16f, 0.07f, 0.98f) : new Color(0.1f, 0.1f, 0.12f, 0.94f);
                names[i].color = locked[i] ? UiTheme.Mute : on ? UiTheme.Gold : UiTheme.Ink;
                cards[i].transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
        }
    }
}
