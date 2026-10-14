using System;
using UnityEngine;

namespace HoldTheCircle
{
    public sealed class CircleIntegrity : MonoBehaviour
    {
        private ArenaBuilder m_arena;
        private bool m_active;
        private float m_integrity = 100f;
        private float m_sampleTimer;
        private int m_invaders;
        public float Ratio => m_integrity / 100f;
        public int Invaders => m_invaders;
        public event Action Failed;

        public void Initialize(ArenaBuilder arena) => m_arena = arena;
        public void ResetForNewRun()
        {
            m_integrity = 100f;
            m_invaders = 0;
            m_active = false;
            m_arena.SetStability(1f);
        }
        public void SetCombatActive(bool active)
        {
            m_active = active;
            if (!active)
            {
                m_invaders = 0;
                m_arena.SetStability(Ratio);
            }
        }
        public void Restore(float amount)
        {
            m_integrity = Mathf.Clamp(m_integrity + amount, 0f, 100f);
            m_arena.SetStability(Ratio);
            if (m_active && m_integrity <= 0f)
            {
                m_active = false;
                Failed?.Invoke();
            }
        }

        private void Update()
        {
            if (!m_active || m_arena == null || Time.timeScale <= 0f) return;
            m_sampleTimer -= Time.deltaTime;
            if (m_sampleTimer <= 0f)
            {
                m_sampleTimer = 0.2f;
                m_invaders = 0;
                var enemies = Enemy.Active;
                for (int i = 0; i < enemies.Count; i++)
                {
                    Enemy enemy = enemies[i];
                    if (enemy == null || enemy.IsDead) continue;
                    Vector3 delta = enemy.transform.position - m_arena.Origin;
                    delta.y = 0f;
                    if (delta.sqrMagnitude < ArenaBuilder.Radius * ArenaBuilder.Radius) m_invaders++;
                }
            }
            float change = m_invaders > 0 ? -m_invaders * 2.5f : 2f;
            m_integrity = Mathf.Clamp(m_integrity + change * Time.deltaTime, 0, 100);
            m_arena.SetStability(Ratio);
            if (m_integrity > 0f) return;
            m_active = false;
            Failed?.Invoke();
        }
    }
}
