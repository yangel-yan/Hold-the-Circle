using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace HoldTheCircle
{
    public class EnemyProjectile : MonoBehaviour
    {
        private static readonly List<EnemyProjectile> s_active = new(64);
        private static ObjectPool<EnemyProjectile> s_pool;
        private Vector3 m_velocity;
        private float m_damage;
        private float m_lifetime;
        private RiteGlyph m_glyph;
        private TrailRenderer m_trail;
        private Transform m_camera;

        public static void Spawn(Vector3 origin, Vector3 direction, float speed, float damage, Color tint)
        {
            s_pool ??= new ObjectPool<EnemyProjectile>(Create,
                p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false),
                p => { if (p != null) Destroy(p.gameObject); }, false, 32, 96);
            var projectile = s_pool.Get();
            projectile.transform.position = origin;
            projectile.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward);
            projectile.m_velocity = direction.normalized * speed;
            projectile.m_damage = damage;
            projectile.m_lifetime = 4f;
            projectile.m_glyph.SetTint(tint);
            projectile.m_trail.Clear();
            projectile.m_trail.startColor = tint;
            tint.a = 0f;
            projectile.m_trail.endColor = tint;
            projectile.m_camera = Camera.main != null ? Camera.main.transform : null;
            s_active.Add(projectile);
        }

        private static EnemyProjectile Create()
        {
            var go = new GameObject("Lantern Cinder");
            var p = go.AddComponent<EnemyProjectile>();
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.13f;
            collider.isTrigger = true;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            p.m_glyph = RiteGlyph.Create(go.transform, "Cinder", RiteMotif.Ember,
                new Vector2(0.26f, 0.3f), Color.white);
            p.m_trail = go.AddComponent<TrailRenderer>();
            p.m_trail.sharedMaterial = RiteGlyph.RibbonMaterial;
            p.m_trail.time = 0.12f;
            p.m_trail.startWidth = 0.09f;
            p.m_trail.endWidth = 0f;
            p.m_trail.minVertexDistance = 0.03f;
            p.m_trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        public static void ClearAll()
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
                if (s_active[i] != null) s_pool.Release(s_active[i]);
            s_active.Clear();
        }

        private void Update()
        {
            Vector3 step = m_velocity * Time.deltaTime;
            if (step.sqrMagnitude > 0.00001f &&
                Physics.SphereCast(transform.position, 0.1f, step.normalized, out RaycastHit hit,
                    step.magnitude, ~0, QueryTriggerInteraction.Collide))
            {
                var player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.ApplyDamage(m_damage);
                    Expire();
                    return;
                }
            }
            transform.position += step;
            m_lifetime -= Time.deltaTime;
            if (m_lifetime <= 0f) Expire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!gameObject.activeSelf) return;
            if (other.GetComponentInParent<WaveBlocker>() != null) { Expire(); return; }
            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null) return;
            player.ApplyDamage(m_damage);
            Expire();
        }

        private void LateUpdate()
        {
            if (m_camera != null) m_glyph.transform.rotation = m_camera.rotation;
        }

        private void Expire()
        {
            if (!s_active.Remove(this)) return;
            s_pool.Release(this);
        }

        private void OnDestroy() => s_active.Remove(this);
    }
}
