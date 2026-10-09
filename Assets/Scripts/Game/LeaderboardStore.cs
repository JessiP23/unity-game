using System;
using System.Collections;
using System.Text;
using NightSupermarket.Core;
using UnityEngine;
using UnityEngine.Networking;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Keeps a per-shift <see cref="Leaderboard"/> in PlayerPrefs and, when a server URL is set, mirrors
    /// it there so friends on the same shift can compare. The server speaks plain text: GET
    /// {url}/shift/{n} returns board lines, POST {url}/shift/{n} with one line adds a score and returns
    /// the merged board. Tools/leaderboard_server.py is a 60-line server that does exactly this.
    /// Offline, everything still works locally; the remote is best effort and never blocks the game.
    /// </summary>
    public sealed class LeaderboardStore : MonoBehaviour
    {
        public const string UrlKey = "ns-board-url", NameKey = "ns-name";
        public string PlayerName { get; private set; }
        public string ServerUrl { get; private set; }
        public bool Online => !string.IsNullOrEmpty(ServerUrl);
        public string Status { get; private set; } = "";
        public event Action<int, Leaderboard> Changed;

        private void Awake()
        {
            PlayerName = BoardEntry.Clean(PlayerPrefs.GetString(NameKey, DefaultName()));
            ServerUrl = PlayerPrefs.GetString(UrlKey, "").TrimEnd('/');
        }

        private static string DefaultName()
        {
            try { return string.IsNullOrWhiteSpace(Environment.UserName) ? "mannequin" : Environment.UserName; }
            catch { return "mannequin"; }
        }

        /// <summary>Changes the player's name and carries tonight's entry over to it, locally and on the server.</summary>
        public void SetName(string name, int shift = -1)
        {
            string previous = PlayerName;
            PlayerName = BoardEntry.Clean(name);
            PlayerPrefs.SetString(NameKey, PlayerName);
            if (shift >= 0 && previous != PlayerName)
            {
                var board = Local(shift);
                if (board.Rename(previous, PlayerName))
                {
                    SaveLocal(shift, board);
                    Changed?.Invoke(shift, board);
                    int index = board.RankOf(PlayerName) - 1;
                    if (Online && index >= 0) StartCoroutine(Sync(shift, board.Entries[index]));
                }
            }
            PlayerPrefs.Save();
        }

        public void SetServer(string url)
        {
            ServerUrl = (url ?? "").Trim().TrimEnd('/');
            PlayerPrefs.SetString(UrlKey, ServerUrl);
            PlayerPrefs.Save();
        }

        public Leaderboard Local(int shift) => Leaderboard.Parse(PlayerPrefs.GetString("ns-board-" + shift, ""));

        private void SaveLocal(int shift, Leaderboard board)
        {
            PlayerPrefs.SetString("ns-board-" + shift, board.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Records a finished night locally at once, then mirrors it to the server if one is set.</summary>
        public int Record(int shift, int points, string grade)
        {
            var entry = new BoardEntry(PlayerName, points, grade, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            var board = Local(shift);
            int rank = board.Submit(entry);
            SaveLocal(shift, board);
            Changed?.Invoke(shift, board);
            if (Online) StartCoroutine(Sync(shift, entry));
            else Status = "local only — set a server to compare with friends";
            return rank;
        }

        /// <summary>Pulls the server board for a shift (for the start toast); merges into local.</summary>
        public void Refresh(int shift)
        {
            if (Online) StartCoroutine(Sync(shift, null));
        }

        private IEnumerator Sync(int shift, BoardEntry? entry)
        {
            string url = ServerUrl + "/shift/" + shift;
            UnityWebRequest request;
            if (entry.HasValue)
            {
                var line = Encoding.UTF8.GetBytes(new Leaderboard { }.SubmitAndSerialize(entry.Value));
                request = new UnityWebRequest(url, "POST") { uploadHandler = new UploadHandlerRaw(line), downloadHandler = new DownloadHandlerBuffer() };
                request.SetRequestHeader("Content-Type", "text/plain");
            }
            else request = UnityWebRequest.Get(url);
            request.timeout = 6;
            Status = "syncing…";
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Status = "server unreachable (" + request.error + ") — scores kept locally";
                yield break;
            }
            var remote = Leaderboard.Parse(request.downloadHandler.text);
            var merged = Leaderboard.Merge(Local(shift), remote);
            SaveLocal(shift, merged);
            Status = "shared board · " + merged.Entries.Count + (merged.Entries.Count == 1 ? " player" : " players");
            Changed?.Invoke(shift, merged);
        }
    }

    internal static class LeaderboardExtensions
    {
        /// <summary>One submitted entry as the line the server expects.</summary>
        public static string SubmitAndSerialize(this Leaderboard board, BoardEntry entry)
        {
            board.Submit(entry);
            return board.Serialize();
        }
    }
}
