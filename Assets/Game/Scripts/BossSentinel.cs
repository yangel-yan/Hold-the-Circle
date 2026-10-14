using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    [RequireComponent(typeof(SphereCollider))]
    public class BossSentinel : MonoBehaviour, IDamageable
    {
        private static readonly List<BossSentinel> s_active = new(2);
        public static IReadOnlyList<BossSentinel> Active => s_active;
        public Vector3 AimPosition => transform.position + Vector3.up * 0.9f;
        public event Action<BossSentinel> Killed;
        public Color Tint => m_tint;
        public bool IsDead => m_isDead;
        public bool DeathComplete => m_visual == null || m_visual.DeathComplete;
        public float CurrentHealth => m_health;
        public float MaxHealth => m_maxHealth;
        private Transform m_player;
        private RunState m_runState;
        private EnemyVisual m_visual;
        private Transform m_halo;
        private RiteGlyph m_crest;
        private Transform m_camera;
        private float m_health;
        private float m_maxHealth;
        private bool m_isDead;
        private bool m_isActive;
        private float m_radius;
        private float m_angle;
        private float m_angularSpeed;
        private float m_castInterval;
        private float m_castTimer;
        private float m_projectileSpeed;
        private float m_projectileDamage;
        private Color m_tint;
        private float m_flashTimer;
        private Vector3 m_encounterForward;
        private bool m_windingUp;

        public static BossSentinel Spawn(Vector3 position, GameConfigSO config, Transform player,
            RunState runState, Color tint, float maxHealth, Vector3 encounterForward = default)
        {
            var go = new GameObject("Lantern Sentinel");
            go.transform.position = position;
            var sentinel = go.AddComponent<BossSentinel>();
            s_active.Add(sentinel);
            sentinel.m_player = player;
            sentinel.m_encounterForward = encounterForward.sqrMagnitude > 0.001f
                ? encounterForward.normalized : FrontApproach.CaptureForward(player);
            sentinel.m_runState = runState;
            sentinel.m_tint = tint;
            sentinel.m_maxHealth = sentinel.m_health = maxHealth;
            var collider = go.GetComponent<SphereCollider>();
            collider.center = Vector3.up * 0.75f;
            collider.radius = 0.9f;
            collider.isTrigger = true;
            sentinel.m_visual = go.AddComponent<EnemyVisual>();
            sentinel.m_visual.Configure(EnemyArchetype.Weaver);
            sentinel.m_visual.SetAccent(tint);
            sentinel.m_visual.transform.localScale = Vector3.one * 1.5f;
            sentinel.m_halo = new GameObject("Sentinel Crown").transform;
            sentinel.m_halo.SetParent(go.transform, false);
            // The sentinel root already has 1.5x scale; keep the crown just above the hood.
            sentinel.m_halo.localPosition = Vector3.up * 1.17f;
            sentinel.m_crest = RiteGlyph.Create(sentinel.m_halo, "Sentinel Crest", RiteMotif.Lantern,
                new Vector2(0.48f, 0.48f), tint);
            for (int i = 0; i < 2; i++)
            {
                RiteGlyph prong = RiteGlyph.Create(sentinel.m_halo, "Crown Prong", RiteMotif.Lance,
                    new Vector2(0.21f, 0.28f), new Color(1f, 0.81f, 0.52f, 0.72f));
                prong.transform.localPosition = new Vector3(i == 0 ? -0.21f : 0.21f, -0.06f, 0);
                prong.transform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 18f : -18f);
            }
            sentinel.m_camera = Camera.main != null ? Camera.main.transform : null;
            return sentinel;
        }

        public void Activate(float orbitRadiusInMeters, float orbitDegPerSec, float castIntervalInSeconds,
            float projectileSpeed, float projectileDamage, float startAngleInDegrees)
        {
            if (m_isDead) return;
            m_radius = orbitRadiusInMeters;
            m_angularSpeed = orbitDegPerSec;
            m_castInterval = castIntervalInSeconds;
            m_projectileSpeed = projectileSpeed;
            m_projectileDamage = projectileDamage;
            m_angle = startAngleInDegrees;
            m_castTimer = m_castInterval * 0.5f;
            m_windingUp = false;
            m_isActive = true;
        }

        public void SetTint(Color tint)
        {
            m_tint = tint;
            m_visual.SetAccent(tint);
            m_crest.SetTint(tint);
        }

        public void FireRadialVolley(int shotCount, float speed, float damage)
        {
            if (m_isDead) return;
            for (int i = 0; i < shotCount; i++)
            {
                float angle = i * Mathf.PI * 2f / shotCount;
                EnemyProjectile.Spawn(transform.position + Vector3.up * 0.8f,
                    new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)), speed, damage, m_tint);
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0) return;
            if (m_isDead) { m_visual?.Present(0f, 0f); return; }
            if (!m_isActive || m_player == null) return;
            m_angle += m_angularSpeed * Time.deltaTime;
            float angle = m_angle * Mathf.Deg2Rad;
            Vector3 origin = m_player.position;
            // Oscillate across the front. Smoothly approach from the spawn distance,
            // including phase changes, rather than teleporting onto a circular orbit.
            Vector3 target = FrontApproach.Position(m_player, m_encounterForward,
                m_radius, Mathf.Cos(angle) * m_radius * 0.45f, 0.1f);
            Vector3 before = transform.position;
            if (!m_windingUp) transform.position = Vector3.MoveTowards(transform.position, target, Time.deltaTime * 2f);
            transform.position = FrontApproach.Constrain(transform.position, m_player, m_encounterForward);
            Vector3 forward = origin - transform.position;
            forward.y = 0;
            if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward);
            m_flashTimer = Mathf.Max(0, m_flashTimer - Time.deltaTime);
            m_visual.Present(m_flashTimer / 0.1f, 1f - Mathf.Clamp01(m_castTimer / 0.75f),
                Vector3.Distance(before, transform.position) / Mathf.Max(Time.deltaTime, 0.0001f));
            if (m_visual.Head != null) m_halo.position = m_visual.Head.position + Vector3.up * 0.22f;
            m_halo.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 1.8f) * 0.025f);
            if (m_camera != null) m_halo.rotation = m_camera.rotation;
            m_castTimer -= Time.deltaTime;
            if (m_castTimer <= 0.75f && !m_windingUp)
            {
                m_windingUp = true;
                m_visual.WindUp(Mathf.Max(0.05f, m_castTimer));
            }
            if (m_castTimer > 0) return;
            m_windingUp = false;
            m_castTimer = m_castInterval;
            Vector3 shotOrigin = transform.position + Vector3.up * 0.8f;
            EnemyProjectile.Spawn(shotOrigin, origin + Vector3.up - shotOrigin,
                m_projectileSpeed, m_projectileDamage, m_tint);
        }

        public bool ApplyDamage(float amount)
        {
            if (m_isDead || amount <= 0) return false;
            m_health = Mathf.Max(0, m_health - amount);
            m_flashTimer = 0.1f;
            if (m_health > 0) return false;
            m_isDead = true;
            GetComponent<Collider>().enabled = false;
            m_visual.Die();
            m_isActive = false;
            m_halo.gameObject.SetActive(false);
            s_active.Remove(this);
            Killed?.Invoke(this);
            return true;
        }

        public void Revive(float health)
        {
            m_health = health;
            m_maxHealth = Mathf.Max(m_maxHealth, health);
            m_isDead = false;
            GetComponent<Collider>().enabled = true;
            m_visual.Configure(EnemyArchetype.Weaver);
            m_visual.SetAccent(m_tint);
            m_isActive = true;
            m_halo.gameObject.SetActive(true);
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        private void OnDisable() => s_active.Remove(this);
    }
}
