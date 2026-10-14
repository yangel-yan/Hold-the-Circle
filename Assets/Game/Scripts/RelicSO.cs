using UnityEngine;

namespace HoldTheCircle
{
    public enum RelicRarity
    {
        Common,
        Rare,
        Legendary
    }

    public enum RelicKind
    {
        // Stat-add (immediate, applied once on pickup)
        FlatDamage,
        FlatFireRate,
        ExtraProjectile,

        // Event-driven (subscribe on attach)
        FireRateOnKill,         // every kill: +Amount fire rate
        DamageOnKill,           // every kill: +Amount damage
        HealEveryNKills,        // every SecondaryAmount kills: heal Amount HP
        FireRateOnHurt,         // every time the player is hit: +Amount fire rate
        DamageOnWaveCleared,    // every wave cleared: +Amount damage
        ProjectileEveryNKills,  // every SecondaryAmount kills: +1 projectile
        LowHpDamageOnce         // first time HP drops below SecondaryAmount fraction: +Amount damage (once)
    }

    /// <summary>
    /// Authorable relic definition. The runtime effect is built fresh per run
    /// (CreateEffect) so we never mutate the asset and can hold multiple
    /// instances of the same relic across runs.
    /// </summary>
    [CreateAssetMenu(fileName = "Relic", menuName = "Hold The Circle/Relic", order = 11)]
    public class RelicSO : ScriptableObject
    {
        [Tooltip("Display name shown to the player")]
        [SerializeField] private string m_displayName;
        [Tooltip("Short flavor / mechanic description")]
        [TextArea]
        [SerializeField] private string m_description;
        [SerializeField] private RelicRarity m_rarity = RelicRarity.Common;
        [Tooltip("Glow tint for the relic shrine and HUD entry")]
        [SerializeField] private Color m_tint = new Color(0.95f, 0.85f, 0.4f, 1f);

        [Header("Effect")]
        [SerializeField] private RelicKind m_kind = RelicKind.FlatDamage;
        [Tooltip("Primary magnitude (damage/fire-rate add, heal amount, projectile count, etc.)")]
        [SerializeField] private float m_amount = 0.25f;
        [Tooltip("Secondary number used by some kinds (e.g. kills required, HP threshold fraction)")]
        [SerializeField] private float m_secondaryAmount;

        public string DisplayName => m_displayName;
        public string Description => m_description;
        public RelicRarity Rarity => m_rarity;
        public Color Tint => m_tint;
        public RelicKind Kind => m_kind;
        public float Amount => m_amount;
        public float SecondaryAmount => m_secondaryAmount;

        // Build a fresh runtime effect for the active run.
        public RelicEffect CreateEffect()
        {
            switch (m_kind)
            {
                case RelicKind.FireRateOnKill:
                    return new FireRateOnKillRelicEffect(this);
                case RelicKind.DamageOnKill:
                    return new DamageOnKillRelicEffect(this);
                case RelicKind.HealEveryNKills:
                    return new HealEveryNKillsRelicEffect(this);
                case RelicKind.FireRateOnHurt:
                    return new FireRateOnHurtRelicEffect(this);
                case RelicKind.DamageOnWaveCleared:
                    return new DamageOnWaveClearedRelicEffect(this);
                case RelicKind.ProjectileEveryNKills:
                    return new ProjectileEveryNKillsRelicEffect(this);
                case RelicKind.LowHpDamageOnce:
                    return new LowHpDamageOnceRelicEffect(this);
                default:
                    return new SimpleStatRelicEffect(this);
            }
        }
    }
}
