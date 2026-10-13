using System;
using System.Collections.Generic;
using NightSupermarket.Core;
using NUnit.Framework;
namespace NightSupermarket.Tests
{
    public sealed class CrowdPlanTests
    {
        [Test] public void TheMallGetsBusierNightByNightAndStopsAtTheTopLevel()
        {
            Assert.That(CrowdPlan.Level(0), Is.EqualTo(0));
            Assert.That(CrowdPlan.Level(1), Is.EqualTo(0), "the first night is the gentle one");
            Assert.That(CrowdPlan.Level(4), Is.EqualTo(3));
            Assert.That(CrowdPlan.Level(500), Is.EqualTo(CrowdPlan.MaxLevel));
            int quiet = CrowdPlan.Target(10, 20, 45, 180, 1, 0, false, 99);
            int packed = CrowdPlan.Target(10, 20, 45, 180, 1, CrowdPlan.MaxLevel, false, 99);
            Assert.That(quiet, Is.EqualTo(20), "a quarter of the way through the wave is its peak");
            Assert.That(packed, Is.EqualTo(36));
            Assert.That(CrowdPlan.LevelName(0), Is.Not.EqualTo(CrowdPlan.LevelName(CrowdPlan.MaxLevel)));
        }

        [Test] public void TheBudgetAndTheClockAlwaysWin()
        {
            Assert.That(CrowdPlan.Target(10, 20, 45, 180, 1, CrowdPlan.MaxLevel, true, 18), Is.EqualTo(18), "never above the frame-rate budget");
            Assert.That(CrowdPlan.Target(10, 20, 45, 180, 0.5, 0, false, 99), Is.EqualTo(10), "closing time halves it");
            Assert.That(CrowdPlan.Target(10, 20, 45, 180, 0, 0, true, 99), Is.Zero, "lockdown: a sale brings nobody in");
        }

        [Test] public void ASaleBringsMorePeopleEvenInAQuietPartOfTheWave()
        {
            int trough = CrowdPlan.Target(10, 20, 135, 180, 1, 0, false, 99);
            int sale = CrowdPlan.Target(10, 20, 135, 180, 1, 0, true, 99);
            Assert.That(trough, Is.EqualTo(10));
            Assert.That(sale, Is.EqualTo(13));
            Assert.That(CrowdPlan.Target(10, 20, 135, 180, 0.15, 0, true, 99), Is.GreaterThanOrEqualTo(7), "a sale after dark still draws a little crowd");
        }

        [Test] public void PeopleArriveInSmallGroupsAndFasterWhenTheMallIsEmpty()
        {
            Assert.That(CrowdPlan.Arrivals(0, 0.99), Is.Zero);
            Assert.That(CrowdPlan.Arrivals(1, 0.99), Is.EqualTo(1), "never more than the shortfall");
            Assert.That(CrowdPlan.Arrivals(5, 0.1), Is.EqualTo(1));
            Assert.That(CrowdPlan.Arrivals(5, 0.9), Is.EqualTo(3));
            Assert.That(CrowdPlan.Arrivals(12, 0.99), Is.EqualTo(5), "a big shortfall brings bigger groups");
            Assert.That(CrowdPlan.ArrivalGap(4, 12, 0.5), Is.LessThan(CrowdPlan.ArrivalGap(4, 1, 0.5)));
        }

        [Test] public void TwoSalesANightTheSameForEveryoneOnTheShift()
        {
            var a = CrowdPlan.SalesFor(312); var b = CrowdPlan.SalesFor(312); var c = CrowdPlan.SalesFor(313);
            Assert.That(a.Count, Is.EqualTo(2));
            for (int i = 0; i < 2; i++) { Assert.That(a[i].OpensAt, Is.EqualTo(b[i].OpensAt)); Assert.That(a[i].Pick, Is.EqualTo(b[i].Pick)); }
            Assert.That(a[0].OpensAt != c[0].OpensAt || a[0].Pick != c[0].Pick, Is.True);
            foreach (var s in a)
                Assert.That(s.OpensAt, Is.InRange(NightSchedule.DarkAt + 0.1, 0.9), "sales happen while shoppers are around");
            Assert.That(a[0].OpensAt, Is.GreaterThan(NightSchedule.ClosingAt), "the first while the store is open");
            Assert.That(a[1].OpensAt, Is.LessThan(NightSchedule.ClosingAt), "the second at closing time");
        }

        [Test] public void ASaleIsAnnouncedThenOpenThenOver()
        {
            var sales = new[] { new FlashSale(0.5, 50, 0) };
            const double duration = 600; // opens with 300 s to go
            Assert.That(CrowdPlan.StageAt(sales, 400, duration, out _, out _), Is.EqualTo(SaleStage.None));
            Assert.That(CrowdPlan.StageAt(sales, 306, duration, out int i, out double left), Is.EqualTo(SaleStage.Announced));
            Assert.That(i, Is.EqualTo(0)); Assert.That(left, Is.EqualTo(6).Within(1e-9));
            Assert.That(CrowdPlan.StageAt(sales, 290, duration, out _, out left), Is.EqualTo(SaleStage.Open));
            Assert.That(left, Is.EqualTo(40).Within(1e-9));
            Assert.That(CrowdPlan.StageAt(sales, 249, duration, out _, out _), Is.EqualTo(SaleStage.None));
        }

