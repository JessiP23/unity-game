using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Ordered key → action table that also documents itself for on-screen help, so the keys a player
    /// reads can never drift from the keys the game polls. Entries without a key are display-only.
    /// </summary>
    public sealed class KeyCommandMap
    {
        public readonly struct Entry
        {
            public readonly Key Key;
            public readonly string Label;
            public readonly string Description;
            public readonly Action Run;
            public Entry(Key key, string label, string description, Action run) { Key = key; Label = label; Description = description; Run = run; }
        }
        private readonly List<Entry> entries = new List<Entry>();
        public string Title { get; }
        public IReadOnlyList<Entry> Entries => entries;
        public KeyCommandMap(string title) { Title = title; }
        /// <summary>Binds a key; the label defaults to the key's printed name.</summary>
        public KeyCommandMap Bind(Key key, string description, Action run, string label = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            entries.Add(new Entry(key, label ?? Printed(key), description, run));
            return this;
        }
        /// <summary>Documents an input handled elsewhere, such as WASD or the mouse.</summary>
        public KeyCommandMap Describe(string label, string description)
        {
            entries.Add(new Entry(Key.None, label, description, null));
            return this;
        }
        public void Poll(Keyboard keyboard)
        {
            if (keyboard == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Run != null && entries[i].Key != Key.None && keyboard[entries[i].Key].wasPressedThisFrame)
                    entries[i].Run();
        }
        private static string Printed(Key key)
        {
            if (key >= Key.Digit1 && key <= Key.Digit9) return ((int)(key - Key.Digit1) + 1).ToString();
            if (key == Key.Digit0) return "0";
            return key.ToString();
        }
    }
}
