using NUnit.Framework;
using NightSupermarket.Core;
namespace NightSupermarket.Tests
{
    public sealed class LeaderboardTests
    {
        [Test] public void KeepsOneBestPerNameSortedAndCapped()
        {
            var board = new Leaderboard();
            Assert.That(board.Submit(new BoardEntry("jessi", 1200, "B", "2026-10-08")), Is.EqualTo(1));
            Assert.That(board.Submit(new BoardEntry("sam", 1800, "A", "2026-10-08")), Is.EqualTo(1));
            Assert.That(board.Submit(new BoardEntry("Jessi", 900, "C", "2026-10-08")), Is.EqualTo(2), "a worse night keeps the old best");
            Assert.That(board.Submit(new BoardEntry("jessi", 2600, "S", "2026-10-08")), Is.EqualTo(1));
            Assert.That(board.Entries.Count, Is.EqualTo(2));
            for (int i = 0; i < 20; i++) board.Submit(new BoardEntry("bot" + i, 3000 + i, "S", ""));
            Assert.That(board.Entries.Count, Is.EqualTo(Leaderboard.Capacity));
            Assert.That(board.RankOf("jessi"), Is.EqualTo(0), "pushed off the board");
            Assert.That(board.Entries[0].Points, Is.EqualTo(3019));
        }
        [Test] public void RoundTripsThroughTextAndMerges()
        {
            var board = new Leaderboard();
            board.Submit(new BoardEntry("a|b", 500, "C", "2026-10-08"));
            board.Submit(new BoardEntry("sam", 1800, "A", "2026-10-08"));
            var parsed = Leaderboard.Parse(board.Serialize());
            Assert.That(parsed.Entries.Count, Is.EqualTo(2));
            Assert.That(parsed.Entries[0].Name, Is.EqualTo("sam"));
            Assert.That(parsed.Entries[1].Name, Is.EqualTo("ab"), "pipes are not allowed in names");
            var remote = Leaderboard.Parse("sam|2100|S|2026-10-09\nkai|700|C|\n");
            var merged = Leaderboard.Merge(parsed, remote);
            Assert.That(merged.Entries.Count, Is.EqualTo(3));
            Assert.That(merged.Entries[0].Points, Is.EqualTo(2100));
            Assert.That(Leaderboard.Parse("garbage\n\n|x|").Entries.Count, Is.EqualTo(0));
        }
        [Test] public void RenameMovesTheEntry()
        {
            var board = new Leaderboard();
            board.Submit(new BoardEntry("mannequin", 1500, "B", ""));
            board.Submit(new BoardEntry("sam", 1000, "C", ""));
            Assert.That(board.Rename("mannequin", "Jessi"), Is.True);
            Assert.That(board.Rename("nobody", "x"), Is.False);
            Assert.That(board.Entries[0].Name, Is.EqualTo("Jessi"));
            Assert.That(board.RankOf("mannequin"), Is.EqualTo(0));
            Assert.That(board.Entries.Count, Is.EqualTo(2));
        }
        [Test] public void NamesAreCleaned()
        {
            Assert.That(BoardEntry.Clean("  Jessi Pavia!!  "), Is.EqualTo("Jessi Pavia"));
            Assert.That(BoardEntry.Clean(""), Is.EqualTo("mannequin"));
            Assert.That(BoardEntry.Clean("abcdefghijklmnopqrstuvwxyz").Length, Is.EqualTo(16));
        }
    }
}
