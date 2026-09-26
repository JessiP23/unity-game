using System;
using NUnit.Framework;
using NightSupermarket.Core;

namespace NightSupermarket.Tests
{
    public sealed class CoreTests
    {
        [Test] public void RulesRejectInvalidDuration()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(duration: 0)); }
        [Test] public void RulesRejectNonFiniteValues()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(duration: double.NaN)); }
        [Test] public void ClockPausesAndDawnOnlyFiresOnce()
        {
            var clock = new GameClock(10); int dawn = 0; clock.Dawn += () => dawn++;
            clock.Paused = true; clock.Tick(8); Assert.That(clock.Remaining, Is.EqualTo(10));
            clock.Paused = false; clock.Tick(12); clock.Tick(2);
            Assert.That(clock.Remaining, Is.Zero); Assert.That(dawn, Is.EqualTo(1));
        }
        [Test] public void ClockRejectsReverseTime()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(10).Tick(-1)); }
        [Test] public void MatchCannotSkipLobbyOrRestartAfterEnd()
        {
            var flow = new MatchFlow();
            Assert.That(flow.TryTransition(MatchPhase.Night), Is.False);
            Assert.That(flow.TryTransition(MatchPhase.Lobby), Is.True);
            Assert.That(flow.TryTransition(MatchPhase.Night), Is.True);
            Assert.That(flow.TryTransition(MatchPhase.Victory), Is.True);
            Assert.That(flow.TryTransition(MatchPhase.Night), Is.False);
        }
        [Test] public void SubscriptionDisposesWithoutLeakingHandlers()
        {
            var stream = new EventStream<int>(); int value = 0;
            var subscription = stream.Subscribe(x => value += x);
            stream.Publish(2); subscription.Dispose(); subscription.Dispose(); stream.Publish(5);
            Assert.That(value, Is.EqualTo(2));
        }
        [Test] public void LocalSessionRejectsImpersonation()
        {
            var session = new LocalSession(); string player = session.Join();
            Assert.That(session.CanControl(player, player), Is.True);
            Assert.That(session.CanControl(player, "another-player"), Is.False);
            Assert.That(session.CanControl("unknown", "unknown"), Is.False);
        }
    }
}
