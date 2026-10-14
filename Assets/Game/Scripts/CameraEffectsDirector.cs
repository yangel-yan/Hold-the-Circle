using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoldTheCircle
{
    /// <summary>
    /// Runtime URP post-processing director. Builds (or attaches to) a global
    /// Volume with Bloom, Vignette, ColorAdjustments, and ChromaticAberration
    /// — then animates them in response to gameplay events:
    ///
    /// - On hit: brief chromatic aberration spike + saturation pop
    /// - On kill: vignette pulses inward + small bloom flash
    /// - On low HP: pulsing red vignette tint
    /// - On combo tier change: hue shifts the whole scene a touch warmer
    ///
    /// All values are kept in VR-safe ranges (no aggressive vignette darkening
    /// that would induce sickness, no DoF since VR can't tolerate forced
    /// focus, no motion blur).
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraEffectsDirector : MonoBehaviour
    {
        private const float k_baseBloomIntensity = 0.24f;
        private const float k_killBloomIntensity = 0.4f;
        private const float k_baseVignette = 0f;
        private const float k_lowHpVignette = 0.12f;
        private const float k_baseSaturation = 6f;
        private const float k_baseChroma = 0f;

        private RunState m_runState;
        private PlayerHealth m_playerHealth;

        private Volume m_volume;
        private VolumeProfile m_profile;
        private Bloom m_bloom;
        private Vignette m_vignette;
        private ColorAdjustments m_color;
        private ChromaticAberration m_chroma;

        private float m_killPulse;
        private float m_hitPulse;
        private float m_currentComboTint;
        private float m_targetComboTint;

        public void Initialize(RunState runState, PlayerHealth playerHealth)
        {
            m_runState = runState;
            m_playerHealth = playerHealth;

            BuildVolume();
            Subscribe();
        }

        private void BuildVolume()
        {
            var existing = FindFirstObjectByType<Volume>();
            if (existing != null && existing.profile != null)
            {
                m_volume = existing;
                m_profile = existing.profile;
            }
            else
            {
                var go = new GameObject("HTC_GlobalVolume");
                go.transform.SetParent(transform, false);
                m_volume = go.AddComponent<Volume>();
                m_volume.isGlobal = true;
                m_volume.priority = 10f;
                m_profile = ScriptableObject.CreateInstance<VolumeProfile>();
                m_volume.sharedProfile = m_profile;
            }

            m_bloom = GetOrAdd<Bloom>();
            m_bloom.intensity.overrideState = true;
            m_bloom.intensity.value = k_baseBloomIntensity;
            m_bloom.threshold.overrideState = true;
            // Higher threshold so only genuinely bright emissives (cores, wave
            // trails, palm orbs) bloom — keeps the room from glowing wholesale.
            m_bloom.threshold.value = 1.0f;
            m_bloom.scatter.overrideState = true;
            m_bloom.scatter.value = 0.65f;
            m_bloom.tint.overrideState = true;
            m_bloom.tint.value = new Color(0.9f, 0.97f, 1f, 1f);

            m_vignette = GetOrAdd<Vignette>();
            m_vignette.intensity.overrideState = true;
            m_vignette.intensity.value = k_baseVignette;
            m_vignette.smoothness.overrideState = true;
            m_vignette.smoothness.value = 0.4f;
            m_vignette.color.overrideState = true;
            m_vignette.color.value = new Color(0.05f, 0.0f, 0.1f, 1f);

            m_color = GetOrAdd<ColorAdjustments>();
            m_color.saturation.overrideState = true;
            m_color.saturation.value = k_baseSaturation;
            m_color.contrast.overrideState = true;
            // Mild contrast — enemies still read as silhouettes but the scene
            // doesn't crush blacks or blow highlights.
            m_color.contrast.value = 8f;
            m_color.postExposure.overrideState = true;
            m_color.postExposure.value = 0f;
            m_color.colorFilter.overrideState = true;
            m_color.colorFilter.value = Color.white;

            m_chroma = GetOrAdd<ChromaticAberration>();
            m_chroma.intensity.overrideState = true;
            m_chroma.intensity.value = k_baseChroma;
        }

        private T GetOrAdd<T>() where T : VolumeComponent
        {
            if (m_profile.TryGet<T>(out var component)) return component;
            return m_profile.Add<T>(true);
        }

        private void Subscribe()
        {
            if (m_runState != null)
            {
                m_runState.Events.EnemyHit += HandleEnemyHit;
                m_runState.Events.EnemyKilled += HandleEnemyKilled;
                m_runState.Events.ComboChanged += HandleComboChanged;
                m_runState.Events.DamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (m_runState != null)
            {
                m_runState.Events.EnemyHit -= HandleEnemyHit;
                m_runState.Events.EnemyKilled -= HandleEnemyKilled;
                m_runState.Events.ComboChanged -= HandleComboChanged;
                m_runState.Events.DamageTaken -= HandleDamageTaken;
            }
        }

        private void HandleEnemyHit(EnemyHitEvent e)
        {
            m_hitPulse = e.IsCrit ? 0.35f : 0.18f;
        }

        private void HandleEnemyKilled(EnemyKilledEvent e)
        {
            m_killPulse = 1f;
        }

        private void HandleComboChanged(int combo)
        {
            // Hue/exposure target shifts warmer as the streak climbs.
            // Capped so even a wild combo doesn't blow out the scene.
            m_targetComboTint = Mathf.Clamp01(combo / 30f);
        }

        private void HandleDamageTaken(DamageTakenEvent e)
        {
            // Red flash on player damage so the hit registers without breaking
            // VR comfort (no full screen red).
            m_killPulse = Mathf.Max(m_killPulse, 0.6f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Hit pulse: fast attack, fast decay.
            m_hitPulse = Mathf.Max(0f, m_hitPulse - dt * 4f);
            // Kill pulse: bigger, slower decay.
            m_killPulse = Mathf.Max(0f, m_killPulse - dt * 2.2f);
            // Combo tint smoothly catches up to its target.
            m_currentComboTint = Mathf.MoveTowards(m_currentComboTint, m_targetComboTint, dt * 0.5f);

            if (m_bloom != null)
            {
                m_bloom.intensity.value =
                    Mathf.Lerp(k_baseBloomIntensity, k_killBloomIntensity, m_killPulse) +
                    m_currentComboTint * 0.12f;
            }

            if (m_vignette != null)
            {
                float lowHp = ComputeLowHpFactor();
                float v = k_baseVignette + lowHp * (k_lowHpVignette - k_baseVignette);
                m_vignette.intensity.value = Mathf.Clamp(v, 0f, 0.12f);

                Color baseColor = new(0.05f, 0f, 0.1f, 1f);
                Color hurtColor = new(0.45f, 0.05f, 0.05f, 1f);
                Color comboColor = new(0.35f, 0.15f, 0f, 1f);
                Color blended = Color.Lerp(baseColor, hurtColor, lowHp);
                blended = Color.Lerp(blended, comboColor, m_currentComboTint * 0.7f);
                m_vignette.color.value = blended;
            }

            if (m_color != null)
            {
                m_color.saturation.value = k_baseSaturation + m_hitPulse * 10f + m_currentComboTint * 6f;
                m_color.postExposure.value = 0f;

                Color filter = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.78f, 1f), m_currentComboTint);
                m_color.colorFilter.value = filter;
            }

            if (m_chroma != null)
            {
                m_chroma.intensity.value = k_baseChroma;
            }
        }

        private float ComputeLowHpFactor()
        {
            if (m_playerHealth == null) return 0f;
            float maxHp = m_playerHealth.MaxHealth;
            if (maxHp <= 0f) return 0f;
            float ratio = m_playerHealth.CurrentHealth / maxHp;
            // Heartbeat-style pulse below 30% HP.
            float intensity = Mathf.Clamp01(1f - ratio / 0.3f);
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3.5f);
            return intensity * pulse;
        }
    }
}
