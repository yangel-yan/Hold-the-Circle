using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Tracks the player's hit points. Raises events the GameDirector / HUD react to.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private float m_maxHealth = 100f;
        private float m_currentHealth;
        private RunState m_runState;

        // current, max
        public event Action<float, float> HealthChanged;
        public event Action Died;

        public bool IsDead => m_currentHealth <= 0f;
        public float CurrentHealth => m_currentHealth;
        public float MaxHealth => m_maxHealth;

        // Optional bus wiring; if absent, damage still applies but no relic hook fires.
        public void BindRunState(RunState runState)
        {
            m_runState = runState;
        }

        public void Initialize(float maxHealth)
        {
            m_maxHealth = maxHealth;
            m_currentHealth = maxHealth;
            HealthChanged?.Invoke(m_currentHealth, m_maxHealth);
        }

        public bool ApplyDamage(float amount)
        {
            if (IsDead) return false;

            // Aegis specialization absorbs a fraction of incoming damage.
            float reduction = m_runState != null ? m_runState.GetMaxDamageReduction() : 0f;
            float effectiveAmount = amount * (1f - reduction);

            m_currentHealth = Mathf.Max(0f, m_currentHealth - effectiveAmount);
            HealthChanged?.Invoke(m_currentHealth, m_maxHealth);

            m_runState?.Events.RaiseDamageTaken(new DamageTakenEvent
            {
                Amount = effectiveAmount,
                CurrentHealth = m_currentHealth,
                MaxHealth = m_maxHealth
            });

            if (m_currentHealth <= 0f)
            {
                Died?.Invoke();
                return true;
            }

            return false;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            m_currentHealth = Mathf.Min(m_maxHealth, m_currentHealth + amount);
            HealthChanged?.Invoke(m_currentHealth, m_maxHealth);
        }
    }
}
