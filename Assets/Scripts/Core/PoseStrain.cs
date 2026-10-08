using System;
namespace NightSupermarket.Core
{
    /// <summary>The four poses a mannequin can hold. Each department expects one of them.</summary>
    public enum Stance { Neutral, Display, Browsing, Lounging, Staff }

    public static class PoseLibrary
    {
        public static readonly Stance[] Selectable = { Stance.Display, Stance.Browsing, Stance.Lounging, Stance.Staff };

        /// <summary>The pose that looks like it belongs in a department. Anywhere else, nothing matches.</summary>
        public static Stance ExpectedIn(ZoneType? zone) => zone switch
        {
            ZoneType.Clothing => Stance.Display,
            ZoneType.Electronics => Stance.Browsing,
            ZoneType.Supermarket => Stance.Browsing,
            ZoneType.Home => Stance.Lounging,
            ZoneType.Checkout => Stance.Staff,
            ZoneType.CustomerService => Stance.Staff,
            ZoneType.Employee => Stance.Staff,
            ZoneType.Warehouse => Stance.Staff,
            _ => Stance.Neutral
        };

        public static string Name(Stance pose) => pose switch
        {
            Stance.Display => "Display",
            Stance.Browsing => "Browsing",
            Stance.Lounging => "Lounging",
            Stance.Staff => "Staff",
            _ => "Standing"
        };

        public static string Hint(Stance pose) => pose switch
        {
            Stance.Display => "arms out, chin up — Clothing",
            Stance.Browsing => "hand on a shelf — Aisles, Electronics",
            Stance.Lounging => "leaning, relaxed — Home",
            Stance.Staff => "hands behind the back — Checkout, Staff rooms",
            _ => "just standing"
        };
    }

    /// <summary>
    /// Holding a pose is a choice with a cost. The first seconds are free; after that strain builds
    /// until the mannequin twitches once. Letting go recovers twice as fast. This makes "when do I
    /// freeze" a decision instead of a default.
    /// </summary>
    public sealed class PoseStrain
    {
        public const double Comfort = 8, BuildSeconds = 5, RecoverRate = 2;
        public Stance Current { get; private set; } = Stance.Neutral;
        public bool Holding => Current != Stance.Neutral;
        /// <summary>0 = fresh, 1 = about to twitch.</summary>
        public double Strain { get; private set; }
        public double Held { get; private set; }
        public int Twitches { get; private set; }

        public void Begin(Stance pose)
        {
            if (pose == Stance.Neutral) { Release(); return; }
            if (Current == pose) return;
            // Switching between poses is still holding still: the held time carries over.
            if (!Holding) Held = 0;
            Current = pose;
        }

        public void Release() { Current = Stance.Neutral; Held = 0; }

        /// <summary>Returns true on the tick the mannequin twitches. Callers treat a twitch as one real movement.</summary>
        public bool Tick(double delta)
        {
            GameRules.RequireDelta(delta);
            if (!Holding)
            {
                Strain = Math.Max(0, Strain - delta * RecoverRate / BuildSeconds);
                return false;
            }
            Held += delta;
            if (Held <= Comfort) return false;
            Strain = Math.Min(1, Strain + delta / BuildSeconds);
            if (Strain < 1) return false;
            Twitches++;
            Strain = 0.5;
            Held = Comfort;
            return true;
        }

        /// <summary>Seconds of comfortable holding left before strain starts. Zero once it has begun.</summary>
        public double ComfortLeft => Holding ? Math.Max(0, Comfort - Held) : Comfort;
    }
}
