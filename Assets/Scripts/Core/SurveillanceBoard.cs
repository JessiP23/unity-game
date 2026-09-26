using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public enum LightingMode { Normal, Dark, Emergency, Partial, Dawn }
    public readonly struct MapPoint
    {
        public readonly float X, Y, Z;
        public MapPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    public readonly struct PlayerSighting
    {
        public readonly string Id;
        public readonly PlayerState State;
        public readonly MapPoint Position;
        public PlayerSighting(string id, PlayerState state, MapPoint position) { Id = id; State = state; Position = position; }
    }
    public readonly struct DoorSighting
    {
        public readonly string Id;
        public readonly bool Open, Locked;
        public DoorSighting(string id, bool open, bool locked) { Id = id; Open = open; Locked = locked; }
    }
    public readonly struct ObjectiveSighting
    {
        public readonly string Id;
        public readonly int Progress, Quantity;
        public readonly bool Complete, Failed;
        public ObjectiveSighting(string id, int progress, int quantity, bool complete, bool failed)
        { Id = id; Progress = progress; Quantity = quantity; Complete = complete; Failed = failed; }
    }
    public readonly struct NoiseSighting
    {
        public readonly MapPoint Position;
        public readonly float Loudness;
        public readonly string Source;
        public readonly double Timestamp;
        public NoiseSighting(MapPoint position, float loudness, string source, double timestamp)
        { Position = position; Loudness = loudness; Source = source; Timestamp = timestamp; }
    }
    public sealed class LightingState
    {
        public LightingMode Mode { get; private set; } = LightingMode.Normal;
        public event Action<LightingMode> Changed;
        public void Set(LightingMode mode)
        {
            if (Mode == mode) return;
            Mode = mode; Changed?.Invoke(mode);
        }
    }
    /// <summary>Security-room facts pushed by world adapters. Reading is limited to captured players.</summary>
    public sealed class SurveillanceBoard
    {
        public GuardState GuardState { get; private set; }
        public MapPoint GuardPosition { get; private set; }
        public LightingMode Lighting { get; private set; }
        private readonly List<PlayerSighting> players = new List<PlayerSighting>();
        private readonly List<DoorSighting> doors = new List<DoorSighting>();
        private readonly List<ObjectiveSighting> objectives = new List<ObjectiveSighting>();
        private readonly List<NoiseSighting> noises = new List<NoiseSighting>();
        public void ReportGuard(GuardState state, MapPoint position) { GuardState = state; GuardPosition = position; }
        public void ReportLighting(LightingMode mode) => Lighting = mode;
        public void ReportPlayers(IReadOnlyList<PlayerSighting> sightings) => Replace(players, sightings);
        public void ReportDoors(IReadOnlyList<DoorSighting> sightings) => Replace(doors, sightings);
        public void ReportObjectives(IReadOnlyList<ObjectiveSighting> sightings) => Replace(objectives, sightings);
        public void ReportNoise(NoiseSighting noise)
        {
            noises.Add(noise);
            if (noises.Count > 8) noises.RemoveAt(0);
        }
        public bool TryRead(PlayerRecord reader, out SurveillanceView view)
        {
            view = null;
            if (reader == null || (reader.State != PlayerState.Captured && reader.State != PlayerState.Surveillance)) return false;
            view = new SurveillanceView(GuardState, GuardPosition, Lighting,
                players.ToArray(), doors.ToArray(), objectives.ToArray(), noises.ToArray());
            return true;
        }
        private static void Replace<T>(List<T> destination, IReadOnlyList<T> source)
        {
            destination.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Count; i++) destination.Add(source[i]);
        }
    }
    public sealed class SurveillanceView
    {
        public GuardState GuardState { get; }
        public MapPoint GuardPosition { get; }
        public LightingMode Lighting { get; }
        public IReadOnlyList<PlayerSighting> Players { get; }
        public IReadOnlyList<DoorSighting> Doors { get; }
        public IReadOnlyList<ObjectiveSighting> Objectives { get; }
        public IReadOnlyList<NoiseSighting> Noises { get; }
        public SurveillanceView(GuardState guardState, MapPoint guardPosition, LightingMode lighting,
            IReadOnlyList<PlayerSighting> players, IReadOnlyList<DoorSighting> doors,
            IReadOnlyList<ObjectiveSighting> objectives, IReadOnlyList<NoiseSighting> noises)
        {
            GuardState = guardState; GuardPosition = guardPosition; Lighting = lighting;
            Players = players; Doors = doors; Objectives = objectives; Noises = noises;
        }
    }
}
