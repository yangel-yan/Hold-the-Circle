using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    public enum SpecializationBranch
    {
        Lance, // piercing, single-target burst
        Storm, // spread, area pressure
        Aegis  // control, summon, defense
    }

    /// <summary>
    /// One unlockable node along a specialization tree. Authored as plain data
    /// so the SO inspector can edit three nodes per specialization without
    /// extra asset creation.
    /// </summary>
    [Serializable]
    public struct SpecializationNode
    {
        [Tooltip("Designer label for the node (e.g. Pierce / Charge / Overload)")]
        public string Title;
        [Tooltip("Additive damage multiplier bonus added on unlock")]
        public float DamageBonus;
        [Tooltip("Additive fire-rate multiplier bonus added on unlock")]
        public float FireRateBonus;
        [Tooltip("Additional projectiles added on unlock")]
        public int ExtraProjectiles;
        [Tooltip("Additional pierce count added on unlock (Lance flavor)")]
        public int ExtraPierce;
        [Tooltip("Additional damage reduction added on unlock, 0..1 (Aegis flavor)")]
        public float ExtraDamageReduction;
        [Tooltip("Additional ricochet count added on unlock (waves bounce off real-world walls)")]
        public int ExtraRicochet;
    }

    /// <summary>
    /// Authorable definition of a hand specialization. Holds the static stat
    /// block applied on equip plus an ordered list of three nodes that unlock
    /// across a run as the player clears waves.
    /// </summary>
    [CreateAssetMenu(fileName = "Specialization", menuName = "Hold The Circle/Specialization", order = 10)]
    public class SpecializationSO : ScriptableObject
    {
        [Tooltip("Display name shown in the selection orb")]
        [SerializeField] private string m_displayName;
        [Tooltip("Short flavor / mechanic description")]
        [TextArea]
        [SerializeField] private string m_description;
        [Tooltip("Primary playstyle branch")]
        [SerializeField] private SpecializationBranch m_branch;
        [Tooltip("Glow tint for the selection orb and FX")]
        [SerializeField] private Color m_tint = Color.cyan;

        [Header("Base Stat Block (applied on equip)")]
        [Tooltip("Multiplier applied to wave fire rate when this branch is equipped")]
        [SerializeField] private float m_fireRateMultiplier = 1f;
        [Tooltip("Multiplier applied to wave damage when this branch is equipped")]
        [SerializeField] private float m_damageMultiplier = 1f;
        [Tooltip("Extra projectiles added on equip")]
        [SerializeField] private int m_baseExtraProjectiles;
        [Tooltip("How many extra enemies a wave can pierce (Lance flavor)")]
        [SerializeField] private int m_basePierceCount;
        [Tooltip("Damage reduction granted to the player on equip, 0..1 (Aegis flavor)")]
        [SerializeField, Range(0f, 0.9f)] private float m_baseDamageReduction;
        [Tooltip("Base ricochet count (waves bounce off real-world walls). 1-2 recommended.")]
        [SerializeField, Range(0, 3)] private int m_baseRicochetCount;

        [Header("Tree (exactly 3 nodes recommended)")]
        [SerializeField] private List<SpecializationNode> m_nodes = new();

        public string DisplayName => m_displayName;
        public string Description => m_description;
        public SpecializationBranch Branch => m_branch;
        public Color Tint => m_tint;
        public float FireRateMultiplier => m_fireRateMultiplier;
        public float DamageMultiplier => m_damageMultiplier;
        public int BaseExtraProjectiles => m_baseExtraProjectiles;
        public int BasePierceCount => m_basePierceCount;
        public float BaseDamageReduction => m_baseDamageReduction;
        public int BaseRicochetCount => m_baseRicochetCount;
        public IReadOnlyList<SpecializationNode> Nodes => m_nodes;
        public int NodeCount => m_nodes.Count;
    }
}
