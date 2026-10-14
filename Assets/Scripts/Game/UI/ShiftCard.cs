using System;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// The main menu, before every night: which night (tonight's shift or a special night, one of which is featured
    /// each day for a bonus), which shift (Relaxed, Normal, Rush hour), Settings and Quit. Last time's choices are
    /// preselected, so a returning player only presses Enter. Locked choices say how many nights until they open.
    /// </summary>
    public sealed class ShiftCard : MonoBehaviour
    {
        private static readonly Difficulty[] Choices = { Difficulty.Relaxed, Difficulty.Normal, Difficulty.RushHour };
        private static readonly NightKind[] Nights = { NightKind.Regular, NightKind.Blackout, NightKind.Birthday, NightKind.BlackFriday, NightKind.Heist };
        private GameObject root;
        private readonly Image[] cards = new Image[3], nightCards = new Image[5];
        private readonly Text[] names = new Text[3], details = new Text[3], locks = new Text[3];
        private readonly Text[] nightNames = new Text[5], nightLocks = new Text[5];
        private Text subtitle, nightDetail;
        private readonly bool[] locked = new bool[3], nightLocked = new bool[5];
        private NightKind featured;
        public int Selected { get; private set; } = 1;
        public int SelectedNight { get; private set; }
        public bool Visible => root != null && root.activeSelf;
        public Difficulty Choice => Choices[Selected];
        public NightKind Night => Nights[SelectedNight];
        public bool FeaturedPicked => Night != NightKind.Regular && Night == featured;
        /// <summary>Clicked "clock in", or clicked the already selected shift.</summary>
        public Action Confirmed;
        public Action SettingsWanted;

        public static ShiftCard Create(Canvas canvas)
        {
            var card = canvas.gameObject.AddComponent<ShiftCard>();
            card.Build(canvas.transform);
            return card;
        }

        private static Button Clickable(Transform parent, string name, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
            image.sprite = UiTheme.Round; image.type = Image.Type.Sliced;
            var button = go.GetComponent<Button>();
            var nav = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            button.targetGraphic = image;
            return button;
        }

        private void Build(Transform canvas)
        {
            var dim = UiTheme.Fill(canvas, "Shift card", new Color(0.02f, 0.02f, 0.03f, 0.82f));
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var title = UiTheme.Label(root.transform, "Title", 60, UiTheme.Gold, TextAnchor.MiddleCenter, header: true);
            UiTheme.Pin(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 340), new Vector2(1000, 76));
            title.text = "NIGHT MALL";
            subtitle = UiTheme.Label(root.transform, "Subtitle", 20, UiTheme.Mute, TextAnchor.MiddleCenter);
            UiTheme.Pin(subtitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(1300, 30));

            // Which night
            var nightTitle = UiTheme.Label(root.transform, "Night title", 18, UiTheme.Mute, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Pin(nightTitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 252), new Vector2(800, 26));
            nightTitle.text = "WHICH NIGHT?   (N / ↑↓)";
            for (int i = 0; i < Nights.Length; i++)
            {
                int index = i;
                var button = Clickable(root.transform, "Night " + NightKinds.Name(Nights[i]), out var image);
                button.onClick.AddListener(() => PickNight(index));
                UiTheme.Pin(image, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 252, 186), new Vector2(236, 100));
                nightCards[i] = image;
                nightNames[i] = UiTheme.Label(button.transform, "Name", 22, UiTheme.Ink, TextAnchor.MiddleCenter, bold: true);
                UiTheme.Pin(nightNames[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(226, 34));
                nightNames[i].text = NightKinds.Name(Nights[i]).ToUpperInvariant();
                nightLocks[i] = UiTheme.Label(button.transform, "Lock", 16, UiTheme.Warn, TextAnchor.MiddleCenter, bold: true);
                UiTheme.Pin(nightLocks[i], new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(226, 44));
                nightLocks[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            nightDetail = UiTheme.Label(root.transform, "Night detail", 19, UiTheme.Ink, TextAnchor.MiddleCenter);
            UiTheme.Pin(nightDetail, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 112), new Vector2(1200, 30));

            // Which shift
            var shiftTitle = UiTheme.Label(root.transform, "Shift title", 18, UiTheme.Mute, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Pin(shiftTitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 72), new Vector2(800, 26));
            shiftTitle.text = "WHICH SHIFT?   (1 · 2 · 3)";
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = Clickable(root.transform, "Shift " + DifficultyRules.Name(Choices[i]), out var image);
                button.onClick.AddListener(() => Pick(index, true));
                UiTheme.Pin(image, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 340, -30), new Vector2(310, 170));
                cards[i] = image;
                var key = UiTheme.Fill(button.transform, "Key", UiTheme.Chip, UiTheme.Round);
                UiTheme.Pin(key, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(34, 34));
                var keyText = UiTheme.Label(key.transform, "N", 20, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true, shadow: false);
                UiTheme.Stretch(keyText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                keyText.text = (i + 1).ToString();
                names[i] = UiTheme.Label(button.transform, "Name", 28, UiTheme.Ink, TextAnchor.UpperLeft, bold: true);
                UiTheme.Pin(names[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -14), new Vector2(240, 40));
                names[i].text = DifficultyRules.Name(Choices[i]).ToUpperInvariant();
                details[i] = UiTheme.Label(button.transform, "Detail", 18, UiTheme.Mute, TextAnchor.UpperLeft);
                UiTheme.Pin(details[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -62), new Vector2(278, 80));
                details[i].text = DifficultyRules.Describe(Choices[i]);
                details[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                details[i].verticalOverflow = VerticalWrapMode.Truncate;
                locks[i] = UiTheme.Label(button.transform, "Lock", 16, UiTheme.Warn, TextAnchor.LowerCenter, bold: true);
                UiTheme.Pin(locks[i], new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(290, 26));
            }

            // Buttons
            Button("Settings", "SETTINGS  (S)", new Vector2(-330, -180), new Vector2(220, 56), new Color(0.16f, 0.16f, 0.18f, 0.98f), () => SettingsWanted?.Invoke());
            Button("Clock in", "CLOCK IN  ⏎", new Vector2(0, -180), new Vector2(320, 64), new Color(0.91f, 0.29f, 0.18f, 0.97f), () => Confirmed?.Invoke());
            Button("Quit", "QUIT", new Vector2(330, -180), new Vector2(220, 56), new Color(0.16f, 0.16f, 0.18f, 0.98f), Application.Quit);
            var footer = UiTheme.Label(root.transform, "Footer", 17, UiTheme.Mute, TextAnchor.MiddleCenter);
            UiTheme.Pin(footer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -240), new Vector2(1300, 28));
            footer.text = "WASD move · Ctrl crouch · Shift run · Right mouse pose · E use · Tab briefing.  The mall gets busier every night you play.";
            root.SetActive(false);
        }

        private void Button(string name, string text, Vector2 at, Vector2 size, Color color, Action click)
        {
            var button = Clickable(root.transform, name, out var image);
            image.color = color;
            button.onClick.AddListener(() => click());
            UiTheme.Pin(image, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, size);
            var label = UiTheme.Label(button.transform, "Label", 24, Color.white, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Stretch(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = text;
        }

        public void Show(string shiftLine, Difficulty remembered, int nightsPlayed) => Show(shiftLine, remembered, NightKind.Regular, NightKind.Blackout, nightsPlayed);

        public void Show(string shiftLine, Difficulty remembered, NightKind rememberedNight, NightKind featuredNight, int nightsPlayed)
        {
            subtitle.text = shiftLine;
            featured = featuredNight;
            for (int i = 0; i < 3; i++)
            {
                locked[i] = !DifficultyRules.Unlocked(Choices[i], nightsPlayed);
                int left = DifficultyRules.RushHourUnlocksAfterNights - nightsPlayed;
                locks[i].text = locked[i] ? "LOCKED · play " + left + " more night" + (left == 1 ? "" : "s") : "";
            }
            for (int i = 0; i < Nights.Length; i++)
            {
                nightLocked[i] = !NightKinds.Unlocked(Nights[i], nightsPlayed);
                int left = NightKinds.UnlocksAfter(Nights[i]) - nightsPlayed;
                nightLocks[i].text = nightLocked[i] ? "LOCKED · " + left + " more night" + (left == 1 ? "" : "s")
                    : Nights[i] == featured ? "★ FEATURED TODAY · score ×" + NightKinds.FeaturedBonus.ToString("0.00") : "";
            }
            int start = Array.IndexOf(Choices, remembered);
            Selected = start >= 0 && !locked[start] ? start : 1;
            int night = Array.IndexOf(Nights, rememberedNight);
            SelectedNight = night >= 0 && !nightLocked[night] ? night : 0;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Paint();
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        /// <summary>Select a shift; a second click (or <paramref name="confirmIfSame"/> on the selected one) clocks in.</summary>
        public void Pick(int index, bool confirmIfSame = false)
        {
            if (index < 0 || index > 2 || locked[index]) return;
            bool same = index == Selected;
            Selected = index;
            Paint();
            if (same && confirmIfSame) Confirmed?.Invoke();
        }

        public void PickNight(int index)
        {
            if (index < 0 || index >= Nights.Length || nightLocked[index]) return;
            SelectedNight = index;
            Paint();
        }

        /// <summary>Next (or previous) open night.</summary>
        public void StepNight(int step)
        {
            for (int k = 1; k <= Nights.Length; k++)
            {
                int i = ((SelectedNight + step * k) % Nights.Length + Nights.Length) % Nights.Length;
                if (!nightLocked[i]) { SelectedNight = i; break; }
            }
            Paint();
        }

        private void Paint()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == Selected;
                cards[i].color = locked[i] ? new Color(0.09f, 0.09f, 0.1f, 0.85f) : on ? new Color(0.22f, 0.16f, 0.07f, 0.98f) : new Color(0.1f, 0.1f, 0.12f, 0.94f);
                names[i].color = locked[i] ? UiTheme.Mute : on ? UiTheme.Gold : UiTheme.Ink;
                cards[i].transform.localScale = Vector3.one * (on ? 1.05f : 1f);
            }
            for (int i = 0; i < Nights.Length; i++)
            {
                bool on = i == SelectedNight;
                nightCards[i].color = nightLocked[i] ? new Color(0.09f, 0.09f, 0.1f, 0.85f) : on ? new Color(0.16f, 0.12f, 0.24f, 0.98f) : new Color(0.1f, 0.1f, 0.12f, 0.94f);
                nightNames[i].color = nightLocked[i] ? UiTheme.Mute : on ? UiTheme.Gold : UiTheme.Ink;
                nightCards[i].transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
            nightDetail.text = NightKinds.Describe(Night) + (FeaturedPicked ? "   ★ Featured today: score ×" + NightKinds.FeaturedBonus.ToString("0.00") : "");
        }
    }
}
