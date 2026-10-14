using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// State machine for the act-1 boss "Twin Sentinels". Drives intro -> P1
    /// (mirrored slow casts) -> Transition (radial bullet hell) -> P2 (split,
    /// faster, asynchronous) -> Enrage (last-stand burst) -> Defeat.
    /// Spawns and owns its sentinels + health bar; reports completion via the
    /// Defeated event so GameDirector can run reward + map continuation.
    /// </summary>
    public class BossSystem : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Intro,
            PhaseOne,
            Transition,
            PhaseTwo,
            Enrage,
            Defeated
        }

        public event Action Defeated;

        private GameConfigSO m_config;
        private Transform m_player;
        private RunState m_runState;

        private BossSentinel m_left;
        private BossSentinel m_right;
        private BossHealthBar m_healthBar;
        private Phase m_phase = Phase.Idle;
        private float m_phaseTimer;

        private static readonly Color k_phaseOneTint = new(0.45f, 0.85f, 1f, 1f);
        private static readonly Color k_phaseTwoTint = new(1f, 0.45f, 0.85f, 1f);
        private static readonly Color k_enrageTint = new(1f, 0.55f, 0.25f, 1f);

        public Phase CurrentPhase => m_phase;
        public bool IsActive => m_phase != Phase.Idle && m_phase != Phase.Defeated;

        public void Initialize(GameConfigSO config, Transform player, RunState runState)
        {
            m_config = config;
            m_player = player;
            m_runState = runState;
        }

        public void StartEncounter()
        {
            if (IsActive) return;

            Vector3 forward = FrontApproach.CaptureForward(m_player);
            Vector3 leftPos = FrontApproach.Position(m_player, forward, 9f, -2.5f, 0.1f);
            Vector3 rightPos = FrontApproach.Position(m_player, forward, 9f, 2.5f, 0.1f);

            float baseHealth = 60f;
            m_left = BossSentinel.Spawn(leftPos, m_config, m_player, m_runState, k_phaseOneTint, baseHealth, forward);
            m_right = BossSentinel.Spawn(rightPos, m_config, m_player, m_runState, k_phaseOneTint, baseHealth, forward);
            m_left.Killed += HandleSentinelKilled;
            m_right.Killed += HandleSentinelKilled;

            m_healthBar = BossHealthBar.Spawn(m_player, m_left, m_right);

            EnterPhase(Phase.Intro);
        }

        public void StopEncounter()
        {
            if (m_left != null)
            {
                m_left.Killed -= HandleSentinelKilled;
                Destroy(m_left.gameObject);
                m_left = null;
            }
            if (m_right != null)
            {
                m_right.Killed -= HandleSentinelKilled;
                Destroy(m_right.gameObject);
                m_right = null;
            }
            if (m_healthBar != null)
            {
                Destroy(m_healthBar.gameObject);
                m_healthBar = null;
            }
            m_phase = Phase.Idle;
        }

        private void Update()
        {
            if (!IsActive) return;
            if ((m_left == null || m_left.IsDead) && (m_right == null || m_right.IsDead))
            {
                if ((m_left == null || m_left.DeathComplete) && (m_right == null || m_right.DeathComplete))
                    EnterPhase(Phase.Defeated);
                return;
            }

            m_phaseTimer += Time.deltaTime;

            switch (m_phase)
            {
                case Phase.Intro:
                    if (m_phaseTimer >= 1.5f) EnterPhase(Phase.PhaseOne);
                    break;

                case Phase.PhaseOne:
                    // Trigger transition once either sentinel falls below 50%.
                    if (BelowHealth(m_left, 0.5f) || BelowHealth(m_right, 0.5f))
                    {
                        EnterPhase(Phase.Transition);
                    }
                    break;

                case Phase.Transition:
                    if (m_phaseTimer >= 2.0f) EnterPhase(Phase.PhaseTwo);
                    break;

                case Phase.PhaseTwo:
                    // Once one sentinel dies, the survivor enrages.
                    if ((m_left == null || m_left.IsDead) ^ (m_right == null || m_right.IsDead))
                    {
                        EnterPhase(Phase.Enrage);
                    }
                    break;

                case Phase.Enrage:
                    if ((m_left == null || m_left.IsDead) && (m_right == null || m_right.IsDead))
                    {
                        EnterPhase(Phase.Defeated);
                    }
                    break;
            }
        }

        private void EnterPhase(Phase next)
        {
            m_phase = next;
            m_phaseTimer = 0f;

            switch (next)
            {
                case Phase.Intro:
                    // Drift in slowly, no casting yet.
                    m_left?.Activate(orbitRadiusInMeters: 5f, orbitDegPerSec: 8f, castIntervalInSeconds: 99f,
                        projectileSpeed: 0f, projectileDamage: 0f, startAngleInDegrees: 160f);
                    m_right?.Activate(orbitRadiusInMeters: 5f, orbitDegPerSec: 8f, castIntervalInSeconds: 99f,
                        projectileSpeed: 0f, projectileDamage: 0f, startAngleInDegrees: 20f);
                    m_healthBar?.FlashPhase(new Color(0.4f, 0.4f, 0.5f, 1f));
                    break;

                case Phase.PhaseOne:
                    // Mirrored slow casts so the player learns the rhythm.
                    m_left?.Activate(4.5f, 25f, 1.6f, 5f, 8f, 180f);
                    m_right?.Activate(4.5f, -25f, 1.6f, 5f, 8f, 0f);
                    m_healthBar?.FlashPhase(k_phaseOneTint);
                    break;

                case Phase.Transition:
                    // Brief screen-clear bullet hell, then enter phase 2.
                    if (m_left != null && !m_left.IsDead) m_left.FireRadialVolley(16, 4f, 6f);
                    if (m_right != null && !m_right.IsDead) m_right.FireRadialVolley(16, 4f, 6f);
                    m_left?.SetTint(k_phaseTwoTint);
                    m_right?.SetTint(k_phaseTwoTint);
                    m_healthBar?.FlashPhase(k_phaseTwoTint);
                    break;

                case Phase.PhaseTwo:
                    // Asynchronous orbits + faster casts so the player must
                    // alternate targets quickly — the design intent.
                    m_left?.Activate(4.0f, 45f, 1.0f, 6f, 9f, 200f);
                    m_right?.Activate(4.0f, -55f, 1.1f, 6f, 9f, 30f);
                    m_healthBar?.FlashPhase(k_phaseTwoTint);
                    break;

                case Phase.Enrage:
                    // Surviving sentinel speeds up and rains a final volley.
                    BossSentinel survivor = (m_left != null && !m_left.IsDead) ? m_left : m_right;
                    if (survivor != null)
                    {
                        survivor.SetTint(k_enrageTint);
                        survivor.Activate(3.5f, 80f, 0.7f, 7f, 11f, 0f);
                        survivor.FireRadialVolley(20, 5f, 7f);
                    }
                    m_healthBar?.FlashPhase(k_enrageTint);
                    break;

                case Phase.Defeated:
                    // Final flourish: clear the sentinels and notify the director.
                    StopEncounter();
                    Defeated?.Invoke();
                    break;
            }
        }

        private static bool BelowHealth(BossSentinel sentinel, float ratio)
        {
            if (sentinel == null || sentinel.IsDead || sentinel.MaxHealth <= 0f) return false;
            return sentinel.CurrentHealth / sentinel.MaxHealth <= ratio;
        }

        private void HandleSentinelKilled(BossSentinel sentinel)
        {
            // Final transition runs in Update after the damage caller has
            // published its kill event, including simultaneous burst kills.
        }

        private void OnDestroy()
        {
            StopEncounter();
        }
    }
}
