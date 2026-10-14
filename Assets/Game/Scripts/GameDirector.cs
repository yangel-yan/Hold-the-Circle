using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    public enum GameState
    {
        Booting,
        Lobby,
        Playing,
        MapSelect,
        Dead
    }

    /// <summary>
    /// Top-level state machine and orchestrator for the roguelike loop:
    /// Booting -> Playing -> Dead -> (restart) -> Playing.
    /// Wires together player health, hand wave casters, the wave system, and the HUD.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameConfigSO m_config;

        [Header("Scene References")]
        [SerializeField] private PlayerHealth m_playerHealth;
        [SerializeField] private Transform m_playerTransform;
        [SerializeField] private WaveSystem m_waveSystem;
        [SerializeField] private GameHUD m_hud;
        [SerializeField] private List<HandWaveCaster> m_handCasters = new();
        private readonly List<ChainLightningCaster> m_chainCasters = new();

        [Header("Debug / Test")]
        [Tooltip("Relics granted at the start of every run. Used in W1 to verify the SO -> effect -> event-bus chain end to end.")]
        [SerializeField] private List<RelicSO> m_startingRelics = new();

        [Header("Specializations")]
        [Tooltip("Three branch SOs (Lance / Storm / Aegis) presented at run start so each hand can pick one.")]
        [SerializeField] private List<SpecializationSO> m_specializationBranches = new();

        [Header("Relics")]
        [Tooltip("Pool of relic SOs the relic system can offer. Empty pool falls back to the upgrade path.")]
        [SerializeField] private List<RelicSO> m_relicPool = new();
        [Tooltip("Number of cleared waves between relic offerings. Only used when no map system is active.")]
        [SerializeField] private int m_wavesPerRelic = 3;

        [Header("MR Mode")]
        [Tooltip("Enable Mixed Reality: skip procedural pillars and let PICO's spatial mesh (real walls / furniture) act as wave-blocking cover.")]
        [SerializeField] private bool m_useRealWorldCover;

        [Header("Endless Mode")]
        [Tooltip("Skip the map/upgrade flow and just stream enemies in continuously with a difficulty ramp. The arena keeps filling and the player keeps mowing it down — no wave counter, no menus.")]
        [SerializeField] private bool m_endlessMode = true;

        [Header("Vertical Slice Demo")]
        [Tooltip("Run the short tutorial -> normal encounter -> relic -> elite encounter demo instead of the normal route.")]
        [SerializeField] private bool m_verticalSliceDemo = true;

        private GameState m_state = GameState.Booting;

        // Single source of truth for the active run. Replaces the old RunModifiers.
        private readonly RunState m_runState = new();
        private ArenaBuilder m_arenaBuilder;
        private ArenaAudioSystem m_arenaAudio;
        private ComboSystem m_comboSystem;
        private UpgradeSystem m_upgradeSystem;
        private SpecializationPicker m_specPicker;
        private RelicSystem m_relicSystem;
        private MapSystem m_mapSystem;
        private BossSystem m_bossSystem;
        private MerchantSystem m_merchantSystem;
        private EventNodeSystem m_eventSystem;
        private SpatialMeshCoverSystem m_spatialMeshCover;
        private CameraEffectsDirector m_cameraEffects;
        private VerticalSliceDemoPresenter m_demoPresenter;
        private int m_wavesSinceLastRelic;
        private DemoPhase m_demoPhase;
        private CircleIntegrity m_circle;
        private bool m_hasResult;
        private bool m_hasFocus = true;
        private Transform[] m_hands;
        private Camera m_camera;
        private float m_pauseHold;
        private bool m_menuButtonHeld;
        // The node the player is currently resolving (combat/elite/treasure/...).
        private MapNode m_activeNode;

        public GameState State => m_state;
        public RunState RunState => m_runState;

        private enum DemoPhase
        {
            Inactive,
            Tutorial,
            NormalCombat,
            RelicChoice,
            EliteCombat,
            Complete
        }

        private void OnEnable()
        {
            if (m_playerHealth != null) m_playerHealth.Died += HandlePlayerDied;
            if (m_hud != null) m_hud.RestartRequested += HandleRestartRequested;
        }

        private void OnDisable()
        {
            if (m_playerHealth != null) m_playerHealth.Died -= HandlePlayerDied;
            if (m_hud != null) m_hud.RestartRequested -= HandleRestartRequested;
            if (m_waveSystem != null)
            {
                m_waveSystem.WaveCleared -= HandleWaveCleared;
                m_waveSystem.WaveStarted -= HandleWaveStartedCinematic;
            }
            if (m_upgradeSystem != null) m_upgradeSystem.UpgradePicked -= HandleUpgradePicked;
            if (m_specPicker != null) m_specPicker.Completed -= HandleSpecPickerCompleted;
            if (m_relicSystem != null) m_relicSystem.RelicPicked -= HandleRelicPicked;
            if (m_mapSystem != null) m_mapSystem.NodeSelected -= HandleNodeSelected;
            if (m_bossSystem != null) m_bossSystem.Defeated -= HandleBossDefeated;
            if (m_merchantSystem != null) m_merchantSystem.Resolved -= HandleMerchantResolved;
            if (m_eventSystem != null) m_eventSystem.Resolved -= HandleEventResolved;
            if (m_runState != null) m_runState.Events.EnemyKilled -= HandleEnemyKilledForShards;
            if (m_demoPresenter != null)
            {
                m_demoPresenter.RightSlashLearned -= HandleDemoRightSlashLearned;
                m_demoPresenter.TutorialCompleted -= HandleDemoTutorialCompleted;
            }
        }

        private void Start()
        {
            m_hands = CollectHandTransforms();
            m_camera = Camera.main;
            RetireLegacySceneTemplates();
            // Initialize the run-wide state with one slot per hand caster, then
            // wire every dependent system to the same RunState instance.
            m_runState.Initialize(m_handCasters.Count);

            if (m_playerHealth != null) m_playerHealth.BindRunState(m_runState);
            m_runState.BindPlayerHealth(m_playerHealth);
            m_waveSystem.Initialize(m_config, m_playerTransform, m_runState);

            for (int i = 0; i < m_handCasters.Count; i++)
            {
                m_handCasters[i].Configure(m_config, m_runState, i);

                // Left hand replaces its slash-wave with the chain-lightning
                // beam so the two hands have distinct combat roles. We keep
                // the HandWaveCaster present (for its palm orb visuals) but
                // disable its actual wave casting.
                if (m_handCasters[i].Side == HandSide.Left)
                {
                    m_handCasters[i].DisableWaveCasting();
                    var chain = m_handCasters[i].gameObject.GetComponent<ChainLightningCaster>();
                    if (chain == null)
                    {
                        chain = m_handCasters[i].gameObject.AddComponent<ChainLightningCaster>();
                    }
                    chain.Configure(m_config, m_runState, i);
                    m_chainCasters.Add(chain);
                }
            }

            // Build the roguelike systems at runtime; no prefab/scene wiring.
            m_arenaBuilder = gameObject.AddComponent<ArenaBuilder>();
            m_arenaBuilder.Initialize(m_playerTransform);

            // ArenaBuilder honours the MR toggle in every environment so the
            // Editor preview matches the device build (no hidden virtual
            // pillars when the designer enabled real-world cover).
            m_arenaBuilder.SetUseRealWorldCover(m_useRealWorldCover);
            m_circle = gameObject.AddComponent<CircleIntegrity>();
            m_circle.Initialize(m_arenaBuilder);
            m_circle.Failed += HandlePlayerDied;

            // SpatialMeshCoverSystem only attaches on real PICO devices —
            // its native subsystem can crash Editor Play when no headset is
            // connected. The MeshPrefab shipped by the SDK already carries
            // a MeshCollider, so on device we just need to tag chunks.
            if (m_useRealWorldCover && IsRealDeviceRuntime())
            {
                m_spatialMeshCover = gameObject.AddComponent<SpatialMeshCoverSystem>();
                m_spatialMeshCover.Initialize();
            }

            m_arenaAudio = gameObject.AddComponent<ArenaAudioSystem>();
            m_arenaAudio.Initialize(m_runState, m_playerTransform);

            m_cameraEffects = gameObject.AddComponent<CameraEffectsDirector>();
            m_cameraEffects.Initialize(m_runState, m_playerHealth);
            ArenaSkyEnvironment.Apply(m_camera, ArenaSkyEnvironment.UsePassthrough);

            m_comboSystem = gameObject.AddComponent<ComboSystem>();
            m_comboSystem.Initialize(m_runState);

            m_demoPresenter = gameObject.AddComponent<VerticalSliceDemoPresenter>();
            m_demoPresenter.Initialize(m_runState, m_playerTransform);
            m_demoPresenter.RightSlashLearned += HandleDemoRightSlashLearned;
            m_demoPresenter.TutorialCompleted += HandleDemoTutorialCompleted;

            m_upgradeSystem = gameObject.AddComponent<UpgradeSystem>();
            m_upgradeSystem.Initialize(m_runState, m_playerTransform, CollectHandTransforms());

            m_specPicker = gameObject.AddComponent<SpecializationPicker>();
            m_specPicker.Initialize(m_runState, m_playerTransform, CollectHandTransforms(),
                m_specializationBranches != null ? m_specializationBranches.ToArray() : null);

            m_relicSystem = gameObject.AddComponent<RelicSystem>();
            m_relicSystem.Initialize(m_runState, m_playerTransform, CollectHandTransforms(), m_relicPool);

            m_mapSystem = gameObject.AddComponent<MapSystem>();
            m_mapSystem.Initialize(m_runState, m_playerTransform, CollectHandTransforms());

            m_bossSystem = gameObject.AddComponent<BossSystem>();
            m_bossSystem.Initialize(m_config, m_playerTransform, m_runState);

            m_merchantSystem = gameObject.AddComponent<MerchantSystem>();
            m_merchantSystem.Initialize(m_runState, m_relicSystem, m_playerTransform, CollectHandTransforms());

            m_eventSystem = gameObject.AddComponent<EventNodeSystem>();
            m_eventSystem.Initialize(m_runState, m_playerHealth, m_playerTransform, CollectHandTransforms());

            m_waveSystem.WaveCleared += HandleWaveCleared;
            m_waveSystem.WaveStarted += HandleWaveStartedCinematic;
            m_upgradeSystem.UpgradePicked += HandleUpgradePicked;
            m_specPicker.Completed += HandleSpecPickerCompleted;
            m_relicSystem.RelicPicked += HandleRelicPicked;
            m_mapSystem.NodeSelected += HandleNodeSelected;
            m_bossSystem.Defeated += HandleBossDefeated;
            m_merchantSystem.Resolved += HandleMerchantResolved;
            m_eventSystem.Resolved += HandleEventResolved;
            m_runState.Events.EnemyKilled += HandleEnemyKilledForShards;

            if (m_hud != null)
            {
                m_hud.Bind(m_playerHealth, m_waveSystem);
                m_hud.BindRunState(m_runState);
            }

            m_arenaBuilder.Rebuild();
            AudioListener.volume = PlayerPrefs.GetInt("HTC.Muted", 0) == 1 ? 0 : 1;
            ShowLobby();
        }

        private void ShowLobby()
        {
            CancelInvoke();
            HitstopManager.SetPaused(false);
            m_state = GameState.Lobby;
            SetCastersFiring(false);
            m_circle.SetCombatActive(false);
            m_waveSystem.StopRun();
            EnemyProjectile.ClearAll();
            EnergyWave.ClearAll();
            m_bossSystem.StopEncounter();
            m_merchantSystem.Close();
            m_eventSystem.Close();
            m_hud?.HideAll();
            m_demoPresenter?.Hide();
            WorldMenu.Show(m_playerTransform, CollectHandTransforms(), "KEEP THE LANTERN LIT",
                $"Right hand slashes; left hand chains. Hold both hands together at your chest to pause.  " +
                $"{MetaProgression.RunsCompleted} runs completed.",
                new WorldMenu.Choice("BEGIN A RUN", "Six connected encounters\nRelics, pacts and twin sentinels", () => StartMode(false, false)),
                new WorldMenu.Choice("LEARN THE RITES", "Guided tutorial\nTwo short encounters", () => StartMode(true, false)),
                new WorldMenu.Choice("ENDLESS", "Continuous invasion\nSurvive for score", () => StartMode(false, true)),
                new WorldMenu.Choice("SETTINGS", "Audio and comfort", ShowSettings));
        }

        private void RetireLegacySceneTemplates()
        {
            // These scene objects were visible authoring examples, never encounter instances.
            foreach (BossSentinel sentinel in FindObjectsByType<BossSentinel>(FindObjectsSortMode.None))
            {
                sentinel.gameObject.SetActive(false);
            }
            foreach (BossHealthBar bar in FindObjectsByType<BossHealthBar>(FindObjectsSortMode.None))
            {
                bar.gameObject.SetActive(false);
            }
            foreach (EnemyProjectile projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                projectile.gameObject.SetActive(false);
            }
            Transform oldGuide = transform.Find("VerticalSliceGuide");
            if (oldGuide != null) oldGuide.gameObject.SetActive(false);
        }

        private void StartMode(bool demo, bool endless)
        {
            m_verticalSliceDemo = demo;
            m_endlessMode = endless;
            StartRun();
        }

        private void ShowSettings()
        {
            bool muted = PlayerPrefs.GetInt("HTC.Muted", 0) == 1;
            WorldMenu.Show(m_playerTransform, CollectHandTransforms(), "SETTINGS",
                "Settings are saved for your next session.",
                new WorldMenu.Choice(muted ? "ENABLE AUDIO" : "MUTE AUDIO", "All game sound",
                    () => { PlayerPrefs.SetInt("HTC.Muted", muted ? 0 : 1); PlayerPrefs.Save(); AudioListener.volume = muted ? 1 : 0; ShowSettings(); }),
                new WorldMenu.Choice("中文 / ENGLISH", "Choose your language", ShowLanguageSettings),
                new WorldMenu.Choice(ArenaSkyEnvironment.UsePassthrough ? "SHOW NIGHT SKY" : "SHOW REAL ROOM",
                    ArenaSkyEnvironment.UsePassthrough ? "Switch to the lantern night sky" : "Switch to MR passthrough",
                    () => { ArenaSkyEnvironment.SetPassthrough(!ArenaSkyEnvironment.UsePassthrough, m_camera); ShowSettings(); }),
                new WorldMenu.Choice("RETURN", "Back to the lantern", ShowLobby));
        }

        private void ShowLanguageSettings()
        {
            WorldMenu.Show(m_playerTransform, CollectHandTransforms(), "中文 / ENGLISH",
                "Choose your language",
                new WorldMenu.Choice("简体中文", "使用中文界面", () => { L10n.SetLocale("zh-Hans"); ShowSettings(); }),
                new WorldMenu.Choice("ENGLISH", "Use the English interface", () => { L10n.SetLocale("en"); ShowSettings(); }),
                new WorldMenu.Choice("RETURN", "Back to settings", ShowSettings));
        }

        private void Update()
        {
#if UNITY_EDITOR
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                (m_state == GameState.Playing || m_state == GameState.MapSelect)) PauseRun();
#endif
            UpdatePauseInput();
            if (m_circle != null)
                m_hud?.SetCombatStatus(m_circle.Ratio,
                    m_chainCasters.Count > 0 ? m_chainCasters[0].FocusRatio : 1f,
                    m_chainCasters.Count > 0 && m_chainCasters[0].Overheated, m_circle.Invaders);
        }

        private void UpdatePauseInput()
        {
            var controller = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            bool menuHeld = controller.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool pressed)
                && pressed;
            bool menuPressed = menuHeld && !m_menuButtonHeld;
            m_menuButtonHeld = menuHeld;
            if (m_state != GameState.Playing || WorldMenu.Current != null && WorldMenu.Current.IsOpen)
            {
                m_pauseHold = 0f;
                return;
            }
            if (menuPressed)
            {
                PauseRun();
                return;
            }
            // Intentional hands-only pause: hold both wrists together below the gaze for 1.2 seconds.
            if (m_hands == null || m_hands.Length < 2 || m_camera == null ||
                m_hands[0] == null || m_hands[1] == null ||
                !HandTrackingGate.IsTracked(HandSide.Left) || !HandTrackingGate.IsTracked(HandSide.Right))
            {
                m_pauseHold = 0f;
                return;
            }
            Vector3 midpoint = (m_hands[0].position + m_hands[1].position) * 0.5f;
            Vector3 relative = m_camera.transform.InverseTransformPoint(midpoint);
            bool holding = (m_hands[0].position - m_hands[1].position).sqrMagnitude < 0.0121f &&
                Mathf.Abs(relative.x) < 0.25f && relative.y > -0.65f && relative.y < -0.18f &&
                relative.z > 0.2f && relative.z < 0.65f;
            m_pauseHold = holding ? m_pauseHold + Time.unscaledDeltaTime : 0f;
            if (m_pauseHold >= 1.2f)
            {
                m_pauseHold = 0f;
                PauseRun();
            }
        }

        public void PauseRun()
        {
            if (HitstopManager.IsPaused || WorldMenu.Current != null && WorldMenu.Current.IsOpen) return;
            HitstopManager.SetPaused(true);
            SetCastersFiring(false);
            WorldMenu.Show(m_playerTransform, CollectHandTransforms(), "THE LANTERN WAITS",
                "Take a breath. The encounter is paused.",
                new WorldMenu.Choice("RESUME", "Continue this encounter", ResumeRun),
                new WorldMenu.Choice("RETURN TO LANTERN", "End this run", ShowLobby));
        }

        private void ResumeRun()
        {
            if (!m_hasFocus)
            {
                WorldMenu.Show(m_playerTransform, CollectHandTransforms(), "THE LANTERN WAITS",
                    "Resume when the headset is active.",
                    new WorldMenu.Choice("RESUME", "Continue this encounter", ResumeRun));
                return;
            }
            HitstopManager.SetPaused(false);
            SetCastersFiring(m_state == GameState.Playing);
        }

        private void OnApplicationPause(bool paused)
        {
#if !UNITY_EDITOR
            m_hasFocus = !paused;
            if (paused && m_state == GameState.Playing) PauseRun();
            if (!paused) ArenaSkyEnvironment.Apply(m_camera, ArenaSkyEnvironment.UsePassthrough);
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
#if !UNITY_EDITOR
            m_hasFocus = focused;
            if (!focused && m_state == GameState.Playing) PauseRun();
            if (focused) ArenaSkyEnvironment.Apply(m_camera, ArenaSkyEnvironment.UsePassthrough);
#endif
        }

        // Editor / Standalone has no headset to source spatial-mesh data from
        // and the SDK's native init can crash Play in that situation. MR cover
        // is therefore only enabled on Android device builds.
        private static bool IsRealDeviceRuntime()
        {
#if UNITY_EDITOR
            return false;
#else
            return Application.platform == RuntimePlatform.Android;
#endif
        }

        // VST layer blending is now left to PXR_Manager's own Awake configuration
        // (PXR_Manager.usePremultipliedAlpha / useLayerBlend / srcColor / ...). A
        // previous build forced One / OneMinusSrcAlpha here, but URP's post stack
        // doesn't output predivided alpha, so the framebuffer alpha stayed at 1
        // and the VST passthrough was multiplied to black. Letting PXR_Manager
        // handle this restores the default SDK compositing path that VST expects.

        private Transform[] CollectHandTransforms()
        {
            var hands = new Transform[m_handCasters.Count];
            for (int i = 0; i < m_handCasters.Count; i++)
            {
                hands[i] = m_handCasters[i] != null ? m_handCasters[i].transform : null;
            }

            return hands;
        }

        private void StartRun()
        {
            CancelInvoke();
            HitstopManager.SetPaused(false);
            m_hasResult = false;
            EnemyProjectile.ClearAll();
            EnergyWave.ClearAll();
            WorldMenu.Current?.Hide();
            m_state = GameState.Playing;

            m_runState.Reset();
            m_arenaBuilder.Rebuild();
            m_relicSystem.ResetForNewRun();
            m_mapSystem.ResetForNewRun();
            m_bossSystem.StopEncounter();
            m_comboSystem.ResetCombo();
            m_wavesSinceLastRelic = 0;
            m_activeNode = null;
            m_demoPhase = DemoPhase.Inactive;
            m_circle.ResetForNewRun();
            foreach (var chain in m_chainCasters) chain.ResetForNewRun();
            AudioListener.volume = PlayerPrefs.GetInt("HTC.Muted", 0) == 1 ? 0 : 1;

            // Hand out any starting relics through the proper SO -> effect ->
            // event-bus path. Empty by default; populated in the inspector when
            // verifying the W1 hook system.
            foreach (RelicSO relic in m_startingRelics)
            {
                m_runState.AddRelic(relic);
            }

            m_playerHealth.Initialize(m_config.PlayerMaxHealth);

            if (m_verticalSliceDemo)
            {
                BeginVerticalSliceDemo();
                return;
            }

            // If three branch SOs are configured, let each hand pick before any
            // waves spawn. Otherwise fall back to the W1 path so the project
            // still runs while the SO assets are being authored. Endless mode
            // skips the picker so the player drops straight into the fight.
            if (!m_endlessMode && m_specPicker != null && m_specPicker.HasUsableData())
            {
                SetCastersFiring(false);
                if (m_hud != null) m_hud.ShowGameplay();
                m_specPicker.Present();
                return;
            }

            BeginRouteOrFirstWave();
        }

        private void HandleSpecPickerCompleted()
        {
            if (m_state != GameState.Playing) return;
            BeginRouteOrFirstWave();
        }

        // Branch point right after spec picking: if the relic pool is rich
        // enough to drive a node map we generate one and present the first
        // floor; otherwise fall back to the linear wave loop.
        private void BeginRouteOrFirstWave()
        {
            // Endless mode bypasses the map/relic routing entirely and drops
            // straight into the continuous swarm loop.
            if (m_endlessMode)
            {
                BeginEndlessLoop();
                return;
            }

            if (m_relicSystem != null && m_relicSystem.HasUsableData())
            {
                m_mapSystem.GenerateNewMap();
                EnterMapSelect();
                return;
            }

            BeginWaveLoop();
        }

        // Continuous-spawn loop: no wave counter, no upgrade interruptions.
        private void BeginEndlessLoop()
        {
            m_state = GameState.Playing;
            m_waveSystem.StartEndless();
            SetCastersFiring(true);
            m_arenaAudio?.OnCombatStarted();

            if (m_hud != null)
            {
                m_hud.ShowGameplay();
                m_hud.HideWaveCounter();
                m_hud.HideHealthBar();
            }
        }

        private void EnterMapSelect()
        {
            EnemyProjectile.ClearAll();
            EnergyWave.ClearAll();
            m_state = GameState.MapSelect;
            SetCastersFiring(false);
            if (m_hud != null) m_hud.ShowGameplay();
            m_mapSystem.PresentCurrentFloor();
        }

        private void BeginWaveLoop()
        {
            m_state = GameState.Playing;
            m_waveSystem.StartRun();
            SetCastersFiring(true);

            if (m_hud != null)
            {
                m_hud.ShowGameplay();
            }
        }

        // Player committed to a node on the route map. Branch on its kind.
        private void HandleNodeSelected(MapNode node)
        {
            if (m_state != GameState.MapSelect) return;
            m_activeNode = node;

            // Rebuild the arena to match the node kind so each room reads
            // visually distinct (cover ring vs obelisks vs single plinth).
            m_arenaBuilder.Rebuild(LayoutForNode(node.Kind));

            switch (node.Kind)
            {
                case MapNodeKind.Combat:
                    StartNodeCombat(false);
                    break;
                case MapNodeKind.Elite:
                    StartNodeCombat(true);
                    break;
                case MapNodeKind.Treasure:
                    m_state = GameState.Playing;
                    m_relicSystem.PresentSingleChoice();
                    break;
                case MapNodeKind.Merchant:
                    m_state = GameState.Playing;
                    m_merchantSystem.Open();
                    break;
                case MapNodeKind.Event:
                    m_state = GameState.Playing;
                    m_eventSystem.Open();
                    break;
                case MapNodeKind.Boss:
                    StartBossEncounter();
                    break;
            }
        }

        // Map node kind -> arena layout preset. Merchant / Event nodes reuse
        // the combat layout so the player still has cover to read visually.
        private static ArenaLayoutKind LayoutForNode(MapNodeKind kind)
        {
            switch (kind)
            {
                case MapNodeKind.Elite:    return ArenaLayoutKind.Elite;
                case MapNodeKind.Treasure: return ArenaLayoutKind.Treasure;
                case MapNodeKind.Boss:     return ArenaLayoutKind.Boss;
                default:                    return ArenaLayoutKind.Combat;
            }
        }

        private void StartNodeCombat(bool isElite)
        {
            m_state = GameState.Playing;

            // Treat each map-driven encounter as a one-wave fight: start a
            // fresh run on the wave system so the count scales from 1, then
            // optionally inflate for elite nodes.
            m_waveSystem.StartEncounter(m_activeNode.Floor, isElite);
            SetCastersFiring(true);
            m_hud?.ShowGameplay();
            m_arenaAudio?.OnCombatStarted();
        }

        // Boss node entry: skip the wave system entirely and let the boss
        // state machine drive the encounter. HandleBossDefeated picks the
        // continuation when the twins go down.
        private void StartBossEncounter()
        {
            m_state = GameState.Playing;
            m_waveSystem.StopRun();
            SetCastersFiring(true);
            m_arenaAudio?.OnCombatStarted();
            WaveCinematics.PlayBossEntrance(m_playerTransform);
            m_bossSystem.StartEncounter();
        }

        private void HandleBossDefeated()
        {
            if (m_state != GameState.Playing) return;

            // Victory cash-out: convert the act into meta currency before the
            // map continuation puts us back into another loop.
            CashOutAndShowResult(victory: true);
        }

        // Merchant or event nodes simply roll forward when their resolved
        // event fires; both types are pure interaction with no combat.
        private void HandleMerchantResolved()
        {
            if (m_state != GameState.Playing) return;
            m_activeNode = null;
            ContinueAfterNode();
        }

        private void HandleEventResolved()
        {
            if (m_state != GameState.Playing) return;
            m_activeNode = null;
            ContinueAfterNode();
        }

        // Every kill drops a small in-run currency tip. Elite nodes layer an
        // extra payout in HandleWaveCleared so the merchant route stays
        // meaningful without needing a per-enemy elite flag.
        private void HandleEnemyKilledForShards(EnemyKilledEvent e)
        {
            m_runState.AddShards(1);

            // Arcade scoreboard: award the dead enemy's archetype-weighted
            // point value. Falls back to the baseline grunt score if the enemy
            // reference is gone (e.g. boss / projectile kills).
            int points = e.Enemy != null
                ? EnemyArchetypes.GetScore(e.Enemy.Archetype)
                : EnemyArchetypes.GetScore(EnemyArchetype.Drifter);
            m_runState.AddScore(points);
        }

        // After a non-combat node resolves (or a combat node's wave is cleared)
        // we either return to the map for the next floor or end the run.
        private void ContinueAfterNode()
        {
            SetCastersFiring(false);
            m_arenaAudio?.OnCombatEnded();

            if (m_mapSystem.IsExhausted)
            {
                CashOutAndShowResult(true);
                return;
            }

            EnterMapSelect();
        }

        // Cinematic banner + ring when a new wave kicks off so the moment of
        // combat onset feels weighty rather than just enemies appearing.
        private void HandleWaveStartedCinematic(int waveIndex)
        {
            if (m_playerTransform == null) return;
            WaveCinematics.PlayWaveStart(m_playerTransform, waveIndex);
        }

        // Inspect the soonest-to-unlock spec-tree node title across all hands.
        // Returns null when every hand has already exhausted its tree so the
        // caller can skip the banner.
        private string ExtractNextNodeNameForBanner()
        {
            if (m_runState == null) return null;
            var hands = m_runState.Hands;
            if (hands == null) return null;

            for (int i = 0; i < hands.Count; i++)
            {
                HandSlot hand = hands[i];
                if (hand == null || hand.Specialization == null) continue;
                int next = hand.UnlockedNodeCount;
                if (next < hand.Specialization.NodeCount)
                {
                    return hand.Specialization.Nodes[next].Title;
                }
            }
            return null;
        }

        // A wave was fully cleared: pause firing, advance every hand's
        // specialization tree by one node, then either return to the map (if
        // we're inside a node-driven run) or fall back to the legacy relic /
        // upgrade rotation.
        private void HandleWaveCleared(int waveIndex)
        {
            if (m_state != GameState.Playing) return;

            SetCastersFiring(false);
            EnemyProjectile.ClearAll();
            EnergyWave.ClearAll();
            m_circle.Restore(12f);

            if (m_verticalSliceDemo && HandleDemoEncounterCleared())
            {
                return;
            }

            // Cinematic clear sweep so the moment of victory feels earned.
            WaveCinematics.PlayWaveClear(m_playerTransform, waveIndex);

            // Capture the unlocked node title before advancing so we can
            // surface it in the cinematic banner.
            string nodeName = ExtractNextNodeNameForBanner();
            m_runState.UnlockNextNodeForAllHands();
            if (!string.IsNullOrEmpty(nodeName))
            {
                WaveCinematics.PlayUnlock(m_playerTransform, nodeName);
            }

            // Map-driven path: every combat/elite node clears in a single wave.
            if (m_activeNode != null)
            {
                if (m_activeNode.Kind == MapNodeKind.Elite)
                {
                    m_runState.AddShards(3);
                }
                m_relicSystem.PresentChoices();
                return;
            }

            // Legacy fallback (no map): rotate relic vs upgrade by wave count.
            m_wavesSinceLastRelic++;
            bool relicReady = m_wavesPerRelic > 0
                && m_wavesSinceLastRelic >= m_wavesPerRelic
                && m_relicSystem.HasUsableData();

            if (relicReady)
            {
                m_wavesSinceLastRelic = 0;
                m_relicSystem.PresentChoices();
                return;
            }

            m_upgradeSystem.PresentChoices();
        }

        // The player grabbed an upgrade: resume firing and start the next wave.
        private void HandleUpgradePicked()
        {
            if (m_state != GameState.Playing) return;

            SetCastersFiring(true);
            m_waveSystem.AdvanceToNextWave();
        }

        // The player grabbed a relic: same continuation as upgrades when no
        // map drives the run; otherwise return to the route picker.
        private void HandleRelicPicked()
        {
            if (m_state != GameState.Playing) return;

            if (m_verticalSliceDemo && m_demoPhase == DemoPhase.RelicChoice)
            {
                BeginDemoEliteCombat();
                return;
            }

            if (m_activeNode != null)
            {
                m_activeNode = null;
                ContinueAfterNode();
                return;
            }

            SetCastersFiring(true);
            m_waveSystem.AdvanceToNextWave();
        }

        private void HandlePlayerDied()
        {
            if (m_state != GameState.Playing) return;

            m_state = GameState.Dead;
            SetCastersFiring(false);
            m_waveSystem.StopRun();
            m_bossSystem?.StopEncounter();
            m_merchantSystem?.Close();
            m_eventSystem?.Close();
            EnemyProjectile.ClearAll();

            CashOutAndShowResult(victory: false);
        }

        private void BeginVerticalSliceDemo()
        {
            m_state = GameState.Playing;
            m_demoPhase = DemoPhase.Tutorial;
            m_waveSystem.StartTutorialWave();
            SetCastersFiring(false);
            SetRightHandFiring(true);
            m_arenaAudio?.OnCombatStarted();

            if (m_hud != null)
            {
                m_hud.ShowGameplay();
            }

            m_demoPresenter?.BeginTutorial();
        }

        private void HandleDemoRightSlashLearned()
        {
            if (m_demoPhase != DemoPhase.Tutorial) return;
            SetLeftHandFiring(true);
        }

        private void HandleDemoTutorialCompleted()
        {
            if (m_demoPhase != DemoPhase.Tutorial) return;
            SetCastersFiring(false);
            Invoke(nameof(BeginDemoNormalCombat), 0.25f);
        }

        private void BeginDemoNormalCombat()
        {
            m_demoPhase = DemoPhase.NormalCombat;
            m_waveSystem.StartDemoEncounter(false);
            SetCastersFiring(true);
            m_comboSystem.ResetCombo();
            m_demoPresenter?.ShowNormalCombat();
        }

        private bool HandleDemoEncounterCleared()
        {
            if (m_demoPhase == DemoPhase.NormalCombat)
            {
                m_demoPhase = DemoPhase.RelicChoice;
                m_arenaAudio?.OnCombatEnded();
                m_demoPresenter?.ShowRelicChoice();

                if (m_relicSystem != null && m_relicSystem.HasUsableData())
                {
                    m_relicSystem.PresentDemoChoices();
                }
                else
                {
                    BeginDemoEliteCombat();
                }
                return true;
            }

            if (m_demoPhase != DemoPhase.EliteCombat) return false;

            m_demoPhase = DemoPhase.Complete;
            m_state = GameState.Dead;
            SetCastersFiring(false);
            m_waveSystem.StopRun();
            m_arenaAudio?.OnCombatEnded();
            m_demoPresenter?.Hide();
            m_hud?.HideAll();
            ShowResultMenu(true, 0, true);
            return true;
        }

        private void BeginDemoEliteCombat()
        {
            m_demoPhase = DemoPhase.EliteCombat;
            m_waveSystem.StartDemoEncounter(true);
            SetCastersFiring(true);
            m_comboSystem.ResetCombo();
            m_arenaAudio?.OnCombatStarted();
            m_demoPresenter?.ShowEliteCombat();
            m_hud?.ShowGameplay();
        }

        private void SetRightHandFiring(bool isFiring)
        {
            foreach (HandWaveCaster caster in m_handCasters)
            {
                if (caster != null && caster.Side == HandSide.Right)
                {
                    caster.SetFiring(isFiring);
                }
            }
        }

        private void SetLeftHandFiring(bool isFiring)
        {
            foreach (ChainLightningCaster chain in m_chainCasters)
            {
                if (chain != null) chain.SetFiring(isFiring);
            }
        }

        // Convert a finished run into persistent meta progression and route
        // the player to the result screen. Boss victory awards a chunky bonus
        // on top of whatever in-run shards survived.
        private void CashOutAndShowResult(bool victory)
        {
            if (m_hasResult) return;
            m_hasResult = true;
            int payout = m_runState.Shards;
            if (victory) payout += 25;

            MetaProgression.AwardShards(payout);
            MetaProgression.NoteRunCompleted(victory);

            m_state = GameState.Dead;
            SetCastersFiring(false);
            m_waveSystem.StopRun();
            m_bossSystem?.StopEncounter();
            m_merchantSystem?.Close();
            m_eventSystem?.Close();
            m_hud?.HideAll();
            ShowResultMenu(victory, payout, false);
        }

        private void ShowResultMenu(bool victory, int payout, bool tutorial)
        {
            CancelInvoke();
            HitstopManager.SetPaused(false);
            m_circle.SetCombatActive(false);
            EnemyProjectile.ClearAll();
            EnergyWave.ClearAll();
            m_demoPresenter?.Hide();
            m_arenaAudio?.OnCombatEnded();
            WorldMenu.Show(m_playerTransform, CollectHandTransforms(),
                tutorial ? "THE RITES COMPLETE" : victory ? "THE LANTERN ENDURES" : "THE SEAL HAS FALLEN",
                $"Score {m_runState.Score:N0}  /  {m_runState.Kills} defeated\n" +
                (tutorial ? "You are ready for the path." : $"+{payout} shards  /  Total {MetaProgression.TotalShards}"),
                new WorldMenu.Choice("RETURN", "Back to the lantern", ShowLobby),
                new WorldMenu.Choice("BEGIN A RUN", "A new path awaits", () => StartMode(false, false)));
        }

        private void HandleRestartRequested()
        {
            if (m_state != GameState.Dead) return;
            StartRun();
        }

        private void SetCastersFiring(bool isFiring)
        {
            m_circle?.SetCombatActive(isFiring &&
                !(m_verticalSliceDemo && m_demoPhase == DemoPhase.Tutorial));
            foreach (HandWaveCaster caster in m_handCasters)
            {
                caster.SetFiring(isFiring);
            }
            foreach (ChainLightningCaster chain in m_chainCasters)
            {
                if (chain != null) chain.SetFiring(isFiring);
            }
        }
    }
}
