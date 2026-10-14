using UnityEngine;

namespace HoldTheCircle
{
    public enum EnemyArchetype
    {
        Drifter, // baseline wanderer
        Charger, // fast, fragile, presses in close
        Tank,    // slow, bulky, high health
        Weaver   // ranged caster: stays at range, throws projectiles
    }

    /// <summary>
    /// Static stat multipliers and visual tint for each enemy archetype.
    /// Applied on top of the wave-scaled base values from GameConfig so the
    /// existing data-driven tuning is preserved.
    /// </summary>
    public static class EnemyArchetypes
    {
        public struct Profile
        {
            public float HealthMultiplier;
            public float SpeedMultiplier;
            public float ScaleMultiplier;
            public Color Tint;
            // Weaver-only: positive value enables projectile casting at this
            // preferred radius (in meters). Other archetypes ignore this.
            public float PreferredRangeInMeters;
            // Points awarded to the player's score when this archetype is killed.
            // Tougher / more annoying types are worth more.
            public int ScoreValue;
        }

        public static Profile Get(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Charger:
                    return new Profile
                    {
                        HealthMultiplier = 0.5f,
                        SpeedMultiplier = 1.7f,
                        ScaleMultiplier = 0.8f,
                        Tint = new Color(1f, 0.55f, 0.15f, 1f), // hot orange
                        ScoreValue = 15 // fast + annoying, worth more
                    };
                case EnemyArchetype.Tank:
                    return new Profile
                    {
                        HealthMultiplier = 2.8f,
                        SpeedMultiplier = 0.55f,
                        ScaleMultiplier = 1.6f,
                        Tint = new Color(0.18f, 0.68f, 0.86f, 1f), // blocky blue tank
                        ScoreValue = 25 // tanky, takes effort to drop
                    };
                case EnemyArchetype.Weaver:
                    return new Profile
                    {
                        HealthMultiplier = 0.9f,
                        SpeedMultiplier = 0.7f,
                        ScaleMultiplier = 0.95f,
                        // Bright shell-caster blue for the voxel turtle visual.
                        Tint = new Color(0.28f, 0.83f, 1f, 1f),
                        PreferredRangeInMeters = 5.5f,
                        ScoreValue = 30 // ranged threat, highest priority to kill
                    };
                default:
                    return new Profile
                    {
                        HealthMultiplier = 1f,
                        SpeedMultiplier = 1f,
                        ScaleMultiplier = 1f,
                        Tint = new Color(0.33f, 0.92f, 0.62f, 1f), // blocky green drifter
                        ScoreValue = 10 // baseline grunt
                    };
            }
        }

        // Convenience: points awarded for killing the given archetype.
        public static int GetScore(EnemyArchetype archetype)
        {
            return Get(archetype).ScoreValue;
        }
    }
}
