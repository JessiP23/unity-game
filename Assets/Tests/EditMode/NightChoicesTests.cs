using NightSupermarket.Core;
using NUnit.Framework;
namespace NightSupermarket.Tests
{
    public sealed class NightChoicesTests
    {
        [Test] public void DisplayBuysTwoSecondsOnlyWhenPosedBeforeObservation()
        {
            var detection = new DetectionSystem(new GameRules());
            detection.Tick(true, 0, 1.5, true);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Orange));
            Assert.That(detection.GraceRemaining, Is.EqualTo(2).Within(0.001));
            detection.Tick(true, 0, 2, true);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Red));
            detection.Reset(); detection.Tick(true, 0, 1);
            detection.Tick(true, 0, 0.5, true);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Red), "posing after being seen cannot extend grace");
        }
        [Test] public void LeavingPoseUsesEarnedWindowWithoutRegrantingGrace()
        {
            var detection = new DetectionSystem(new GameRules());
            detection.Tick(true, 0, 3, true);
            detection.Tick(true, 1, 0.01, false);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Orange));
            detection.Tick(true, 1, 0.5, false);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Red));
            Assert.That(detection.Suspicion.Value, Is.EqualTo(1));
            detection.Tick(true, 0, 0.1, true);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Red));
            detection.Tick(false, 0, 0.1);
            detection.Tick(true, 0, 0.5, true);
            Assert.That(detection.State, Is.EqualTo(DetectionState.Red), "a tenth of a second out of sight does not reset the guard");
            detection.Tick(false, 0, 4);
            detection.Tick(true, 0, 0.5, true);
            Assert.That(detection.GraceRemaining, Is.EqualTo(3).Within(0.001), "fully out of mind: a fresh posed grace");
        }
        [Test] public void BonusHelpsGradeButCannotTurnDefeatIntoVictory()
        {
            var stats = new NightStats(); stats.RecordReport(); stats.RecordReport();
            Assert.That(stats.Grade(true, 120), Is.EqualTo("B"));
            stats.RecordBonus(); Assert.That(stats.Grade(true, 120), Is.EqualTo("A"));
            Assert.That(stats.Grade(false, 120), Is.EqualTo("D"));
        }
    }
}
