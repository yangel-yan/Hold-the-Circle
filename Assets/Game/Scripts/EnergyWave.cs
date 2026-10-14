using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// A projectile energy wave fired from the player's hand.
    /// Flies straight, damages the first enemy it touches, and recycles itself
    /// on hit or after its lifetime expires. Reused through an object pool.
    /// Lance specialization grants pierce: the wave keeps flying through up to
    /// PierceCount additional enemies before expiring.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class EnergyWave : MonoBehaviour
    {
        private static readonly List<EnergyWave> s_active = new(64);
        // Cyan-blue tint matching the wave's emissive material, reused for the hit burst.
        private static readonly Color k_hitTint = new Color(0.6f, 0.95f, 1f, 1f);
        // Golden tint for critical hits so a crit reads instantly as "big".
        private static readonly Color k_critTint = new Color(1f, 0.82f, 0.25f, 1f);
        // Ricochet stage colors: streak heats up cyan -> gold -> orange as it bounces.
        private static readonly Color[] k_ricochetTints =
        {
            new(0.6f, 0.95f, 1f, 1f),
            new(1f, 0.82f, 0.25f, 1f),
            new(1f, 0.45f, 0.1f, 1f),
            new(1f, 0.25f, 0.4f, 1f)
        };

        // Each successive bounce damps damage and amps crit reward to keep the
        // power fantasy escalating without invalidating the first hit.
        private const float k_ricochetDamageDamping = 0.85f;
        private const float k_ricochetSpeedDamping = 0.6f;
        private const float k_ricochetCritBoost = 0.35f;
        private const float k_ricochetSeekRadius = 8f;
        private const float k_ricochetSeekHalfAngle = 110f;
        private const float k_ricochetSnapStrength = 0.85f;
        private const float k_ricochetSurfaceOffset = 0.25f;
        private const float k_ricochetCooldownInSeconds = 0.05f;

        private Rigidbody m_rigidbody;
        private float m_damage;
        private float m_lifetimeRemaining;
        private int m_remainingPierce;
        private int m_remainingRicochet;
        private int m_ricochetStage;
        private float m_ricochetCooldown;
        private bool m_isActive;
        private bool m_isCrit;

        // Tracks enemies already hit by this wave so a single pierce shot can't
        // double-dip on the same target (collider may re-trigger as it exits).
        private readonly HashSet<IDamageable> m_hitTargets = new();

        // Run-wide event bus + state, used to broadcast hits/kills to relics.
        private RunState m_runState;

        // Raised when this wave wants to return to its pool.
        private Action<EnergyWave> m_onExpired;

        // Motion streak built at runtime so each shot reads as a fast-moving
        // beam rather than a floating dot. Cleared on Launch so a pooled wave
        // doesn't draw a line from its previous flight.
        private TrailRenderer m_trail;
        private RiteGlyph m_slash;
        private Transform m_camera;
        private readonly Gradient m_gradient = new();
        private readonly GradientColorKey[] m_colors = new GradientColorKey[2];
        private readonly GradientAlphaKey[] m_alphas =
        {
            new(0.8f, 0f), new(0f, 1f)
        };

        private void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_rigidbody.useGravity = false;
            m_rigidbody.isKinematic = false;
            GetComponent<Collider>().isTrigger = true;

            BuildTrail();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                if (renderer != m_trail) renderer.enabled = false;
            m_slash = RiteGlyph.Create(transform, "Authored Slash", RiteMotif.Slash,
                new Vector2(0.55f, 0.55f), k_hitTint);
            // The legacy dot prefab has 0.04x scale. Size only its new art in meters.
            Vector3 parentScale = transform.lossyScale;
            m_slash.transform.localScale = new Vector3(
                0.55f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                0.55f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            m_camera = Camera.main != null ? Camera.main.transform : null;
        }

        public void Initialize(Action<EnergyWave> onExpired, RunState runState)
        {
            m_onExpired = onExpired;
            m_runState = runState;
        }

        // Place, aim, and launch the wave.
        public void Launch(Vector3 position, Vector3 direction, float speed, float damage, float lifetime,
            int pierceCount, bool isCrit = false, int ricochetCount = 0)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            m_damage = damage;
            m_lifetimeRemaining = lifetime;
            m_remainingPierce = pierceCount;
            m_remainingRicochet = ricochetCount;
            m_ricochetStage = 0;
            m_ricochetCooldown = 0f;
            m_isCrit = isCrit;
            m_hitTargets.Clear();
            m_isActive = true;
            if (!s_active.Contains(this)) s_active.Add(this);

            // Recolor the trail so crit shots streak gold; normal shots cyan.
            if (m_trail != null)
            {
                m_trail.Clear();
                ApplyTrailTint(isCrit ? k_critTint : k_hitTint);
            }

            m_rigidbody.linearVelocity = direction.normalized * speed;
        }

        // Emissive streak that fades quickly behind the wave.
        private void BuildTrail()
        {
            m_trail = gameObject.GetComponent<TrailRenderer>();
            if (m_trail == null) m_trail = gameObject.AddComponent<TrailRenderer>();

            m_trail.time = 0.14f;
            m_trail.startWidth = 0.1f;
            m_trail.endWidth = 0f;
            m_trail.minVertexDistance = 0.015f;
            m_trail.autodestruct = false;
            m_trail.emitting = true;

            m_trail.sharedMaterial = RiteGlyph.RibbonMaterial;
            m_trail.textureMode = LineTextureMode.Stretch;
            m_trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ApplyTrailTint(k_hitTint);
        }

        // Recolor the existing trail (gradient + emissive material) so the same
        // pooled wave can streak cyan or gold depending on the crit roll.
        private void ApplyTrailTint(Color tint)
        {
            if (m_trail == null) return;

            m_colors[0] = new GradientColorKey(tint, 0f);
            m_colors[1] = new GradientColorKey(tint, 1f);
            m_gradient.SetKeys(m_colors, m_alphas);
            m_trail.colorGradient = m_gradient;
            if (m_slash != null) m_slash.SetTint(tint);
        }

        private void LateUpdate()
        {
            if (m_isActive && m_camera != null)
            {
                // Rotate only the art; the projectile's direction and collider are unchanged.
                m_slash.transform.rotation = m_camera.rotation;
            }
        }

        private void Update()
        {
            if (!m_isActive) return;

            if (m_ricochetCooldown > 0f)
            {
                m_ricochetCooldown -= Time.deltaTime;
            }

            m_lifetimeRemaining -= Time.deltaTime;
            if (m_lifetimeRemaining <= 0f)
            {
                Expire();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!m_isActive) return;

            // Cover pillars / spatial-mesh chunks: ricochet if we still have
            // bounces left, otherwise absorb. The bounce uses an approximate
            // surface normal (incoming velocity) plus a soft snap toward the
            // nearest visible enemy so the ricochet feels like the wave is
            // actively seeking the next target rather than pinball noise.
            if (other.GetComponentInParent<WaveBlocker>() != null)
            {
                if (m_ricochetCooldown > 0f) return;

                if (m_remainingRicochet > 0 && TryRicochet(other))
                {
                    return;
                }

                HitFlashEffect.Spawn(transform.position, GetCurrentTint());
                Expire();
                return;
            }

            // Only damageable enemies react; ignore everything else.
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable is PlayerHealth) return;

            // Skip targets this wave already pierced through this flight.
            if (!m_hitTargets.Add(damageable)) return;

            // Pass the wave's travel direction so the enemy can knock back and
            // flash along the impact axis.
            Vector3 hitDirection = m_rigidbody.linearVelocity.sqrMagnitude > 0.0001f
                ? m_rigidbody.linearVelocity.normalized
                : transform.forward;
            bool wasLethal = damageable.ApplyDamage(m_damage, hitDirection);

            // Crit hits burst gold and pop bigger; normal hits stay cyan.
            // Ricochet stage overrides so later bounces visibly heat up.
            Color burstTint = m_ricochetStage > 0 ? GetCurrentTint() : (m_isCrit ? k_critTint : k_hitTint);

            // Broadcast hit / kill so relics and specs can react.
            Vector3 hitPosition = transform.position;
            Enemy enemy = damageable as Enemy;
            m_runState?.Events.RaiseEnemyHit(new EnemyHitEvent
            {
                Enemy = enemy,
                Damage = m_damage,
                Position = hitPosition,
                IsCrit = m_isCrit
            });

            if (wasLethal)
            {
                m_runState?.Events.RaiseEnemyKilled(new EnemyKilledEvent
                {
                    Enemy = enemy,
                    Position = hitPosition
                });

                // Killing blow gets the big burst; ordinary hits get the small
                // spark cluster so a kill reads as a clear escalation.
                HitFlashEffect.SpawnKill(hitPosition, burstTint);
                HitstopManager.Kill();
                DamageNumber.Spawn(hitPosition, m_damage, m_isCrit, true, m_ricochetStage);
            }
            else if (m_isCrit)
            {
                // A non-lethal crit still deserves the bigger golden burst.
                HitFlashEffect.SpawnKill(hitPosition, burstTint);
                HitstopManager.Crit();
                DamageNumber.Spawn(hitPosition, m_damage, true, false, m_ricochetStage);
            }
            else
            {
                HitFlashEffect.Spawn(hitPosition, burstTint);
                HitstopManager.Hit();
                DamageNumber.Spawn(hitPosition, m_damage, false, false, m_ricochetStage);
            }

            // Lance pierce: keep flying if charges remain, otherwise expire.
            if (m_remainingPierce > 0)
            {
                m_remainingPierce--;
                return;
            }

            Expire();
        }

        // Reflect the wave off the cover surface, then bias the new direction
        // toward the nearest enemy in front so the bounce reads as a smart
        // homing strike rather than random pinball. Returns false if reflection
        // produced a degenerate direction (e.g. wave came in along the normal).
        private bool TryRicochet(Collider blocker)
        {
            Vector3 incoming = m_rigidbody.linearVelocity;
            float speed = incoming.magnitude;
            if (speed < 0.001f) return false;

            Vector3 incomingDir = incoming / speed;

            // Recover a real surface normal by raycasting backwards from the
            // wave's current position into the blocker's mesh. ClosestPoint is
            // unreliable for non-convex spatial-mesh chunks (it returns the
            // input point if we're already inside), which produced the "wave
            // explodes on contact" bug we're fixing.
            Vector3 normal = -incomingDir;
            Vector3 hitPoint = transform.position;

            // Step a bit forward into the wall, then ray back along the
            // travel direction to find the wall's actual face.
            Vector3 rayOrigin = transform.position + incomingDir * 0.6f;
            if (blocker.Raycast(new Ray(rayOrigin, -incomingDir), out RaycastHit hit, 1.5f))
            {
                normal = hit.normal;
                hitPoint = hit.point;
            }
            else
            {
                // Fallback: closest-point delta (works for convex / pillars).
                Vector3 closest = blocker.ClosestPoint(transform.position);
                Vector3 delta = transform.position - closest;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    normal = delta.normalized;
                    hitPoint = closest;
                }
            }

            Vector3 reflected = Vector3.Reflect(incomingDir, normal);
            // Keep bounces roughly horizontal so VR users don't track the wave
            // skyward or into the floor.
            reflected.y *= 0.25f;
            if (reflected.sqrMagnitude < 0.0001f) return false;
            reflected = reflected.normalized;

            // Snap a fraction of the new direction toward the nearest visible
            // enemy. We use a wide cone (110°) and high snap (0.85) so the
            // ricochet visibly homes in — pure physics on noisy spatial mesh
            // looked like the wave just exploded sideways.
            Vector3 seekDir = FindRicochetTarget(reflected);
            Vector3 finalDir = seekDir.sqrMagnitude > 0.001f
                ? Vector3.Slerp(reflected, seekDir, k_ricochetSnapStrength).normalized
                : reflected;

            // Pop the wave well clear of the surface so the same MeshCollider
            // doesn't immediately re-trigger as the wave glides along it.
            transform.position = hitPoint + normal * k_ricochetSurfaceOffset;
            transform.rotation = Quaternion.LookRotation(finalDir);

            // Slow the wave a bit so the human eye can follow the new heading.
            // High-velocity bounces just looked like a flicker.
            float newSpeed = speed * k_ricochetSpeedDamping;
            m_rigidbody.linearVelocity = finalDir * newSpeed;

            m_remainingRicochet--;
            m_ricochetStage++;
            m_ricochetCooldown = k_ricochetCooldownInSeconds;

            // Damage damps a little so reactivity stays meaningful, but the
            // visual heats up: each bounce lights the trail in a hotter tint.
            m_damage *= k_ricochetDamageDamping;

            // Bonus crit on bounced shots. Once the wave has bounced it's
            // effectively a "free" extra hit so escalating crit feels like a
            // reward for setting it up.
            if (!m_isCrit && UnityEngine.Random.value < k_ricochetCritBoost * m_ricochetStage)
            {
                m_isCrit = true;
            }

            // Visual feedback: chunky burst at the impact, then repaint trail.
            // We deliberately do NOT clear the trail so the player sees the
            // incoming streak meet the outgoing streak as a sharp angle.
            Color stageTint = GetCurrentTint();
            HitFlashEffect.SpawnKill(hitPoint, stageTint);
            ApplyTrailTint(stageTint);

            return true;
        }

        // Search for a visible enemy in a forward cone around the reflected
        // direction. Falls back to the reflection itself if none are in range,
        // so the wave still bounces in pure-physics fashion when the room is
        // empty in front of it.
        private Vector3 FindRicochetTarget(Vector3 reflectedDir)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, k_ricochetSeekRadius);
            if (hits == null || hits.Length == 0) return Vector3.zero;

            float bestScore = float.MinValue;
            Vector3 bestDir = Vector3.zero;
            float halfAngleCos = Mathf.Cos(k_ricochetSeekHalfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null) continue;

                var damageable = c.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable is PlayerHealth) continue;
                if (m_hitTargets.Contains(damageable)) continue;

                Vector3 toTarget = c.bounds.center - transform.position;
                float dist = toTarget.magnitude;
                if (dist < 0.05f) continue;

                Vector3 dir = toTarget / dist;
                float dot = Vector3.Dot(dir, reflectedDir);
                if (dot < halfAngleCos) continue;

                // Prefer alignment over proximity so the wave commits to a
                // clear target instead of darting back into the wall.
                float score = dot - dist * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                }
            }

            return bestDir;
        }

        private Color GetCurrentTint()
        {
            int idx = Mathf.Clamp(m_ricochetStage, 0, k_ricochetTints.Length - 1);
            return k_ricochetTints[idx];
        }

        private void Expire()
        {
            if (!m_isActive) return;

            m_isActive = false;
            s_active.Remove(this);
            m_rigidbody.linearVelocity = Vector3.zero;
            m_onExpired?.Invoke(this);
        }

        public static void ClearAll()
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                if (s_active[i] != null) s_active[i].Expire();
            }
            s_active.Clear();
        }

        private void OnDestroy()
        {
            s_active.Remove(this);
        }
    }
}
