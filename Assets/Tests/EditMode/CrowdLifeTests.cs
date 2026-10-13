using System;
using NightSupermarket.Core;
using NUnit.Framework;
namespace NightSupermarket.Tests
{
    public sealed class CoverRulesTests
    {
        [Test] public void WalkingTheirWayWithTwoOrMoreHidesYouAndRunningNeverDoes()
        {
            Assert.That(CoverRules.Strength(0, false), Is.Zero);
            Assert.That(CoverRules.Strength(1, false), Is.Zero, "one person is not a crowd");
            Assert.That(CoverRules.Strength(2, false), Is.EqualTo(0.55));
            Assert.That(CoverRules.Strength(5, false), Is.EqualTo(0.75));
            Assert.That(CoverRules.Strength(5, true), Is.Zero, "running gives you away");
        }

        [Test] public void ACompanionIsCloseAndGoingYourWay()
        {
            Assert.That(CoverRules.Companion(1, 0, 0, 1.2, 0, 1.3), Is.True, "beside you, same way");
            Assert.That(CoverRules.Companion(1, 0, 0.6, 1.0, 0, 1.3), Is.True, "about the same way");
            Assert.That(CoverRules.Companion(1, 0, 0, -1.2, 0, 1.3), Is.False, "against the flow");
            Assert.That(CoverRules.Companion(1, 0, 1.2, 0, 0, 1.3), Is.False, "crossing your path");
            Assert.That(CoverRules.Companion(3, 0, 0, 1.2, 0, 1.3), Is.False, "too far away");
            Assert.That(CoverRules.Companion(1, 0, 0, 0, 0, 1.3), Is.False, "walking past people standing still");
            Assert.That(CoverRules.Companion(1, 0, 0, 0, 0, 0), Is.True, "standing among people standing");
        }

        [Test] public void TheCrowdCutsWhatAWatcherMakesOfYourMovement()
        {
            var settings = new AwarenessSettings { NoticeTime = 0, SuspicionGain = 1, Threshold = 10 };
            var alone = new AwarenessTracker(settings);
            var crowd = new AwarenessTracker(settings);
            var here = new MapPoint(0, 0, 0);
            for (int i = 0; i < 10; i++) { alone.Tick(true, 1, here, 0.1); crowd.Tick(true, 1, here, 0.1, 0.75); }
            Assert.That(alone.Suspicion, Is.EqualTo(1).Within(1e-9));
            Assert.That(crowd.Suspicion, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(CoverRules.Gain(1, 2), Is.EqualTo(0.1).Within(1e-9), "never completely invisible");
        }
    }

    public sealed class JobPressureTests
    {
        [Test] public void MorePeopleGatherAsYouGetBetterButNeverMoreThanFour()
        {
            Assert.That(JobPressure.Lingerers(0, Difficulty.Normal), Is.EqualTo(2));
            Assert.That(JobPressure.Lingerers(3, Difficulty.Normal), Is.EqualTo(3));
            Assert.That(JobPressure.Lingerers(8, Difficulty.Normal), Is.EqualTo(4));
            Assert.That(JobPressure.Lingerers(8, Difficulty.RushHour), Is.EqualTo(4), "capped");
            Assert.That(JobPressure.Lingerers(0, Difficulty.Relaxed), Is.EqualTo(1));
            Assert.That(JobPressure.Lingerers(0, Difficulty.RushHour), Is.EqualTo(3));
        }

        [Test] public void EveryJobGoesQuietForFifteenSecondsEachMinuteOrSoWithAWarning()
        {
            var cycle = new PressureCycle(75, 0);
            Assert.That(JobPressure.StageAt(cycle, 10, out double until), Is.EqualTo(PressureStage.Busy));
            Assert.That(until, Is.EqualTo(50).Within(1e-9), "quiet starts at 60 s");
            Assert.That(JobPressure.StageAt(cycle, 56, out until), Is.EqualTo(PressureStage.QuietSoon));
            Assert.That(until, Is.EqualTo(4).Within(1e-9));
            Assert.That(JobPressure.StageAt(cycle, 61, out until), Is.EqualTo(PressureStage.Quiet));
            Assert.That(until, Is.EqualTo(14).Within(1e-9));
            Assert.That(JobPressure.StageAt(cycle, 76, out _), Is.EqualTo(PressureStage.Busy), "and round again");
        }

