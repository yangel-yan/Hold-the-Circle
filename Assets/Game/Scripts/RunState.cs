using System;
using System.Collections.Generic;

namespace HoldTheCircle
{
    /// <summary>
    /// Per-hand runtime stat block. The director creates one per hand caster
    /// and HandWaveCaster reads its composed multipliers each frame. Holds the
    /// chosen specialization plus how many of its tree nodes have been unlocked.
    /// </summary>
    public class HandSlot
    {
        public SpecializationSO Specialization { get; private set; }
        public int UnlockedNodeCount { get; private set; }

        // Final composed values; combat reads these directly.
        public float FireRateMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;
        public int ExtraProjectiles { get; set; }
        public float SpreadHalfAngleInDegrees { get; set; } = 8f;

        // Lance flavor: max enemies a single wave can pass through.
        public int PierceCount { get; set; }
        // Aegis flavor: 0..1 fraction of incoming player damage absorbed.
        public float DamageReductionPercent { get; set; }
        // Ricochet: how many times a wave can bounce off a WaveBlocker (real
        // walls / spatial mesh) before expiring. Capped at 3 in HandWaveCaster
        // so MR mode stays predictable.
        public int RicochetCount { get; set; }

        // Additive bonus to the config's base crit chance (0..1) and an
        // additive bonus to the crit damage multiplier. Relics / spec nodes
        // stack onto these so a crit build can become a clear playstyle.
        public float BonusCritChance { get; set; }
        public float BonusCritMultiplier { get; set; }

        public void Reset()
        {
            Specialization = null;
            UnlockedNodeCount = 0;
            FireRateMultiplier = 1f;
            DamageMultiplier = 1f;
            ExtraProjectiles = 0;
            SpreadHalfAngleInDegrees = 8f;
            PierceCount = 0;
            DamageReductionPercent = 0f;
            RicochetCount = 0;
            BonusCritChance = 0f;
            BonusCritMultiplier = 0f;
        }

        // Apply a specialization's static stat block onto this hand's current
        // values. Tree nodes unlock separately as the run progresses.
        public void ApplySpecialization(SpecializationSO spec)
        {
            if (spec == null) return;
            Specialization = spec;
            FireRateMultiplier *= spec.FireRateMultiplier;
            DamageMultiplier *= spec.DamageMultiplier;
            ExtraProjectiles += spec.BaseExtraProjectiles;
            PierceCount += spec.BasePierceCount;
            DamageReductionPercent = Combine(DamageReductionPercent, spec.BaseDamageReduction);
            RicochetCount += spec.BaseRicochetCount;
        }

        // Unlock the next node in the chosen tree, if any remain. Returns true
        // when a node was actually applied so the director can drive feedback.
        public bool UnlockNextNode()
        {
            if (Specialization == null) return false;
            if (UnlockedNodeCount >= Specialization.NodeCount) return false;

            SpecializationNode node = Specialization.Nodes[UnlockedNodeCount];
            ApplyNode(node);
            UnlockedNodeCount++;
            return true;
        }

        // Add the node's bonuses on top of whatever is currently composed.
        private void ApplyNode(SpecializationNode node)
        {
            DamageMultiplier += node.DamageBonus;
            FireRateMultiplier += node.FireRateBonus;
            ExtraProjectiles += node.ExtraProjectiles;
            PierceCount += node.ExtraPierce;
            DamageReductionPercent = Combine(DamageReductionPercent, node.ExtraDamageReduction);
            RicochetCount += node.ExtraRicochet;
        }

        // Multiplicative damage-reduction stacking: 1 - (1-a)*(1-b).
        // Caps near 0.95 so Aegis can't make the player invulnerable.
        private static float Combine(float current, float addition)
        {
            if (addition <= 0f) return current;
            float inverted = (1f - current) * (1f - addition);
            float result = 1f - inverted;
            return result > 0.95f ? 0.95f : result;
        }
    }

    /// <summary>
    /// Single source of truth for one in-progress run. Owns the event bus,
    /// per-hand slots, and the active relic list. Reset between runs so no
    /// state or hook subscriptions leak across the restart loop.
    /// </summary>
    public class RunState
    {
        public RunEventBus Events { get; } = new();

