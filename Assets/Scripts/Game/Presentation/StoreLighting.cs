using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace NightSupermarket.Game
{
    /// <summary>Presentation of <see cref="LightingMode"/>: fixtures, emergency lights, fog, and post-processing.</summary>
    public sealed class StoreLighting : MonoBehaviour
    {
        public static StoreLighting Active { get; private set; }
        private static readonly Color Fluorescent = new Color(0.86f, 0.93f, 1f);
        private readonly List<Fixture> fixtures = new List<Fixture>();
        private readonly List<Light> emergency = new List<Light>();
        private readonly List<Light> alwaysOn = new List<Light>();
        private Light dawn;
        private ReflectionProbe probe;
        private LightingMode mode = LightingMode.Normal;
        private float probeTimer = -1;
        private sealed class Fixture
        {
            public Light Light;
            public Renderer[] Tubes;
            public float Intensity;
            public bool Flickers;
            public bool Warehouse;
            public bool On;
        }
        public static StoreLighting Create(Transform root)
        {
            var lighting = new GameObject("Store lighting").AddComponent<StoreLighting>();
            lighting.transform.SetParent(root, false);
            Active = lighting;
            lighting.Configure();
            return lighting;
        }
        private void Configure()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { light.intensity = 0.02f; light.shadows = LightShadows.None; light.color = new Color(0.5f, 0.6f, 0.9f); }
            var probeObject = new GameObject("Store reflections");
            probeObject.transform.SetParent(transform, false);
            probeObject.transform.position = new Vector3(0, 1.5f, 0);
            probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(30, 3.2f, 30);
            probe.resolution = 128;
            probe.boxProjection = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            CreateVolume();
        }
        private void CreateVolume()
        {
            var volume = new GameObject("Night grade").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.7f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.72f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.36f); vignette.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(0.28f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f); color.contrast.Override(14f); color.saturation.Override(-18f);
            color.colorFilter.Override(new Color(0.93f, 0.98f, 1f));
            var aberration = profile.Add<ChromaticAberration>(true); aberration.intensity.Override(0.07f);
            var balance = profile.Add<WhiteBalance>(true); balance.temperature.Override(-6f);
            volume.profile = profile;
        }
        /// <summary>Ceiling fixture with a real downward light and glowing tubes.</summary>
        public void AddFixture(GameObject visual, Vector3 position, float intensity, float range, bool flickers, bool warehouse, Color? tint = null)
        {
            var lamp = new GameObject("Fixture light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = position;
            lamp.transform.rotation = Quaternion.Euler(90, 0, 0);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 160; lamp.innerSpotAngle = 80;
            lamp.range = range; lamp.intensity = intensity;
            lamp.color = tint ?? Fluorescent;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
            var tubes = visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0];
            fixtures.Add(new Fixture { Light = lamp, Tubes = tubes, Intensity = intensity, Flickers = flickers, Warehouse = warehouse });
        }
        public void AddEmergency(Vector3 position, float range)
        {
            var lamp = new GameObject("Emergency light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = position;
            lamp.type = LightType.Point; lamp.range = range; lamp.intensity = 8f;
            lamp.color = new Color(1f, 0.08f, 0.05f);
            lamp.enabled = false;
            emergency.Add(lamp);
        }
        /// <summary>Signs and screens that stay lit in every mode.</summary>
        public Light AddGlow(Vector3 position, Color color, float range, float intensity)
        {
            var lamp = new GameObject("Glow").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = position;
            lamp.type = LightType.Point; lamp.range = range; lamp.intensity = intensity; lamp.color = color;
            alwaysOn.Add(lamp);
            return lamp;
        }
        public void AddDawn(Vector3 position, Vector3 target)
        {
            dawn = new GameObject("Dawn light").AddComponent<Light>();
            dawn.transform.SetParent(transform, false);
            dawn.transform.position = position;
            dawn.transform.LookAt(target);
            dawn.type = LightType.Spot; dawn.spotAngle = 120; dawn.range = 40; dawn.intensity = 0;
            dawn.color = new Color(1f, 0.72f, 0.45f);
            dawn.shadows = LightShadows.Soft;
        }
        public void Apply(LightingMode next)
        {
            mode = next;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                bool on = mode switch
                {
                    LightingMode.Normal => true,
                    LightingMode.Partial => i % 3 == 0 || fixture.Warehouse,
                    LightingMode.Dawn => fixture.Warehouse,
                    _ => false
                };
                SetFixture(fixture, on ? 1f : 0f);
            }
            foreach (var lamp in emergency) lamp.enabled = mode == LightingMode.Emergency || mode == LightingMode.Dark;
            foreach (var lamp in emergency) lamp.intensity = mode == LightingMode.Emergency ? 7f : 1.6f;
            if (dawn != null) dawn.intensity = mode == LightingMode.Dawn ? 60f : 0f;
            SignFactory.SetBrightness(mode == LightingMode.Dark ? 0.25f : mode == LightingMode.Emergency ? 0.4f : 1f);
            RenderSettings.ambientLight = mode switch
            {
                LightingMode.Dark => new Color(0.026f, 0.028f, 0.04f),
                LightingMode.Emergency => new Color(0.07f, 0.012f, 0.012f),
                LightingMode.Partial => new Color(0.04f, 0.042f, 0.05f),
                LightingMode.Dawn => new Color(0.26f, 0.19f, 0.14f),
                _ => new Color(0.09f, 0.095f, 0.105f)
            };
            RenderSettings.fogColor = mode switch
            {
                LightingMode.Emergency => new Color(0.05f, 0.005f, 0.005f),
                LightingMode.Dawn => new Color(0.3f, 0.22f, 0.16f),
                _ => new Color(0.015f, 0.018f, 0.024f)
            };
            RenderSettings.fogDensity = mode == LightingMode.Dark ? 0.05f : 0.028f;
            probeTimer = 0.1f;
        }
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private void SetFixture(Fixture fixture, float level)
        {
            fixture.On = level > 0.01f;
            fixture.Light.enabled = fixture.On;
            fixture.Light.intensity = fixture.Intensity * level;
            block ??= new MaterialPropertyBlock();
            block.SetColor(EmissionColor, (fixture.Light.color * 3.5f) * level);
            foreach (var tube in fixture.Tubes) tube.SetPropertyBlock(block);
        }
        private void Update()
        {
            if (probeTimer >= 0)
            {
                probeTimer -= Time.deltaTime;
                if (probeTimer < 0 && probe != null) probe.RenderProbe();
            }
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (!fixture.Flickers || !fixture.On) continue;
                float noise = Mathf.PerlinNoise(Time.time * 6f, i * 3.1f);
                float level = noise < 0.3f ? 0.04f : noise < 0.38f ? 0.45f : 1f;
                fixture.Light.intensity = fixture.Intensity * level;
                block ??= new MaterialPropertyBlock();
                block.SetColor(EmissionColor, fixture.Light.color * 3.5f * level);
                foreach (var tube in fixture.Tubes) tube.SetPropertyBlock(block);
            }
        }
        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
