using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Manages the relic offering pool and presents three random unclaimed
    /// relics to the player at run-defined milestones. Pulls SOs from a pool
    /// configured on GameDirector, removes ones the player already owns, and
    /// raises RelicPicked once the choice has been added to RunState.
    /// </summary>
    public class RelicSystem : MonoBehaviour
    {
        private const int k_choiceCount = 3;
        private const float k_distanceInMeters = 0.95f;
        private const float k_heightInMeters = 0f;
        private const float k_spreadInMeters = 0.34f;

        private RunState m_runState;
        private Transform m_player;
        private Transform[] m_hands;
        private List<RelicSO> m_pool;

        private readonly List<RelicShrine> m_activeShrines = new(k_choiceCount);
        private readonly HashSet<RelicSO> m_claimed = new();
        private readonly List<RelicSO> m_workingPool = new();

        // Raised once the player has touched one of the offered shrines.
        public event Action RelicPicked;

        public void Initialize(RunState runState, Transform player, Transform[] hands, List<RelicSO> pool)
        {
            m_runState = runState;
            m_player = player;
            m_hands = hands;
            m_pool = pool;
        }

        public bool HasUsableData()
        {
            if (m_pool == null || m_pool.Count == 0) return false;
            foreach (RelicSO so in m_pool)
            {
                if (so == null) return false;
            }
            return true;
        }

        // Reset claim state for a fresh run so previously taken relics are
        // eligible again.
        public void ResetForNewRun()
        {
            m_claimed.Clear();
            ClearShrines();
        }

        // Pick three random unclaimed relics and float them in front of the
        // player. If fewer than three remain, presents whatever is left.
        public void PresentChoices()
        {
            PresentInternal(k_choiceCount);
        }

        // Curated vertical-slice offer: guarantee visibly different categories
        // and exclude rewards that only trigger after another cleared wave.
        public void PresentDemoChoices()
        {
            ClearShrines();
            BuildWorkingPool();
            m_workingPool.RemoveAll(
                so => so.Kind == RelicKind.DamageOnWaveCleared);

            var choices = new List<RelicSO>(k_choiceCount);
            AddFirstMatching(
                choices,
                RelicKind.ExtraProjectile,
                RelicKind.ProjectileEveryNKills);
            AddFirstMatching(
                choices,
                RelicKind.DamageOnKill,
                RelicKind.FlatDamage);
            AddFirstMatching(
                choices,
                RelicKind.HealEveryNKills,
                RelicKind.LowHpDamageOnce,
                RelicKind.FlatFireRate,
                RelicKind.FireRateOnKill);

            while (choices.Count < k_choiceCount && m_workingPool.Count > 0)
            {
                RelicSO chosen = WeightedPick(m_workingPool);
                if (chosen == null) break;
                m_workingPool.Remove(chosen);
                choices.Add(chosen);
            }

            SpawnChoices(choices);
        }

        // Treasure node: hand the player a single guaranteed relic.
        public void PresentSingleChoice()
        {
            PresentInternal(1);
        }

        // Merchant node: two relics, both biased rare.
        public void PresentMerchantChoices()
        {
            PresentInternal(2);
        }

        // Award one rarity-weighted relic without spawning shrines (used for
        // Elite-clear bonuses). Returns silently if nothing is left.
        public void AutoGrantOne()
        {
            BuildWorkingPool();
            if (m_workingPool.Count == 0) return;
            RelicSO chosen = WeightedPick(m_workingPool);
            if (chosen == null) return;
            m_workingPool.Remove(chosen);
            m_claimed.Add(chosen);
            m_runState?.AddRelic(chosen);
        }

        // Boss-clear bonus: prefer a Legendary, fall back to Rare, then Common
        // so the act capstone always feels like a real reward.
        public void AutoGrantHighRarity()
        {
            BuildWorkingPool();
            if (m_workingPool.Count == 0) return;

            RelicSO chosen = PickByRarity(m_workingPool, RelicRarity.Legendary)
                          ?? PickByRarity(m_workingPool, RelicRarity.Rare)
                          ?? PickByRarity(m_workingPool, RelicRarity.Common);
            if (chosen == null) return;
            m_claimed.Add(chosen);
            m_runState?.AddRelic(chosen);
        }

        // Read-only sample of a random unclaimed relic. Used by the merchant
        // node to pre-pick a relic for sale without taking it out of the pool.
        public RelicSO PeekRandom()
        {
            BuildWorkingPool();
            if (m_workingPool.Count == 0) return null;
            return WeightedPick(m_workingPool);
        }

        // Mark a relic as taken out of the offering pool. Merchant nodes
        // call this once the player commits to a purchase.
        public void MarkClaimed(RelicSO so)
        {
            if (so != null) m_claimed.Add(so);
        }

        private static RelicSO PickByRarity(List<RelicSO> source, RelicRarity rarity)
        {
            int count = 0;
            foreach (RelicSO so in source) if (so.Rarity == rarity) count++;
            if (count == 0) return null;

            int target = UnityEngine.Random.Range(0, count);
            foreach (RelicSO so in source)
            {
                if (so.Rarity != rarity) continue;
                if (target == 0) return so;
                target--;
            }
            return null;
        }

        private void PresentInternal(int requestedCount)
        {
            ClearShrines();
            BuildWorkingPool();

            if (m_workingPool.Count == 0)
            {
                // Nothing left to offer; skip straight through so the director
                // continues the loop.
                RelicPicked?.Invoke();
                return;
            }

            int count = Mathf.Min(requestedCount, m_workingPool.Count);
            var choices = new List<RelicSO>(count);
            for (int i = 0; i < count; i++)
            {
                RelicSO so = WeightedPick(m_workingPool);
                if (so == null) break;
                m_workingPool.Remove(so);
                choices.Add(so);
            }

            SpawnChoices(choices);
        }

        private void SpawnChoices(IReadOnlyList<RelicSO> choices)
        {
            if (choices == null || choices.Count == 0)
            {
                RelicPicked?.Invoke();
                return;
            }

            var options = new WorldMenu.Choice[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                RelicSO so = choices[i];
                options[i] = new WorldMenu.Choice(so.DisplayName,
                    $"{so.Rarity}\n{so.Description}", () => HandleShrineSelected(so), icon: RiteGlyph.RelicIcon(so));
            }
            WorldMenu.Show(m_player, m_hands, "A LANTERN GIFT",
                "Choose one inscription to carry into the next encounter.", options);
        }

        private void AddFirstMatching(
            List<RelicSO> choices,
            params RelicKind[] kinds)
        {
            foreach (RelicKind kind in kinds)
            {
                RelicSO match = m_workingPool.Find(so => so.Kind == kind);
                if (match == null) continue;

                m_workingPool.Remove(match);
                choices.Add(match);
                return;
            }
        }

        private void BuildWorkingPool()
        {
            m_workingPool.Clear();
            if (m_pool == null) return;
            foreach (RelicSO so in m_pool)
            {
                if (so != null && !m_claimed.Contains(so)) m_workingPool.Add(so);
            }
        }

        // Common 70 / Rare 25 / Legendary 5 weights. Picks weighted-randomly
        // from whatever rarity actually remains in the pool.
        private static RelicSO WeightedPick(List<RelicSO> source)
        {
            if (source == null || source.Count == 0) return null;

            float total = 0f;
            foreach (RelicSO so in source) total += WeightFor(so.Rarity);
            if (total <= 0f) return source[UnityEngine.Random.Range(0, source.Count)];

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (RelicSO so in source)
            {
                roll -= WeightFor(so.Rarity);
                if (roll <= 0f) return so;
            }
            return source[source.Count - 1];
        }

        private static float WeightFor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 70f;
                case RelicRarity.Rare: return 25f;
                case RelicRarity.Legendary: return 5f;
            }
            return 1f;
        }

        private void HandleShrineSelected(RelicSO so)
        {
            m_claimed.Add(so);
            m_runState?.AddRelic(so);
            ClearShrines();
            RelicPicked?.Invoke();
        }

        private void ClearShrines()
        {
            foreach (RelicShrine shrine in m_activeShrines)
            {
                if (shrine != null) Destroy(shrine.gameObject);
            }
            m_activeShrines.Clear();
        }

        private void OnDestroy()
        {
            ClearShrines();
        }
    }
}
