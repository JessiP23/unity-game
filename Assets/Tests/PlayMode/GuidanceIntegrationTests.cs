using System.Collections;
using System.Reflection;
using NightSupermarket.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    public sealed class GuidanceIntegrationTests
    {
        [UnityTest] public IEnumerator GuideSkipsCollectedCratesAndPointsToEscapeWhenDone()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            root.Guard.PlayerDriven = true;
            var refresh = typeof(PrototypeRoot).GetMethod("FillHud", BindingFlags.NonPublic | BindingFlags.Instance);
            var modelField = typeof(PrototypeRoot).GetField("hudModel", BindingFlags.NonPublic | BindingFlags.Instance);
            refresh.Invoke(root, null); var model = (HudModel)modelField.GetValue(root);
            Assert.That(model.Guide, Is.True); Assert.That(model.GuideText, Is.EqualTo("CRATE"));
            PhysicalItem chosen = null;
            foreach (var item in PhysicalItem.Active)
                if (Vector3.Distance(item.transform.position, new Vector3(model.GuideX, model.GuideY, model.GuideZ)) < 0.01f) chosen = item;
            Assert.That(chosen, Is.Not.Null);
            var actor = root.MannequinAt(0);
            actor.Teleport(chosen.transform.position + new Vector3(0, -chosen.transform.position.y + 0.1f, 0.9f));
            Physics.SyncTransforms(); Assert.That(chosen.TryInteract(actor), Is.True);
            Assert.That(actor.GetComponent<CarrySystem>().Release(false), Is.True);
            refresh.Invoke(root, null);
            Assert.That(Vector3.Distance(chosen.transform.position, new Vector3(model.GuideX, model.GuideY, model.GuideZ)), Is.GreaterThan(0.2f));
            foreach (var mission in root.Missions.Missions) mission.DebugComplete();
            refresh.Invoke(root, null); Assert.That(model.GuideText, Is.EqualTo("ESCAPE"));
            root.Authority.TryCapture(actor.Record.Id);
            refresh.Invoke(root, null); Assert.That(model.Guide, Is.False, "captured player must not be sent on an impossible pickup");
        }
        [UnityTest] public IEnumerator ItemRegistryTracksEnabledLifetime()
        {
            var host = new GameObject("registry-test"); var item = host.AddComponent<PhysicalItem>();
            try
            {
                Assert.That(PhysicalItem.Active, Has.Member(item));
                host.SetActive(false); Assert.That(PhysicalItem.Active, Has.No.Member(item));
                host.SetActive(true); Assert.That(PhysicalItem.Active, Has.Member(item));
                yield return null;
            }
            finally { Object.Destroy(host); }
        }
    }
}
