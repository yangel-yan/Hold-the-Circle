using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Left-hand "Chain Lightning" weapon — counterpart to the right-hand
    /// slash-wave caster. Designed to feel completely different so the two
    /// hands have distinct combat roles:
    ///
    /// - Right hand: discrete swings, single-target damage, ricochet.
    /// - Left hand (this): continuous beam, AOE chain, focus-bar managed.
    ///
    /// While the hand points roughly forward, this caster:
    /// 1. Picks the closest enemy in a forward cone as the primary target.
    /// 2. Damages that target on a tick interval (small DPS).
    /// 3. Visually draws a procedural lightning polyline from the palm to the
    ///    primary target and 2-3 jumps to nearby enemies.
    /// 4. Drains a "focus" bar; when depleted the beam shuts off until it
    ///    refills (player must let go).
    ///
    /// VR-comfort: no aiming gestures, no held fist — just hold the hand
    /// roughly outstretched. The detection cone is generous (60deg) so the
    /// player isn't fighting precision aim.
    /// </summary>
    public class ChainLightningCaster : MonoBehaviour
    {
        // Tuning constants. Picked to feel like a steady "drain" beam rather
        // than an overpowered lazer; the right hand keeps burst damage, this
        // hand keeps sustained pressure on multiple targets.
        private const float k_aimConeHalfAngle = 60f;
        private const float k_maxRangeInMeters = 9f;
        private const float k_tickIntervalInSeconds = 0.12f;
        private const float k_baseDamagePerTick = 0.45f;
        private const float k_chainFalloff = 0.7f;
        private const int k_maxChainTargets = 4;
        private const float k_chainJumpRadius = 3.5f;
        private const float k_focusMax = 3.0f;
        private const float k_focusDrainPerSecond = 1f;
        private const float k_focusRegenPerSecond = 1.4f;
        private const float k_focusOverheatPenalty = 0.8f;
        private const float k_aimAssistMinDot = 0.3f;
        private const int k_lightningSegments = 14;
        private const float k_lightningJitterAmplitude = 0.08f;
        private const float k_lightningWidth = 0.04f;

        private static readonly Color k_chainTint = new(0.55f, 0.85f, 1f, 1f);
        private static readonly Color k_chainTintHot = new(1f, 0.55f, 0.85f, 1f);

        [Tooltip("Direction sign applied to forward axis (matches right hand convention)")]
        [SerializeField] private float m_forwardSign = 1f;

        private GameConfigSO m_config;
        private RunState m_runState;
        private HandSlot m_handSlot;
        private int m_handIndex;
        private bool m_active;

        private float m_focus = k_focusMax;
        private bool m_overheated;
        private float m_tickTimer;
        private float m_jitterTime;
        private bool m_hadActiveChain;

        private readonly List<IDamageable> m_chainBuffer = new(k_maxChainTargets);
        private readonly List<IDamageable> m_candidates = new(64);
        private readonly List<LineRenderer> m_segments = new();
        private Transform m_riteRoot;
        private Transform m_palmGlow;
        private RiteGlyph m_palmGlyph;
        private LineRenderer m_focusArc;

        public float FocusRatio => Mathf.Clamp01(m_focus / k_focusMax);
        public bool Overheated => m_overheated;

        public void Configure(GameConfigSO config, RunState runState, int handIndex)
        {
            m_config = config;
            m_runState = runState;
            m_handIndex = handIndex;
            m_handSlot = runState != null ? runState.GetHand(handIndex) : null;
        }

        public void SetFiring(bool isFiring)
        {
            m_active = isFiring;
            if (m_riteRoot != null) m_riteRoot.gameObject.SetActive(isFiring);
            if (!isFiring)
            {
                ClearLightningSegments();
                ApplyPalmIntensity(0f);
            }
        }

        public void ResetForNewRun()
        {
            m_focus = k_focusMax;
            m_overheated = false;
            m_tickTimer = 0f;
            m_chainBuffer.Clear();
            m_hadActiveChain = false;
        }

        private void Awake()
        {
            BuildPalmGlow();
            EnsureLightningSegments();
        }

        private void Update()
        {
            if (!m_active || m_runState == null || HitstopManager.IsPaused ||
                !HandTrackingGate.IsTracked(HandSide.Left))
            {
                m_hadActiveChain = false;
                ClearLightningSegments();
                ApplyPalmIntensity(0f);
                return;
            }

            m_chainBuffer.Clear();
            BuildChain(m_chainBuffer);
            UpdateFocus(Time.deltaTime, m_chainBuffer.Count > 0);

            if (m_overheated || m_focus <= 0.01f)
            {
                m_hadActiveChain = false;
                ClearLightningSegments();
                ApplyPalmIntensity(0.15f);
                return;
            }

            if (m_chainBuffer.Count > 0)
            {
                if (!m_hadActiveChain)
                {
                    m_runState.Events.RaiseCombatAction(new CombatActionEvent
                    {
                        HandIndex = m_handIndex,
                        Kind = CombatActionKind.ChainLightning
                    });
                    m_hadActiveChain = true;
                }

                m_tickTimer -= Time.deltaTime;
                if (m_tickTimer <= 0f)
                {
                    ApplyChainDamage(m_chainBuffer);
                    float fireRate = m_handSlot != null
                        ? Mathf.Clamp(m_handSlot.FireRateMultiplier, 0.5f, 4f)
                        : 1f;
                    m_tickTimer = k_tickIntervalInSeconds / fireRate;
                }
                DrawLightning(m_chainBuffer);
                ApplyPalmIntensity(0.7f + Mathf.PingPong(Time.time * 7f, 0.3f));
            }
            else
            {
                m_hadActiveChain = false;
                // No targets: idle palm pulse so the player still sees the
                // weapon is "armed" and ready.
                ClearLightningSegments();
                ApplyPalmIntensity(0.3f);
            }
        }

        private void UpdateFocus(float dt, bool hasTargets)
        {
            if (m_overheated)
            {
                m_focus = Mathf.Min(k_focusMax, m_focus + dt * k_focusRegenPerSecond);
                if (m_focus >= k_focusMax * 0.6f) m_overheated = false;
            }
            else if (hasTargets)
            {
                m_focus -= dt * k_focusDrainPerSecond;
                if (m_focus <= 0f)
                {
                    m_focus = -k_focusOverheatPenalty;
                    m_overheated = true;
                }
            }
            else
            {
                m_focus = Mathf.Min(k_focusMax, m_focus + dt * k_focusRegenPerSecond * 0.7f);
            }
        }

        private void BuildChain(List<IDamageable> output)
        {
            m_candidates.Clear();
            foreach (Enemy enemy in Enemy.Active) m_candidates.Add(enemy);
            foreach (BossSentinel sentinel in BossSentinel.Active) m_candidates.Add(sentinel);
            Vector3 origin = transform.position;
            Vector3 aim = (transform.forward * m_forwardSign).normalized;
            float cosCone = Mathf.Cos(k_aimConeHalfAngle * Mathf.Deg2Rad);

            // Pick the primary by best alignment with aim direction within cone.
            IDamageable primary = null;
            float bestScore = float.NegativeInfinity;
            var candidates = m_candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                IDamageable enemy = candidates[i];
                if (enemy == null || enemy.IsDead) continue;

                Vector3 to = AimPosition(enemy) - origin;
                float dist = to.magnitude;
                if (dist < 0.05f || dist > k_maxRangeInMeters) continue;
                Vector3 dir = to / dist;
                float dot = Vector3.Dot(dir, aim);
                if (dot < k_aimAssistMinDot) continue;

                float score = dot * 2f - dist * 0.05f;
                if (score > bestScore && dot >= cosCone)
                {
                    bestScore = score;
                    primary = enemy;
                }
            }

            if (primary == null) return;
            output.Add(primary);

            // Chain forward: each next link picks the closest enemy not yet
            // chained, within k_chainJumpRadius of the previous link.
            int slotChainBonus = m_handSlot != null ? m_handSlot.RicochetCount : 0;
            int maxLinks = Mathf.Min(k_maxChainTargets, 2 + slotChainBonus);
            while (output.Count < maxLinks)
            {
                IDamageable last = output[output.Count - 1];
                IDamageable next = FindClosestUnchainedEnemy(AimPosition(last), output);
                if (next == null) break;
                output.Add(next);
            }
        }

        private IDamageable FindClosestUnchainedEnemy(Vector3 from, List<IDamageable> chained)
        {
            var candidates = m_candidates;
            IDamageable best = null;
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                IDamageable enemy = candidates[i];
                if (enemy == null || enemy.IsDead) continue;
                if (chained.Contains(enemy)) continue;
                float d = (AimPosition(enemy) - from).sqrMagnitude;
                if (d <= k_chainJumpRadius * k_chainJumpRadius && d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            return best;
        }

        private void ApplyChainDamage(List<IDamageable> chain)
        {
            float damageMul = m_handSlot != null ? m_handSlot.DamageMultiplier : 1f;
            float critChance = m_config != null ? m_config.BaseCritChance : 0.1f;
            critChance += m_handSlot != null ? m_handSlot.BonusCritChance : 0f;
            float critMul = m_config != null ? m_config.CritDamageMultiplier : 2f;
            critMul += m_handSlot != null ? m_handSlot.BonusCritMultiplier : 0f;

            float currentDamage = k_baseDamagePerTick * damageMul;
            for (int i = 0; i < chain.Count; i++)
            {
                IDamageable enemy = chain[i];
                if (enemy == null || enemy.IsDead) continue;

                bool isCrit = Random.value < critChance;
                float dmg = isCrit ? currentDamage * critMul : currentDamage;
                Vector3 dir = (AimPosition(enemy) - transform.position).normalized;
                bool wasLethal = enemy.ApplyDamage(dmg, dir);

                Vector3 hitPos = AimPosition(enemy);
                m_runState?.Events.RaiseEnemyHit(new EnemyHitEvent
                {
                    Enemy = enemy as Enemy,
                    Damage = dmg,
                    Position = hitPos,
                    IsCrit = isCrit
                });

                Color tickTint = m_overheated ? k_chainTintHot : k_chainTint;

                if (wasLethal)
                {
                    m_runState?.Events.RaiseEnemyKilled(new EnemyKilledEvent
                    {
                        Enemy = enemy as Enemy,
                        Position = hitPos
                    });
                    HitFlashEffect.SpawnKill(hitPos, tickTint);
                    HitstopManager.Kill();
                    DamageNumber.Spawn(hitPos, dmg, isCrit, true);
                }
                else if (isCrit)
                {
                    HitFlashEffect.SpawnKill(hitPos, tickTint);
                    HitstopManager.Crit();
                    DamageNumber.Spawn(hitPos, dmg, true, false);
                }
                else
                {
                    HitFlashEffect.Spawn(hitPos, tickTint);
                    DamageNumber.Spawn(hitPos, dmg, false, false);
                }

                currentDamage *= k_chainFalloff;
            }
        }

        private void DrawLightning(List<IDamageable> chain)
        {
            EnsureLightningSegments();
            int needed = chain.Count;
            for (int i = 0; i < m_segments.Count; i++)
            {
                m_segments[i].enabled = i < needed;
            }
            if (needed == 0) return;

            m_jitterTime += Time.deltaTime * 24f;

            Vector3 prev = transform.position;
            Color tint = m_overheated ? k_chainTintHot : k_chainTint;
            for (int i = 0; i < needed; i++)
            {
                IDamageable enemy = chain[i];
                if (enemy == null || enemy.IsDead) { m_segments[i].enabled = false; continue; }
                Vector3 endPoint = AimPosition(enemy);
                DrawJitteredLine(m_segments[i], prev, endPoint, tint, i);
                prev = endPoint;
            }
        }

        private void DrawJitteredLine(LineRenderer line, Vector3 a, Vector3 b, Color tint, int seed)
        {
            line.positionCount = k_lightningSegments + 1;
            line.startWidth = k_lightningWidth;
            line.endWidth = k_lightningWidth * 0.8f;

            Vector3 axis = b - a;
            Vector3 perp1 = Vector3.Cross(axis, Vector3.up);
            if (perp1.sqrMagnitude < 0.001f) perp1 = Vector3.Cross(axis, Vector3.right);
            perp1 = perp1.normalized;
            Vector3 perp2 = Vector3.Cross(axis.normalized, perp1).normalized;

            for (int i = 0; i <= k_lightningSegments; i++)
            {
                float t = (float)i / k_lightningSegments;
                Vector3 basePoint = a + axis * t;

                // Endpoints anchor to the targets; middle bows out with jitter.
                float jitterAmount = (i == 0 || i == k_lightningSegments)
                    ? 0f
                    : k_lightningJitterAmplitude;

                float ox = Mathf.PerlinNoise(t * 4f + seed * 11f, m_jitterTime + seed * 2.7f) - 0.5f;
                float oy = Mathf.PerlinNoise(t * 4f + seed * 17f + 100f, m_jitterTime * 1.3f) - 0.5f;
                Vector3 offset = perp1 * (ox * jitterAmount * 2f) + perp2 * (oy * jitterAmount * 2f);
                line.SetPosition(i, basePoint + offset);
            }

            tint.a = Mathf.Lerp(0.9f, 0.55f, seed / 3f);
            line.startColor = tint;
            tint.a *= 0.65f;
            line.endColor = tint;
        }

        private void EnsureLightningSegments()
        {
            while (m_segments.Count < k_maxChainTargets)
            {
                var go = new GameObject($"ChainSegment_{m_segments.Count}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.numCornerVertices = 2;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                lr.sharedMaterial = RiteGlyph.RibbonMaterial;
                lr.textureMode = LineTextureMode.Stretch;
                lr.enabled = false;

                m_segments.Add(lr);
            }
        }

        private void ClearLightningSegments()
        {
            for (int i = 0; i < m_segments.Count; i++)
            {
                if (m_segments[i] != null) m_segments[i].enabled = false;
            }
        }

        private static Vector3 AimPosition(IDamageable target)
        {
            return target is Enemy enemy ? enemy.AimPosition : ((BossSentinel)target).AimPosition;
        }

        private void BuildPalmGlow()
        {
            var sigil = new GameObject("Chain Rite").transform;
            sigil.SetParent(transform, false);
            m_riteRoot = sigil;
            m_focusArc = RuneVisual.Arc(sigil, "Focus Arc", 0.09f, 15, 320,
                new Color(0.48f, 0.78f, 1f, 0.75f), 0.009f);
            m_focusArc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            m_palmGlyph = RiteGlyph.Create(sigil, "ChainPalmGlow", RiteMotif.Storm,
                Vector2.one, k_chainTint);
            m_palmGlow = m_palmGlyph.transform;
            ApplyPalmIntensity(0f);
            sigil.gameObject.SetActive(false);
        }

        private void ApplyPalmIntensity(float intensity01)
        {
            if (m_palmGlyph == null) return;
            Color tint = m_overheated ? k_chainTintHot : k_chainTint;
            float t = Mathf.Clamp01(intensity01);
            Color ink = tint * (0.7f + t * 0.6f);
            ink.a = 0.7f + t * 0.2f;
            m_palmGlyph.SetTint(ink);
            m_focusArc.startColor = m_focusArc.endColor = new Color(ink.r, ink.g, ink.b, FocusRatio * 0.65f);
            if (m_palmGlow != null)
            {
                m_palmGlow.localScale = Vector3.one * Mathf.Lerp(0.17f, 0.21f, t);
            }
        }
    }
}
