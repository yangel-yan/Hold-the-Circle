using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Lightweight, allocation-free visual flourish: gently pulses the local
    /// scale and spins the transform. Pool-safe (re-reads the base scale on
    /// enable). Used purely for decoration on enemies and energy waves.
    /// </summary>
    public class PulseSpin : MonoBehaviour
    {
        [Header("Pulse")]
        [Tooltip("Peak scale offset as a fraction of the base scale")]
        [SerializeField] private float m_pulseAmount = 0.12f;
        [Tooltip("Pulses per second")]
        [SerializeField] private float m_pulseFrequency = 2f;

        [Header("Spin")]
        [Tooltip("Local rotation speed in degrees per second")]
        [SerializeField] private Vector3 m_spinSpeedInDegrees = new(0f, 90f, 0f);

        private Vector3 m_baseScale;
        private float m_phase;

        private void OnEnable()
        {
            m_baseScale = transform.localScale;
            // Randomize the starting phase so pooled copies do not pulse in lockstep.
            m_phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            m_phase += m_pulseFrequency * Mathf.PI * 2f * Time.deltaTime;
            float pulse = 1f + Mathf.Sin(m_phase) * m_pulseAmount;
            transform.localScale = m_baseScale * pulse;

            transform.Rotate(m_spinSpeedInDegrees * Time.deltaTime, Space.Self);
        }
    }
}
