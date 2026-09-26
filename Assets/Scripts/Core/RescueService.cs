namespace NightSupermarket.Core
{
    /// <summary>Releases a warehoused mannequin back to normal play under configurable rules.</summary>
    public static class RescueService
    {
        public static bool TryRescue(GameRules rules, MatchPhase phase, PlayerRecord rescuer, PlayerRecord captive, WarehouseRoster warehouse)
        {
            if (rules == null || phase != MatchPhase.Night || rescuer == null || captive == null || warehouse == null) return false;
            if (rescuer.Role != PlayerRole.Mannequin || captive.Role != PlayerRole.Mannequin) return false;
            if (captive.State != PlayerState.Captured && captive.State != PlayerState.Surveillance) return false;
            if (!warehouse.Contains(captive.Id)) return false;
            bool self = rescuer.Id == captive.Id;
            if (self)
            {
                if (!rules.AllowSelfRescue) return false;
            }
            else if (!rescuer.Free) return false;
            if (!warehouse.Release(captive.Id)) return false;
            captive.SetState(PlayerState.Normal);
            return true;
        }
    }
}
