using System.Collections.Generic;
using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class JobTests
    {
        [Test] public void ShiftZeroIsTheClassicTrio()
        {
            var jobs = JobCatalog.Draw(0);
            Assert.That(jobs.Count, Is.EqualTo(3));
            Assert.That(jobs[0].Id, Is.EqualTo("collect-crates"));
            Assert.That(jobs[1].Id, Is.EqualTo("place-crate"));
            Assert.That(jobs[2].Id, Is.EqualTo("steal-shirt"));
        }
        [Test] public void EveryShiftDrawsThreeDistinctFamiliesAndRepeatsDeterministically()
        {
            var seen = new HashSet<string>();
            for (int shift = 1; shift < 400; shift++)
            {
                var jobs = JobCatalog.Draw(shift);
                var again = JobCatalog.Draw(shift);
                Assert.That(jobs.Count, Is.EqualTo(JobCatalog.PerNight));
                var families = new HashSet<string>();
                for (int i = 0; i < jobs.Count; i++)
                {
                    Assert.That(families.Add(jobs[i].Family), Is.True, "two " + jobs[i].Family + " jobs on shift " + shift);
                    Assert.That(again[i].Id, Is.EqualTo(jobs[i].Id));
                    seen.Add(jobs[i].Id);
                }
            }
            Assert.That(seen.Count, Is.EqualTo(JobCatalog.All.Length), "every job appears somewhere in the first 400 shifts");
        }
        [Test] public void TemplatesMakeValidMissionRules()
        {
            foreach (var job in JobCatalog.All)
            {
                var rule = job.Rule();
                Assert.That(rule.Id, Is.EqualTo(job.Id));
                Assert.That(rule.Quantity, Is.EqualTo(job.Quantity));
                var tracker = new MissionTracker(rule);
                for (int i = 0; i < job.Quantity; i++)
                    tracker.Apply(new ObjectAction(job.Kind, "obj" + i, job.Tag, "p1", job.Destination));
                Assert.That(tracker.Complete, Is.True, job.Id);
            }
        }
    }
}
