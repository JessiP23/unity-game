using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace NightSupermarket.Core
{
    /// <summary>What a mannequin wears. Clothes are camouflage: in the department they fit, a still mannequin reads as part of the display.</summary>
    public enum Outfit { Plain, ShopperCoat, BoutiqueBlack, TechTee, Loungewear, StaffApron }

    /// <summary>
    /// Outfits, what they cost in gems, and where they buy doubt. The night's hidden gems are the only
    /// currency; an outfit is bought once and kept. Pure rules so the HUD, the detection system and
    /// the save file agree on one table.
    /// </summary>
    public static class Wardrobe
    {
        public static readonly Outfit[] All = { Outfit.Plain, Outfit.ShopperCoat, Outfit.BoutiqueBlack, Outfit.TechTee, Outfit.Loungewear, Outfit.StaffApron };

        public static int Cost(Outfit outfit) => outfit switch
        {
            Outfit.ShopperCoat => 3,
            Outfit.BoutiqueBlack => 3,
            Outfit.TechTee => 3,
            Outfit.Loungewear => 4,
            Outfit.StaffApron => 6,
            _ => 0
        };

        public static string Name(Outfit outfit) => outfit switch
        {
            Outfit.ShopperCoat => "Shopper's coat",
            Outfit.BoutiqueBlack => "Boutique black",
            Outfit.TechTee => "Tech tee",
            Outfit.Loungewear => "Loungewear",
            Outfit.StaffApron => "Staff apron",
            _ => "Plain"
        };

        public static string Describe(Outfit outfit) => outfit switch
        {
            Outfit.ShopperCoat => "blends in the aisles",
            Outfit.BoutiqueBlack => "blends in Clothing",
            Outfit.TechTee => "blends in Electronics",
            Outfit.Loungewear => "blends in Home and Upstairs",
            Outfit.StaffApron => "blends in Checkout, Service and every staff room",
            _ => "blends nowhere — your pose is all you have"
        };

        /// <summary>True when the outfit belongs in the zone: the guard grants the same extra doubt a fitting pose does.</summary>
        public static bool Fits(Outfit outfit, ZoneType? zone)
        {
            if (zone == null) return false;
            switch (outfit)
            {
                case Outfit.ShopperCoat: return zone == ZoneType.Supermarket;
                case Outfit.BoutiqueBlack: return zone == ZoneType.Clothing;
                case Outfit.TechTee: return zone == ZoneType.Electronics;
                case Outfit.Loungewear: return zone == ZoneType.Home || zone == ZoneType.Mezzanine;
                case Outfit.StaffApron:
                    return zone == ZoneType.Checkout || zone == ZoneType.CustomerService || zone == ZoneType.Warehouse || zone == ZoneType.Employee || zone == ZoneType.Security;
                default: return false;
            }
        }
    }

    /// <summary>Gems owned, outfits bought, outfit worn. One line in PlayerPrefs: tokens|worn|owned,owned,…</summary>
    public sealed class WardrobeState
    {
        private readonly HashSet<Outfit> owned = new HashSet<Outfit> { Outfit.Plain };
        public int Tokens { get; private set; }
        public Outfit Worn { get; private set; } = Outfit.Plain;
        public int GemsEver { get; private set; }
        public IReadOnlyCollection<Outfit> Owned => owned;

        public bool Owns(Outfit outfit) => owned.Contains(outfit);

        public void AddGem(int count = 1)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            Tokens += count; GemsEver += count;
        }

        /// <summary>Buys and wears the outfit. False when already owned or unaffordable.</summary>
        public bool TryBuy(Outfit outfit)
        {
            if (owned.Contains(outfit) || Tokens < Wardrobe.Cost(outfit)) return false;
            Tokens -= Wardrobe.Cost(outfit);
            owned.Add(outfit);
            Worn = outfit;
            return true;
        }

        public bool TryWear(Outfit outfit)
        {
            if (!owned.Contains(outfit)) return false;
            Worn = outfit;
            return true;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Tokens.ToString(CultureInfo.InvariantCulture)).Append('|').Append((int)Worn).Append('|');
            bool first = true;
            foreach (var outfit in Wardrobe.All)
            {
                if (!owned.Contains(outfit)) continue;
                if (!first) sb.Append(',');
                sb.Append((int)outfit); first = false;
            }
            sb.Append('|').Append(GemsEver.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static WardrobeState Parse(string text)
        {
            var state = new WardrobeState();
            if (string.IsNullOrEmpty(text)) return state;
            var parts = text.Split('|');
            if (parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tokens)) state.Tokens = Math.Max(0, tokens);
            if (parts.Length > 2)
                foreach (var raw in parts[2].Split(','))
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && Enum.IsDefined(typeof(Outfit), id)) state.owned.Add((Outfit)id);
            if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int worn) && Enum.IsDefined(typeof(Outfit), worn) && state.owned.Contains((Outfit)worn))
                state.Worn = (Outfit)worn;
            if (parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ever)) state.GemsEver = Math.Max(state.Tokens, ever);
            return state;
        }
    }

    /// <summary>
    /// Which hiding spots hold a gem tonight. The store offers a fixed list of spots (some reachable
    /// only while holding a pose); the shift seed picks a few so everyone on the same daily shift
    /// hunts the same gems. Gems only show after lights out, which is why they are worth the risk.
    /// </summary>
    public static class GemPlan
    {
        public const int PerNight = 3;

        /// <summary>Distinct spot indices, at least one of them from the pose-gated set when there is one.</summary>
        public static int[] Pick(int seed, int spots, int count, IReadOnlyList<int> poseGated = null)
        {
            if (spots <= 0 || count <= 0) return Array.Empty<int>();
            count = Math.Min(count, spots);
            var rng = new Random(seed * 7919 + 17);
            var chosen = new List<int>(count);
            if (poseGated != null && poseGated.Count > 0)
            {
                int gated = poseGated[rng.Next(poseGated.Count)];
                if (gated >= 0 && gated < spots) chosen.Add(gated);
            }
            while (chosen.Count < count)
            {
                int next = rng.Next(spots);
                if (!chosen.Contains(next)) chosen.Add(next);
            }
            chosen.Sort();
            return chosen.ToArray();
        }
    }
}
