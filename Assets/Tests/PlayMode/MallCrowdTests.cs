using System.Collections;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>The busier mall: more shoppers, flash sales that flood a job's department, and a budget that keeps it smooth.</summary>
    public sealed class MallCrowdTests
    {
        private static IEnumerator Load(int shift = 0)
        {
            PrototypeRoot.NextShift = shift;
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var crowd = Object.FindAnyObjectByType<PrototypeRoot>().Crowd;
            float until = Time.time + 5f;
            while (!crowd.Ready && Time.time < until) yield return null;
            Assert.That(crowd.Ready, Is.True, "sale spots were placed");
            crowd.SalesEnabled = true; // scripted tests run without sales; this one is about them
        }

        [UnityTest] public IEnumerator TheMallOpensBusyAndTheBudgetCapsTheCrowd()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var population = root.Population;
            Assert.That(population.Customers, Is.GreaterThanOrEqualTo(10), "the mall opens with a crowd already shopping");
            Assert.That(population.TargetCustomers, Is.GreaterThanOrEqualTo(population.Settings.minimumCustomers));
            population.Budget = 6;
            Assert.That(population.TargetCustomers, Is.LessThanOrEqualTo(6), "the frame-rate budget has the last word");
            population.Budget = int.MaxValue;
            population.Level = CrowdPlan.MaxLevel;
            Assert.That(population.TargetCustomers, Is.LessThanOrEqualTo(population.Settings.maxActiveCustomers));
        }

        /// <summary>Several shifts, because each draws different jobs, crates and props into the aisles.</summary>
        [UnityTest] public IEnumerator SaleSpotsStandOnOpenFloorInsideTheirDepartment()
        {
            foreach (int shift in new[] { 0, 2, 5, 13 })
            {
                yield return Load(shift);
                var root = Object.FindAnyObjectByType<PrototypeRoot>();
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                Assert.That(root.Crowd.SaleZones.Count, Is.GreaterThan(0));
                Assert.That(root.Crowd.SaleSpots, Is.GreaterThanOrEqualTo(root.Crowd.SaleZones.Count * 3), "shift " + shift + ": each sale department gets real standing room");
                foreach (var point in root.Directory.Points)
                {
                    if (point.kind != PointKind.Sale) continue;
                    string where = "shift " + shift + ": " + point.name + " at " + point.Position;
                    Assert.That(root.Directory.ZoneAt(point.Position), Is.EqualTo(point.zone), where + " left its department");
                    Assert.That(ZoneAccess.Customer.Allows(point.zone), Is.True);
                    Assert.That(NavMesh.SamplePosition(point.Position, out var hit, 0.3f, NavigationAreas.CivilianMask), Is.True, where + " is off the shoppers' floor");
                    foreach (var c in Physics.OverlapCapsule(point.Position + Vector3.up * 0.3f, point.Position + Vector3.up * 1.6f, 0.2f, 1 | 1 << 3, QueryTriggerInteraction.Ignore))
                        Assert.That(c.GetComponentInParent<NpcController>() != null || c.GetComponentInParent<PlayerMotor>() != null, Is.True, where + " stands inside " + c.name);
                }
            }
            PrototypeRoot.NextShift = 0;
        }

        [UnityTest] public IEnumerator AFlashSaleIsAnnouncedFloodsItsDepartmentAndEnds()
        {
            yield return Load();
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var crowd = root.Crowd;
            var population = root.Population;
            double duration = root.Authority.Clock.Remaining;
            var sale = crowd.Sales[0];
            // Just inside the announcement.
            root.Authority.DebugAdvance(duration - (sale.OpensAt * duration + 5));
            yield return null; yield return null;
            Assert.That(crowd.Stage, Is.EqualTo(SaleStage.Announced));
            Assert.That(crowd.EventLine, Does.Contain("FLASH SALE"));
            Assert.That(GameObject.Find("Flash sale sign"), Is.Not.Null, "the sign hangs over the sale");
            Assert.That(population.Pull.Active, Is.False, "nobody moves before it opens");
            root.Authority.DebugAdvance(6);
            yield return null; yield return null;
            Assert.That(crowd.Stage, Is.EqualTo(SaleStage.Open));
            Assert.That(population.Pull.Active, Is.True);
            Assert.That(population.Pull.Zone, Is.EqualTo(crowd.SaleZone));
            Assert.That(crowd.SaleZones, Does.Contain(crowd.SaleZone.Value), "sales happen where tonight's jobs are");
            Assert.That(crowd.TryMark(out _, out var label), Is.True, "the sale is on the map");
            Assert.That(label, Is.EqualTo("SALE"));
            float scale = Time.timeScale;
            Time.timeScale = 4;
            float end = Time.time + 30;
            int peak = 0;
            while (Time.time < end) { yield return null; peak = Mathf.Max(peak, population.AtSale); }
            Time.timeScale = scale;
            Assert.That(peak, Is.GreaterThanOrEqualTo(3), "a sale pulls a crowd, not one shopper");
            root.Authority.DebugAdvance(CrowdPlan.SaleSeconds);
            yield return null; yield return null;
            Assert.That(crowd.Stage, Is.EqualTo(SaleStage.None));
            Assert.That(population.Pull.Active, Is.False, "the crowd is released");
            Assert.That(GameObject.Find("Flash sale sign"), Is.Null, "the sign is put away");
        }

        [UnityTest] public IEnumerator TheTwoSalesUseDifferentDepartmentsWhenThereIsAChoice()
        {
            yield return Load();
            var crowd = Object.FindAnyObjectByType<PrototypeRoot>().Crowd;
            if (crowd.SaleZones.Count < 2) Assert.Ignore("only one sale department tonight");
            Assert.That(crowd.ZoneForSale(0), Is.Not.EqualTo(crowd.ZoneForSale(1)));
        }
    }
}
