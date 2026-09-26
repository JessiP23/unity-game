using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Compact in-world strip plus a Tab briefing menu. Reads <see cref="HudModel"/> only.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public bool MenuOpen { get; private set; }
        public MenuPage Page { get; private set; } = MenuPage.Night;
        public System.Action<MenuPage> PagePicked;
        private GameObject menuRoot, toastPanel, promptPanel;
        private Text clock, chipA, chipB, chipC, missionHint, prompt, toast, hint;
        private Text nightCopy, packCopy, reportCopy, cameraCopy, cameraNote;
        private Transform missionList, controlList, testList;
        private readonly List<Row> missionRows = new List<Row>();
        private readonly List<Row> controlRows = new List<Row>();
        private readonly List<Row> testRows = new List<Row>();
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
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>().sendNavigationEvents = false;
                events.AddComponent<InputSystemUIInputModule>();
            }
            BuildHud(canvasObject.transform);
            BuildMenu(canvasObject.transform);
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
            SetMenu(model.MenuOpen, model.Page);
            clock.text = UiTheme.Read(model.Clock + (model.Paused ? "  PAUSED" : ""));
            chipA.text = UiTheme.Read("SEEN    " + model.Detection);
            chipA.color = UiTheme.Hex(model.DetectionTint);
            chipB.text = UiTheme.Read("PEOPLE    " + model.Crowd);
            chipB.color = UiTheme.Hex(model.CrowdTint);
            chipC.text = UiTheme.Read(model.Reports == 0 ? "REPORTS    NONE" : "REPORTS    " + model.Reports);
            chipC.color = model.Reports == 0 ? UiTheme.Mute : UiTheme.Warn;
            missionHint.text = UiTheme.Read(FirstOpen(model));
            prompt.text = UiTheme.Read(model.Prompt ?? "");
            promptPanel.SetActive(!string.IsNullOrEmpty(model.Prompt));
            toast.text = UiTheme.Read(model.Toast ?? "");
            toastPanel.SetActive(!string.IsNullOrEmpty(model.Toast));
            hint.text = UiTheme.Read(MenuOpen ? "TAB   close" : "TAB   menu");
            if (!MenuOpen) return;
            PaintTabs();
            nightCopy.text = NightPage(model);
            packCopy.text = PackPage(model);
            reportCopy.text = ListPage(model.ReportLines, "Security radio", "Nobody has called the guard yet. If a shopper sees you move, they may report you.");
            cameraCopy.text = UiTheme.Read(model.LiveCameras ? "LIVE FEED" : "STORE MAP");
            cameraNote.text = UiTheme.Read(model.CameraNote ?? "");
            FillRows(missionRows, missionList, model.Missions, true);
            FillRows(controlRows, controlList, model.Controls, false);
            FillRows(testRows, testList, model.Testing, false);
            DrawMarks(model);
        }

        /// <summary>Kept for tests that only need the help string path.</summary>
        public void Show(string statusText, string promptText, string helpText)
        {
            if (!Ready) return;
            prompt.text = UiTheme.Read(promptText);
            promptPanel.SetActive(!string.IsNullOrEmpty(promptText));
            nightCopy.text = statusText;
        }

        private void BuildHud(Transform canvas)
        {
            clock = UiTheme.Label(canvas, "Clock", 40, UiTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            UiTheme.Pin(clock, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(420, 44));
            var chips = new GameObject("Chips", typeof(RectTransform)).transform;
            chips.SetParent(canvas, false);
            UiTheme.Pin(chips, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -20), new Vector2(520, 96));
            chipA = Chip(chips, 0);
            chipB = Chip(chips, 1);
            chipC = Chip(chips, 2);
            missionHint = UiTheme.Label(canvas, "Mission hint", 18, UiTheme.Ink, TextAnchor.UpperLeft);
            UiTheme.Pin(missionHint, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -122), new Vector2(560, 48));
            promptPanel = UiTheme.Fill(canvas, "Prompt", UiTheme.Chip, UiTheme.Round).gameObject;
            UiTheme.Pin(promptPanel.GetComponent<Image>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(640, 52));
            prompt = UiTheme.Label(promptPanel.transform, "Text", 20, UiTheme.Ink, TextAnchor.MiddleCenter);
            UiTheme.Stretch(prompt, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-16, -6));
            toastPanel = UiTheme.Fill(canvas, "Toast", new Color(0.12f, 0.08f, 0.04f, 0.92f), UiTheme.Round).gameObject;
            UiTheme.Pin(toastPanel.GetComponent<Image>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(680, 44));
            toast = UiTheme.Label(toastPanel.transform, "Text", 18, UiTheme.Gold, TextAnchor.MiddleCenter);
            UiTheme.Stretch(toast, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            toastPanel.SetActive(false);
            hint = UiTheme.Label(canvas, "Hint", 16, UiTheme.Mute, TextAnchor.LowerRight);
            UiTheme.Pin(hint, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 22), new Vector2(220, 28));
            var cross = UiTheme.Fill(canvas, "Crosshair", new Color(1, 1, 1, 0.7f), UiTheme.Disk);
            UiTheme.Pin(cross, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
        }

        private static Text Chip(Transform parent, int index)
        {
            var panel = UiTheme.Fill(parent, "Chip", UiTheme.Chip, UiTheme.Round);
            UiTheme.Pin(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -index * 36), new Vector2(380, 32));
            var text = UiTheme.Label(panel.transform, "Text", 18, UiTheme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiTheme.Stretch(text, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
            return text;
        }

        private void BuildMenu(Transform canvas)
        {
            menuRoot = new GameObject("Menu", typeof(RectTransform));
            menuRoot.transform.SetParent(canvas, false);
            UiTheme.Stretch(menuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = UiTheme.Fill(menuRoot.transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.62f));
            dim.raycastTarget = true;
            UiTheme.Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = UiTheme.Fill(menuRoot.transform, "Card", UiTheme.Glass, UiTheme.Round);
            UiTheme.Stretch(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-620, -360), new Vector2(620, 360));
            var accent = UiTheme.Fill(card.transform, "Accent", UiTheme.Coral);
            UiTheme.Stretch(accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), new Vector2(0, 0));
            var title = UiTheme.Label(card.transform, "Title", 28, UiTheme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiTheme.Pin(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -28), new Vector2(480, 40));
            title.text = UiTheme.Read("NIGHT MART");
            var sub = UiTheme.Label(card.transform, "Sub", 16, UiTheme.Gold, TextAnchor.MiddleLeft);
            UiTheme.Pin(sub, new Vector2(0, 1), new Vector2(0, 1), new Vector2(248, -32), new Vector2(360, 32));
            sub.text = UiTheme.Read("Briefing");
            var close = UiTheme.Label(card.transform, "Close", 15, UiTheme.Mute, TextAnchor.MiddleRight);
            UiTheme.Pin(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -30), new Vector2(280, 28));
            close.text = UiTheme.Read("TAB or ESC   close");
            var rail = UiTheme.Fill(card.transform, "Rail", UiTheme.Rail);
            UiTheme.Stretch(rail, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 18), new Vector2(220, -86));
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
                UiTheme.Pin(image, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16 - i * 52), new Vector2(188, 44));
                var key = UiTheme.Fill(button.transform, "Key", UiTheme.Chip, UiTheme.Round);
                UiTheme.Pin(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(28, 28));
                var keyText = UiTheme.Label(key.transform, "N", 14, UiTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiTheme.Stretch(keyText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                keyText.text = (i + 1).ToString();
                keyText.color = UiTheme.Gold;
                var label = UiTheme.Label(button.transform, "Label", 17, UiTheme.Ink, TextAnchor.MiddleLeft);
                UiTheme.Stretch(label, Vector2.zero, Vector2.one, new Vector2(48, 0), new Vector2(-10, 0));
                label.text = UiTheme.Read(TabNames[i]);
                tabs[i] = image;
                tabLabels[i] = label;
            }
            var body = new GameObject("Body", typeof(RectTransform)).transform;
            body.SetParent(card.transform, false);
            UiTheme.Stretch(body.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(244, 24), new Vector2(-28, -86));
            nightCopy = PageText(body, "Night");
            packCopy = PageText(body, "Inventory");
            reportCopy = PageText(body, "Reports");
            missionList = PageColumn(body, "Missions");
            controlList = PageColumn(body, "Controls");
            testList = PageColumn(body, "Testing");
            UiTheme.Stretch(testList.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(500, 0), Vector2.zero);
            var cameras = new GameObject("Cameras", typeof(RectTransform)).transform;
            cameras.SetParent(body, false);
            UiTheme.Stretch(cameras.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cameraCopy = UiTheme.Label(cameras, "Title", 20, UiTheme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiTheme.Pin(cameraCopy, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, 28));
            cameraNote = UiTheme.Label(cameras, "Note", 16, UiTheme.Mute, TextAnchor.UpperLeft);
            UiTheme.Pin(cameraNote, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(900, 40));
            mapRoot = UiTheme.Fill(cameras, "Map", new Color(0.12f, 0.12f, 0.13f, 1f), UiTheme.Round).rectTransform;
            UiTheme.Pin(mapRoot.GetComponent<Image>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -78), new Vector2(560, 560));
            BuildStoreMap(mapRoot);
            markRoot = new GameObject("Marks", typeof(RectTransform)).GetComponent<RectTransform>();
            markRoot.SetParent(mapRoot, false);
            UiTheme.Stretch(markRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var legend = UiTheme.Label(cameras, "Legend", 15, UiTheme.Mute, TextAnchor.UpperLeft);
            UiTheme.Pin(legend, new Vector2(0, 1), new Vector2(0, 1), new Vector2(580, -78), new Vector2(280, 200));
            legend.text = UiTheme.Read("White   you and friends\nGold   shoppers\nRed   guard\nYellow   last report");
            menuRoot.SetActive(false);
        }

        private static Text PageText(Transform parent, string name)
        {
            var text = UiTheme.Label(parent, name, 18, UiTheme.Ink, TextAnchor.UpperLeft);
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

        private static Row MakeRow(Transform parent, bool mission)
        {
            var row = new Row();
            row.Root = UiTheme.Fill(parent, "Row", new Color(1, 1, 1, 0.04f), UiTheme.Round).gameObject;
            var key = UiTheme.Fill(row.Root.transform, "Key", new Color(0.18f, 0.14f, 0.07f, 1f), UiTheme.Round);
            UiTheme.Pin(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(mission ? 40 : 100, 34));
            row.Key = UiTheme.Label(key.transform, "K", 16, UiTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiTheme.Stretch(row.Key, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            row.Title = UiTheme.Label(row.Root.transform, "Title", 18, UiTheme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiTheme.Stretch(row.Title, Vector2.zero, Vector2.one, new Vector2(mission ? 62 : 124, 10), new Vector2(-16, -4));
            row.Detail = UiTheme.Label(row.Root.transform, "Detail", 15, UiTheme.Mute, TextAnchor.MiddleLeft);
            UiTheme.Stretch(row.Detail, Vector2.zero, Vector2.one, new Vector2(mission ? 62 : 124, 4), new Vector2(-16, -22));
            return row;
        }

        private static void FillRows(List<Row> rows, Transform parent, List<HudLine> lines, bool mission)
        {
            while (rows.Count < lines.Count) rows.Add(MakeRow(parent, mission));
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < lines.Count;
                rows[i].Root.SetActive(on);
                if (!on) continue;
                var line = lines[i];
                UiTheme.Pin(rows[i].Root.GetComponent<Image>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 58), new Vector2(mission ? 900 : 470, 52));
                rows[i].Key.text = UiTheme.Read(mission ? (line.Failed ? "!" : line.Done ? "OK" : (i + 1).ToString()) : line.Title);
                rows[i].Title.text = UiTheme.Read(mission ? line.Title : line.Detail);
                rows[i].Detail.text = UiTheme.Read(mission ? line.Detail : "");
                rows[i].Key.color = mission && line.Failed ? UiTheme.Bad : mission && line.Done ? UiTheme.Good : UiTheme.Gold;
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
            float mapSize = 560f;
            float px = (x0 + 15f) / 30f * mapSize;
            float pz = (z0 + 15f) / 30f * mapSize;
            float w = (x1 - x0) / 30f * mapSize;
            float h = (z1 - z0) / 30f * mapSize;
            var image = UiTheme.Fill(map, name, UiTheme.Hex(hex) * new Color(1, 1, 1, 0.55f));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(px, pz);
            rect.sizeDelta = new Vector2(Mathf.Max(8, w), Mathf.Max(8, h));
            var label = UiTheme.Label(image.transform, "L", 12, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiTheme.Stretch(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = UiTheme.Read(name);
        }

        private void PaintTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = Pages[i] == Page;
                tabs[i].color = on ? new Color(0.91f, 0.29f, 0.18f, 0.95f) : Color.clear;
                tabLabels[i].color = on ? Color.white : UiTheme.Ink;
                nightCopy.gameObject.SetActive(Page == MenuPage.Night);
                missionList.gameObject.SetActive(Page == MenuPage.Missions);
                packCopy.gameObject.SetActive(Page == MenuPage.Inventory);
                reportCopy.gameObject.SetActive(Page == MenuPage.Reports);
                controlList.gameObject.SetActive(Page == MenuPage.Controls);
                testList.gameObject.SetActive(Page == MenuPage.Controls);
                mapRoot.parent.gameObject.SetActive(Page == MenuPage.Cameras);
            }
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
                rect.anchoredPosition = new Vector2((mark.X + 15f) / 30f * 560f, (mark.Z + 15f) / 30f * 560f);
                rect.sizeDelta = new Vector2(14, 14);
            }
        }

        private static string FirstOpen(HudModel model)
        {
            for (int i = 0; i < model.Missions.Count; i++)
                if (!model.Missions[i].Done && !model.Missions[i].Failed)
                    return "NEXT   " + model.Missions[i].Title + "   " + model.Missions[i].Detail;
            return model.Missions.Count == 0 ? "" : "NEXT   All jobs done. Reach the entrance.";
        }

        private static string NightPage(HudModel model)
        {
            return UiTheme.Read(
                "How tonight works\n\n" +
                "You are a mannequin. Stay still when people look at you.\n" +
                "Shoppers call the guard if they see you move.\n" +
                "Finish the jobs, then leave through the entrance before dawn.\n\n" +
                model.Role + "    " + model.BodyState + "\n" +
                "Seen    " + model.Detection + "\nPeople    " + model.Crowd + "\n" +
                "Reports    " + model.Reports + "      Clock    " + model.Clock + "\n\n" +
                "Open Missions, Inventory, Reports, Cameras, or Controls from the left.");
        }

        private static string ListPage(List<HudLine> lines, string title, string empty)
        {
            var text = title + "\n\n";
            if (lines.Count == 0) return UiTheme.Read(text + empty);
            for (int i = 0; i < lines.Count; i++)
                text += (i + 1) + ".  " + lines[i].Title + "\n    " + lines[i].Detail + "\n\n";
            return UiTheme.Read(text);
        }

        private static string PackPage(HudModel model)
        {
            var text = "What you are carrying\n\nSlots    " + model.SlotsUsed + " / " + model.SlotsMax + "\n\n";
            if (model.Items.Count == 0) return UiTheme.Read(text + "Hands are empty.\nPress E on a shirt, crate, or key to pick it up.");
            for (int i = 0; i < model.Items.Count; i++)
                text += "-  " + model.Items[i].Title + "    " + model.Items[i].Detail + "\n";
            text += "\nG drops. Q throws. The employee key opens the staff door.";
            return UiTheme.Read(text);
        }
    }
}
