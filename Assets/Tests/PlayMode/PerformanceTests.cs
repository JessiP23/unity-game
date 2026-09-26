using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NightSupermarket.Core;
using NightSupermarket.Game;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace NightSupermarket.Tests
{
    /// <summary>Basic budget check at the current hard cap (16 customers). Writes TestResults/performance.txt.</summary>
    public sealed class PerformanceTests
    {
        [UnityTest] public IEnumerator HardCapPopulationStaysWithinBasicBudgets()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var root = Object.FindAnyObjectByType<PrototypeRoot>();
            var population = root.Population;
            var random = new System.Random(3);
            int guard = 0;
            while (population.Customers < population.Settings.maxActiveCustomers && guard++ < 64)
            {
                var point = root.Directory.Pick(PointKind.Browse, null, ZoneAccess.Customer, random, -1);
                point?.Release(-1);
                if (point != null) population.Spawn(NpcRole.Customer, point.Position, 0, true);
            }
            Assert.That(population.Customers, Is.EqualTo(population.Settings.maxActiveCustomers));
            for (int i = 0; i < root.MannequinCount; i++) root.MannequinAt(i).Teleport(new Vector3(-2.5f + (i % 2) * 5f, 0.1f, -2f + (i / 2) * 3f));
            yield return new WaitForSeconds(2f);

            using var npcUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NPC.Update", 600);
            using var perception = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NPC.Perception", 600);
            long raysBefore = NpcPerception.TotalLineTests;
            float start = Time.realtimeSinceStartup, gameStart = Time.time;
            int frames = 0; double frameSum = 0, frameWorst = 0, npcSum = 0, perceptionSum = 0;
            while (Time.time - gameStart < 10f)
            {
                yield return null;
                frames++;
                double ms = Time.unscaledDeltaTime * 1000.0;
                frameSum += ms; if (frames > 10) frameWorst = System.Math.Max(frameWorst, ms);
                npcSum += npcUpdate.LastValue / 1e6;
                perceptionSum += perception.LastValue / 1e6;
                if (frames % 5 == 0)
                    for (int i = 0; i < root.MannequinCount; i++) root.InputAt(i).Submit(new PlayerCommand(new Vector2(frames % 40 < 20 ? 1 : -1, 0), false, false));
            }
            float gameSeconds = Time.time - gameStart;
            double raysPerSecond = (NpcPerception.TotalLineTests - raysBefore) / System.Math.Max(0.001, gameSeconds);
            int snapshotBytes = Marshal.SizeOf<NpcSnapshot>();
            var snapshots = new List<NpcSnapshot>();
            population.CaptureSnapshots(snapshots);
            int bodies = population.GetComponentsInChildren<NpcController>(true).Length;
            int lights = 0, enabledLights = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                lights++;
                if (light.enabled && light.intensity > 0.01f) enabledLights++;
            }
            int renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
            string report =
                $"NPCs active: {population.Active.Count} (customers {population.Customers}, cap {population.Settings.maxActiveCustomers}); bodies {bodies}\n" +
                $"Frames: {frames} over {Time.realtimeSinceStartup - start:0.0}s real; mean {frameSum / frames:0.00} ms, worst {frameWorst:0.00} ms (batch-mode editor, uncapped; not representative of a player build)\n" +
                $"NPC.Update marker: {npcSum / frames:0.0000} ms per frame; NPC.Perception marker: {perceptionSum / gameSeconds:0.00} ms per game second (interval-based)\n" +
                $"Line-of-sight rays: {raysPerSecond:0} per second across all NPCs\n" +
                $"Lights: {enabledLights} enabled / {lights} total; mesh renderers: {renderers}\n" +
                $"Ambient: {RenderSettings.ambientLight}  fog density: {RenderSettings.fogDensity}\n" +
                $"Snapshot: {snapshotBytes} bytes unpacked per NPC, {snapshotBytes * snapshots.Count} bytes per full update for {snapshots.Count} NPCs\n";
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/performance.txt", report);
            Debug.Log("[PERF]\n" + report);
            Assert.That(bodies, Is.LessThanOrEqualTo(population.Settings.maxActiveCustomers + population.Settings.employees + 2), "no body leak at the cap");
            Assert.That(raysPerSecond, Is.LessThan(population.Active.Count * root.MannequinCount * 6), "at most one ray per NPC-mannequin pair per sample");
            Assert.That(snapshotBytes, Is.LessThanOrEqualTo(48), "snapshot stays small");
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
