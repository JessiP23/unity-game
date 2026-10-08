using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// In-world HUD, Tab briefing menu, centre banners and the end-of-night card. Reads <see cref="HudModel"/> only.
    /// Reference canvas is 1600x900 scaled by height, so a 1280x720 laptop window draws text at 80% of
    /// the sizes written here and nothing a player reads falls under 14 px.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public bool MenuOpen { get; private set; }
        public MenuPage Page { get; private set; } = MenuPage.Night;
        public System.Action<MenuPage> PagePicked;
        public Camera Eyes { get; set; }

        private GameObject menuRoot, toastPanel, promptPanel, tipPanel, bannerRoot, endRoot, posePanel, reportsRow, nextPanel;
        private Text controlsTitle;
        private Text clock, phaseLine, guardText, peopleText, reportsText, nextText, prompt, tip, toast, hint;
        private Text scoreText, multiplierText, shiftText, tacticsText;
        private Text poseName, poseNote;
        private Text bannerTitle, bannerDetail;
        private Text endTitle, endGrade, endDetail, endFooter;
        private Text nightCopy, packCopy, reportCopy, cameraCopy, cameraNote;
        private Image streakFill, strainFill, vignette, bannerDim;
        private readonly Image[] pips = new Image[3];
        private Transform missionList, controlList, scoreList;
        private readonly List<Row> missionRows = new List<Row>();
        private readonly List<Row> controlRows = new List<Row>();
        private readonly List<Row> scoreRows = new List<Row>();
        private readonly Image[] tabs = new Image[6];
        private readonly Text[] tabLabels = new Text[6];
        private RectTransform mapRoot, markRoot;
        private readonly List<Image> marks = new List<Image>();
        private sealed class Row
        {
            public GameObject Root;
            public Text Key, Title, Detail;
        }
        private static readonly string[] TabNames = { "Night", "Missions", "Inventory", "Reports", "Cameras", "Controls" };
        private static readonly MenuPage[] Pages = { MenuPage.Night, MenuPage.Missions, MenuPage.Inventory, MenuPage.Reports, MenuPage.Cameras, MenuPage.Controls };
        private const float MapSize = 520f;

        public void Configure()
        {
            UiTheme.Ensure();
            if (UiTheme.Font == null) { Debug.LogWarning("[UI] Built-in font unavailable; using IMGUI."); return; }
            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>().sendNavigationEvents = false;
                events.AddComponent<InputSystemUIInputModule>();
            }
            BuildVignette(canvasObject.transform);
            BuildHud(canvasObject.transform);
            BuildMenu(canvasObject.transform);
            BuildBanner(canvasObject.transform);
            BuildEndCard(canvasObject.transform);
            Ready = true;
        }

        public void SetMenu(bool open, MenuPage? page = null)
        {
            MenuOpen = open;
            if (page.HasValue) Page = page.Value;
            if (menuRoot != null) menuRoot.SetActive(open);
        }

        public void Show(HudModel model)
        {
            if (!Ready || model == null) return;
            SetMenu(model.MenuOpen && !model.Ended, model.Page);
            tabLabels[4].text = model.Solo ? "Store map" : "Cameras";

            // Top centre: the clock and what the night is doing.
            clock.text = model.Clock + (model.Paused ? "  PAUSED" : "");
            phaseLine.text = string.IsNullOrEmpty(model.PhaseCountdown) ? model.PhaseLabel ?? "" : model.PhaseLabel + "   " + model.PhaseCountdown;

            // Top left: who can see you.
            guardText.text = "GUARD   " + model.Detection;
            guardText.color = UiTheme.Hex(model.DetectionTint);
            for (int i = 0; i < pips.Length; i++)
            {
                bool shown = i < model.SuspicionMax;
                pips[i].gameObject.SetActive(shown);
                if (!shown) continue;
                bool lit = i < model.Suspicion;
                pips[i].color = lit ? (model.Suspicion >= model.SuspicionMax - 1 ? UiTheme.Bad : UiTheme.Warn) : new Color(1, 1, 1, 0.18f);
            }
            peopleText.text = "PEOPLE   " + model.Crowd;
            peopleText.color = UiTheme.Hex(model.CrowdTint);
            reportsRow.SetActive(model.Reports > 0);
            reportsText.text = "REPORTS   " + model.Reports;
            nextText.text = FirstOpen(model);
            nextPanel.SetActive(!string.IsNullOrEmpty(nextText.text) && !model.Ended);

            // Top right: score.
            scoreText.text = model.Score.ToString("N0");
            multiplierText.text = model.Multiplier > 1.01f ? "x" + model.Multiplier.ToString("0.#") + "  " + model.StreakLabel : model.StreakLabel ?? "";
            multiplierText.color = model.Multiplier > 1.01f ? UiTheme.Gold : UiTheme.Mute;
            streakFill.fillAmount = Mathf.Clamp01(model.StreakFraction);
            streakFill.color = model.Multiplier >= 2f ? UiTheme.Gold : model.Multiplier > 1.01f ? UiTheme.Good : UiTheme.Mute;
            shiftText.text = "SHIFT #" + model.Shift + (model.Best > 0 ? "   BEST " + model.Best.ToString("N0") : "");
            tacticsText.text = model.Tactics ?? "";

            // Bottom left: pose.
            posePanel.SetActive(model.Posing && !model.Ended);
            if (model.Posing)
            {
                poseName.text = "POSE   " + model.PoseName;
                poseName.color = model.PoseMatches ? UiTheme.Pose : UiTheme.Ink;
                poseNote.text = model.PoseHint ?? "";
                poseNote.color = model.PoseMatches ? UiTheme.Good : UiTheme.Warn;
                strainFill.fillAmount = model.ComfortLeft > 0 ? 1f - model.ComfortLeft / (float)Core.PoseStrain.Comfort : 1f;
                strainFill.color = model.ComfortLeft > 0 ? UiTheme.Good : Color.Lerp(UiTheme.Warn, UiTheme.Bad, model.Strain);
                if (model.ComfortLeft <= 0) strainFill.fillAmount = Mathf.Max(0.05f, model.Strain);
            }

            // Bottom centre.
            prompt.text = model.Prompt ?? "";
            promptPanel.SetActive(!string.IsNullOrEmpty(model.Prompt) && !model.Ended);
            tip.text = model.Tip ?? "";
            tipPanel.SetActive(!string.IsNullOrEmpty(model.Tip) && !model.Ended && string.IsNullOrEmpty(model.Banner));
            toast.text = model.Toast ?? "";
            toastPanel.SetActive(!string.IsNullOrEmpty(model.Toast) && !model.Ended);
            hint.text = MenuOpen ? "TAB  close" : "TAB  menu";

            // Screen edges darken and redden as you are watched.
            vignette.color = new Color(0.25f, 0.02f, 0.02f, Mathf.Clamp01(model.Danger) * 0.75f);

            // Centre banner.
            bool banner = !string.IsNullOrEmpty(model.Banner) && !model.Ended;
            bannerRoot.SetActive(banner);
            if (banner)
            {
                bannerTitle.text = model.Banner;
                bannerTitle.color = model.BannerDanger ? UiTheme.Coral : UiTheme.Gold;
                bannerDetail.text = model.BannerDetail ?? "";
                bannerDim.color = new Color(0, 0, 0, model.BannerDanger ? 0.55f : 0.35f);
            }

            // End card.
            endRoot.SetActive(model.Ended);
            if (model.Ended)
            {
                endTitle.text = model.EndTitle ?? (model.Victory ? "ESCAPED" : "DAWN");
                endTitle.color = model.Victory ? UiTheme.Good : UiTheme.Coral;
                endGrade.text = model.Grade ?? "";
                endDetail.text = model.EndDetail ?? "";
                FillRows(scoreRows, scoreList, model.ScoreLines, RowStyle.Score);
                endFooter.text = "ENTER  play shift #" + model.Shift + " again        N  new shift";
            }

            if (!MenuOpen) return;
            PaintTabs();
            nightCopy.text = NightPage(model);
            packCopy.text = PackPage(model);
            reportCopy.text = ListPage(model.ReportLines, "Security radio", "Nobody has called the guard yet. If a shopper sees you move, they may report you.");
            cameraCopy.text = model.LiveCameras ? "LIVE FEED" : "STORE MAP";
            cameraNote.text = model.CameraNote ?? "";
            FillRows(missionRows, missionList, model.Missions, RowStyle.Mission);
            bool testingShown = model.Testing.Count > 0;
            controlsTitle.text = testingShown ? "TESTING KEYS — development only. Press ` to turn them off and see the normal controls." : "CONTROLS";
            FillRows(controlRows, controlList, testingShown ? model.Testing : model.Controls, RowStyle.Key);
            DrawMarks(model);
        }

        private void BuildVignette(Transform canvas)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 0.9f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d))));
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            vignette = UiTheme.Fill(canvas, "Danger vignette", Color.clear, sprite);
            vignette.type = Image.Type.Simple;
            UiTheme.Stretch(vignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildHud(Transform canvas)
        {
            clock = UiTheme.Label(canvas, "Clock", 56, UiTheme.Gold, TextAnchor.UpperCenter, header: true);
            UiTheme.Pin(clock, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(520, 64));
            phaseLine = UiTheme.Label(canvas, "Phase", 20, UiTheme.Mute, TextAnchor.UpperCenter);
            UiTheme.Pin(phaseLine, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(760, 28));

            // Status card
            var status = UiTheme.Fill(canvas, "Status", UiTheme.Chip, UiTheme.Round);
            UiTheme.Pin(status, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(460, 128));
            guardText = UiTheme.Label(status.transform, "Guard", 24, UiTheme.Ink, TextAnchor.MiddleLeft, bold: true);
            UiTheme.Pin(guardText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -6), new Vector2(340, 40));
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = UiTheme.Fill(status.transform, "Pip", Color.white, UiTheme.Disk);
                UiTheme.Pin(pips[i], new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18 - (pips.Length - 1 - i) * 24, -26), new Vector2(16, 16));
            }
            peopleText = UiTheme.Label(status.transform, "People", 24, UiTheme.Ink, TextAnchor.MiddleLeft, bold: true);
            UiTheme.Pin(peopleText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -46), new Vector2(420, 40));
            reportsRow = new GameObject("Reports row", typeof(RectTransform));
            reportsRow.transform.SetParent(status.transform, false);
            UiTheme.Pin(reportsRow.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -86), new Vector2(420, 40));
            reportsText = UiTheme.Label(reportsRow.transform, "Reports", 24, UiTheme.Warn, TextAnchor.MiddleLeft, bold: true);
            UiTheme.Stretch(reportsText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Next job
            nextPanel = UiTheme.Fill(canvas, "Next job", UiTheme.Chip, UiTheme.Round).gameObject;
            UiTheme.Pin(nextPanel.GetComponent<Image>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -156), new Vector2(480, 48));
            var nextTag = UiTheme.Label(nextPanel.transform, "Tag", 20, UiTheme.Gold, TextAnchor.MiddleLeft, bold: true);
            UiTheme.Pin(nextTag, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(60, 40));
            nextTag.text = "NEXT";
            nextText = UiTheme.Label(nextPanel.transform, "Text", 22, UiTheme.Ink, TextAnchor.MiddleLeft);
            UiTheme.Stretch(nextText, Vector2.zero, Vector2.one, new Vector2(84, 0), new Vector2(-14, 0));

            // Score card
            var score = UiTheme.Fill(canvas, "Score", UiTheme.Chip, UiTheme.Round);
            UiTheme.Pin(score, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(340, 128));
            scoreText = UiTheme.Label(score.transform, "Points", 44, UiTheme.Gold, TextAnchor.MiddleRight, header: true);
            UiTheme.Pin(scoreText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -4), new Vector2(300, 52));
            multiplierText = UiTheme.Label(score.transform, "Multiplier", 20, UiTheme.Mute, TextAnchor.MiddleRight);
            UiTheme.Pin(multiplierText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -56), new Vector2(300, 28));
            var streakBack = UiTheme.Fill(score.transform, "Streak back", new Color(1, 1, 1, 0.10f), UiTheme.Round);
            UiTheme.Pin(streakBack, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -88), new Vector2(304, 8));
            streakFill = UiTheme.Fill(streakBack.transform, "Streak", UiTheme.Good, UiTheme.Round);
            UiTheme.Stretch(streakFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            streakFill.type = Image.Type.Filled; streakFill.fillMethod = Image.FillMethod.Horizontal; streakFill.fillOrigin = 0;
            shiftText = UiTheme.Label(score.transform, "Shift", 18, UiTheme.Mute, TextAnchor.MiddleRight);
            UiTheme.Pin(shiftText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -100), new Vector2(300, 26));
            tacticsText = UiTheme.Label(canvas, "Tactics", 20, UiTheme.Gold, TextAnchor.UpperRight);
            UiTheme.Pin(tacticsText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -156), new Vector2(560, 60));

            // Pose panel
            posePanel = UiTheme.Fill(canvas, "Pose", UiTheme.Chip, UiTheme.Round).gameObject;
            UiTheme.Pin(posePanel.GetComponent<Image>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 24), new Vector2(320, 110));
            poseName = UiTheme.Label(posePanel.transform, "Name", 24, UiTheme.Pose, TextAnchor.MiddleLeft, bold: true);
            UiTheme.Pin(poseName, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -6), new Vector2(290, 34));
            poseNote = UiTheme.Label(posePanel.transform, "Note", 18, UiTheme.Mute, TextAnchor.UpperLeft);
            poseNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Pin(poseNote, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -40), new Vector2(290, 48));
            var strainBack = UiTheme.Fill(posePanel.transform, "Strain back", new Color(1, 1, 1, 0.10f), UiTheme.Round);
            UiTheme.Pin(strainBack, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 10), new Vector2(288, 8));
            strainFill = UiTheme.Fill(strainBack.transform, "Strain", UiTheme.Good, UiTheme.Round);
            UiTheme.Stretch(strainFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            strainFill.type = Image.Type.Filled; strainFill.fillMethod = Image.FillMethod.Horizontal; strainFill.fillOrigin = 0;
            posePanel.SetActive(false);

            // Prompt, tip, toast
            promptPanel = UiTheme.Fill(canvas, "Prompt", UiTheme.Chip, UiTheme.Round).gameObject;
            UiTheme.Pin(promptPanel.GetComponent<Image>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(60, 100), new Vector2(900, 76));
            prompt = UiTheme.Label(promptPanel.transform, "Text", 24, UiTheme.Ink, TextAnchor.MiddleCenter);
            prompt.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Stretch(prompt, Vector2.zero, Vector2.one, new Vector2(18, 4), new Vector2(-18, -4));
            tipPanel = UiTheme.Fill(canvas, "Tip", new Color(0.16f, 0.11f, 0.03f, 0.92f), UiTheme.Round).gameObject;
            UiTheme.Pin(tipPanel.GetComponent<Image>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(60, 190), new Vector2(900, 54));
            tip = UiTheme.Label(tipPanel.transform, "Text", 21, UiTheme.Gold, TextAnchor.MiddleCenter);
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Stretch(tip, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            tipPanel.SetActive(false);
            toastPanel = UiTheme.Fill(canvas, "Toast", new Color(0.12f, 0.08f, 0.04f, 0.92f), UiTheme.Round).gameObject;
            UiTheme.Pin(toastPanel.GetComponent<Image>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -142), new Vector2(560, 56));
            toast = UiTheme.Label(toastPanel.transform, "Text", 21, UiTheme.Gold, TextAnchor.MiddleCenter);
            toast.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Stretch(toast, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            toastPanel.SetActive(false);
            hint = UiTheme.Label(canvas, "Hint", 18, UiTheme.Mute, TextAnchor.LowerRight);
            UiTheme.Pin(hint, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 22), new Vector2(240, 30));
            var cross = UiTheme.Fill(canvas, "Crosshair", new Color(1, 1, 1, 0.7f), UiTheme.Disk);
            UiTheme.Pin(cross, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
        }

        private void BuildBanner(Transform canvas)
        {
            bannerRoot = new GameObject("Banner", typeof(RectTransform));
            bannerRoot.transform.SetParent(canvas, false);
            UiTheme.Stretch(bannerRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bannerDim = UiTheme.Fill(bannerRoot.transform, "Dim", new Color(0, 0, 0, 0.4f));
            UiTheme.Stretch(bannerDim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var band = UiTheme.Fill(bannerRoot.transform, "Band", new Color(0.04f, 0.04f, 0.05f, 0.82f));
            UiTheme.Stretch(band, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -96), new Vector2(0, 96));
            bannerTitle = UiTheme.Label(band.transform, "Title", 84, UiTheme.Gold, TextAnchor.MiddleCenter, header: true);
            UiTheme.Stretch(bannerTitle, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, -8), new Vector2(0, -8));
            bannerDetail = UiTheme.Label(band.transform, "Detail", 28, UiTheme.Ink, TextAnchor.MiddleCenter);
            bannerDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Stretch(bannerDetail, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(120, 10), new Vector2(-120, 0));
            bannerRoot.SetActive(false);
        }

        private void BuildEndCard(Transform canvas)
        {
            endRoot = new GameObject("End card", typeof(RectTransform));
            endRoot.transform.SetParent(canvas, false);
            UiTheme.Stretch(endRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = UiTheme.Fill(endRoot.transform, "Dim", new Color(0, 0, 0, 0.6f));
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = UiTheme.Fill(endRoot.transform, "Card", UiTheme.Glass, UiTheme.Round);
            UiTheme.Stretch(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, -345), new Vector2(400, 345));
            var accent = UiTheme.Fill(card.transform, "Accent", UiTheme.Coral);
            UiTheme.Stretch(accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero);
            endTitle = UiTheme.Label(card.transform, "Title", 64, UiTheme.Good, TextAnchor.MiddleLeft, header: true);
            UiTheme.Pin(endTitle, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(480, 80));
            endGrade = UiTheme.Label(card.transform, "Grade", 150, UiTheme.Gold, TextAnchor.MiddleRight, header: true);
            UiTheme.Pin(endGrade, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -20), new Vector2(240, 170));
            endDetail = UiTheme.Label(card.transform, "Detail", 21, UiTheme.Mute, TextAnchor.UpperLeft);
            endDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Pin(endDetail, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(500, 70));
            scoreList = new GameObject("Score lines", typeof(RectTransform)).transform;
            scoreList.SetParent(card.transform, false);
            UiTheme.Stretch(scoreList.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 96), new Vector2(-40, -200));
            endFooter = UiTheme.Label(card.transform, "Footer", 22, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Pin(endFooter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(720, 36));
            endRoot.SetActive(false);
        }

        private void BuildMenu(Transform canvas)
        {
            menuRoot = new GameObject("Menu", typeof(RectTransform));
            menuRoot.transform.SetParent(canvas, false);
            UiTheme.Stretch(menuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = UiTheme.Fill(menuRoot.transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.66f));
            dim.raycastTarget = true;
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = UiTheme.Fill(menuRoot.transform, "Card", UiTheme.Glass, UiTheme.Round);
            UiTheme.Stretch(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-640, -370), new Vector2(640, 370));
            var accent = UiTheme.Fill(card.transform, "Accent", UiTheme.Coral);
            UiTheme.Stretch(accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(0, 0));
            var title = UiTheme.Label(card.transform, "Title", 36, UiTheme.Ink, TextAnchor.MiddleLeft, header: true);
            UiTheme.Pin(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -32), new Vector2(480, 44));
            title.text = "NIGHT MART";
            var sub = UiTheme.Label(card.transform, "Sub", 18, UiTheme.Gold, TextAnchor.MiddleLeft);
            UiTheme.Pin(sub, new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -36), new Vector2(360, 32));
            sub.text = "Briefing";
            var close = UiTheme.Label(card.transform, "Close", 18, UiTheme.Mute, TextAnchor.MiddleRight);
            UiTheme.Pin(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -34), new Vector2(320, 28));
            close.text = "TAB or ESC closes";
            var rail = UiTheme.Fill(card.transform, "Rail", UiTheme.Rail);
            UiTheme.Stretch(rail, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 18), new Vector2(230, -92));
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var button = new GameObject(TabNames[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                button.transform.SetParent(rail.transform, false);
                var image = button.GetComponent<Image>();
                image.sprite = UiTheme.Round;
                image.type = Image.Type.Sliced;
                image.color = Color.clear;
                var nav = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None;
                var btn = button.GetComponent<Button>();
                btn.navigation = nav;
                btn.targetGraphic = image;
                btn.onClick.AddListener(() => { Page = Pages[index]; PagePicked?.Invoke(Page); });
                UiTheme.Pin(image, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16 - i * 56), new Vector2(198, 48));
                var key = UiTheme.Fill(button.transform, "Key", UiTheme.Chip, UiTheme.Round);
                UiTheme.Pin(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(30, 30));
                var keyText = UiTheme.Label(key.transform, "N", 18, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true, shadow: false);
                UiTheme.Stretch(keyText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                keyText.text = (i + 1).ToString();
                var label = UiTheme.Label(button.transform, "Label", 20, UiTheme.Ink, TextAnchor.MiddleLeft);
                UiTheme.Stretch(label, Vector2.zero, Vector2.one, new Vector2(50, 0), new Vector2(-10, 0));
                label.text = TabNames[i];
                tabs[i] = image;
                tabLabels[i] = label;
            }
            var body = new GameObject("Body", typeof(RectTransform)).transform;
            body.SetParent(card.transform, false);
            UiTheme.Stretch(body.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(256, 24), new Vector2(-28, -92));
            nightCopy = PageText(body, "Night");
            packCopy = PageText(body, "Inventory");
            reportCopy = PageText(body, "Reports");
            missionList = PageColumn(body, "Missions");
            var controlsPage = PageColumn(body, "Controls page");
            controlsTitle = UiTheme.Label(controlsPage, "Title", 18, UiTheme.Mute, TextAnchor.UpperLeft);
            UiTheme.Pin(controlsTitle, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(960, 26));
            controlList = PageColumn(controlsPage, "Controls");
            UiTheme.Stretch(controlList.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -34));
            var cameras = new GameObject("Cameras", typeof(RectTransform)).transform;
            cameras.SetParent(body, false);
            UiTheme.Stretch(cameras.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cameraCopy = UiTheme.Label(cameras, "Title", 24, UiTheme.Gold, TextAnchor.UpperLeft, bold: true);
            UiTheme.Pin(cameraCopy, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, 30));
            cameraNote = UiTheme.Label(cameras, "Note", 18, UiTheme.Mute, TextAnchor.UpperLeft);
            cameraNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Pin(cameraNote, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -32), new Vector2(960, 48));
            mapRoot = UiTheme.Fill(cameras, "Map", new Color(0.12f, 0.12f, 0.13f, 1f), UiTheme.Round).rectTransform;
            UiTheme.Pin(mapRoot.GetComponent<Image>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -84), new Vector2(MapSize, MapSize));
            BuildStoreMap(mapRoot);
            markRoot = new GameObject("Marks", typeof(RectTransform)).GetComponent<RectTransform>();
            markRoot.SetParent(mapRoot, false);
            UiTheme.Stretch(markRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var legend = UiTheme.Label(cameras, "Legend", 18, UiTheme.Mute, TextAnchor.UpperLeft);
            UiTheme.Pin(legend, new Vector2(0, 1), new Vector2(0, 1), new Vector2(MapSize + 24, -84), new Vector2(300, 240));
            legend.text = "Gold   your next job\nPurple   optional swaps\nWhite   you and friends\nAmber   shoppers\nRed   guard\nYellow   last report";
            menuRoot.SetActive(false);
        }

        private static Text PageText(Transform parent, string name)
        {
            var text = UiTheme.Label(parent, name, 22, UiTheme.Ink, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiTheme.Stretch(text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return text;
        }

        private static Transform PageColumn(Transform parent, string name)
        {
            var column = new GameObject(name, typeof(RectTransform)).transform;
            column.SetParent(parent, false);
            UiTheme.Stretch(column.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return column;
        }

        private enum RowStyle { Mission, Key, Score }

        private static Row MakeRow(Transform parent, RowStyle style)
        {
            var row = new Row();
            row.Root = UiTheme.Fill(parent, "Row", new Color(1, 1, 1, 0.05f), UiTheme.Round).gameObject;
            int left = style == RowStyle.Key ? 136 : style == RowStyle.Mission ? 70 : 16;
            if (style != RowStyle.Score)
            {
                var key = UiTheme.Fill(row.Root.transform, "Key", new Color(0.18f, 0.14f, 0.07f, 1f), UiTheme.Round);
                UiTheme.Pin(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(style == RowStyle.Mission ? 46 : 118, style == RowStyle.Key ? 32 : 38));
                row.Key = UiTheme.Label(key.transform, "K", 19, UiTheme.Gold, TextAnchor.MiddleCenter, bold: true, shadow: false);
                UiTheme.Stretch(row.Key, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            }
            // Title sits in the top half, detail in the bottom half: they never overlap.
            row.Title = UiTheme.Label(row.Root.transform, "Title", style == RowStyle.Score ? 22 : 23, UiTheme.Ink, TextAnchor.MiddleLeft, bold: style != RowStyle.Score);
            row.Detail = UiTheme.Label(row.Root.transform, "Detail", 18, UiTheme.Mute, TextAnchor.MiddleLeft);
            if (style == RowStyle.Score)
            {
                UiTheme.Stretch(row.Title, Vector2.zero, Vector2.one, new Vector2(left, 0), new Vector2(-120, 0));
                row.Detail.alignment = TextAnchor.MiddleRight;
                row.Detail.fontSize = 22;
                UiTheme.Stretch(row.Detail, Vector2.zero, Vector2.one, new Vector2(left, 0), new Vector2(-16, 0));
            }
            else if (style == RowStyle.Key)
            {
                row.Title.fontSize = 20;
                UiTheme.Stretch(row.Title, Vector2.zero, Vector2.one, new Vector2(left, 0), new Vector2(-12, 0));
                UiTheme.Stretch(row.Detail, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            else
            {
                UiTheme.Stretch(row.Title, new Vector2(0, 0.5f), Vector2.one, new Vector2(left, -2), new Vector2(-16, -6));
                UiTheme.Stretch(row.Detail, Vector2.zero, new Vector2(1, 0.5f), new Vector2(left, 6), new Vector2(-16, 2));
            }
            return row;
        }

        private static void FillRows(List<Row> rows, Transform parent, List<HudLine> lines, RowStyle style)
        {
            while (rows.Count < lines.Count) rows.Add(MakeRow(parent, style));
            float height = style == RowStyle.Score ? 36 : style == RowStyle.Key ? 44 : 66;
            float width = style == RowStyle.Mission ? 940 : style == RowStyle.Key ? 470 : 720;
            // Key rows run in two columns so 20 bindings fit the card; the split is half the list, rounded up.
            int perColumn = style == RowStyle.Key ? Mathf.Max(1, (lines.Count + 1) / 2) : int.MaxValue;
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < lines.Count;
                rows[i].Root.SetActive(on);
                if (!on) continue;
                var line = lines[i];
                int column = i / perColumn, slot = i % perColumn;
                UiTheme.Pin(rows[i].Root.GetComponent<Image>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(column * (width + 20), -slot * (height + 6)), new Vector2(width, height));
                if (style == RowStyle.Mission)
                {
                    rows[i].Key.text = line.Failed ? "!" : line.Done ? "OK" : (i + 1).ToString();
                    rows[i].Key.color = line.Failed ? UiTheme.Bad : line.Done ? UiTheme.Good : UiTheme.Gold;
                    rows[i].Title.text = line.Title;
                    rows[i].Title.color = line.Done ? UiTheme.Mute : UiTheme.Ink;
                    rows[i].Detail.text = line.Detail;
                }
                else if (style == RowStyle.Key)
                {
                    rows[i].Key.text = line.Title;
                    rows[i].Key.color = UiTheme.Gold;
                    rows[i].Title.text = line.Detail;
                    rows[i].Detail.text = "";
                }
                else
                {
                    rows[i].Title.text = line.Title;
                    rows[i].Detail.text = line.Detail;
                    rows[i].Detail.color = line.Failed ? UiTheme.Bad : line.Done ? UiTheme.Good : UiTheme.Ink;
                }
            }
        }

        private static void BuildStoreMap(RectTransform map)
        {
            AddZone(map, "Entrance", -3, -15, 3, -12, "#2EAE5C");
            AddZone(map, "Checkout", 6.5f, -13.5f, 13.5f, -8, "#DB3340");
            AddZone(map, "Aisles", -15, -12, 8, 5, "#ED8C24");
            AddZone(map, "Clothing", -8, 5, 0, 15, "#9452C7");
            AddZone(map, "Electronics", 0, 5, 8, 15, "#2EB8DB");
            AddZone(map, "Home", 8, -7.5f, 15, 5, "#F2C72E");
            AddZone(map, "Warehouse", -15, 8, -8, 11.6f, "#387AE0");
            AddZone(map, "Security", -15, 11.6f, -8, 15, "#EB6B9E");
            AddZone(map, "Staff", 8, 5, 15, 15, "#8C61D1");
        }

        private static void AddZone(RectTransform map, string name, float x0, float z0, float x1, float z1, string hex)
        {
            float px = (x0 + 15f) / 30f * MapSize;
            float pz = (z0 + 15f) / 30f * MapSize;
            float w = (x1 - x0) / 30f * MapSize;
            float h = (z1 - z0) / 30f * MapSize;
            var image = UiTheme.Fill(map, name, UiTheme.Hex(hex) * new Color(1, 1, 1, 0.55f));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(px, pz);
            rect.sizeDelta = new Vector2(Mathf.Max(8, w), Mathf.Max(8, h));
            var label = UiTheme.Label(image.transform, "L", 18, Color.white, TextAnchor.MiddleCenter, bold: true);
            UiTheme.Stretch(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = name;
        }

        private void PaintTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = Pages[i] == Page;
                tabs[i].color = on ? new Color(0.91f, 0.29f, 0.18f, 0.95f) : Color.clear;
                tabLabels[i].color = on ? Color.white : UiTheme.Ink;
            }
            nightCopy.gameObject.SetActive(Page == MenuPage.Night);
            missionList.gameObject.SetActive(Page == MenuPage.Missions);
            packCopy.gameObject.SetActive(Page == MenuPage.Inventory);
            reportCopy.gameObject.SetActive(Page == MenuPage.Reports);
            controlList.parent.gameObject.SetActive(Page == MenuPage.Controls);
            mapRoot.parent.gameObject.SetActive(Page == MenuPage.Cameras);
        }

        private void DrawMarks(HudModel model)
        {
            while (marks.Count < model.Marks.Count)
            {
                var dot = UiTheme.Fill(markRoot, "Mark", Color.white, UiTheme.Disk);
                marks.Add(dot);
            }
            for (int i = 0; i < marks.Count; i++)
            {
                bool on = i < model.Marks.Count;
                marks[i].gameObject.SetActive(on);
                if (!on) continue;
                var mark = model.Marks[i];
                marks[i].color = UiTheme.Hex(mark.Tint);
                var rect = marks[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((mark.X + 15f) / 30f * MapSize, (mark.Z + 15f) / 30f * MapSize);
                bool big = mark.Label == "Rescue" || mark.Label == "Bell" || mark.Tint == "#F4C15D" && !string.IsNullOrEmpty(mark.Label);
                rect.sizeDelta = big ? new Vector2(20, 20) : new Vector2(14, 14);
            }
        }

        private static string FirstOpen(HudModel model)
        {
            for (int i = 0; i < model.Missions.Count; i++)
                if (!model.Missions[i].Optional && !model.Missions[i].Done && !model.Missions[i].Failed)
                    return model.Missions[i].Title + "   " + Progress(model.Missions[i].Detail);
            return model.Missions.Count == 0 ? "" : "All jobs done — follow the marker to the exit";
        }

        /// <summary>Mission detail begins "n/m  how"; the chip only needs the count.</summary>
        private static string Progress(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "";
            int cut = detail.IndexOf(' ');
            return cut > 0 ? detail.Substring(0, cut) : detail;
        }

        private static string NightPage(HudModel model)
        {
            return "How tonight works\n\n" +
                "You are a mannequin. People only notice you when you move while they look.\n" +
                "Freeze when the guard or a shopper turns your way. Hold RIGHT MOUSE for a pose that fits the department: it buys extra doubt.\n" +
                "Finish the jobs, then reach the exit before dawn. The store darkens as the night goes on.\n" +
                (model.Solo ? "Caught? You wake in the back hall. Clear 3 rooms to return. Only dawn ends the night.\n" : "Caught? Clear the back hall, or a teammate frees you from the warehouse console.\n") +
                "\n" + model.Role + "    " + model.BodyState + "\n" +
                "Guard    " + model.Detection + "\nPeople    " + model.Crowd + "\n" +
                "Score    " + model.Score.ToString("N0") + "      Clock    " + model.Clock + "\n\n" +
                "Pages on the left: Missions, Inventory, Reports, " + (model.Solo ? "Store map" : "Cameras") + ", Controls.";
        }

        private static string ListPage(List<HudLine> lines, string title, string empty)
        {
            var text = title + "\n\n";
            if (lines.Count == 0) return text + empty;
            for (int i = 0; i < lines.Count; i++)
                text += (i + 1) + ".  " + lines[i].Title + "\n    " + lines[i].Detail + "\n\n";
            return text;
        }

        private static string PackPage(HudModel model)
        {
            var text = "What you are carrying\n\nSlots    " + model.SlotsUsed + " / " + model.SlotsMax + "\n\n";
            if (model.Items.Count == 0) return text + "Hands are empty.\nPress E on a shirt, crate, or key to pick it up.";
            for (int i = 0; i < model.Items.Count; i++)
                text += "-  " + model.Items[i].Title + "    " + model.Items[i].Detail + "\n";
            text += "\nG drops. Q throws. The employee key opens the staff door.";
            return text;
        }
    }
}
