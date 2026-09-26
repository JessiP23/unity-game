using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    public enum ClientClaim { Escaped, MissionComplete, NotSeen, PickedUpItem, UnlockedDoor, Captured }
    /// <summary>
    /// Local server for offline play. Player commands must match the session identity.
    /// Untrusted declarations are ignored. Fusion should call these methods from the state authority only.
    /// </summary>
    public sealed class LocalMatchAuthority : IMatchService
    {
        public GameRules Rules { get; }
        public IPlayerSession Session { get; }
        public MatchFlow Flow { get; }
        public GameClock Clock { get; }
        public WarehouseRoster Warehouse { get; } = new WarehouseRoster();
        public EscapeLedger Escapes { get; } = new EscapeLedger();
        public SurveillanceBoard Surveillance { get; } = new SurveillanceBoard();
        public LightingState Lighting { get; } = new LightingState();
        public EventStream<AudioCue> Audio { get; } = new EventStream<AudioCue>();
        public NightStats Stats { get; } = new NightStats();
        /// <summary>Accepted civilian reports. Security listens here; clients only ever see accepted reports.</summary>
        public EventStream<SuspiciousActivityEvent> Reports { get; } = new EventStream<SuspiciousActivityEvent>();
        public Func<bool> MissionsComplete { get; set; }
        private readonly List<PlayerRecord> players = new List<PlayerRecord>();
        public LocalMatchAuthority(GameRules rules, IPlayerSession session)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (session == null) throw new ArgumentNullException(nameof(session));
            Rules = rules; Session = session; Flow = new MatchFlow(); Clock = new GameClock(rules.Duration);
        }
        public MatchPhase Phase => Flow.Phase;
        public bool TryBeginNight()
        {
            if (Flow.Phase == MatchPhase.Bootstrap) Flow.TryTransition(MatchPhase.Lobby);
            return Flow.Phase == MatchPhase.Night || Flow.TryTransition(MatchPhase.Night);
        }
        public bool Register(PlayerRecord player)
        {
            if (player == null || Find(player.Id) != null) return false;
            players.Add(player); return true;
        }
        public PlayerRecord Find(string id)
        {
            for (int i = 0; i < players.Count; i++) if (players[i].Id == id) return players[i];
            return null;
        }
        /// <summary>Client declarations never change match state.</summary>
        public bool AcceptClientClaim(ClientClaim claim, string playerId) => false;
        public bool TryCapture(string playerId)
        {
            var player = Find(playerId);
            if (!CaptureService.TryCapture(Flow.Phase, player, Warehouse)) return false;
            Stats.RecordCapture();
            Audio.Publish(AudioCue.Capture);
            ApplyOutcome();
            return true;
        }
        public int TryRescueGroup(string connection, string rescuerId)
        {
            if (!Session.CanControl(connection, rescuerId)) return 0;
            var rescuer = Find(rescuerId);
            if (rescuer == null) return 0;
            var ids = new List<string>(Warehouse.Occupants);
            int released = 0;
            for (int i = 0; i < ids.Count && released < Rules.RescuesPerAction; i++)
            {
                var captive = Find(ids[i]);
                if (!RescueService.TryRescue(Rules, Flow.Phase, rescuer, captive, Warehouse)) continue;
                released++;
                Stats.RecordRescue();
                Audio.Publish(AudioCue.Rescue);
            }
            if (released > 0) ApplyOutcome();
            return released;
        }
        /// <summary>Accepts a report raised by an authority-simulated NPC during the night.</summary>
        public bool TryReport(SuspiciousActivityEvent report)
        {
            if (Flow.Phase != MatchPhase.Night || string.IsNullOrEmpty(report.SourceId)) return false;
            Stats.RecordReport();
            Surveillance.ReportAlert(report);
            Audio.Publish(AudioCue.CustomerReport);
            Reports.Publish(report);
            return true;
        }
        public bool TryEscape(string connection, string playerId, bool atExit, Inventory inventory, string requiredKey)
        {
            if (!atExit || !Session.CanControl(connection, playerId) || Flow.Phase != MatchPhase.Night) return false;
            var player = Find(playerId);
            if (player == null) return false;
            if (Rules.RequireMissionsToEscape && !MissionsDone()) return false;
            if (!string.IsNullOrEmpty(requiredKey) && (inventory == null || inventory.Count(requiredKey) == 0)) return false;
            if (!Escapes.TryMark(player)) return false;
            Audio.Publish(AudioCue.Escape);
            ApplyOutcome();
            return true;
        }
        public bool TryEnterSurveillance(string connection, string playerId)
        {
            if (!Session.CanControl(connection, playerId)) return false;
            var player = Find(playerId);
            if (player == null || !Warehouse.Contains(playerId) || player.State != PlayerState.Captured) return false;
            player.SetState(PlayerState.Surveillance);
            return true;
        }
        public bool TryLeaveSurveillance(string connection, string playerId)
        {
            if (!Session.CanControl(connection, playerId)) return false;
            var player = Find(playerId);
            if (player == null || player.State != PlayerState.Surveillance) return false;
            player.SetState(PlayerState.Captured);
            return true;
        }
        public bool TryReadSurveillance(string connection, string playerId, out SurveillanceView view)
        {
            view = null;
            if (!Session.CanControl(connection, playerId)) return false;
            return Surveillance.TryRead(Find(playerId), out view);
        }
        public void Tick(double delta)
        {
            if (Flow.Phase != MatchPhase.Night) return;
            double before = Clock.Remaining;
            Clock.Tick(delta);
            if (before > 0 && Clock.Remaining == 0) Audio.Publish(AudioCue.Dawn);
            ApplyOutcome();
        }
        public void DebugAdvance(double seconds)
        {
            bool paused = Clock.Paused;
            Clock.Paused = false;
            Tick(seconds);
            if (Flow.Phase == MatchPhase.Night) Clock.Paused = paused;
        }
        public bool DebugForce(MatchPhase phase)
        {
            if (Flow.Phase != MatchPhase.Night || (phase != MatchPhase.Victory && phase != MatchPhase.Defeat)) return false;
            if (!Flow.TryTransition(phase)) return false;
            Audio.Publish(phase == MatchPhase.Victory ? AudioCue.Victory : AudioCue.Defeat);
            return true;
        }
        private bool MissionsDone() => MissionsComplete != null && MissionsComplete();
        private void ApplyOutcome()
        {
            MatchPhase? next = MatchOutcomeEvaluator.Evaluate(Rules, Facts());
            if (next == null || !Flow.TryTransition(next.Value)) return;
            Audio.Publish(next == MatchPhase.Victory ? AudioCue.Victory : AudioCue.Defeat);
        }
        private RosterFacts Facts()
        {
            int mannequins = 0, free = 0, captured = 0, escaped = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.Role != PlayerRole.Mannequin) continue;
                mannequins++;
                if (player.State == PlayerState.Escaped) escaped++;
                else if (player.State == PlayerState.Captured || player.State == PlayerState.Surveillance) captured++;
                else if (player.Free) free++;
            }
            return new RosterFacts(mannequins, free, captured, escaped, MissionsDone(), Clock.Remaining, Flow.Phase);
        }
    }
    public interface IMatchService
    {
        MatchPhase Phase { get; }
        bool TryBeginNight();
    }
}
