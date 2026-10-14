using System;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoldTheCircle
{
    /// <summary>Compact, self-initializing combat view. Gameplay event contracts remain unchanged.</summary>
    public class GameHUD : MonoBehaviour
    {
        // Retire the serialized Canvas readout while retaining legacy result entry points.
        [SerializeField] private GameObject m_gameplayRoot;
        [SerializeField] private GameObject m_gameOverRoot;
        [SerializeField] private TMP_Text m_resultLabel;
        [SerializeField] private UnityEngine.UI.Button m_restartButton;

        private static readonly Color k_healthTint = new(0.65f, 0.85f, 0.95f);
        private static readonly Color k_sealTint = new(0.91f, 0.74f, 0.45f);
        private static readonly Color k_focusTint = new(0.5f, 0.75f, 1f);
        private static readonly Color k_warningTint = new(1f, 0.43f, 0.23f);

        private PlayerHealth m_playerHealth;
        private WaveSystem m_waveSystem;
        private RunState m_runState;
        private RitePanel m_panel;
        private RiteMeter m_health;
        private RiteMeter m_seal;
        private RiteMeter m_focus;
        private Label m_score;
        private Label m_kills;
        private Label m_wave;
        private Label m_combo;
        private Label m_shards;
        private Label m_warning;
        private Transform m_camera;
        private bool m_visible;
        private bool m_panelVisible;
        private int m_lastInvaders = -1;
        private bool m_lastOverheated;
        private int m_lastHealth = -1;
        private int m_lastMaxHealth = -1;

        public event Action RestartRequested;

        private void Awake()
        {
            if (m_gameplayRoot != null) m_gameplayRoot.SetActive(false);
            if (m_gameOverRoot != null) m_gameOverRoot.SetActive(false);
            m_camera = Camera.main != null ? Camera.main.transform : null;
            m_panel = new RitePanel("Lantern Combat Readout", new Vector2(920, 195));
            VisualElement heading = RitePanel.Row(m_panel.Root, "rite-hud__headline");
            RitePanel.Label(heading, "SCORE", "rite-hud__minor");
            m_score = RitePanel.Label(heading, "0", "rite-hud__score");
            m_kills = RitePanel.Label(heading, "0 KILLS", "rite-hud__minor");
            m_wave = RitePanel.Label(heading, "WAVE 1", "rite-hud__wave");
            m_shards = RitePanel.Label(heading, "0 SHARDS", "rite-hud__minor");
            m_combo = RitePanel.Label(heading, "", "rite-hud__combo");
            VisualElement meters = RitePanel.Row(m_panel.Root, "rite-hud__meters");
            m_health = new RiteMeter(meters, "VITAL", RiteMotif.Aegis, k_healthTint);
            m_seal = new RiteMeter(meters, "SEAL", RiteMotif.Lantern, k_sealTint);
            m_focus = new RiteMeter(meters, "FOCUS", RiteMotif.Storm, k_focusTint);
            m_warning = RitePanel.Label(m_panel.Root, "", "rite-hud__warning");
            m_panel.SetVisible(false);
        }

        private void OnEnable()
        {
            if (m_restartButton != null) m_restartButton.onClick.AddListener(HandleRestartClicked);
        }

        private void OnDisable()
        {
            if (m_restartButton != null) m_restartButton.onClick.RemoveListener(HandleRestartClicked);
            Unbind();
            m_panelVisible = false;
            m_panel?.SetVisible(false);
        }

        private void OnDestroy()
        {
            m_panel?.Dispose();
        }

        private void LateUpdate()
        {
            bool visible = m_visible && (WorldMenu.Current == null || !WorldMenu.Current.IsOpen);
            if (visible != m_panelVisible)
            {
                m_panelVisible = visible;
                m_panel.SetVisible(visible);
            }
            if (visible && m_camera != null) m_panel.Place(m_camera, 2.2f, 0.55f);
        }

        public void Bind(PlayerHealth playerHealth, WaveSystem waveSystem)
        {
            Unbind();
            m_playerHealth = playerHealth;
            m_waveSystem = waveSystem;
            if (m_playerHealth != null)
            {
                m_playerHealth.HealthChanged += HandleHealthChanged;
                HandleHealthChanged(m_playerHealth.CurrentHealth, m_playerHealth.MaxHealth);
            }
            if (m_waveSystem != null)
            {
                m_waveSystem.WaveStarted += HandleWaveStarted;
                HandleWaveStarted(m_waveSystem.CurrentWaveIndex);
            }
        }

        public void BindRunState(RunState runState)
        {
            UnbindRun();
            m_runState = runState;
            if (m_runState == null) return;
            m_runState.ShardsChanged += HandleShardsChanged;
            m_runState.ScoreChanged += HandleScoreChanged;
            m_runState.Events.ComboChanged += HandleComboChanged;
            HandleShardsChanged(m_runState.Shards);
            HandleScoreChanged(m_runState.Score, m_runState.Kills);
            HandleComboChanged(0);
        }

        public void ShowGameplay()
        {
            m_visible = true;
            if (m_gameOverRoot != null) m_gameOverRoot.SetActive(false);
            m_wave.style.display = DisplayStyle.Flex;
            m_health.Root.style.display = DisplayStyle.Flex;
            m_panelVisible = false;
            m_panel.SetVisible(false);
            m_lastInvaders = -1;
        }

        public void HideAll()
        {
            m_visible = false;
            m_panelVisible = false;
            m_panel?.SetVisible(false);
            if (m_gameOverRoot != null) m_gameOverRoot.SetActive(false);
        }

        public void SetCombatStatus(float circle, float focus, bool overheated, int invaders)
        {
            if (!m_visible) return;
            m_seal.Set(circle, circle < 0.35f ? k_warningTint : k_sealTint);
            m_focus.Set(focus, overheated ? k_warningTint : k_focusTint);
            if (m_lastInvaders == invaders && m_lastOverheated == overheated) return;
            m_lastInvaders = invaders;
            m_lastOverheated = overheated;
            L10n.Set(m_warning, overheated
                ? (invaders > 0 ? $"LEFT HAND COOLING  /  {invaders} BREACHING" : "LEFT HAND COOLING")
                : (invaders > 0 ? $"{invaders} BREACHING THE SEAL" : ""));
        }

        public void HideWaveCounter() => m_wave.style.display = DisplayStyle.None;
        public void HideHealthBar() => m_health.Root.style.display = DisplayStyle.None;

        public void ShowFinalScore(int score, int kills) =>
            ShowLegacyResult($"GAME OVER\nSCORE  {score:N0}\n{kills} kills");

        public void ShowDemoComplete(int score, int kills) =>
            ShowLegacyResult($"VERTICAL SLICE COMPLETE\nSCORE  {score:N0}\n{kills} kills");

        public void ShowGameOver(int reachedWave) => ShowLegacyResult($"You reached wave {reachedWave}");

        public void ShowRunSummary(bool victory, int reachedWave, int payout, int totalShards)
        {
            string headline = victory ? "Sentinels Down\nVictory" : $"Defeated at wave {reachedWave}";
            ShowLegacyResult($"{headline}\n+{payout} shards\nTotal: {totalShards}");
        }

        private void Unbind()
        {
            if (m_playerHealth != null) m_playerHealth.HealthChanged -= HandleHealthChanged;
            if (m_waveSystem != null) m_waveSystem.WaveStarted -= HandleWaveStarted;
            UnbindRun();
        }

        private void UnbindRun()
        {
            if (m_runState == null) return;
            m_runState.ShardsChanged -= HandleShardsChanged;
            m_runState.ScoreChanged -= HandleScoreChanged;
            m_runState.Events.ComboChanged -= HandleComboChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            int health = Mathf.CeilToInt(current);
            int maximum = Mathf.CeilToInt(max);
            if (health == m_lastHealth && maximum == m_lastMaxHealth) return;
            m_lastHealth = health;
            m_lastMaxHealth = maximum;
            m_health.Set(max > 0 ? current / max : 0f,
                current < max * 0.3f ? k_warningTint : k_healthTint, health + " / " + maximum);
        }

        private void HandleWaveStarted(int waveIndex) => L10n.Set(m_wave, "WAVE " + waveIndex);
        private void HandleShardsChanged(int shards) => L10n.Set(m_shards, shards + " SHARDS");

        private void HandleScoreChanged(int score, int kills)
        {
            L10n.Set(m_score, score.ToString("N0"));
            L10n.Set(m_kills, kills + " KILLS");
        }

        private void HandleComboChanged(int combo)
        {
            L10n.Set(m_combo, combo >= 2 ? combo + "x" : "");
            m_combo.style.color = combo >= 15 ? k_warningTint : k_sealTint;
        }

        private void ShowLegacyResult(string text)
        {
            HideAll();
            if (m_gameOverRoot != null) m_gameOverRoot.SetActive(true);
            if (m_resultLabel != null) L10n.Set(m_resultLabel, text);
        }

        private void HandleRestartClicked() => RestartRequested?.Invoke();
    }
}
