using NUnit.Framework;
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
        }
    }
}
