namespace NightSupermarket.Core
{
    /// <summary>Moves a discovered mannequin into the warehouse without removing the player.</summary>
    public static class CaptureService
    {
        public static bool TryCapture(MatchPhase phase, PlayerRecord player, WarehouseRoster warehouse)
        {
            if (phase != MatchPhase.Night || player == null || warehouse == null) return false;
            if (player.Role != PlayerRole.Mannequin || !player.Free) return false;
            if (!warehouse.Admit(player.Id)) return false;
            player.SetState(PlayerState.Captured);
            return true;
        }
    }
}
