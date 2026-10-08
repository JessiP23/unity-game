using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class ScoreTests
    {
        [Test] public void StreakMultipliesGainsButNeverPenalties()
        {
            var score = new NightScore();
            Assert.That(score.Add(ScoreEvent.RequiredJob), Is.EqualTo(500));
            score.TickUnseen(60);
            Assert.That(score.Multiplier, Is.EqualTo(1.5));
            Assert.That(score.Add(ScoreEvent.CloseCall), Is.EqualTo(150));
            Assert.That(score.Add(ScoreEvent.Report), Is.EqualTo(-150), "penalties are flat");
            score.TickUnseen(60);
            Assert.That(score.Multiplier, Is.EqualTo(2));
            Assert.That(score.Add(ScoreEvent.OptionalJob), Is.EqualTo(600));
            Assert.That(score.Total, Is.EqualTo(500 + 150 - 150 + 600));
            Assert.That(score.CloseCalls, Is.EqualTo(1));
            Assert.That(score.BestStreakSeconds, Is.EqualTo(120));
        }
        [Test] public void CaptureBreaksTheStreak()
        {
            var score = new NightScore();
            score.TickUnseen(130);
            score.Add(ScoreEvent.Capture);
            Assert.That(score.Multiplier, Is.EqualTo(1));
            Assert.That(score.Total, Is.EqualTo(-400));
            Assert.That(score.Lines.Count, Is.EqualTo(1));
        }
        [Test] public void GradesSpreadAndDefeatIsAlwaysD()
        {
            var score = new NightScore();
            Assert.That(score.Grade(true), Is.EqualTo("D"));
            score.Add(ScoreEvent.RequiredJob); score.Add(ScoreEvent.RequiredJob);
            Assert.That(score.Grade(true), Is.EqualTo("C"));
            score.Add(ScoreEvent.RequiredJob);
            Assert.That(score.Grade(true), Is.EqualTo("B"));
            score.AddEscapeTime(65);
            Assert.That(score.Grade(true), Is.EqualTo("A"));
            score.Add(ScoreEvent.OptionalJob); score.Add(ScoreEvent.OptionalJob); score.Add(ScoreEvent.CloseCall);
            Assert.That(score.Grade(true), Is.EqualTo("S"));
            Assert.That(score.Grade(false), Is.EqualTo("D"));
        }
    }
}
