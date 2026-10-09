using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class WardrobeTests
    {
        [Test] public void BuyingSpendsGemsAndWearsTheOutfit()
        {
            var state = new WardrobeState();
            Assert.That(state.Worn, Is.EqualTo(Outfit.Plain));
            Assert.That(state.TryBuy(Outfit.TechTee), Is.False, "nothing to pay with");
            state.AddGem(3);
            Assert.That(state.TryBuy(Outfit.TechTee), Is.True);
            Assert.That(state.Tokens, Is.Zero);
            Assert.That(state.Worn, Is.EqualTo(Outfit.TechTee));
            Assert.That(state.TryBuy(Outfit.TechTee), Is.False, "already owned");
            Assert.That(state.TryWear(Outfit.StaffApron), Is.False);
            Assert.That(state.TryWear(Outfit.Plain), Is.True);
            Assert.That(state.GemsEver, Is.EqualTo(3));
        }
        [Test] public void RoundTripsThroughText()
        {
            var state = new WardrobeState();
            state.AddGem(10);
            state.TryBuy(Outfit.Loungewear); state.TryBuy(Outfit.ShopperCoat);
            var back = WardrobeState.Parse(state.Serialize());
            Assert.That(back.Tokens, Is.EqualTo(3));
            Assert.That(back.Worn, Is.EqualTo(Outfit.ShopperCoat));
            Assert.That(back.Owns(Outfit.Loungewear), Is.True);
            Assert.That(back.Owns(Outfit.StaffApron), Is.False);
            Assert.That(back.GemsEver, Is.EqualTo(10));
            Assert.That(WardrobeState.Parse("garbage").Worn, Is.EqualTo(Outfit.Plain));
            Assert.That(WardrobeState.Parse("2|5|0").Worn, Is.EqualTo(Outfit.Plain), "cannot wear what is not owned");
        }
        [Test] public void OutfitsFitTheirDepartments()
        {
            Assert.That(Wardrobe.Fits(Outfit.Loungewear, ZoneType.Mezzanine), Is.True);
            Assert.That(Wardrobe.Fits(Outfit.Loungewear, ZoneType.Clothing), Is.False);
            Assert.That(Wardrobe.Fits(Outfit.StaffApron, ZoneType.Warehouse), Is.True);
            Assert.That(Wardrobe.Fits(Outfit.Plain, ZoneType.Supermarket), Is.False);
            Assert.That(Wardrobe.Fits(Outfit.TechTee, null), Is.False);
            foreach (var outfit in Wardrobe.All) Assert.That(Wardrobe.Name(outfit), Is.Not.Empty);
        }
        [Test] public void MezzanineIsForShoppersAndLounging()
        {
            Assert.That(ZoneAccess.Customer.Allows(ZoneType.Mezzanine), Is.True);
            Assert.That(ZoneAccess.IsRestricted(ZoneType.Mezzanine), Is.False);
            Assert.That(PoseLibrary.ExpectedIn(ZoneType.Mezzanine), Is.EqualTo(Stance.Lounging));
        }
        [Test] public void GemPicksAreSeededDistinctAndIncludeAGatedSpot()
        {
            var a = GemPlan.Pick(5, 12, 3, new[] { 2, 7 });
            var b = GemPlan.Pick(5, 12, 3, new[] { 2, 7 });
            Assert.That(string.Join(",", a), Is.EqualTo(string.Join(",", b)));
            Assert.That(a.Length, Is.EqualTo(3));
            Assert.That(a[0] != a[1] && a[1] != a[2], Is.True);
            Assert.That(System.Array.IndexOf(a, 2) >= 0 || System.Array.IndexOf(a, 7) >= 0, Is.True);
            Assert.That(GemPlan.Pick(1, 2, 5).Length, Is.EqualTo(2), "capped by spots");
            Assert.That(GemPlan.Pick(1, 0, 3).Length, Is.Zero);
            Assert.That(new NightScore().Add(ScoreEvent.Gem), Is.EqualTo(NightScore.GemPoints));
        }
    }
}
