using NightSupermarket.Game;
using UnityEditor;
using UnityEngine;
namespace NightSupermarket.EditorTools
{
    /// <summary>
    /// Small window to point the game at a shared leaderboard server (Tools/leaderboard_server.py)
    /// and to rename or reset the local player. Settings live in PlayerPrefs, same as the game reads.
    /// </summary>
    public sealed class LeaderboardSetup : EditorWindow
    {
        private string url, playerName;

        [MenuItem("Night Supermarket/Leaderboard/Settings…")]
        private static void Open()
        {
            var window = GetWindow<LeaderboardSetup>("Leaderboard");
            window.minSize = new Vector2(420, 190);
        }

        private void OnEnable()
        {
            url = PlayerPrefs.GetString(LeaderboardStore.UrlKey, "");
            playerName = PlayerPrefs.GetString(LeaderboardStore.NameKey, "");
        }

        private void OnGUI()
        {
            GUILayout.Label("Shared server", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Run  python3 Tools/leaderboard_server.py  on any machine your friends can reach, then paste its address here, e.g. http://192.168.1.20:8787. Leave empty to keep scores on this computer only.", MessageType.Info);
            url = EditorGUILayout.TextField("Server URL", url);
            GUILayout.Space(8);
            GUILayout.Label("Player", EditorStyles.boldLabel);
            playerName = EditorGUILayout.TextField("Name", playerName);
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    PlayerPrefs.SetString(LeaderboardStore.UrlKey, (url ?? "").Trim().TrimEnd('/'));
                    PlayerPrefs.SetString(LeaderboardStore.NameKey, NightSupermarket.Core.BoardEntry.Clean(playerName));
                    PlayerPrefs.Save();
                    ShowNotification(new GUIContent("Saved"));
                }
                if (GUILayout.Button("Clear local boards") && EditorUtility.DisplayDialog("Clear local boards", "Delete every locally stored leaderboard (ns-board-*)? Career and best scores stay.", "Clear", "Cancel"))
                {
                    for (int shift = 0; shift < 2000; shift++) PlayerPrefs.DeleteKey("ns-board-" + shift);
                    PlayerPrefs.Save();
                    ShowNotification(new GUIContent("Cleared"));
                }
            }
        }
    }
}
