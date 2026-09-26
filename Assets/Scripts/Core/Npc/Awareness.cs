using System;
namespace NightSupermarket.Core
{
    /// <summary>How an ordinary person interprets what they see. Security uses <see cref="DetectionSystem"/> instead.</summary>
    public enum AwarenessState { Unaware, Observing, Suspicious, Reporting }

    public enum ReportKind { MovingMannequin }

    /// <summary>Tuning for a non-security observer. Values are data, not constants, so profiles can differ.</summary>
    public sealed class AwarenessSettings
    {
        /// <summary>Target speed (m/s) that counts as "it moved". Matches the guard's threshold semantics.</summary>
        public double MovementThreshold { get; set; } = 0.08;
        /// <summary>Seconds of looking before movement registers at all (people need a moment to focus).</summary>
        public double NoticeTime { get; set; } = 0.35;
        /// <summary>Suspicion per second while watching a moving mannequin, before sensitivity.</summary>
        public double SuspicionGain { get; set; } = 0.9;
        /// <summary>Suspicion lost per second while nothing suspicious is seen.</summary>
        public double SuspicionDecay { get; set; } = 0.35;
        /// <summary>Suspicion lost per second while watching a mannequin that stands still.</summary>
        public double StillDecay { get; set; } = 0.12;
        /// <summary>Suspicion (0-1 scale) at which the observer becomes suspicious.</summary>
        public double Threshold { get; set; } = 1.0;
        /// <summary>Multiplier for how easily this person is unsettled.</summary>
        public double Sensitivity { get; set; } = 1.0;
        /// <summary>Seconds between becoming suspicious and deciding to report.</summary>
        public double ReactionTime { get; set; } = 1.2;
        /// <summary>Seconds from deciding to report until the report reaches security.</summary>
        public double ReportDelay { get; set; } = 2.0;
        /// <summary>Below this fraction of the threshold, a suspicious observer talks themselves out of it.</summary>
        public double CalmRatio { get; set; } = 0.45;
        /// <summary>Seconds an observer keeps watching a spot after losing sight before forgetting it.</summary>
        public double ForgetTime { get; set; } = 2.5;
        public void Validate()
        {
            GameRules.RequirePositive(Threshold, nameof(Threshold));
            GameRules.RequirePositive(SuspicionGain, nameof(SuspicionGain));
            if (MovementThreshold < 0 || NoticeTime < 0 || SuspicionDecay < 0 || StillDecay < 0 || Sensitivity < 0 ||
                ReactionTime < 0 || ReportDelay < 0 || ForgetTime < 0 || CalmRatio < 0 || CalmRatio >= 1)
                throw new ArgumentOutOfRangeException(nameof(AwarenessSettings));
        }
    }

    /// <summary>
    /// One observer's reading of one target. A still mannequin is just a mannequin; only movement while
    /// watched builds suspicion. The last known position only updates while the target is actually seen.
    /// </summary>
    public sealed class AwarenessTracker
    {
        private readonly AwarenessSettings settings;
        private double watched, unseen, timer;
        private bool reported;
        public AwarenessState State { get; private set; }
        public double Suspicion { get; private set; }
        public MapPoint LastKnown { get; private set; }
        public bool HasLastKnown { get; private set; }
        public bool Visible { get; private set; }
        public event Action<AwarenessState> Changed;
        public AwarenessTracker(AwarenessSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
        }

        /// <summary>Advances the reading. Returns true exactly once, when a report should be sent.</summary>
        public bool Tick(bool visible, double targetSpeed, MapPoint targetPosition, double delta)
        {
            GameRules.RequireDelta(delta);
            if (delta == 0) return false;
            Visible = visible;
            if (visible)
            {
                LastKnown = targetPosition; HasLastKnown = true;
                watched += delta; unseen = 0;
                bool moving = targetSpeed > settings.MovementThreshold && watched >= settings.NoticeTime;
                if (moving) Suspicion += settings.SuspicionGain * settings.Sensitivity * delta;
                else Suspicion -= settings.StillDecay * delta;
            }
            else
            {
                watched = 0; unseen += delta;
                Suspicion -= settings.SuspicionDecay * delta;
            }
            Suspicion = Math.Max(0, Math.Min(settings.Threshold * 1.5, Suspicion));
            switch (State)
            {
                case AwarenessState.Unaware:
                    if (Suspicion >= settings.Threshold) Enter(AwarenessState.Suspicious);
                    else if (visible) Enter(AwarenessState.Observing);
                    break;
                case AwarenessState.Observing:
                    if (Suspicion >= settings.Threshold) Enter(AwarenessState.Suspicious);
                    else if (!visible && unseen >= settings.ForgetTime && Suspicion <= 0) Enter(AwarenessState.Unaware);
                    break;
                case AwarenessState.Suspicious:
                    timer += delta;
                    if (Suspicion < settings.Threshold * settings.CalmRatio) Enter(visible ? AwarenessState.Observing : AwarenessState.Unaware);
                    else if (timer >= settings.ReactionTime) Enter(AwarenessState.Reporting);
                    break;
                case AwarenessState.Reporting:
                    timer += delta;
                    if (!reported && timer >= settings.ReportDelay) { reported = true; return true; }
                    break;
            }
            return false;
        }

        /// <summary>Clears everything after the observer has acted on (or given up on) a report.</summary>
        public void Reset()
        {
            watched = unseen = timer = 0; reported = false; Suspicion = 0; Visible = false; HasLastKnown = false;
            Enter(AwarenessState.Unaware);
        }

        private void Enter(AwarenessState next)
        {
            if (State == next) return;
            State = next; timer = 0;
            Changed?.Invoke(next);
        }
    }

    /// <summary>A civilian's report. Carries only what they saw: where the target was when last seen.</summary>
    public readonly struct SuspiciousActivityEvent
    {
        public readonly string SourceId;
        public readonly string TargetId;
        public readonly MapPoint LastKnownPosition;
        public readonly double Timestamp;
        public readonly double Confidence;
        public readonly ReportKind Kind;
        /// <summary>Store area of the last-known position ("near Clothing"), if it lies inside one.</summary>
        public readonly ZoneType? Zone;
        public SuspiciousActivityEvent(string sourceId, string targetId, MapPoint lastKnownPosition, double timestamp, double confidence, ReportKind kind, ZoneType? zone = null)
        {
            SourceId = sourceId; TargetId = targetId; LastKnownPosition = lastKnownPosition;
            Timestamp = timestamp; Confidence = confidence; Kind = kind; Zone = zone;
        }
    }
}
