using NUnit.Framework;
using NightSupermarket.Core;
using NightSupermarket.Game;
using UnityEngine.InputSystem;
namespace NightSupermarket.Tests
{
    public sealed class KeyCommandMapTests
    {
        [Test] public void HelpListsBoundAndDescribedKeysInOrder()
        {
            var map = new KeyCommandMap("TESTING").Describe("WASD", "move").Bind(Key.H, "help", () => { }).Bind(Key.Digit8, "dawn", () => { });
            Assert.That(map.Entries.Count, Is.EqualTo(3));
            Assert.That(map.Entries[1].Label, Is.EqualTo("H"));
            Assert.That(map.Entries[2].Label, Is.EqualTo("8"), "digits print as numbers, not Digit8");
            string help = HudText.Help(true, map);
            Assert.That(help.IndexOf("move"), Is.LessThan(help.IndexOf("dawn")));
            Assert.That(HudText.Help(false, map), Does.Contain("controls"));
            Assert.That(HudText.ZoneLabel(ZoneType.Clothing), Is.EqualTo("Clothing"));
            Assert.That(HudText.ItemLabel("employee-key"), Is.EqualTo("Employee key"));
            Assert.That(HudText.ReportLine(new SuspiciousActivityEvent("Customer 1", "p1", new MapPoint(0, 0, 0), 0, 1, ReportKind.MovingMannequin, ZoneType.Clothing)),
                Does.Contain("Clothing"));
        }
    }
}
