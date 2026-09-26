using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Which body and clips a <see cref="CharacterVisual"/> uses. Add a profile to add a character.</summary>
    public sealed class CharacterProfile
    {
        public string Model { get; }
        public Vector3 Offset { get; }
        /// <summary>White plastic head, hidden hair cards, and holds its pose when it stops.</summary>
        public bool Mannequin { get; }
        public string Idle { get; }
        public string Walk { get; }
        public string Run { get; }
        public CharacterProfile(string model, Vector3 offset, bool mannequin, string idle, string walk, string run)
        { Model = model; Offset = offset; Mannequin = mannequin; Idle = idle; Walk = walk; Run = run; }

        /// <summary>The guard's pivot is the capsule centre, one metre above the feet.</summary>
        public static readonly CharacterProfile Guard =
            new CharacterProfile("Guard", new Vector3(0, -1, 0), false, "m_idle_look_around_01", "m_walk_neutral_01", "m_run_neutral_01");
        public static readonly CharacterProfile MannequinMale =
            new CharacterProfile("MannequinMale", Vector3.zero, true, "m_idle_neutral_01", "m_walk_neutral_01", "m_run_neutral_01");
        public static readonly CharacterProfile MannequinFemale =
            new CharacterProfile("MannequinFemale", Vector3.zero, true, "f_idle_neutral_01", "f_walk_neutral_01", "f_run_neutral_01");
        public static CharacterProfile MannequinFor(int playerIndex) => playerIndex % 2 == 0 ? MannequinMale : MannequinFemale;
    }
}
