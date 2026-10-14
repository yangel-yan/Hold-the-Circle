using TMPro;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Cinematic moments triggered at run pacing beats:
    /// - Wave start: pulsing "WAVE N" banner + radial rings expanding from
    ///   the player + brief world-tint flash so combat onset feels weighty
    ///   rather than just enemies appearing.
    /// - Wave clear: shower of glowing shards rising from killed enemies +
    ///   "CLEAR" announcement that punches and fades.
    /// - Upgrade unlock: omnidirectional pulse around the player + a hold
    ///   on the new orb evolution so the buff is seen, not just felt.
    /// All visuals are code-built and self-destruct after their lifetime.
    /// VR-safe: no motion that disagrees with head tracking, no flashing
    /// brighter than the URP bloom envelope.
    /// </summary>
    public static class WaveCinematics
    {
        private static GameObject s_banner;
        private static readonly Color k_waveStartTint = new(1f, 0.79f, 0.48f, 1f);
        private static readonly Color k_waveClearTint = new(0.65f, 0.85f, 1f, 1f);
        private static readonly Color k_unlockTint = new(1f, 0.86f, 0.6f, 1f);
        private static readonly Color k_bossTint = new(1f, 0.69f, 0.4f, 1f);

        public static void PlayWaveStart(Transform player, int waveIndex)
        {
            if (player == null) return;
            SpawnBanner(player, $"WAVE {waveIndex}", k_waveStartTint, 1.2f);
            SpawnExpandingRing(player.position, k_waveStartTint, 5.5f, 0.7f);
            HitstopManager.Cinematic(0.12f, 0.35f);
        }

        public static void PlayWaveClear(Transform player, int waveIndex)
        {
            if (player == null) return;
            SpawnBanner(player, "CLEAR", k_waveClearTint, 1.4f);
            SpawnExpandingRing(player.position, k_waveClearTint, 6.5f, 0.9f);
            SpawnShardFountain(player.position, k_waveClearTint);
            HitstopManager.Cinematic(0.18f, 0.2f);
        }

        public static void PlayUnlock(Transform player, string nodeName)
        {
            if (player == null) return;
            string text = string.IsNullOrEmpty(nodeName) ? "UNLOCKED" : nodeName.ToUpperInvariant();
            SpawnBanner(player, text, k_unlockTint, 1.6f);
            SpawnExpandingRing(player.position, k_unlockTint, 4f, 0.6f);
            SpawnShardFountain(player.position, k_unlockTint);
            HitstopManager.Cinematic(0.15f, 0.3f);
        }

        public static void PlayBossEntrance(Transform player)
        {
            if (player == null) return;
            SpawnBanner(player, "BOSS", k_bossTint, 1.8f);
            SpawnExpandingRing(player.position, k_bossTint, 8f, 1.2f);
            HitstopManager.Cinematic(0.25f, 0.1f);
        }

        // World-space text banner roughly 3m in front of the player at eye
        // height. Punches in, holds, then drifts up and fades.
        private static void SpawnBanner(Transform player, string text, Color tint, float lifetime)
        {
            if (s_banner != null) Object.Destroy(s_banner);
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            float eyeHeight = Camera.main != null ? Camera.main.transform.position.y : player.position.y + 1.5f;
            Vector3 position = player.position + forward * 3f;
            position.y = eyeHeight + 0.95f;

            var go = new GameObject("CinematicBanner");
            s_banner = go;
            go.transform.position = position;
            var canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(go.transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(720f, 80f);
            rect.localScale = Vector3.one * 0.0025f;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);
            var label = textObj.AddComponent<TextMeshProUGUI>();
            L10n.Set(label, text);
            label.fontSize = 38f;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = tint;
            label.enableWordWrapping = false;
            label.raycastTarget = false;

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.sizeDelta = rect.sizeDelta;
            labelRect.anchoredPosition = Vector2.zero;

            var ticker = go.AddComponent<CinematicBannerTicker>();
            ticker.Bind(label, lifetime, player);
        }

        public static void ClearBanner()
        {
            if (s_banner != null) Object.Destroy(s_banner);
        }

        private static void SpawnExpandingRing(Vector3 center, Color tint, float maxRadius, float lifetime)
        {
            var go = new GameObject("CinematicRing");
            go.transform.position = new Vector3(center.x, 0.04f, center.z);
            var ticker = go.AddComponent<CinematicRingTicker>();
            ticker.Build(tint, maxRadius, lifetime);
        }

        // A burst of small glowing shards rising and drifting outward — feels
        // like loot popping out of dispatched enemies.
        private static void SpawnShardFountain(Vector3 center, Color tint)
        {
            var go = new GameObject("ShardFountain");
            go.transform.position = new Vector3(center.x, 0.16f, center.z);

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.065f);
            main.startColor = tint;
            main.gravityModifier = 0.4f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var col = particles.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.1f;
            renderer.sharedMaterial = RiteGlyph.RibbonMaterial;

            particles.Play();
            Object.Destroy(go, 2.5f);
        }
    }

    // Drives the banner's punch-in / hold / fade-out + camera-facing.
    public class CinematicBannerTicker : MonoBehaviour
    {
        private TMP_Text m_text;
        private float m_lifetime;
        private float m_elapsed;
        private Transform m_player;
        private Transform m_camera;
        private Vector3 m_startPosition;

        public void Bind(TMP_Text text, float lifetime, Transform player)
        {
            m_text = text;
            m_lifetime = lifetime;
            m_player = player;
            m_camera = Camera.main != null ? Camera.main.transform : null;
            m_startPosition = transform.position;
        }

        private void LateUpdate()
        {
            m_elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_elapsed / m_lifetime);

            float punchT = Mathf.Clamp01(m_elapsed / 0.25f);
            float punch = punchT < 1f
                ? Mathf.Sin(punchT * Mathf.PI) * 0.12f + Mathf.Lerp(0.8f, 1f, punchT)
                : 1f;

            float fadeT = Mathf.Clamp01((t - 0.65f) / 0.35f);
            float scale = punch * (1f - fadeT * 0.3f);
            transform.localScale = Vector3.one * scale;

            transform.position = m_startPosition + Vector3.up * (t * 0.06f);

            if (m_camera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - m_camera.position);
            }

            if (m_text != null)
            {
                Color c = m_text.color;
                c.a = 1f - fadeT;
                m_text.color = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }

    // Procedural expanding emissive ring on the floor plane around the player.
    // Built from a flat torus so it reads as a "shockwave" without needing a
    // particle system per-call.
    public class CinematicRingTicker : MonoBehaviour
    {
        private LineRenderer m_ring;
        private Color m_tint;
        private float m_lifetime;
        private float m_elapsed;
        private float m_maxRadius;

        public void Build(Color tint, float maxRadius, float lifetime)
        {
            m_tint = tint;
            m_lifetime = lifetime;
            m_maxRadius = maxRadius;

            m_ring = RuneVisual.Arc(transform, "Etched Ritual Pulse", 1f, 0f, 360f, tint, 0.025f);
            transform.localScale = Vector3.one * 0.1f;
        }

        private void Update()
        {
            m_elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_elapsed / m_lifetime);

            // Ease-out radius growth so the ring looks fast then settles.
            float radius = Mathf.SmoothStep(0f, m_maxRadius, t);
            transform.localScale = Vector3.one * Mathf.Max(0.1f, radius);
            m_ring.startWidth = m_ring.endWidth = 0.025f / Mathf.Max(0.1f, radius);
            Color ink = m_tint;
            ink.a = 0.55f * (1f - t) * (1f - t);
            m_ring.startColor = m_ring.endColor = ink;

            if (t >= 1f) Destroy(gameObject);
        }

    }
}
