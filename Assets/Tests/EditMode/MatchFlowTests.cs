using System;
using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class MatchFlowTests
    {
        private static LocalMatchAuthority Night(GameRules rules, out LocalSession session)
        {
            session = new LocalSession();
            var authority = new LocalMatchAuthority(rules, session);
            Assert.That(authority.TryBeginNight(), Is.True);
            return authority;
        }
        private static PlayerRecord Join(LocalMatchAuthority authority, LocalSession session, PlayerRole role = PlayerRole.Mannequin)
        {
            var player = new PlayerRecord(session.Join(), role);
            Assert.That(authority.Register(player), Is.True);
            return player;
        }
        [Test] public void RulesRejectInvalidRescueAndEscapeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(rescuesPerAction: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(requiredEscapes: 0));
        }
        [Test] public void DiscoveryCapturesIntoWarehouseWithoutRemovingIdentity()
        {
            var authority = Night(new GameRules(), out var session);
            var player = Join(authority, session);
            Assert.That(authority.TryCapture(player.Id), Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.Captured));
            Assert.That(player.Free, Is.False);
            Assert.That(authority.Warehouse.Contains(player.Id), Is.True);
            Assert.That(authority.Find(player.Id), Is.SameAs(player));
            Assert.That(authority.TryCapture(player.Id), Is.False);
        }
        [Test] public void GuardAndLobbyCaptureAreRejected()
        {
            var authority = new LocalMatchAuthority(new GameRules(), new LocalSession());
            var session = (LocalSession)authority.Session;
            var guard = Join(authority, session, PlayerRole.Guard);
            var mannequin = Join(authority, session);
            Assert.That(authority.TryCapture(mannequin.Id), Is.False);
            Assert.That(authority.TryBeginNight(), Is.True);
            Assert.That(authority.TryCapture(guard.Id), Is.False);
            Assert.That(guard.State, Is.EqualTo(PlayerState.Normal));
        }
        [Test] public void RescueOneThenManyAndRejectInvalidRescues()
        {
            var authority = Night(new GameRules(rescuesPerAction: 2), out var session);
            var rescuer = Join(authority, session);
            var first = Join(authority, session);
            var second = Join(authority, session);
            var stranger = new PlayerRecord("missing");
            Assert.That(authority.TryRescueGroup(rescuer.Id, first.Id), Is.Zero);
            Assert.That(authority.TryCapture(first.Id), Is.True);
            Assert.That(authority.TryCapture(second.Id), Is.True);
            Assert.That(authority.TryRescueGroup(rescuer.Id, second.Id), Is.Zero);
            Assert.That(authority.TryRescueGroup(rescuer.Id, rescuer.Id), Is.EqualTo(2));
            Assert.That(first.State, Is.EqualTo(PlayerState.Normal));
            Assert.That(second.Free, Is.True);
            Assert.That(authority.Warehouse.Count, Is.Zero);
            Assert.That(authority.Register(stranger), Is.True);
            Assert.That(authority.TryRescueGroup(rescuer.Id, stranger.Id), Is.Zero);
        }
        [Test] public void CapturedRescuerCannotRescueUnlessSelfRescueIsEnabled()
        {
            var blocked = Night(new GameRules(), out var blockedSession);
            var a = Join(blocked, blockedSession);
            var b = Join(blocked, blockedSession);
            blocked.TryCapture(a.Id); blocked.TryCapture(b.Id);
            Assert.That(blocked.Flow.Phase, Is.EqualTo(MatchPhase.Defeat));
            var open = Night(new GameRules(allowSelfRescue: true), out var openSession);
            var solo = Join(open, openSession);
            Assert.That(open.TryCapture(solo.Id), Is.True);
            Assert.That(open.Flow.Phase, Is.EqualTo(MatchPhase.Night));
            Assert.That(open.TryRescueGroup(solo.Id, solo.Id), Is.EqualTo(1));
            Assert.That(solo.State, Is.EqualTo(PlayerState.Normal));
        }
        [Test] public void RescueAfterTheMatchEndsDoesNothing()
        {
            var authority = Night(new GameRules(duration: 5), out var session);
            var rescuer = Join(authority, session);
            var captive = Join(authority, session);
            authority.TryCapture(captive.Id);
            authority.Tick(5);
            Assert.That(authority.Flow.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(authority.TryRescueGroup(rescuer.Id, rescuer.Id), Is.Zero);
            Assert.That(captive.State, Is.EqualTo(PlayerState.Captured));
        }
        [Test] public void SurveillanceIsReadableOnlyByCapturedPlayers()
        {
            var authority = Night(new GameRules(allowSelfRescue: true, defeatWhenNoRescueRemains: false), out var session);
            var free = Join(authority, session);
            var captive = Join(authority, session);
            authority.Surveillance.ReportGuard(GuardState.Patrol, new MapPoint(1, 0, 2));
            authority.Surveillance.ReportLighting(LightingMode.Dark);
            authority.Surveillance.ReportNoise(new NoiseSighting(new MapPoint(3, 0, 4), 1, "crate", 9));
            authority.TryCapture(captive.Id);
            Assert.That(authority.TryReadSurveillance(free.Id, free.Id, out _), Is.False);
            Assert.That(authority.TryReadSurveillance(captive.Id, free.Id, out _), Is.False);
            Assert.That(authority.TryEnterSurveillance(captive.Id, captive.Id), Is.True);
            Assert.That(captive.State, Is.EqualTo(PlayerState.Surveillance));
            Assert.That(authority.Warehouse.Contains(captive.Id), Is.True);
            Assert.That(authority.TryReadSurveillance(captive.Id, captive.Id, out var view), Is.True);
            Assert.That(view.GuardState, Is.EqualTo(GuardState.Patrol));
            Assert.That(view.Lighting, Is.EqualTo(LightingMode.Dark));
            Assert.That(view.Noises.Count, Is.EqualTo(1));
            Assert.That(authority.TryLeaveSurveillance(captive.Id, captive.Id), Is.True);
            Assert.That(captive.State, Is.EqualTo(PlayerState.Captured));
        }
        [Test] public void DawnDefeatsAndEscapeWinsBeforeDawn()
        {
            var dawn = Night(new GameRules(duration: 10, requireMissionsToEscape: false), out var dawnSession);
            Join(dawn, dawnSession);
            dawn.Clock.Paused = true; dawn.Tick(10);
            Assert.That(dawn.Flow.Phase, Is.EqualTo(MatchPhase.Night));
            dawn.Clock.Paused = false; dawn.Tick(10); dawn.Tick(1);
            Assert.That(dawn.Flow.Phase, Is.EqualTo(MatchPhase.Defeat));
            var win = Night(new GameRules(duration: 10), out var winSession);
            var player = Join(win, winSession);
            win.MissionsComplete = () => false;
            Assert.That(win.TryEscape(player.Id, player.Id, true, new Inventory(0), ""), Is.False);
            win.MissionsComplete = () => true;
            Assert.That(win.TryEscape(player.Id, player.Id, false, new Inventory(0), ""), Is.False);
            var locked = new Inventory(0);
            Assert.That(win.TryEscape(player.Id, player.Id, true, locked, "exit-key"), Is.False);
            locked.TryAdd("exit-key", 1, 0);
            Assert.That(win.TryEscape(player.Id, player.Id, true, locked, "exit-key"), Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.Escaped));
            Assert.That(win.Flow.Phase, Is.EqualTo(MatchPhase.Victory));
            win.Tick(10);
            Assert.That(win.Flow.Phase, Is.EqualTo(MatchPhase.Victory));
        }
        [Test] public void AllCapturedDefeatsUnlessRescueRemains()
        {
            var authority = Night(new GameRules(requiredEscapes: 2), out var session);
            var first = Join(authority, session);
            var second = Join(authority, session);
            var guard = Join(authority, session, PlayerRole.Guard);
            authority.TryCapture(first.Id);
            Assert.That(authority.Flow.Phase, Is.EqualTo(MatchPhase.Night));
            authority.TryCapture(second.Id);
            Assert.That(authority.Flow.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(guard.Free, Is.True);
        }
        [Test] public void ClientClaimsAndImpersonationDoNotChangeState()
        {
            var authority = Night(new GameRules(requireMissionsToEscape: false), out var session);
            var player = Join(authority, session);
            var other = Join(authority, session);
            Assert.That(authority.AcceptClientClaim(ClientClaim.Escaped, player.Id), Is.False);
            Assert.That(authority.AcceptClientClaim(ClientClaim.MissionComplete, player.Id), Is.False);
            Assert.That(authority.AcceptClientClaim(ClientClaim.NotSeen, player.Id), Is.False);
            Assert.That(authority.AcceptClientClaim(ClientClaim.PickedUpItem, player.Id), Is.False);
            Assert.That(authority.AcceptClientClaim(ClientClaim.UnlockedDoor, player.Id), Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerState.Normal));
            Assert.That(authority.TryEscape(other.Id, player.Id, true, null, ""), Is.False);
            Assert.That(authority.TryCapture(player.Id), Is.True);
            Assert.That(authority.TryRescueGroup(other.Id, player.Id), Is.Zero);
            Assert.That(player.State, Is.EqualTo(PlayerState.Captured));
        }
        [Test] public void LocalServicesStaySeparateFromMatchState()
        {
            var network = new LocalNetworkService();
            Assert.That(network.IsAuthority, Is.True);
            Assert.That(network.Mode, Is.EqualTo("LOCAL_TEST_MODE"));
            var lobby = new LocalLobbyService();
            string room = lobby.Create("night");
            Assert.That(lobby.TryJoin(room, "player-a"), Is.True);
            Assert.That(lobby.TryJoin(room, "player-a"), Is.False);
            Assert.That(lobby.TryJoin("missing", "player-a"), Is.False);
            var profiles = new MemoryPlayerDataService();
            var profile = new PlayerProfile("player-a") { LookSensitivity = 0.2f };
            Assert.That(profiles.TrySave(profile), Is.True);
            Assert.That(profiles.TryLoad("player-a", out var loaded), Is.True);
            Assert.That(loaded.LookSensitivity, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(profiles.TryLoad("missing", out _), Is.False);
        }
        [Test] public void FlashlightConeAndPoseMapArePure()
        {
            var lamp = new FlashlightModel(8, 40);
            Assert.That(lamp.Covers(8, 20), Is.True);
            Assert.That(lamp.Covers(8.1f, 0), Is.False);
            Assert.That(lamp.Covers(1, 21), Is.False);
            lamp.SetEnabled(false);
            Assert.That(lamp.Covers(1, 0), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlashlightModel(0, 40));
            Assert.That(PoseMap.From(PlayerState.Normal, 0, DetectionState.Red, false), Is.EqualTo(PresentationPose.Freeze));
            Assert.That(PoseMap.From(PlayerState.Running, 2, DetectionState.Green, false), Is.EqualTo(PresentationPose.Run));
            Assert.That(PoseMap.From(PlayerState.Captured, 2, DetectionState.Red, false), Is.EqualTo(PresentationPose.Captured));
            Assert.That(PoseMap.From(PlayerState.Escaped, 0, DetectionState.Green, false), Is.EqualTo(PresentationPose.Escape));
        }
    }
}
