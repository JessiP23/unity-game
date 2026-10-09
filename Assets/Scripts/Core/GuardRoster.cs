using System;
namespace NightSupermarket.Core
{
    /// <summary>
    /// Who is on duty tonight. Multipliers on the rules asset's guard numbers, so a shift can have a
    /// sharp-eyed slow guard or a half-blind fast one without touching the asset. The tell is what
    /// the player is told at the start, because a fair guard is one you can read.
    /// </summary>
    public sealed class GuardProfile
    {
        public readonly string Name, Trait, Tell;
        public readonly double Vision, FieldOfView, Pace, Hearing, Torch;
        public GuardProfile(string name, string trait, string tell, double vision, double fieldOfView, double pace, double hearing, double torch)
        { Name = name; Trait = trait; Tell = tell; Vision = vision; FieldOfView = fieldOfView; Pace = pace; Hearing = hearing; Torch = torch; }
    }

    public static class GuardRoster
    {
        public static readonly GuardProfile[] All =
        {
            new GuardProfile("Marco", "by the book", "Average eyes, average pace, hears what you'd expect.", 1, 1, 1, 1, 1),
            new GuardProfile("Dana", "sharp eyes, slow feet", "Sees far and wide, but walks slowly: freeze early, then outpace her.", 1.3, 1.15, 0.8, 0.9, 1.2),
            new GuardProfile("Ilya", "quick and short-sighted", "Short sight and a narrow torch, but fast: let him pass close, never run near him.", 0.75, 0.85, 1.25, 1, 0.7),
            new GuardProfile("Rosa", "all ears", "Hears a dropped crate across the store; her eyes are ordinary. Walk, don't run.", 1, 1, 1, 1.6, 1),
            new GuardProfile("Teo", "wide torch", "A wide, bright torch when the lights go out; nearly blind before then.", 0.9, 0.9, 1, 1, 1.6),
            new GuardProfile("Nadia", "tunnel vision", "Sees a long way straight ahead and little to the side: cross behind, not in front.", 1.35, 0.6, 1, 1, 1)
        };

        /// <summary>Shift 0 is always Marco so tests see the plain rules; other shifts rotate deterministically.</summary>
        public static GuardProfile For(int shift)
        {
            if (shift == 0) return All[0];
            var rng = new Random(shift * 7 + 3);
            return All[rng.Next(All.Length)];
        }
    }
}
