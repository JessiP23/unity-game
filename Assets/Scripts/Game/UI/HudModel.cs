using System.Collections.Generic;
using NightSupermarket.Core;
namespace NightSupermarket.Game
{
    public enum MenuPage { Night, Missions, Inventory, Reports, Cameras, Controls }

    public readonly struct HudLine
    {
        public readonly string Title, Detail, Tint;
        public readonly bool Done, Failed;
        public HudLine(string title, string detail, string tint = null, bool done = false, bool failed = false)
        {
            Title = title; Detail = detail; Tint = tint; Done = done; Failed = failed;
        }
    }

    public readonly struct MapMark
    {
        public readonly float X, Z;
        public readonly string Tint, Label;
        public MapMark(float x, float z, string tint, string label = "")
        { X = x; Z = z; Tint = tint; Label = label; }
    }

    /// <summary>One frame of HUD copy. Presentation only; rules stay on the authority.</summary>
    public sealed class HudModel
    {
        public string Clock, Phase, Role, BodyState, Detection, DetectionTint, Crowd, CrowdTint, Prompt, Toast, CameraNote;
        public bool Paused, LiveCameras, MenuOpen;
        public MenuPage Page;
        public int Reports, SlotsUsed, SlotsMax;
        public readonly List<HudLine> Missions = new List<HudLine>();
        public readonly List<HudLine> Items = new List<HudLine>();
        public readonly List<HudLine> ReportLines = new List<HudLine>();
        public readonly List<HudLine> Controls = new List<HudLine>();
        public readonly List<HudLine> Testing = new List<HudLine>();
        public readonly List<MapMark> Marks = new List<MapMark>();

        public void ClearLists()
        {
            Missions.Clear(); Items.Clear(); ReportLines.Clear(); Controls.Clear(); Testing.Clear(); Marks.Clear();
        }
    }
}
