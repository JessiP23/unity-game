using System.Collections.Generic;
using System.Text;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// In-game NPC debugging, drawn in the Game view: labels (state, destination, suspicion), perception
    /// (vision cone, focus target, last known position), and navigation (current path).
    /// </summary>
    public sealed class NpcDebugOverlay : MonoBehaviour
    {
        public bool Labels { get; set; }
        public bool Perception { get; set; }
        public bool Navigation { get; set; }
        private CustomerPopulationManager population;
        private readonly Dictionary<NpcController, Gizmo> gizmos = new Dictionary<NpcController, Gizmo>();
        private readonly List<NpcController> gone = new List<NpcController>();
        private readonly StringBuilder text = new StringBuilder();
        private Material lineMaterial;
        private sealed class Gizmo
        {
            public TextMesh Label;
            public LineRenderer Cone, Focus, Path;
        }

        public void Configure(CustomerPopulationManager npcs) => population = npcs;

        private void LateUpdate()
        {
            if (population == null) return;
            var camera = ActiveCamera();
            foreach (var npc in population.Active)
            {
                var gizmo = Get(npc);
                gizmo.Label.gameObject.SetActive(Labels);
                gizmo.Cone.enabled = gizmo.Focus.enabled = Perception;
                gizmo.Path.enabled = Navigation;
                if (Labels) DrawLabel(npc, gizmo, camera);
                if (Perception) DrawPerception(npc, gizmo);
                if (Navigation) DrawPath(npc, gizmo);
            }
            gone.Clear();
            foreach (var pair in gizmos) if (pair.Key == null || !pair.Key.isActiveAndEnabled) gone.Add(pair.Key);
            foreach (var npc in gone) Hide(gizmos[npc]);
        }

        private void DrawLabel(NpcController npc, Gizmo gizmo, Camera camera)
        {
            text.Clear();
            text.Append(npc.Role).Append(' ').Append(npc.Id).Append("  ").Append(npc.StateLabel).Append('\n');
            if (npc.Behavior is CustomerBehavior customer && customer.Brain != null)
            {
                var brain = customer.Brain;
                text.Append("to ").Append(brain.Destination);
                if (brain.DestinationZone.HasValue) text.Append(' ').Append(brain.DestinationZone.Value);
                text.Append("  list ").Append(brain.List.Picked).Append('/').Append(brain.List.Items.Count).Append('\n');
            }
            var tracker = npc.Perception != null ? npc.Perception.FocusTracker : null;
            text.Append(npc.Awareness);
            if (tracker != null) text.Append("  susp ").Append(tracker.Suspicion.ToString("0.00"));
            if (npc.Behavior != null && npc.Behavior.ReportsSent > 0) text.Append("  reports ").Append(npc.Behavior.ReportsSent);
            gizmo.Label.text = text.ToString();
            gizmo.Label.color = npc.Awareness >= AwarenessState.Suspicious ? new Color(1f, 0.45f, 0.35f) : npc.Awareness == AwarenessState.Observing ? new Color(1f, 0.85f, 0.3f) : Color.white;
            gizmo.Label.transform.position = npc.transform.position + Vector3.up * 2.25f;
            if (camera != null) gizmo.Label.transform.rotation = Quaternion.LookRotation(gizmo.Label.transform.position - camera.transform.position);
        }

        private void DrawPerception(NpcController npc, Gizmo gizmo)
        {
            var perception = npc.Perception;
            if (perception == null || perception.Sensor == null) { gizmo.Cone.positionCount = 0; gizmo.Focus.positionCount = 0; return; }
            Vector3 eye = perception.Eye;
            float half = perception.Sensor.FieldOfView * 0.5f, range = perception.Sensor.Range;
            const int steps = 10;
            gizmo.Cone.positionCount = steps + 3;
            gizmo.Cone.SetPosition(0, eye);
            for (int i = 0; i <= steps; i++)
                gizmo.Cone.SetPosition(i + 1, eye + Quaternion.Euler(0, Mathf.Lerp(-half, half, i / (float)steps), 0) * npc.transform.forward * range);
            gizmo.Cone.SetPosition(steps + 2, eye);
            var color = npc.Awareness >= AwarenessState.Suspicious ? Color.red : npc.Awareness == AwarenessState.Observing ? Color.yellow : new Color(0.4f, 0.8f, 1f);
            gizmo.Cone.startColor = gizmo.Cone.endColor = color;
            var tracker = perception.FocusTracker;
            if (tracker != null && tracker.HasLastKnown)
            {
                Vector3 known = new Vector3(tracker.LastKnown.X, tracker.LastKnown.Y + 0.05f, tracker.LastKnown.Z);
                gizmo.Focus.positionCount = 5;
                gizmo.Focus.SetPosition(0, eye);
                gizmo.Focus.SetPosition(1, known);
                gizmo.Focus.SetPosition(2, known + new Vector3(0.3f, 0, 0.3f));
                gizmo.Focus.SetPosition(3, known + new Vector3(-0.3f, 0, 0.3f));
                gizmo.Focus.SetPosition(4, known);
                gizmo.Focus.startColor = gizmo.Focus.endColor = color;
            }
            else gizmo.Focus.positionCount = 0;
        }

        private static void DrawPath(NpcController npc, Gizmo gizmo)
        {
            var corners = npc.Movement != null ? npc.Movement.Corners : new Vector3[0];
            gizmo.Path.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++) gizmo.Path.SetPosition(i, corners[i] + Vector3.up * 0.05f);
        }

        private Gizmo Get(NpcController npc)
        {
            if (gizmos.TryGetValue(npc, out var gizmo)) return gizmo;
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            var root = new GameObject("NPC debug " + npc.Id).transform;
            root.SetParent(transform, false);
            gizmo = new Gizmo
            {
                Label = SignFactory.Label(root, "", 0.12f, Color.white),
                Cone = Line(root, "Cone", 0.02f), Focus = Line(root, "Focus", 0.03f), Path = Line(root, "Path", 0.04f)
            };
            gizmo.Path.startColor = gizmo.Path.endColor = new Color(0.3f, 1f, 0.5f);
            Hide(gizmo);
            return gizmos[npc] = gizmo;
        }

        private LineRenderer Line(Transform parent, string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true; line.widthMultiplier = width; line.positionCount = 0;
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private static void Hide(Gizmo gizmo)
        {
            gizmo.Label.gameObject.SetActive(false);
            gizmo.Cone.enabled = gizmo.Focus.enabled = gizmo.Path.enabled = false;
        }

        private static Camera ActiveCamera()
        {
            foreach (var camera in Camera.allCameras) if (camera.isActiveAndEnabled && camera.targetTexture == null) return camera;
            return Camera.main;
        }
    }
}
