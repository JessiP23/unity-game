using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
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
        /// <summary>Above the mezzanine floor: drawn in the Upstairs inset instead of the main map.</summary>
        public readonly bool Upstairs;
        public MapMark(float x, float z, string tint, string label = "", bool upstairs = false)
        { X = x; Z = z; Tint = tint; Label = label; Upstairs = upstairs; }
        public MapMark(Vector3 at, string tint, string label = "") : this(at.x, at.z, tint, label, at.y > 2.5f) { }
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
        public string GuardName, GuardTell;

        // Score
        public int Score, Shift, Best;
        public float Multiplier, StreakFraction;
        public string StreakLabel;

        // Wardrobe and gems (Night page)
        public string WardrobeText, GemText;

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
        /// <summary>Top scores for this shift (Done marks the local player's row), plus the name line under it.</summary>
        public readonly List<HudLine> BoardLines = new List<HudLine>();
        public string BoardTitle, BoardNote, PlayerName;
        public bool EditingName;

        public readonly List<HudLine> Missions = new List<HudLine>();
        public readonly List<HudLine> Items = new List<HudLine>();
        public readonly List<HudLine> ReportLines = new List<HudLine>();
        public readonly List<HudLine> Controls = new List<HudLine>();
        public readonly List<HudLine> Testing = new List<HudLine>();
        public readonly List<MapMark> Marks = new List<MapMark>();

        public void ClearLists()
        {
            Missions.Clear(); Items.Clear(); ReportLines.Clear(); Controls.Clear(); Testing.Clear(); Marks.Clear(); ScoreLines.Clear(); BoardLines.Clear();
        }
    }
}
