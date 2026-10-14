using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Run-start orchestrator that lets the player pick a specialization for
    /// each hand independently. Spawns three SpecializationCrystals per hand
    /// (Lance / Storm / Aegis) in front of the player, locks each hand's pick
    /// once that hand touches one of its crystals, and fires Completed when
    /// every hand has chosen so GameDirector can begin the wave loop.
    /// Code-built; no prefab/scene wiring.
    /// </summary>
    public class SpecializationPicker : MonoBehaviour
    {
        private const float k_distanceInMeters = 0.7f;
        private const float k_columnSpreadInMeters = 0.35f;
        private const float k_rowSpreadInMeters = 0.22f;
        private const float k_centerHeightInMeters = -0.05f;

        private RunState m_runState;
        private Transform m_player;
        private Transform[] m_hands;
        private SpecializationSO[] m_branches;

        private readonly List<SpecializationCrystal> m_activeCrystals = new();
        private readonly HashSet<int> m_handsPicked = new();

        // Raised once every hand has selected a branch.
        public event Action Completed;

        public void Initialize(RunState runState, Transform player, Transform[] hands,
            SpecializationSO[] branches)
        {
            m_runState = runState;
            m_player = player;
            m_hands = hands;
            m_branches = branches;
        }

        public bool HasUsableData()
        {
            if (m_branches == null || m_branches.Length == 0) return false;
            foreach (SpecializationSO branch in m_branches)
            {
                if (branch == null) return false;
            }
            return true;
        }

        // Place one column of crystals in front of each hand. Each hand can
        // only claim crystals from its own column.
        public void Present()
        {
            ClearCrystals();
            m_handsPicked.Clear();
            if (m_hands == null || m_branches == null) return;
            PresentHand(0);
        }

        private void PresentHand(int handIndex)
        {
            if (handIndex >= m_hands.Length) { Completed?.Invoke(); return; }
            var options = new WorldMenu.Choice[m_branches.Length];
            for (int i = 0; i < m_branches.Length; i++)
            {
                SpecializationSO spec = m_branches[i];
                options[i] = new WorldMenu.Choice(spec.DisplayName, spec.Description,
                    () => HandleCrystalSelected(handIndex, spec));
            }
            var caster = m_hands[handIndex] != null ? m_hands[handIndex].GetComponent<HandWaveCaster>() : null;
            string side = caster != null ? caster.Side.ToString().ToUpperInvariant() : (handIndex + 1).ToString();
            WorldMenu.Show(m_player, m_hands, side + " HAND RITE",
                "Your rite evolves after each cleared encounter.", options);
        }

        private void HandleCrystalSelected(int handIndex, SpecializationSO spec)
        {
            if (m_handsPicked.Contains(handIndex)) return;
            m_handsPicked.Add(handIndex);

            m_runState?.SetSpecialization(handIndex, spec);

            // Remove the unchosen crystals for this hand so the player can't
            // double-pick, then check whether every hand is done.
            DespawnCrystalsForHand(handIndex);

            PresentHand(handIndex + 1);
        }

        private void DespawnCrystalsForHand(int handIndex)
        {
            for (int i = m_activeCrystals.Count - 1; i >= 0; i--)
            {
                SpecializationCrystal crystal = m_activeCrystals[i];
                if (crystal == null)
                {
                    m_activeCrystals.RemoveAt(i);
                    continue;
                }

                if (crystal.HandIndex == handIndex)
                {
                    Destroy(crystal.gameObject);
                    m_activeCrystals.RemoveAt(i);
                }
            }
        }

        private void ClearCrystals()
        {
            foreach (SpecializationCrystal crystal in m_activeCrystals)
            {
                if (crystal != null) Destroy(crystal.gameObject);
            }
            m_activeCrystals.Clear();
        }

        private void OnDestroy()
        {
            ClearCrystals();
        }
    }
}
