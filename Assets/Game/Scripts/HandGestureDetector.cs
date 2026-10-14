using System;
using UnityEngine;

namespace HoldTheCircle
{
    public enum HandSide
    {
        Left,
        Right
    }

    /// <summary>
    /// Detects a fast hand "slash" (Fruit-Ninja style) and raises
    /// <see cref="Slashed"/> with the swing direction and a 0..1 power based on
    /// swing speed. Far less tiring than a held charge gesture: just swipe.
    ///
    /// Hand velocity is measured from this transform's world-space movement
    /// frame to frame — the caster sits on the tracked hand bone, so its
    /// position is the hand position. A cooldown ensures one swipe = one slash.
    ///
    /// Hand tracking only produces motion on the device, so in the Editor we
    /// fall back to a keyboard sim (tap a key to swing) so the loop is testable
    /// without a headset.
    /// </summary>
    public class HandGestureDetector : MonoBehaviour
    {
        // Hand must move at least this fast (m/s) to register a slash.
        private const float k_swingSpeedThreshold = 1.1f;
        // Swing speed (m/s) that maps to full power / AOE.
        private const float k_swingSpeedForFullPower = 3.5f;
        // Minimum seconds between slashes so one swipe doesn't machine-gun.
        private const float k_swingCooldownSeconds = 0.28f;

        private HandSide m_side;
        private bool m_active = true;

        private Vector3 m_lastPos;
        private bool m_hasLastPos;
        private float m_cooldownTimer;
        private float m_speed01;
        private float m_fireRateMultiplier = 1f;

        // direction (world, horizontal) + power 0..1 from swing speed.
        public event Action<Vector3, float> Slashed;

        // Normalized hand speed 0..1, for energizing the palm blade glow.
        public float SpeedCharge => m_active ? m_speed01 : 0f;

        public void Initialize(HandSide side)
        {
            m_side = side;
        }

        public void SetFireRateMultiplier(float multiplier)
        {
            m_fireRateMultiplier = Mathf.Clamp(multiplier, 0.5f, 4f);
        }

        // Gate the detector off during menus / non-combat. Resets velocity
        // tracking so re-enabling doesn't fire a phantom slash from a stale pos.
        public void SetActive(bool active)
        {
            m_active = active;
            if (!active)
            {
                m_speed01 = 0f;
                m_hasLastPos = false;
            }
        }

        private void Update()
        {
            if (!m_active || HitstopManager.IsPaused || !HandTrackingGate.IsTracked(m_side))
            {
                m_hasLastPos = false;
                m_speed01 = 0f;
                return;
            }
            if (m_cooldownTimer > 0f) m_cooldownTimer -= Time.unscaledDeltaTime;

#if UNITY_EDITOR
            UpdateEditorSim();
#else
            UpdateSwing();
#endif
        }

#if UNITY_EDITOR
        // Left hand = 'F', right hand = 'J'. Tap to swing forward.
        private void UpdateEditorSim()
        {
            if (!m_active)
            {
                m_speed01 = 0f;
                return;
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            var key = m_side == HandSide.Left ? keyboard.fKey : keyboard.jKey;

            // Glow while held so the blade reads as energized, then decays.
            m_speed01 = key.isPressed ? 1f : Mathf.MoveTowards(m_speed01, 0f, Time.deltaTime * 4f);

            if (m_cooldownTimer > 0f) return;
            if (!key.wasPressedThisFrame) return;

            Vector3 dir = transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            m_cooldownTimer = GetSwingCooldown();
            Slashed?.Invoke(dir, 1f);
        }
#else
        private void UpdateSwing()
        {
            Vector3 pos = transform.position;

            // Need a previous sample to derive velocity.
            if (!m_hasLastPos)
            {
                m_lastPos = pos;
                m_hasLastPos = true;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float displacement = (pos - m_lastPos).magnitude;
            Vector3 velocity = dt > 0.0001f ? (pos - m_lastPos) / dt : Vector3.zero;
            m_lastPos = pos;
            if (dt > 0.1f || displacement > 0.35f)
            {
                m_speed01 = 0f;
                return;
            }

            if (!m_active)
            {
                m_speed01 = 0f;
                return;
            }

            float speed = velocity.magnitude;
            m_speed01 = Mathf.Clamp01(speed / k_swingSpeedForFullPower);

            if (m_cooldownTimer > 0f) return;
            if (speed < k_swingSpeedThreshold) return;

            // Slash flies along the horizontal swing direction; a near-vertical
            // chop with no horizontal travel falls back to the hand's forward.
            Vector3 dir = velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = transform.forward;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            float power01 = Mathf.Clamp01(
                (speed - k_swingSpeedThreshold) / (k_swingSpeedForFullPower - k_swingSpeedThreshold));

            m_cooldownTimer = GetSwingCooldown();
            Slashed?.Invoke(dir, power01);
        }
#endif

        private float GetSwingCooldown()
        {
            return k_swingCooldownSeconds / m_fireRateMultiplier;
        }
    }
}
