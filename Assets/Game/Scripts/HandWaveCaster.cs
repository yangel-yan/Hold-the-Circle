using UnityEngine;
using UnityEngine.Pool;

namespace HoldTheCircle
{
    /// <summary>
    /// Attached to each hand (driven by the XR Hands hand transform).
    /// Slash-driven attack (Fruit-Ninja style): swipe the hand fast to fling an
    /// energy wave along the swing direction. Faster swings hit harder, and a
    /// hard flick fans out into an AOE blast. No held pose — just swipe — so it
    /// stays comfortable over a long session. There is no auto-fire.
    ///
    /// Pools waves to avoid runtime allocations. Reads its per-hand stats from a
    /// HandSlot in RunState and broadcasts every shot through the run event bus
    /// so relics/specs can react.
    /// </summary>
    public class HandWaveCaster : MonoBehaviour
    {
        // Cyan-blue tint matching the energy wave's emissive material.
        private static readonly Color k_glowTint = new Color(0.6f, 0.95f, 1f, 1f);
        private const float k_glowMinScaleInMeters = 0.055f;
        private const float k_glowMaxScaleInMeters = 0.13f;
        private const float k_glowPeakEmission = 1.5f;
        // Recoil pulse: on release the palm glow snaps past full size then
        // springs back. With no controllers there's no haptic "kick", so this
        // visual punch is the player's confirmation that the hand just threw.
        private const float k_recoilScaleInMeters = 0.17f;
        private const float k_recoilEmissionBoost = 1.8f;
        private const float k_recoilDurationInSeconds = 0.16f;

        [Tooltip("Prefab containing the EnergyWave component")]
        [SerializeField] private EnergyWave m_wavePrefab;
        [Tooltip("Direction sign applied to this hand's forward axis (1 or -1)")]
        [SerializeField] private float m_forwardSign = 1f;
        [Tooltip("Which physical hand this caster represents (drives gesture + Editor sim key)")]
        [SerializeField] private HandSide m_handSide = HandSide.Right;

        // Allow GameDirector to read which physical side this hand is so it
        // can wire the alternate weapon (ChainLightningCaster) onto the
        // opposite hand without scene-level prefab swaps.
        public HandSide Side => m_handSide;

        // Disable wave casting but keep the orb/glow visuals for cosmetic
        // continuity. Used when the alternate weapon (ChainLightning) takes
        // over this hand's combat role.
        private bool m_waveCastingDisabled;

        public void DisableWaveCasting()
        {
            m_waveCastingDisabled = true;
        }

        private GameConfigSO m_config;
        private RunState m_runState;
        private HandSlot m_handSlot;
        private int m_handIndex;
        private ObjectPool<EnergyWave> m_pool;
        private bool m_active;

        private HandGestureDetector m_gesture;
        private Transform m_riteRoot;
        private Transform m_chargeGlow;
        private RiteGlyph m_chargeGlyph;
        private float m_recoilTimer;

        // Visible "evolution" of the palm orb as the player unlocks
        // specialization nodes. Adds orbiting satellites, rotating rings, and
        // a brighter core so each progression node feels earned.
        private readonly System.Collections.Generic.List<Transform> m_orbitingSatellites = new();
        private int m_appliedEvolutionTier = -1;
        private float m_satelliteSpinPhase;

