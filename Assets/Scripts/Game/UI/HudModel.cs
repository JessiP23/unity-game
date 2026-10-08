using System.Collections.Generic;
using NightSupermarket.Core;
namespace NightSupermarket.Game
{
    public enum MenuPage { Night, Missions, Inventory, Reports, Cameras, Controls }

    public readonly struct HudLine
    {
        public readonly string Title, Detail, Tint;
        public readonly bool Done, Failed, Optional;
        public HudLine(string title, string detail, string tint = null, bool done = false, bool failed = false, bool optional = false)
        {
            Title = title; Detail = detail; Tint = tint; Done = done; Failed = failed; Optional = optional;
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
        public string Tactics, Clock, Phase, Role, BodyState, Detection, DetectionTint, Crowd, CrowdTint, Prompt, Toast, CameraNote, GuideText;
        public bool Solo, Paused, LiveCameras, MenuOpen, Guide;
        public float GuideX, GuideY, GuideZ;
        public MenuPage Page;
        public int Reports, SlotsUsed, SlotsMax;

        // Danger
        public int Suspicion, SuspicionMax;
        /// <summary>0 = unseen, 1 = about to be caught. Drives the screen vignette.</summary>
        public float Danger;
        public bool Seen;

        // Night phase
        public string PhaseLabel, PhaseCountdown;

        // Score
        public int Score, Shift, Best;
        public float Multiplier, StreakFraction;
        public string StreakLabel;

        // Pose
        public bool Posing, PoseMatches;
        public string PoseName, PoseHint;
        public float Strain, ComfortLeft;

        // Big centre messages: capture, back hall, phase changes, tutorial
        public string Banner, BannerDetail, Tip;
        public bool BannerDanger;

        // End of night
        public bool Ended, Victory;
        public string Grade, EndTitle, EndDetail;
        public readonly List<HudLine> ScoreLines = new List<HudLine>();

        public readonly List<HudLine> Missions = new List<HudLine>();
        public readonly List<HudLine> Items = new List<HudLine>();
        public readonly List<HudLine> ReportLines = new List<HudLine>();
        public readonly List<HudLine> Controls = new List<HudLine>();
        public readonly List<HudLine> Testing = new List<HudLine>();
        public readonly List<MapMark> Marks = new List<MapMark>();

        public void ClearLists()
        {
            Missions.Clear(); Items.Clear(); ReportLines.Clear(); Controls.Clear(); Testing.Clear(); Marks.Clear(); ScoreLines.Clear();
        }
    }
}
