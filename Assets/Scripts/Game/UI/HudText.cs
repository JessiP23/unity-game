using System.Collections.Generic;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>Formats HUD strings from match state. Pure text; <see cref="PrototypeHud"/> decides layout.</summary>
    public static class HudText
    {
        private const string Accent = "#FFD54A";
        private static readonly System.Text.StringBuilder Builder = new System.Text.StringBuilder(512);

        public static string ZoneLabel(ZoneType? zone) => zone switch
        {
            ZoneType.EntranceExit => "Entrance",
            ZoneType.Checkout => "Checkout",
            ZoneType.Supermarket => "the aisles",
            ZoneType.Clothing => "Clothing",
            ZoneType.Electronics => "Electronics",
            ZoneType.Home => "Home",
            ZoneType.CustomerService => "Customer Service",
            ZoneType.Warehouse => "the warehouse",
            ZoneType.Employee => "the staff room",
            ZoneType.Security => "Security",
            ZoneType.Mezzanine => "Upstairs",
            _ => "the store"
        };

        public static string ItemLabel(string id) => id switch
        {
            "shirt" => "Shirt",
            "employee-key" => "Employee key",
            "debug-crate" => "Crate",
            _ => string.IsNullOrEmpty(id) ? "Item" : id.Replace('-', ' ')
        };

        public static string ReportLine(SuspiciousActivityEvent report)
        {
            string who = report.SourceId != null && report.SourceId.IndexOf("Employee", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? "A staff member" : "A shopper";
            return who + " reported movement near " + ZoneLabel(report.Zone);
        }

        public static string Clock(LocalMatchAuthority authority) => Clock(authority.Clock.Remaining);
        public static string Clock(double remaining)
        {
            int seconds = Mathf.CeilToInt((float)remaining);
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>Guard chip text for a detection state, in the player's words.</summary>
        public static string GuardLabel(DetectionSystem status, int threshold) => status.State switch
        {
            DetectionState.Green => status.AttentionLingering ? "LOOKING AWAY" : "UNSEEN",
            DetectionState.Orange => (status.DisplayDoubt ? "DOUBT  " : "STOP  ") + status.GraceRemaining.ToString("0.0") + "s",
            DetectionState.Red => "FREEZE",
            _ => "DISCOVERED"
        };

        /// <summary>Lines of "key\taction"; a blank line separates maps so the HUD can lay them side by side.</summary>
        public static string Help(bool expanded, params KeyCommandMap[] maps)
        {
            if (!expanded) return "<b><color=" + Accent + ">TAB</color></b>  menu   <color=" + Accent + ">H</color>  controls";
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

        /// <summary>Customer reactions as the player would read them: no numbers, just body language.</summary>
        public static string CrowdLabel(AwarenessState state) =>
            "<color=" + CrowdTint(state) + ">" + CrowdPlain(state) + "</color>";

        public static string CrowdPlain(AwarenessState state, bool visible = true) => state switch
        {
            AwarenessState.Observing => visible ? "WATCHING YOU" : "LOST SIGHT",
            AwarenessState.Suspicious => visible ? "SUSPICIOUS" : "REMEMBERS MOVEMENT",
            AwarenessState.Reporting => "CALLING GUARD",
            _ => "CALM"
        };

        public static string CrowdTint(AwarenessState state) => state switch
        {
            AwarenessState.Observing => "#FFD54A",
            AwarenessState.Suspicious => "#FF9F45",
            AwarenessState.Reporting => "#FF6B5E",
            _ => "#9FB3C8"
        };
        public static string DetectionColor(DetectionState state) => state switch
        {
            DetectionState.Green => "#7CE38B",
            DetectionState.Orange => "#FFB347",
            DetectionState.Red => "#FF6B5E",
            _ => "#FF3B3B"
        };
    }
}
