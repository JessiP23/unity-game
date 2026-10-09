using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace NightSupermarket.Core
{
    public readonly struct BoardEntry
    {
        public readonly string Name, Grade, Date;
        public readonly int Points;
        public BoardEntry(string name, int points, string grade, string date)
        { Name = Clean(name); Points = points; Grade = grade ?? ""; Date = date ?? ""; }
        /// <summary>Names travel in a text line and over the wire: letters, digits, space, _ and - only, 16 max.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "mannequin";
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') { sb.Append(c); if (sb.Length == 16) break; }
            return sb.Length == 0 ? "mannequin" : sb.ToString();
        }
    }

    /// <summary>
    /// Top scores for one shift. One entry per name (your best), sorted high to low, capped. Pure
    /// data with its own line format so the same code reads a PlayerPrefs string or a server reply.
    /// </summary>
    public sealed class Leaderboard
    {
        public const int Capacity = 10;
        private readonly List<BoardEntry> entries = new List<BoardEntry>();
        public IReadOnlyList<BoardEntry> Entries => entries;

        /// <summary>Adds or raises a name's score. Returns the 1-based rank, or 0 when it did not make the board.</summary>
        public int Submit(BoardEntry entry)
        {
            int existing = entries.FindIndex(e => string.Equals(e.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                if (entries[existing].Points >= entry.Points) return existing + 1;
                entries.RemoveAt(existing);
            }
            entries.Add(entry);
            entries.Sort((a, b) => b.Points.CompareTo(a.Points));
            if (entries.Count > Capacity) entries.RemoveRange(Capacity, entries.Count - Capacity);
            int rank = entries.FindIndex(e => string.Equals(e.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
            return rank + 1;
        }

        /// <summary>Moves a name's entry to a new name (the player retyped theirs on the end card). Returns false when nothing matched.</summary>
        public bool Rename(string from, string to)
        {
            string target = BoardEntry.Clean(to);
            int index = entries.FindIndex(e => string.Equals(e.Name, BoardEntry.Clean(from), StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            var old = entries[index];
            entries.RemoveAt(index);
            Submit(new BoardEntry(target, old.Points, old.Grade, old.Date));
            return true;
        }

        public int RankOf(string name)
        {
            int index = entries.FindIndex(e => string.Equals(e.Name, BoardEntry.Clean(name), StringComparison.OrdinalIgnoreCase));
            return index + 1;
        }

        /// <summary>name|points|grade|date, one entry per line.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var e in entries) sb.Append(e.Name).Append('|').Append(e.Points.ToString(CultureInfo.InvariantCulture)).Append('|').Append(e.Grade).Append('|').Append(e.Date).Append('\n');
            return sb.ToString();
        }

        public static Leaderboard Parse(string text)
        {
            var board = new Leaderboard();
            if (string.IsNullOrEmpty(text)) return board;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var parts = line.Split('|');
                if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int points)) continue;
                board.Submit(new BoardEntry(parts[0], points, parts.Length > 2 ? parts[2] : "", parts.Length > 3 ? parts[3] : ""));
            }
            return board;
        }

        /// <summary>Union of two boards: the higher score per name wins. Used to reconcile local and remote.</summary>
        public static Leaderboard Merge(Leaderboard a, Leaderboard b)
        {
            var merged = new Leaderboard();
            if (a != null) foreach (var e in a.entries) merged.Submit(e);
            if (b != null) foreach (var e in b.entries) merged.Submit(e);
            return merged;
        }
    }
}
