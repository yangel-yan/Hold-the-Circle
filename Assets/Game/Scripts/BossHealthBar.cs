using UnityEngine;
using UnityEngine.UIElements;

namespace HoldTheCircle
{
    /// <summary>Named twin meters with readable phase feedback above the combat HUD.</summary>
    public class BossHealthBar : MonoBehaviour
    {
        private Transform m_viewpoint;
        private BossSentinel m_left;
        private BossSentinel m_right;
        private BossSystem m_system;
        private RitePanel m_panel;
        private RiteMeter m_leftMeter;
        private RiteMeter m_rightMeter;
        private Label m_phase;
        private BossSystem.Phase m_lastPhase = (BossSystem.Phase)(-1);
        private float m_phaseFlashTimer;
        private Color m_phaseTint;

        public static BossHealthBar Spawn(Transform player, BossSentinel left, BossSentinel right)
        {
            var go = new GameObject("BossHealthBar");
            var bar = go.AddComponent<BossHealthBar>();
            bar.Build(player, left, right);
            return bar;
        }

        public void FlashPhase(Color tint)
        {
            m_phaseTint = tint;
            m_phaseFlashTimer = 1.2f;
        }

        private void LateUpdate()
        {
            if (m_viewpoint == null) return;
            m_panel.Place(m_viewpoint, 2.5f, 0.96f);
            UpdateMeter(m_left, m_leftMeter);
            UpdateMeter(m_right, m_rightMeter);
            if (m_system != null && m_system.CurrentPhase != m_lastPhase)
            {
                m_lastPhase = m_system.CurrentPhase;
                L10n.Set(m_phase, m_lastPhase switch
                {
                    BossSystem.Phase.Intro => "THE SENTINELS AWAKEN",
                    BossSystem.Phase.PhaseOne => "I  /  MIRRORED RITES",
                    BossSystem.Phase.Transition => "BREAK THEIR RITUAL",
                    BossSystem.Phase.PhaseTwo => "II  /  SPLIT RITES",
                    BossSystem.Phase.Enrage => "LAST SENTINEL ENRAGED",
                    BossSystem.Phase.Defeated => "THE RITES ARE BROKEN",
                    _ => ""
                });
            }
            m_phaseFlashTimer = Mathf.Max(0, m_phaseFlashTimer - Time.unscaledDeltaTime);
            m_phase.style.color = Color.Lerp(new Color(0.67f, 0.78f, 0.88f),
                m_phaseTint, Mathf.Clamp01(m_phaseFlashTimer));
        }

        private void OnDestroy() => m_panel?.Dispose();

        private void Build(Transform player, BossSentinel left, BossSentinel right)
        {
            m_viewpoint = Camera.main != null ? Camera.main.transform : player;
            m_left = left;
            m_right = right;
            m_system = FindFirstObjectByType<BossSystem>();
            m_panel = new RitePanel("Sentinel Readout", new Vector2(960, 155));
            VisualElement heading = RitePanel.Row(m_panel.Root, "rite-hud__headline");
            RitePanel.Label(heading, "TWIN SENTINELS", "rite-hud__boss-title");
            m_phase = RitePanel.Label(heading, "", "rite-hud__phase");
            VisualElement meters = RitePanel.Row(m_panel.Root, "rite-hud__meters");
            m_leftMeter = new RiteMeter(meters, "SENTINEL I", RiteMotif.Lantern, left.Tint);
            m_rightMeter = new RiteMeter(meters, "SENTINEL II", RiteMotif.Lantern, right.Tint);
        }

        private static void UpdateMeter(BossSentinel sentinel, RiteMeter meter)
        {
            bool alive = sentinel != null && !sentinel.IsDead && sentinel.MaxHealth > 0;
            meter.Set(alive ? sentinel.CurrentHealth / sentinel.MaxHealth : 0f,
                alive ? sentinel.Tint : new Color(0.35f, 0.4f, 0.46f), alive ? null : "FALLEN");
        }
    }
}
