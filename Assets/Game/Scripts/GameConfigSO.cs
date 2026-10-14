using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Data-driven configuration for the whole game loop.
    /// Stored under Resources so it can be loaded without scene references.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Hold The Circle/Game Config", order = 0)]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Player")]
        [Tooltip("Player starting / max hit points")]
        [SerializeField] private float m_playerMaxHealth = 100f;

        [Header("Hand Wave (auto fire)")]
        [Tooltip("Seconds between auto-fired energy waves per hand")]
        [SerializeField] private float m_waveFireIntervalInSeconds = 0.5f;
        [Tooltip("Travel speed of an energy wave in meters per second")]
        [SerializeField] private float m_waveSpeedInMetersPerSecond = 12f;
        [Tooltip("Damage dealt by a single energy wave hit")]
        [SerializeField] private float m_waveDamage = 1f;
        [Tooltip("Seconds an energy wave lives before being recycled")]
        [SerializeField] private float m_waveLifetimeInSeconds = 3f;

        [Header("Gesture Charge Attack")]
        [Tooltip("Damage multiplier for a minimum-charge throw (just past the fire threshold)")]
        [SerializeField] private float m_minChargeDamageMultiplier = 0.8f;
        [Tooltip("Damage multiplier for a full-charge throw (tight, held fist)")]
        [SerializeField] private float m_maxChargeDamageMultiplier = 3.5f;
        [Tooltip("At/above this charge (0..1) the throw fans out extra waves for an AOE blast")]
        [SerializeField] private float m_fullChargeSpreadThreshold = 0.85f;
        [Tooltip("Extra waves added on each side when a throw is at/above the full-charge threshold")]
        [SerializeField] private int m_fullChargeBonusWaves = 2;

        [Header("Critical Hits")]
        [Tooltip("Base chance (0..1) that a wave deals a critical hit")]
        [SerializeField] private float m_baseCritChance = 0.12f;
        [Tooltip("Damage multiplier applied on a critical hit")]
        [SerializeField] private float m_critDamageMultiplier = 2.5f;

        [Header("Enemy (Orbiter)")]
        [Tooltip("Base hit points of an enemy on wave 1")]
        [SerializeField] private float m_enemyBaseHealth = 3f;
        [Tooltip("Extra hit points added per wave index")]
        [SerializeField] private float m_enemyHealthPerWave = 1f;
        [Tooltip("Orbit angular speed around the player in degrees per second")]
        [SerializeField] private float m_enemyOrbitSpeedInDegrees = 40f;
        [Tooltip("Preferred orbit radius around the player in meters")]
        [SerializeField] private float m_enemyOrbitRadiusInMeters = 4f;
        [Tooltip("Speed when lunging toward the player in meters per second")]
        [SerializeField] private float m_enemyLungeSpeedInMetersPerSecond = 2.5f;
        [Tooltip("Distance at which the enemy can attack the player in meters")]
        [SerializeField] private float m_enemyAttackRangeInMeters = 0.8f;
        [Tooltip("Damage dealt to the player per enemy attack")]
        [SerializeField] private float m_enemyAttackDamage = 10f;
        [Tooltip("Seconds between enemy attacks")]
        [SerializeField] private float m_enemyAttackIntervalInSeconds = 1.5f;

        [Header("Waves")]
        [Tooltip("Enemy count on wave 1 (kept low so the run opens calm)")]
        [SerializeField] private int m_baseEnemyCount = 3;
        [Tooltip("Linear growth in enemy count per wave")]
        [SerializeField] private float m_enemyCountPerWave = 1.5f;
        [Tooltip("Quadratic growth term: adds wave^2 * this, so late waves swarm")]
        [SerializeField] private float m_enemyCountQuadratic = 0.35f;
        [Tooltip("Hard cap on enemies spawned in a single wave (performance / readability)")]
        [SerializeField] private int m_maxEnemiesPerWave = 60;
        [Tooltip("Spawn radius around the player in meters")]
        [SerializeField] private float m_spawnRadiusInMeters = 5f;

        public float PlayerMaxHealth => m_playerMaxHealth;

        public float WaveFireIntervalInSeconds => m_waveFireIntervalInSeconds;
        public float WaveSpeedInMetersPerSecond => m_waveSpeedInMetersPerSecond;
        public float WaveDamage => m_waveDamage;
        public float WaveLifetimeInSeconds => m_waveLifetimeInSeconds;

        public float MinChargeDamageMultiplier => m_minChargeDamageMultiplier;
        public float MaxChargeDamageMultiplier => m_maxChargeDamageMultiplier;
        public float FullChargeSpreadThreshold => m_fullChargeSpreadThreshold;
        public int FullChargeBonusWaves => m_fullChargeBonusWaves;

        public float BaseCritChance => m_baseCritChance;
        public float CritDamageMultiplier => m_critDamageMultiplier;

        public float EnemyBaseHealth => m_enemyBaseHealth;
        public float EnemyHealthPerWave => m_enemyHealthPerWave;
        public float EnemyOrbitSpeedInDegrees => m_enemyOrbitSpeedInDegrees;
        public float EnemyOrbitRadiusInMeters => m_enemyOrbitRadiusInMeters;
        public float EnemyLungeSpeedInMetersPerSecond => m_enemyLungeSpeedInMetersPerSecond;
        public float EnemyAttackRangeInMeters => m_enemyAttackRangeInMeters;
        public float EnemyAttackDamage => m_enemyAttackDamage;
        public float EnemyAttackIntervalInSeconds => m_enemyAttackIntervalInSeconds;

        public int BaseEnemyCount => m_baseEnemyCount;
        public int MaxEnemiesPerWave => m_maxEnemiesPerWave;
        public float SpawnRadiusInMeters => m_spawnRadiusInMeters;

        // Enemy hit points for a given 1-based wave index.
        public float GetEnemyHealthForWave(int waveIndex)
        {
            return m_enemyBaseHealth + m_enemyHealthPerWave * (waveIndex - 1);
        }

        // Enemy count for a given 1-based wave index. Linear + quadratic growth
        // so the run opens calm and snowballs into a screen-filling swarm the
        // player gets to mow down — the roguelike power-fantasy curve. Capped to
        // keep performance and on-screen readability sane.
        public int GetEnemyCountForWave(int waveIndex)
        {
            int n = waveIndex - 1; // 0-based so wave 1 == base count
            float count = m_baseEnemyCount
                + m_enemyCountPerWave * n
                + m_enemyCountQuadratic * n * n;
            return Mathf.Min(m_maxEnemiesPerWave, Mathf.RoundToInt(count));
        }
    }
}