        [Test] public void TheBudgetFallsFastWhenFramesAreSlowAndRecoversSlowly()
        {
            Assert.That(CrowdBudget.Next(30, 25, 16.7, 12, 40), Is.EqualTo(27));
            Assert.That(CrowdBudget.Next(30, 19, 16.7, 12, 40), Is.EqualTo(29));
            Assert.That(CrowdBudget.Next(30, 16.7, 16.7, 12, 40), Is.EqualTo(30), "on target: hold");
            Assert.That(CrowdBudget.Next(30, 10, 16.7, 12, 40), Is.EqualTo(31));
            Assert.That(CrowdBudget.Next(12, 50, 16.7, 12, 40), Is.EqualTo(12), "never below the floor");
            Assert.That(CrowdBudget.Next(40, 5, 16.7, 12, 40), Is.EqualTo(40), "never above the ceiling");
        }
    }

    public sealed class FlashSaleShopperTests
    {
        private static CustomerSettings Quick() => new CustomerSettings
        { BrowseMin = 1, BrowseMax = 1, PickTime = 0.5, WaitChance = 0, CheckoutMin = 1, CheckoutMax = 1, CheckoutChance = 1, ReportingProbability = 1, LeaveAfterReport = 1, StareTime = 1 };
        private static ShoppingList List() => new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1), new ShoppingItem(ZoneType.Clothing, "Hat", 1) });

        [Test] public void ShoppersDetourToTheSaleOnceThenFinishTheirList()
        {
            var pull = new CrowdPull { Lure = 1 };
            pull.Open(ZoneType.ToyStore);
            var list = List();
            var brain = new CustomerBrain(list, Quick(), new Random(4), pull);
            var zones = new List<ZoneType?>();
            int lastVersion = -1;
            for (int i = 0; i < 400 && brain.State != CustomerState.Gone; i++)
            {
                if (brain.DestinationVersion != lastVersion) { lastVersion = brain.DestinationVersion; if (brain.Destination == DestinationKind.Department) zones.Add(brain.DestinationZone); }
                brain.Tick(!brain.Stationary, 0.25);
            }
            CollectionAssert.AreEqual(new ZoneType?[] { ZoneType.ToyStore, ZoneType.Home, ZoneType.Clothing }, zones, "one detour, then the list");
            Assert.That(list.Done, Is.True);
            Assert.That(list.Picked, Is.EqualTo(2), "the sale is a bonus purchase, not an item off the list");
        }

        [Test] public void ANewSaleIsANewDetourAndAnUnreachableSaleSkipsNothing()
        {
            var pull = new CrowdPull { Lure = 1 };
            pull.Open(ZoneType.ToyStore);
            var list = List();
            var brain = new CustomerBrain(list, Quick(), new Random(5), pull);
            brain.Tick(true, 0.1); // through the entrance
            Assert.That(brain.OnSale, Is.True);
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.ToyStore));
            brain.Unreachable();
            Assert.That(brain.OnSale, Is.False);
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Home), "the list item is still there");
            Assert.That(list.Picked, Is.Zero);
            pull.Close();
            Assert.That(brain.HearSale(), Is.False, "no sale on");
            pull.Open(ZoneType.Electronics);
            var keen = new CustomerBrain(List(), Quick(), new Random(6), pull);
            keen.Tick(true, 0.1);
            Assert.That(keen.DestinationZone, Is.EqualTo(ZoneType.Electronics));
        }

        [Test] public void SomeoneAlreadyShoppingMayDropItForTheSale()
        {
            var pull = new CrowdPull { Lure = 0 };
            int went = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                pull.Close();
                var brain = new CustomerBrain(List(), Quick(), new Random(seed), pull);
                brain.Tick(true, 0.1); // walking to Home
                pull.Open(ZoneType.ToyStore);
                if (brain.HearSale()) { went++; Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.ToyStore)); Assert.That(brain.HearSale(), Is.False, "only once per sale"); }
            }
            Assert.That(went, Is.InRange(60, 120), "about " + CrowdPlan.SaleRedirect * 100 + "% of them");
        }

        [Test] public void WithoutASaleNothingChanges()
        {
            var list = new ShoppingList(new[] { new ShoppingItem(ZoneType.Home, "Lamp", 1) });
            var brain = new CustomerBrain(list, Quick(), new Random(2), new CrowdPull());
            brain.Tick(true, 0.1);
            Assert.That(brain.DestinationZone, Is.EqualTo(ZoneType.Home));
            Assert.That(brain.HearSale(), Is.False);
        }
    }
}