        [Test] public void InAnyStretchOfAMinuteAndAHalfEachJobHasAQuietMoment()
        {
            for (int shift = 1; shift < 40; shift++)
                for (int job = 0; job < 4; job++)
                {
                    var cycle = JobPressure.For(shift, job);
                    Assert.That(cycle.Period, Is.InRange(JobPressure.MinPeriod, JobPressure.MaxPeriod));
                    for (double start = 0; start < 300; start += 13)
                    {
                        bool quiet = false;
                        for (double t = start; t < start + JobPressure.MaxPeriod && !quiet; t += 1) quiet = JobPressure.StageAt(cycle, t, out _) == PressureStage.Quiet;
                        Assert.That(quiet, Is.True, "shift " + shift + " job " + job + " from " + start);
                    }
                }
            var a = JobPressure.For(5, 0); var b = JobPressure.For(5, 1);
            Assert.That(a.Offset != b.Offset || a.Period != b.Period, Is.True, "jobs breathe out of step");
        }
    }

    public sealed class CrowdHeatAndDifficultyTests
    {
        [Test] public void HeatReadsAtAGlance()
        {
            Assert.That(CrowdHeat.Level(0), Is.Zero);
            Assert.That(CrowdHeat.Level(3), Is.EqualTo(1));
            Assert.That(CrowdHeat.Level(5), Is.EqualTo(2));
            Assert.That(CrowdHeat.Level(9), Is.EqualTo(3));
            Assert.That(CrowdHeat.Trend(2, 5), Is.EqualTo(1));
            Assert.That(CrowdHeat.Trend(5, 4), Is.Zero, "one person is noise");
            Assert.That(CrowdHeat.Trend(6, 2), Is.EqualTo(-1));
            Assert.That(CrowdHeat.Name(3), Is.EqualTo("Packed"));
        }

        [Test] public void ShiftsScaleTheCrowdTheSalesAndTheScore()
        {
            Assert.That(DifficultyRules.CrowdFactor(Difficulty.Relaxed), Is.LessThan(1));
            Assert.That(DifficultyRules.CrowdFactor(Difficulty.RushHour), Is.GreaterThan(1));
            Assert.That(DifficultyRules.Sales(Difficulty.RushHour), Is.EqualTo(3));
            Assert.That(DifficultyRules.Influencers(Difficulty.Relaxed), Is.False);
            Assert.That(DifficultyRules.Unlocked(Difficulty.RushHour, 2), Is.False);
            Assert.That(DifficultyRules.Unlocked(Difficulty.RushHour, 3), Is.True);
            Assert.That(DifficultyRules.Parse(0), Is.EqualTo(Difficulty.Relaxed));
            Assert.That(DifficultyRules.Parse(7), Is.EqualTo(Difficulty.Normal));
            Assert.That(CrowdPlan.Target(10, 20, 45, 180, 1, 0, false, 99, 1.3), Is.EqualTo(26));
            Assert.That(CrowdPlan.Target(10, 20, 45, 180, 1, 0, false, 99, 0.7), Is.EqualTo(14));
        }

        [Test] public void RushHourSalesNeverOverlapAndAllHappenWhileShoppersAreAround()
        {
            for (int shift = 1; shift < 60; shift++)
            {
                var sales = CrowdPlan.SalesFor(shift, 3);
                Assert.That(sales.Count, Is.EqualTo(3));
                const double night = 360;
                for (int i = 0; i < sales.Count; i++)
                {
                    Assert.That(sales[i].OpensAt, Is.GreaterThan(NightSchedule.DarkAt + 0.1));
                    if (i == 0) continue;
                    double previousEnds = (1 - sales[i - 1].OpensAt) * night + sales[i - 1].Seconds;
                    double opens = (1 - sales[i].OpensAt) * night - CrowdPlan.AnnounceSeconds;
                    Assert.That(opens, Is.GreaterThan(previousEnds), "shift " + shift + ": sale " + i + " is announced before the last one ends");
                }
            }
        }

