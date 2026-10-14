using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Tracks the player's kill streak. Each kill bumps the combo and refreshes
    /// a short expiry timer; letting the timer lapse without a kill resets the
    /// streak to zero. Broadcasts the current count through the run event bus so
    /// the HUD and audio system can escalate feedback independently — the combo
    /// system itself owns no UI or sound.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        // No kill within this window drops the streak back to zero. Tuned so a
        // steady stream of fire keeps the combo alive but a lull breaks it.
        private const float k_comboTimeoutInSeconds = 3f;

        private RunState m_runState;
        private int m_combo;
        private float m_expiryTimer;

        public int Combo => m_combo;

        public void Initialize(RunState runState)
        {
            m_runState = runState;
            if (m_runState != null)
            {
                m_runState.Events.EnemyKilled += HandleEnemyKilled;
            }
        }

        private void OnDestroy()
        {
            if (m_runState != null)
            {
                m_runState.Events.EnemyKilled -= HandleEnemyKilled;
            }
        }

        // Reset the streak between runs / rooms so a new fight starts clean.
        public void ResetCombo()
        {
            if (m_combo == 0) return;
            m_combo = 0;
            m_expiryTimer = 0f;
            m_runState?.Events.RaiseComboChanged(0);
        }

        private void Update()
        {
            if (m_combo <= 0) return;

            m_expiryTimer -= Time.deltaTime;
            if (m_expiryTimer <= 0f)
            {
                ResetCombo();
            }
        }

        private void HandleEnemyKilled(EnemyKilledEvent e)
        {
            m_combo++;
            m_expiryTimer = k_comboTimeoutInSeconds;
            m_runState?.Events.RaiseComboChanged(m_combo);
        }
    }
}
