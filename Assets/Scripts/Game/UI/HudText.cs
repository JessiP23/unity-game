using System.Collections.Generic;
using System.Text;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Formats HUD strings from match state. Pure text; <see cref="PrototypeHud"/> decides layout.</summary>
    public static class HudText
    {
        private const string Accent = "#FFD54A";
        private static readonly StringBuilder Builder = new StringBuilder(512);

        public static string Header(LocalMatchAuthority authority)
        {
            Builder.Clear();
            int seconds = Mathf.CeilToInt((float)authority.Clock.Remaining);
            Builder.Append("<b>NIGHT SUPERMARKET</b>   ").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"));
            if (authority.Clock.Paused) Builder.Append("  <color=").Append(Accent).Append(">PAUSED</color>");
            if (authority.Flow.Phase != MatchPhase.Night)
                Builder.Append("  <color=").Append(Accent).Append('>').Append(authority.Flow.Phase.ToString().ToUpperInvariant()).Append("</color>");
            return Builder.ToString();
        }

        public static string GuardStatus(LocalMatchAuthority authority, GuardState state) =>
            Header(authority) + "\nYou are the <b>GUARD</b>   state " + state + "\n<size=16>Same eyes and ears as the AI guard.</size>";

        public static string MannequinStatus(LocalMatchAuthority authority, int playerNumber, PlayerRecord record,
            DetectionSystem detection, IReadOnlyList<MissionTracker> missions, PlayerInventory inventory)
        {
            string header = Header(authority);
            Builder.Clear();
            Builder.Append(header).Append('\n');
            Builder.Append("Mannequin ").Append(playerNumber).Append("   ").Append(record.State).Append('\n');
            if (detection != null)
                Builder.Append("Seen <color=").Append(DetectionColor(detection.State)).Append('>').Append(detection.State.ToString().ToUpperInvariant())
                       .Append("</color>   suspicion ").Append(detection.Suspicion.Value).Append('\n');
            if (missions != null)
                foreach (var mission in missions)
                {
                    Builder.Append(mission.Complete ? "<color=#7CE38B>[x]</color> " : mission.Failed ? "<color=#FF6B5E>[!]</color> " : "[ ] ");
                    Builder.Append(mission.Rule.Title).Append("  ").Append(mission.Progress).Append('/').Append(mission.Rule.Quantity).Append('\n');
                }
            bool carrying = false;
            if (inventory != null)
                foreach (var pair in inventory.Items.Snapshot())
                {
                    Builder.Append(carrying ? ", " : "Carrying ").Append(pair.Key).Append(" x").Append(pair.Value);
                    carrying = true;
                }
            if (carrying) Builder.Append('\n');
            Builder.Append("<size=16>Warehouse ").Append(authority.Warehouse.Count).Append("   escaped ").Append(authority.Escapes.Count)
                   .Append("   lights ").Append(authority.Lighting.Mode).Append("</size>");
            if (record.State == PlayerState.Surveillance && authority.TryReadSurveillance(record.Id, record.Id, out var view))
            {
                Builder.Append("\n<color=#7CE3B0>CAMERAS</color>  guard ").Append(view.GuardState).Append(" at ")
                       .Append(view.GuardPosition.X.ToString("0")).Append(", ").Append(view.GuardPosition.Z.ToString("0"));
                for (int i = 0; i < view.Players.Count; i++) Builder.Append("\nMannequin ").Append(i + 1).Append(' ').Append(view.Players[i].State);
            }
            return Builder.ToString();
        }

        /// <summary>Lines of "key\taction"; a blank line separates maps so the HUD can lay them side by side.</summary>
        public static string Help(bool expanded, params KeyCommandMap[] maps)
        {
            if (!expanded) return "<b><color=" + Accent + ">H</color></b>  controls";
            Builder.Clear();
            for (int m = 0; m < maps.Length; m++)
            {
                if (m > 0) Builder.Append('\n');
                Builder.Append("<b>").Append(maps[m].Title).Append("</b>\n");
                foreach (var entry in maps[m].Entries)
                    Builder.Append("<color=").Append(Accent).Append("><b>").Append(entry.Label).Append("</b></color>\t").Append(entry.Description).Append('\n');
            }
            if (Builder.Length > 0) Builder.Length--;
            return Builder.ToString();
        }

        public static string DetectionColor(DetectionState state) => state switch
        {
            DetectionState.Green => "#7CE38B",
            DetectionState.Orange => "#FFB347",
            DetectionState.Red => "#FF6B5E",
            _ => "#FF3B3B"
        };
    }
}