        [Test] public void DifficultyScalesWhatYouEarnButNotWhatYouLose()
        {
            var rush = new NightScore { DifficultyFactor = 1.3 };
            Assert.That(rush.Add(ScoreEvent.RequiredJob), Is.EqualTo(650));
            Assert.That(rush.Add(ScoreEvent.Report), Is.EqualTo(-NightScore.ReportPenalty));
            Assert.That(rush.AddEscapeTime(10), Is.EqualTo(130));
            var relaxed = new NightScore { DifficultyFactor = 0.8 };
            Assert.That(relaxed.Add(ScoreEvent.RequiredJob), Is.EqualTo(400));
        }

        [Test] public void NightMetricsWriteOneTidyRow()
        {
            var m = new CrowdMetrics { Reports = 3, NightSeconds = 180, JobsDone = 2, JobsTotal = 3, Difficulty = Difficulty.RushHour, Level = 4, WaitSeconds = new[] { 12.4, 30.0, 0 } };
            m.Sample(20, 30, 16); m.Sample(30, 30, 20);
            Assert.That(m.PeakCrowd, Is.EqualTo(30));
            Assert.That(m.ReportsPerMinute, Is.EqualTo(1).Within(1e-9));
            string row = m.Row("2026-10-13 21:00", 286, true);
            Assert.That(row.Split(',').Length, Is.EqualTo(CrowdMetrics.Header.Split(',').Length));
            Assert.That(row, Does.Contain("Rush hour"));
            Assert.That(row, Does.EndWith("12 30 0"));
        }
    }

    public sealed class LingeringShopperTests
    {
        private static CustomerSettings Quick() => new CustomerSettings
        { BrowseMin = 1, BrowseMax = 1, PickTime = 0.5, WaitChance = 0, CheckoutMin = 1, CheckoutMax = 1, CheckoutChance = 1, ReportingProbability = 1, LeaveAfterReport = 1, StareTime = 1 };

        [Test] public void ALingererHangsAroundThenCarriesOnWithTheirList()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(3));
            brain.Tick(true, 0.1); // walking to Home
            Assert.That(brain.Linger(ZoneType.Clothing, 2, 10), Is.True);
            Assert.That(brain.LingerGroup, Is.EqualTo(2));
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Clothing));
            Assert.That(brain.Linger(ZoneType.Clothing, 1, 10), Is.False, "one job at a time");
            brain.Tick(true, 0.1); // arrives, browses
            Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing));
            for (int i = 0; i < 30; i++) brain.Tick(false, 0.25);
            Assert.That(brain.State, Is.EqualTo(CustomerState.Browsing), "still hanging around after 7.5 s of a 10 s linger");
            for (int i = 0; i < 20; i++) brain.Tick(false, 0.25);
            Assert.That(brain.LastPickWasLinger, Is.True);
            Assert.That(brain.Lingering, Is.False);
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Home), "back to the list");
            Assert.That(list.Picked, Is.Zero, "the impulse buy is not the lamp");
        }

        [Test] public void TheQuietMomentSendsThemOnTheirWay()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(4));
            brain.Tick(true, 0.1);
            brain.Linger(ZoneType.Clothing, 0, 30);
            brain.Tick(true, 0.1);
            brain.MoveOn();
            Assert.That(brain.Lingering, Is.False);
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Home));
            brain.Linger(ZoneType.Clothing, 0, 30);
            brain.Unreachable();
            Assert.That(brain.Lingering, Is.False, "nowhere to stand: carry on, skipping nothing");
            Assert.That(list.Done, Is.False);
        }
    }
}