        // Optional player-health reference so heal/leech relics can affect HP
        // without poking at MonoBehaviour singletons.
        public PlayerHealth PlayerHealth { get; private set; }

        private readonly List<HandSlot> m_hands = new();
        private readonly List<RelicEffect> m_relics = new();

        // In-run currency. Earned from kills + clears, spent at merchants.
        // Gets cashed out into MetaProgression shards when a run ends.
        public int Shards { get; private set; }
        public event Action<int> ShardsChanged;

        // Arcade score: points per kill (weighted by archetype). Drives the
        // on-screen scoreboard and the end-of-run total. score, totalKills.
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public event Action<int, int> ScoreChanged;

        public IReadOnlyList<HandSlot> Hands => m_hands;
        public IReadOnlyList<RelicEffect> Relics => m_relics;

        public void BindPlayerHealth(PlayerHealth playerHealth)
        {
            PlayerHealth = playerHealth;
        }

        public void Initialize(int handCount)
        {
            if (m_hands.Count == handCount)
            {
                foreach (HandSlot hand in m_hands) hand.Reset();
                return;
            }

            m_hands.Clear();
            for (int i = 0; i < handCount; i++)
            {
                m_hands.Add(new HandSlot());
            }
        }

        public HandSlot GetHand(int index)
        {
            if (index < 0 || index >= m_hands.Count) return null;
            return m_hands[index];
        }

        public void SetSpecialization(int handIndex, SpecializationSO spec)
        {
            HandSlot hand = GetHand(handIndex);
            hand?.ApplySpecialization(spec);
        }

        // Unlock the next tree node on every hand at once. Used by the wave-
        // cleared hook so progression is visible without extra interaction.
        public void UnlockNextNodeForAllHands()
        {
            foreach (HandSlot hand in m_hands)
            {
                hand.UnlockNextNode();
            }
        }

        // Maximum damage reduction across all hands. Phase 1 picks one number
        // for the player rather than running per-side reduction calculations.
        public float GetMaxDamageReduction()
        {
            float max = 0f;
            foreach (HandSlot hand in m_hands)
            {
                if (hand.DamageReductionPercent > max) max = hand.DamageReductionPercent;
            }
            return max;
        }

        public void Reset()
        {
            for (int i = m_relics.Count - 1; i >= 0; i--)
            {
                m_relics[i].Detach(this);
            }

            m_relics.Clear();

            foreach (HandSlot hand in m_hands)
            {
                hand.Reset();
            }

            Shards = 0;
            ShardsChanged?.Invoke(Shards);

            Score = 0;
            Kills = 0;
            ScoreChanged?.Invoke(Score, Kills);
        }

        public void AddShards(int amount)
        {
            if (amount <= 0) return;
            Shards += amount;
            ShardsChanged?.Invoke(Shards);
        }

        // Award points + increment the kill counter, then broadcast so the HUD
        // scoreboard updates. Called once per enemy kill with the archetype's
        // weighted value.
        public void AddScore(int points)
        {
            if (points > 0) Score += points;
            Kills++;
            ScoreChanged?.Invoke(Score, Kills);
        }

        public bool TrySpendShards(int amount)
        {
            if (amount <= 0) return true;
            if (Shards < amount) return false;
            Shards -= amount;
            ShardsChanged?.Invoke(Shards);
            return true;
        }

        // Forfeit relics for shards (merchant "remove" service). Only fired
        // when the relic is actually present so the bus stays consistent.
        public bool RemoveRelic(RelicEffect effect)
        {
            if (effect == null) return false;
            int index = m_relics.IndexOf(effect);
            if (index < 0) return false;
            effect.Detach(this);
            m_relics.RemoveAt(index);
            return true;
        }

        public void AddFireRate(float amount)
        {
            foreach (HandSlot hand in m_hands) hand.FireRateMultiplier += amount;
        }

        public void AddDamage(float amount)
        {
            foreach (HandSlot hand in m_hands) hand.DamageMultiplier += amount;
        }

        public void AddProjectiles(int amount)
        {
            foreach (HandSlot hand in m_hands) hand.ExtraProjectiles += amount;
        }

        public void AddRelic(RelicSO so)
        {
            if (so == null) return;

            RelicEffect effect = so.CreateEffect();
            if (effect == null) return;

            m_relics.Add(effect);
            effect.Attach(this);
        }
    }
}