        private void Awake()
        {
            m_pool = new ObjectPool<EnergyWave>(
                createFunc: CreateWave,
                actionOnGet: wave => wave.gameObject.SetActive(true),
                // Null-guard for teardown: Unity may have killed the wave
                // GameObject before the pool gets a chance to release it.
                actionOnRelease: wave => { if (wave != null) wave.gameObject.SetActive(false); },
                actionOnDestroy: wave => { if (wave != null) Destroy(wave.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 64);

            BuildChargeGlow();

            m_gesture = gameObject.AddComponent<HandGestureDetector>();
            m_gesture.Initialize(m_handSide);
            m_gesture.Slashed += HandleSlashed;
            m_gesture.SetActive(false);
        }

        // Wire this caster to the active run. The director assigns each hand a
        // distinct slot so per-hand specializations stay independent.
        public void Configure(GameConfigSO config, RunState runState, int handIndex)
        {
            m_config = config;
            m_runState = runState;
            m_handIndex = handIndex;
            m_handSlot = runState != null ? runState.GetHand(handIndex) : null;
        }

        // Enable/disable gesture-driven attacks (combat on, menus off). Kept the
        // SetFiring name so the GameDirector wiring stays untouched.
        public void SetFiring(bool isFiring)
        {
            m_active = isFiring;
            if (m_gesture != null) m_gesture.SetActive(isFiring);
            bool showRite = isFiring && !m_waveCastingDisabled;
            if (m_riteRoot != null) m_riteRoot.gameObject.SetActive(showRite);
            for (int i = 0; i < m_orbitingSatellites.Count; i++)
            {
                if (m_orbitingSatellites[i] != null)
                    m_orbitingSatellites[i].gameObject.SetActive(showRite);
            }
            if (!isFiring)
            {
                m_recoilTimer = 0f;
                SetGlowCharge(0f);
            }
        }

        private void Update()
        {
            if (!m_active || m_config == null) return;

            if (m_gesture != null)
            {
                float fireRate = m_handSlot != null
                    ? m_handSlot.FireRateMultiplier
                    : 1f;
                m_gesture.SetFireRateMultiplier(fireRate);
            }

            // Mirror the live spec-tree progression: every time the hand has
            // a new node unlocked, rebuild the palm visual to a brighter form.
            int currentTier = m_handSlot != null ? m_handSlot.UnlockedNodeCount : 0;
            if (currentTier != m_appliedEvolutionTier)
            {
                ApplyEvolutionTier(currentTier);
            }

            // Spin the orbiting satellites every frame so the palm reads as
            // "alive" even while idle.
            UpdateSatellites();

            // Slash recoil overrides the blade glow while it decays so the
            // strike reads as a distinct "kick".
            if (m_recoilTimer > 0f)
            {
                m_recoilTimer -= Time.deltaTime;
                ApplyRecoilVisual();
                return;
            }

            // Otherwise the glow tracks the live hand swing speed so the blade
            // energizes as the player winds up a swipe.
            SetGlowCharge(m_gesture != null ? m_gesture.SpeedCharge : 0f);
        }

        // Fired by the detector on a fast hand swipe. dir is the horizontal
        // swing direction; power01 (0..1) comes from swing speed and maps to
        // damage + AOE — flick harder, hit harder.
        private void HandleSlashed(Vector3 dir, float power01)
        {
            if (!m_active || m_config == null) return;
            if (m_waveCastingDisabled) return;

            FireWave(dir, Mathf.Clamp01(power01));

            m_recoilTimer = k_recoilDurationInSeconds;
            ApplyRecoilVisual();
        }

        private void FireWave(Vector3 slashDir, float charge01)
        {
            Vector3 forward = slashDir.sqrMagnitude > 0.0001f
                ? slashDir.normalized
                : transform.forward * m_forwardSign;

            // Charge scales damage from the min to the max multiplier on top of
            // the hand's own damage multiplier.
            float chargeDamageMul = Mathf.Lerp(
                m_config.MinChargeDamageMultiplier, m_config.MaxChargeDamageMultiplier, charge01);
            float damage = m_config.WaveDamage
                * (m_handSlot != null ? m_handSlot.DamageMultiplier : 1f)
                * chargeDamageMul;

            int pierce = m_handSlot != null ? m_handSlot.PierceCount : 0;
            int ricochet = m_handSlot != null ? m_handSlot.RicochetCount : 0;

            // Spec/relic extra projectiles always apply; a full-charge throw
            // adds a bonus fan on top for a satisfying AOE payoff.
            int extra = m_handSlot != null ? m_handSlot.ExtraProjectiles : 0;
            if (charge01 >= m_config.FullChargeSpreadThreshold)
            {
                extra += m_config.FullChargeBonusWaves;
            }

            // Center wave plus a symmetric spread fan for the extras. Each
            // projectile rolls its own crit so a wide fan can light up gold.
            LaunchWave(forward, damage, pierce, ricochet);

            if (extra > 0)
            {
                float half = m_handSlot != null ? m_handSlot.SpreadHalfAngleInDegrees : 8f;
                for (int i = 1; i <= extra; i++)
                {
                    int step = (i + 1) / 2;
                    float sign = (i % 2 == 1) ? 1f : -1f;
                    float angle = half * step * sign;
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    LaunchWave(dir, damage, pierce, ricochet);
                }
            }

            // Broadcast a single WaveFired event per throw (center wave).
            m_runState?.Events.RaiseCombatAction(new CombatActionEvent
            {
                HandIndex = m_handIndex,
                Kind = CombatActionKind.SlashWave
            });
            m_runState?.Events.RaiseWaveFired(new WaveFiredEvent
            {
                HandIndex = m_handIndex,
                Origin = transform.position,
                Direction = forward,
                Damage = damage
            });
        }

        private void LaunchWave(Vector3 direction, float damage, int pierce, int ricochet)
        {
            bool isCrit = RollCrit();
            float finalDamage = isCrit ? damage * GetCritMultiplier() : damage;

            EnergyWave wave = m_pool.Get();
            wave.Launch(
                transform.position,
                direction,
                m_config.WaveSpeedInMetersPerSecond,
                finalDamage,
                m_config.WaveLifetimeInSeconds,
                pierce,
                isCrit,
                ricochet);
        }

        private bool RollCrit()
        {
            float chance = m_config.BaseCritChance + (m_handSlot != null ? m_handSlot.BonusCritChance : 0f);
            return Random.value < chance;
        }

        private float GetCritMultiplier()
        {
            return m_config.CritDamageMultiplier + (m_handSlot != null ? m_handSlot.BonusCritMultiplier : 0f);
        }

        private EnergyWave CreateWave()
        {
            EnergyWave wave = Instantiate(m_wavePrefab);
            wave.Initialize(ReturnToPool, m_runState);
            return wave;
        }

        private void ReturnToPool(EnergyWave wave)
        {
            m_pool.Release(wave);
        }

        // A fixed lance inscription frames a smaller ember that reacts to swing speed.
        private void BuildChargeGlow()
        {
            var sigil = new GameObject("Palm Rite").transform;
            sigil.SetParent(transform, false);
            m_riteRoot = sigil;
            RiteGlyph.Create(sigil, "Lance Inscription", RiteMotif.Lance,
                new Vector2(0.22f, 0.22f), new Color(1f, 0.81f, 0.52f, 0.75f));
            m_chargeGlyph = RiteGlyph.Create(sigil, "PalmChargeGlow", RiteMotif.Ember, Vector2.one, k_glowTint);
            m_chargeGlow = m_chargeGlyph.transform;
            m_chargeGlow.localPosition = new Vector3(0f, -0.026f, -0.001f);
            m_chargeGlow.localScale = Vector3.one * k_glowMinScaleInMeters;

            SetGlowCharge(0f);
            sigil.gameObject.SetActive(false);
        }

        // charge: 0 = idle/dim, 1 = fully charged/bright.
        private void SetGlowCharge(float charge)
        {
            if (m_chargeGlow == null) return;

            float eased = charge * charge;
            float scale = Mathf.Lerp(k_glowMinScaleInMeters, k_glowMaxScaleInMeters, eased);
            m_chargeGlow.localScale = Vector3.one * scale;

            Color ink = Color.Lerp(k_glowTint * 0.6f, k_glowTint * k_glowPeakEmission, eased);
            ink.a = 0.65f + charge * 0.3f;
            m_chargeGlyph.SetTint(ink);
        }

        // Recoil pulse: snap the glow oversized + over-bright at the instant of
        // firing, then spring it back down over the recoil window. recoil01
        // runs 1 -> 0 as the timer drains.
        private void ApplyRecoilVisual()
        {
            if (m_chargeGlow == null) return;

            float recoil01 = Mathf.Clamp01(m_recoilTimer / k_recoilDurationInSeconds);
            float scale = Mathf.Lerp(k_glowMinScaleInMeters, k_recoilScaleInMeters, recoil01);
            m_chargeGlow.localScale = Vector3.one * scale;

            Color emission = new Color(k_glowTint.r, k_glowTint.g, k_glowTint.b) * (k_recoilEmissionBoost * recoil01);
            emission.a = 0.9f;
            m_chargeGlyph.SetTint(emission);
        }

        // Evolve the palm orb each time the player unlocks a spec-tree node.
        // tier 0 = base (no satellites), 1 = single satellite + tint warm,
        // tier 2 = three satellites + bigger orb, tier 3 = five satellites +
        // a high-emission core. Each tier is additive on top of the last.
        private void ApplyEvolutionTier(int tier)
        {
            // Wipe any existing satellites so the rebuild is idempotent —
            // useful when the player restarts a run mid-session.
            for (int i = m_orbitingSatellites.Count - 1; i >= 0; i--)
            {
                if (m_orbitingSatellites[i] != null)
                {
                    Destroy(m_orbitingSatellites[i].gameObject);
                }
            }
            m_orbitingSatellites.Clear();

            int satellites = 0;
            float baseScaleMul = 1f;
            Color satelliteTint = new(0.55f, 0.85f, 1f, 1f);
            float emissionMul = 1f;

            switch (tier)
            {
                case 0:
                    satellites = 0;
                    break;
                case 1:
                    satellites = 1;
                    baseScaleMul = 1.15f;
                    satelliteTint = new(1f, 0.85f, 0.4f, 1f);
                    emissionMul = 1.3f;
                    break;
                case 2:
                    satellites = 3;
                    baseScaleMul = 1.35f;
                    satelliteTint = new(0.7f, 0.9f, 1f, 0.85f);
                    emissionMul = 1.6f;
                    break;
                default:
                    satellites = 5;
                    baseScaleMul = 1.55f;
                    satelliteTint = new(1f, 0.83f, 0.53f, 0.9f);
                    emissionMul = 1.6f;
                    break;
            }

            // Bump the orb's emission baseline so visually it gets noticeably
            // brighter at every tier, even before charging.
            if (m_chargeGlyph != null)
            {
                Color ink = k_glowTint * (0.6f * emissionMul);
                ink.a = 0.75f;
                m_chargeGlyph.SetTint(ink);
            }

            for (int i = 0; i < satellites; i++)
            {
                RiteGlyph mark = RiteGlyph.Create(m_riteRoot, $"RiteMark_{i}", RiteMotif.Lance,
                    new Vector2(0.042f, 0.042f), satelliteTint);
                m_orbitingSatellites.Add(mark.transform);
            }

            // Boost the core orb's idle scale so each evolution is also a
            // size jump — instantly readable progression in peripheral VR vision.
            if (m_chargeGlow != null && tier > 0)
            {
                m_chargeGlow.localScale = Vector3.one * (k_glowMinScaleInMeters * baseScaleMul);
            }

            m_appliedEvolutionTier = tier;
        }

        private void UpdateSatellites()
        {
            if (m_orbitingSatellites.Count == 0) return;

            m_satelliteSpinPhase += Time.deltaTime * 0.6f;
            float radius = 0.1f + m_appliedEvolutionTier * 0.008f;

            for (int i = 0; i < m_orbitingSatellites.Count; i++)
            {
                Transform sat = m_orbitingSatellites[i];
                if (sat == null) continue;

                float angle = m_satelliteSpinPhase + (Mathf.PI * 2f * i / m_orbitingSatellites.Count);
                Vector3 local = new(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.002f);
                sat.localPosition = local;
                sat.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
            }
        }

        private void OnDestroy()
        {
            if (m_gesture != null)
            {
                m_gesture.Slashed -= HandleSlashed;
            }

            m_pool?.Dispose();
            m_pool = null;
        }
    }
}
