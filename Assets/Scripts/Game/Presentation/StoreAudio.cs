using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Generated clips for the night. Footsteps scale with speed and also become guard noise.
    /// A job chime plays once when that job completes. The guard has a short spatial bark per state.
    /// </summary>
    public sealed class StoreAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private static AudioClip stepClip, chimeClip, askClip, chaseClip, catchClip, whistleClip, heartClip, exhaleClip;
        private AudioSource steps, chime, radio, heart;
        private float nextBeat;
        private System.IDisposable cueSubscription;
        public int PlayedCues { get; private set; }
        private PlayerMotor motor;
        private WorldSignals signals;
        private GuardController guard;
        private GuardState heardState;
        private float stride, airTime, nextBark, nextWhistle;
        private bool airborne;

        public static StoreAudio Create(Transform parent)
        {
            var host = new GameObject("Store audio");
            host.transform.SetParent(parent, false);
            var audio = host.AddComponent<StoreAudio>();
            audio.steps = Source(host.transform, "Steps", false);
            audio.chime = Source(host.transform, "Chime", false);
            audio.radio = Source(host.transform, "Guard radio", true);
            audio.heart = Source(host.transform, "Heartbeat", false);
            EnsureClips();
            return audio;
        }

        public void Bind(PlayerMotor body, WorldSignals world)
        {
            motor = body;
            signals = world;
            stride = 0;
            airTime = 0;
            airborne = false;
        }

        public void BindGuard(GuardController body)
        {
            guard = body;
            heardState = body != null && body.Brain != null ? body.Brain.State : GuardState.Patrol;
            nextWhistle = Time.time + 8f;
            nextBark = 0;
            if (radio != null && body != null) radio.transform.SetParent(body.transform, false);
        }

        public void Watch(MissionSystem missions)
        {
            if (missions == null) return;
            for (int i = 0; i < missions.Missions.Count; i++)
            {
                var tracker = missions.Missions[i];
                tracker.Changed += () => { if (tracker.Complete) chime.PlayOneShot(chimeClip, 0.55f); };
            }
        }

        public void Listen(EventStream<AudioCue> cues)
        {
            cueSubscription?.Dispose();
            cueSubscription = cues.Subscribe(cue =>
            {
                AudioClip clip = cue switch
                {
                    AudioCue.DetectionWarning => askClip,
                    AudioCue.DetectionRed => chaseClip,
                    AudioCue.Discovery => catchClip,
                    AudioCue.Capture => catchClip,
                    AudioCue.Defeat => catchClip,
                    AudioCue.CustomerReport => askClip,
                    AudioCue.Rescue => chimeClip,
                    AudioCue.MissionComplete => chimeClip,
                    AudioCue.Escape => chimeClip,
                    AudioCue.Victory => chimeClip,
                    AudioCue.Door => stepClip,
                    AudioCue.Break => catchClip,
                    _ => null
                };
                if (clip == null) return;
                chime.PlayOneShot(clip, 0.8f); PlayedCues++;
            });
        }
        /// <summary>
        /// A pulse that quickens with danger (0 = silent, 1 = about to be caught). Called every frame;
        /// it decides when the next beat falls. Muted while the menu or the end card is up.
        /// </summary>
        public void Heartbeat(float danger, bool muted)
        {
            if (heart == null || heartClip == null) return;
            if (muted || danger < 0.08f) { nextBeat = Time.unscaledTime + 0.2f; return; }
            if (Time.unscaledTime < nextBeat) return;
            float interval = Mathf.Lerp(1.15f, 0.42f, Mathf.Clamp01((danger - 0.08f) / 0.92f));
            heart.pitch = Mathf.Lerp(0.9f, 1.25f, danger);
            heart.PlayOneShot(heartClip, Mathf.Lerp(0.18f, 0.6f, danger));
            nextBeat = Time.unscaledTime + interval;
        }

        /// <summary>The breath you let out when a watcher looks away. The "safe again" cue.</summary>
        public void Exhale()
        {
            if (chime == null || exhaleClip == null) return;
            chime.PlayOneShot(exhaleClip, 0.45f);
        }

        public void TestSound()
        {
            if (chime == null) return;
            chime.PlayOneShot(chimeClip, 1); PlayedCues++;
        }
        private void OnDestroy() => cueSubscription?.Dispose();

        private void Update()
        {
            GuardVoice();
            if (motor == null || motor.Record == null || !motor.Record.Free) { stride = 0; airTime = 0; airborne = false; return; }
            float speed = motor.ActualSpeed;
            if (!motor.Grounded) { airborne = true; airTime += Time.deltaTime; return; }
            if (airborne) { airborne = false; if (airTime > 0.12f) Footfall(0.85f); airTime = 0; return; }
            if (speed < 0.45f) { stride = 0; return; }
            float loud = Loudness(speed);
            stride += speed * Time.deltaTime;
            float length = Mathf.Lerp(1.55f, 1.15f, loud);
            if (stride < length) return;
            stride -= length;
            Footfall(loud);
        }

        private float Loudness(float speed)
        {
            float walk = motor.Rules.walkSpeed;
            float sprint = Mathf.Max(walk + 0.1f, motor.Rules.sprintSpeed);
            return Mathf.Clamp01(Mathf.InverseLerp(walk * 0.55f, sprint, speed));
        }

        private void Footfall(float loud)
        {
            steps.PlayOneShot(stepClip, Mathf.Lerp(0.5f, 0.95f, loud));
            if (signals == null || motor.Record == null) return;
            signals.Noise.Publish(new NoiseEvent(motor.transform.position, Mathf.Lerp(0.16f, 0.48f, loud), motor.Record.Id));
        }

        private void GuardVoice()
        {
            if (guard == null || guard.Brain == null || radio == null) return;
            var state = guard.Brain.State;
            if (state != heardState)
            {
                bool capture = state == GuardState.Capture;
                if (capture || Time.time >= nextBark)
                {
                    if (state == GuardState.Investigate) radio.PlayOneShot(askClip, 0.5f);
                    else if (state == GuardState.Chase) radio.PlayOneShot(chaseClip, 0.62f);
                    else if (state == GuardState.Capture) radio.PlayOneShot(catchClip, 0.7f);
                    nextBark = Time.time + 2.4f;
                }
                heardState = state;
            }
            else if (state == GuardState.Patrol && Time.time >= nextWhistle)
            {
                radio.PlayOneShot(whistleClip, 0.28f);
                nextWhistle = Time.time + 16f;
            }
        }

        private static AudioSource Source(Transform parent, string name, bool spatial)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0;
            source.minDistance = 2f;
            source.maxDistance = 18f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private static void EnsureClips()
        {
            if (stepClip == null) stepClip = Step();
            if (chimeClip == null) chimeClip = Chime();
            if (askClip == null) askClip = Ask();
            if (chaseClip == null) chaseClip = Chase();
            if (catchClip == null) catchClip = Catch();
            if (whistleClip == null) whistleClip = Whistle();
            if (heartClip == null) heartClip = Heart();
            if (exhaleClip == null) exhaleClip = Exhale_();
        }

        /// <summary>Lub-dub: two low thumps, the second softer.</summary>
        private static AudioClip Heart()
        {
            int count = (int)(Rate * 0.42f);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)Rate;
                float first = Mathf.Sin(2f * Mathf.PI * 58f * time) * Mathf.Exp(-time * 22f);
                float t2 = time - 0.17f;
                float second = t2 > 0 ? Mathf.Sin(2f * Mathf.PI * 50f * t2) * Mathf.Exp(-t2 * 26f) * 0.65f : 0f;
                data[i] = (first + second) * 0.9f;
            }
            return Clip("Heartbeat", data);
        }

        /// <summary>A short breath out: filtered noise that falls in pitch and fades.</summary>
        private static AudioClip Exhale_()
        {
            int count = (int)(Rate * 0.55f);
            var data = new float[count];
            var random = new System.Random(11);
            float low = 0, band = 0;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)Rate;
                float white = (float)random.NextDouble() * 2f - 1f;
                float cutoff = Mathf.Lerp(0.35f, 0.08f, time / 0.55f);
                low += (white - low) * cutoff;
                band += (low - band) * 0.5f;
                float envelope = Mathf.Sin(Mathf.Clamp01(time / 0.08f) * Mathf.PI * 0.5f) * Mathf.Exp(-time * 5.5f);
                data[i] = (low - band) * envelope * 2.2f;
            }
            return Clip("Exhale", data);
        }

        private static AudioClip Step()
        {
            int count = (int)(Rate * 0.08f);
            var data = new float[count];
            var random = new System.Random(3);
            float sample = 0;
            for (int i = 0; i < count; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                sample = sample * 0.84f + white * 0.16f;
                data[i] = sample * Mathf.Exp(-i / (Rate * 0.018f));
            }
            float peak = 0;
            foreach (float value in data) peak = Mathf.Max(peak, Mathf.Abs(value));
            if (peak > 0) for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / peak;
            return Clip("Footstep", data);
        }

        private static AudioClip Chime()
        {
            int count = (int)(Rate * 0.42f);
            var data = new float[count];
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)Rate;
                bool second = time >= 0.16f;
                float local = second ? time - 0.16f : time;
                float frequency = second ? 1174f : 784f;
                float envelope = Mathf.Sin(Mathf.Clamp01(local / 0.015f) * Mathf.PI * 0.5f) * Mathf.Exp(-local * 7f);
                data[i] = Mathf.Sin(phase) * envelope * 0.4f;
                phase += frequency * Mathf.PI * 2f / Rate;
            }
            return Clip("Job chime", data);
        }

        private static AudioClip Ask()
        {
            int count = (int)(Rate * 0.32f);
            var data = new float[count];
            Tone(data, 0, (int)(Rate * 0.12f), 280f, 0.35f);
            Tone(data, (int)(Rate * 0.14f), (int)(Rate * 0.16f), 520f, 0.4f);
            return Clip("Guard ask", data);
        }

        private static AudioClip Chase()
        {
            int count = (int)(Rate * 0.38f);
            var data = new float[count];
            Tone(data, 0, (int)(Rate * 0.08f), 494f, 0.45f);
            Tone(data, (int)(Rate * 0.1f), (int)(Rate * 0.08f), 740f, 0.45f);
            Tone(data, (int)(Rate * 0.22f), (int)(Rate * 0.12f), 988f, 0.4f);
            return Clip("Guard chase", data);
        }

        private static AudioClip Catch()
        {
            int count = (int)(Rate * 0.45f);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)Rate;
                float frequency = Mathf.Lerp(880f, 196f, time / 0.45f);
                float envelope = Mathf.Exp(-time * 3.5f);
                data[i] = Mathf.Sin(i * frequency * Mathf.PI * 2f / Rate) * envelope * 0.45f;
            }
            return Clip("Guard catch", data);
        }

        private static AudioClip Whistle()
        {
            int count = (int)(Rate * 0.5f);
            var data = new float[count];
            Tone(data, 0, (int)(Rate * 0.16f), 1568f, 0.22f);
            Tone(data, (int)(Rate * 0.2f), (int)(Rate * 0.22f), 2093f, 0.18f);
            return Clip("Guard whistle", data);
        }

        private static void Tone(float[] data, int start, int length, float frequency, float volume)
        {
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float along = i / (float)length;
                float envelope = Mathf.Sin(Mathf.Clamp01(along / 0.12f) * Mathf.PI * 0.5f) * Mathf.Exp(-along * 2.8f);
                data[start + i] += Mathf.Sin((start + i) * frequency * Mathf.PI * 2f / Rate) * envelope * volume;
            }
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
