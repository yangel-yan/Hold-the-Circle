using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    [RequireComponent(typeof(Collider))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        private static readonly List<Enemy> s_active = new(64);
        public static IReadOnlyList<Enemy> Active => s_active;
        private GameConfigSO m_config;
        private Transform m_player;
        private IDamageable m_playerDamageable;
        private Action<Enemy> m_onDied;
        private EnemyVisual m_visual;
        private EnemyArchetype m_archetype;
        private float m_health;
        private float m_attackTimer;
        private float m_speedMultiplier;
        private float m_preferredRangeInMeters;
        private float m_hitFlashTimer;
        private float m_localHitstopTimer;
        private float m_retargetTimer;
        private Vector3 m_wanderTarget;
        private Vector3 m_knockbackVelocity;
        private Vector3 m_baseScale;
        private bool m_isDead;
        private bool m_isTutorialTarget;
        private Color m_tint;
        private Vector3 m_encounterForward;
        private float m_lane;
        private bool m_windingUp;

        private const float k_engageRange = 1.65f;
        private const float k_meleeWindup = 0.65f;
        private const float k_castWindup = 0.8f;

        public bool IsDead => m_isDead;
        public bool DeathComplete => m_visual == null || m_visual.DeathComplete;
        public EnemyArchetype Archetype => m_archetype;
        public Vector3 AimPosition => transform.position + Vector3.up * 0.7f;

        public void Initialize(GameConfigSO config, Transform player, Action<Enemy> onDied)
        {
            m_config = config;
            m_player = player;
            m_playerDamageable = player != null ? player.GetComponent<IDamageable>() : null;
            m_onDied = onDied;
            // Prepared art is authored in metres; legacy primitive prefab scale is unrelated.
            m_baseScale = Vector3.one;
            m_visual = gameObject.GetComponent<EnemyVisual>();
            if (m_visual == null) m_visual = gameObject.AddComponent<EnemyVisual>();
            Transform legacyVisual = transform.Find("Visual");
            if (legacyVisual != null) legacyVisual.gameObject.SetActive(false);
            // Existing collision geometry follows the painted body, not its floor anchor.
            if (TryGetComponent(out SphereCollider sphere))
            {
                sphere.center = Vector3.up * 0.65f;
                sphere.radius = 0.42f;
            }
        }

        public void Spawn(Vector3 position, int waveIndex, EnemyArchetype archetype,
            bool isTutorialTarget = false, Vector3 encounterForward = default)
        {
            var profile = EnemyArchetypes.Get(archetype);
            m_encounterForward = encounterForward.sqrMagnitude > 0.001f
                ? encounterForward.normalized : FrontApproach.CaptureForward(m_player);
            Vector3 offset = position - (m_player != null ? m_player.position : Vector3.zero);
            m_lane = Mathf.Clamp(Vector3.Dot(offset, Vector3.Cross(Vector3.up, m_encounterForward)) * 0.2f, -0.8f, 0.8f);
            transform.position = position;
            // Rigged role proportions are applied by EnemyVisual in metre space.
            transform.localScale = m_baseScale;
            m_archetype = archetype;
            m_health = m_config.GetEnemyHealthForWave(waveIndex) * profile.HealthMultiplier;
            m_speedMultiplier = profile.SpeedMultiplier;
            m_preferredRangeInMeters = profile.PreferredRangeInMeters;
            m_tint = profile.Tint;
            m_isDead = false;
            m_windingUp = false;
            GetComponent<Collider>().enabled = true;
            m_isTutorialTarget = isTutorialTarget;
            m_attackTimer = m_config.EnemyAttackIntervalInSeconds;
            m_hitFlashTimer = 0f;
            m_localHitstopTimer = 0f;
            m_knockbackVelocity = Vector3.zero;
            m_retargetTimer = 0f;
            if (!s_active.Contains(this)) s_active.Add(this);
            m_visual.Configure(archetype);
            PickWanderTarget();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (m_isDead)
            {
                m_visual?.Present(0f, 0f);
                return;
            }
            if (m_player == null || m_config == null) return;
            float dt = Time.deltaTime;
            m_hitFlashTimer = Mathf.Max(0f, m_hitFlashTimer - dt);
            float windup = m_archetype == EnemyArchetype.Weaver ? k_castWindup : k_meleeWindup;
            float threat = m_isTutorialTarget ? 0f : 1f - Mathf.Clamp01(m_attackTimer / windup);
            transform.position += m_knockbackVelocity * dt;
            transform.position = FrontApproach.Constrain(transform.position, m_player, m_encounterForward);
            m_knockbackVelocity = Vector3.Lerp(m_knockbackVelocity, Vector3.zero, dt * 12f);
            if (m_localHitstopTimer > 0f)
            {
                m_localHitstopTimer -= dt;
                m_visual.Present(m_hitFlashTimer / 0.1f, threat, 0f, true);
                return;
            }
            Vector3 before = transform.position;
            UpdateMovement(dt);
            UpdateAttack(dt);
            m_visual.Present(m_hitFlashTimer / 0.1f, threat,
                Vector3.Distance(before, transform.position) / Mathf.Max(dt, 0.0001f));
        }

        private void UpdateMovement(float dt)
        {
            Vector3 toPlayer = m_player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 direction = distance > 0.001f ? toPlayer / distance : Vector3.forward;
            float speed = m_config.EnemyLungeSpeedInMetersPerSecond * m_speedMultiplier;
            if (m_windingUp) { }
            else if (m_archetype == EnemyArchetype.Weaver)
            {
                m_retargetTimer -= dt;
                if (m_retargetTimer <= 0f || (m_wanderTarget - transform.position).sqrMagnitude < 0.25f)
                    PickWanderTarget();
                transform.position = Vector3.MoveTowards(transform.position, m_wanderTarget, speed * dt);
            }
            else if (distance > k_engageRange)
            {
                // Clamp movement so a frame hitch cannot push a body into the player.
                transform.position += direction * Mathf.Min(speed * dt, distance - k_engageRange);
            }
            else
            {
                Vector3 target = FrontApproach.Position(m_player, m_encounterForward,
                    k_engageRange, m_lane + Mathf.Sin(Time.time * 1.3f + m_lane) * 0.12f, transform.position.y);
                transform.position = Vector3.MoveTowards(transform.position, target, speed * 0.2f * dt);
            }
            transform.position = FrontApproach.Constrain(transform.position, m_player, m_encounterForward);
            if (toPlayer.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toPlayer);
        }

        private void PickWanderTarget()
        {
            m_retargetTimer = 2f;
            float depth = Mathf.Max(2.8f, m_preferredRangeInMeters);
            m_wanderTarget = FrontApproach.Position(m_player, m_encounterForward, depth,
                UnityEngine.Random.Range(-0.5f, 0.5f) * depth, transform.position.y);
        }

        private void UpdateAttack(float dt)
        {
            if (m_isTutorialTarget) return;
            Vector3 offset = m_player.position - transform.position;
            offset.y = 0f;
            bool ranged = m_archetype == EnemyArchetype.Weaver;
            float range = ranged ? Mathf.Max(4.5f, m_preferredRangeInMeters * 1.5f) : k_engageRange + 0.25f;
            if (offset.sqrMagnitude > range * range)
            {
                m_attackTimer = Mathf.Max(m_attackTimer, ranged ? k_castWindup : k_meleeWindup);
                m_windingUp = false;
                return;
            }
            m_attackTimer -= dt;
            float windup = ranged ? k_castWindup : k_meleeWindup;
            if (m_attackTimer <= windup && !m_windingUp)
            {
                m_windingUp = true;
                m_visual.WindUp(Mathf.Max(0.05f, m_attackTimer));
            }
            if (m_attackTimer > 0f) return;
            m_windingUp = false;
            m_attackTimer = ranged ? 2.6f : m_config.EnemyAttackIntervalInSeconds;
            if (ranged)
            {
                Vector3 origin = AimPosition;
                Vector3 direction = m_player.position + Vector3.up - origin;
                if (direction.sqrMagnitude > 0.001f)
                    EnemyProjectile.Spawn(origin, direction, 3.8f, m_config.EnemyAttackDamage * 0.6f, m_tint);
            }
            else m_playerDamageable?.ApplyDamage(m_config.EnemyAttackDamage);
        }

        public bool ApplyDamage(float amount) => ApplyDamage(amount, Vector3.zero);

        public bool ApplyDamage(float amount, Vector3 hitDirection)
        {
            if (m_isDead || amount <= 0f) return false;
            m_health -= amount;
            m_hitFlashTimer = 0.1f;
            m_localHitstopTimer = 0.045f;
            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude > 0.001f) m_knockbackVelocity = hitDirection.normalized * 3.5f;
            if (m_health > 0f) return false;
            m_isDead = true;
            GetComponent<Collider>().enabled = false;
            m_visual.Die();
            s_active.Remove(this);
            m_onDied?.Invoke(this);
            return true;
        }

        private void OnDisable() => s_active.Remove(this);
    }
}
