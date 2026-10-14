namespace HoldTheCircle
{
    /// <summary>
    /// Roguelike route node kinds. Selected from a forking map between waves.
    /// </summary>
    public enum MapNodeKind
    {
        Combat,    // standard wave
        Elite,     // boosted-stat single wave; guaranteed relic on clear
        Treasure,  // free relic, no fight
        Merchant,  // pick one of two relics (no cost in Phase 1)
        Event,     // random small buff (damage / fire rate / heal)
        Boss       // arena boss encounter; guaranteed legendary relic on clear
    }
}
