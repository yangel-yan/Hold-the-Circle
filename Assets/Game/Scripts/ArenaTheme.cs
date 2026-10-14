using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Per-node room flavor: combat rooms keep a heavy ring of cover, elite
    /// rooms open up sightlines and add tall obelisks for vertical drama,
    /// treasure rooms are a quiet altar with sparse markers.
    /// </summary>
    public enum ArenaLayoutKind
    {
        Combat,
        Elite,
        Treasure,
        Boss
    }

    /// <summary>
    /// Visual + structural preset for a battle domain. Phase 1 ships one biome
    /// (FrostbiteCloister) but ArenaBuilder is keyed on this struct so future
    /// domains drop in without code changes.
    /// </summary>
    public struct ArenaTheme
    {
        public string Name;
        public Color PillarTint;
        public Color FloorRuneTint;
        public Color AccentTint;
        public float PillarEmissionIntensity;
        public float RuneRadiusInMeters;

        public static ArenaTheme FrostbiteCloister => new ArenaTheme
        {
            Name = "Frostbite Cloister",
            PillarTint = new Color(0.18f, 0.5f, 0.78f, 1f),
            FloorRuneTint = new Color(0.55f, 0.85f, 1f, 1f),
            AccentTint = new Color(0.85f, 0.95f, 1f, 1f),
            PillarEmissionIntensity = 1.6f,
            RuneRadiusInMeters = 1.4f
        };
    }
}
