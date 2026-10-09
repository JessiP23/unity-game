using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>Semantic store areas. Rules ask "is this zone allowed", never "is this the warehouse".</summary>
    public enum ZoneType { EntranceExit, Checkout, Supermarket, Clothing, Home, Electronics, CustomerService, Warehouse, Employee, Security, Mezzanine }

    /// <summary>Which zones an NPC role may walk into.</summary>
    public sealed class ZoneAccess
    {
        private readonly HashSet<ZoneType> allowed;
        public ZoneAccess(IEnumerable<ZoneType> zones)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            allowed = new HashSet<ZoneType>(zones);
        }
        public bool Allows(ZoneType zone) => allowed.Contains(zone);
        public IEnumerable<ZoneType> Allowed => allowed;

        /// <summary>Zones staff keep to themselves. Shoppers must not enter them.</summary>
        public static bool IsRestricted(ZoneType zone) => zone == ZoneType.Warehouse || zone == ZoneType.Employee || zone == ZoneType.Security;

        public static ZoneAccess Customer { get; } = new ZoneAccess(new[]
        {
            ZoneType.EntranceExit, ZoneType.Checkout, ZoneType.Supermarket, ZoneType.Clothing,
            ZoneType.Home, ZoneType.Electronics, ZoneType.CustomerService, ZoneType.Mezzanine
        });
        public static ZoneAccess Employee { get; } = new ZoneAccess((ZoneType[])Enum.GetValues(typeof(ZoneType)));
    }
}
