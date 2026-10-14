using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace HoldTheCircle
{
    /// <summary>
    /// Spawns waves of orbiter enemies around the player. When all enemies in a
    /// wave die, the next (randomized) wave spawns. Supports a full reset for the
    /// restart loop. Enemies are pooled to avoid runtime allocations.
    /// </summary>
    public class WaveSystem : MonoBehaviour
    {
        [Tooltip("Prefab containing the Enemy component")]
        [SerializeField] private Enemy m_enemyPrefab;

        private GameConfigSO m_config;
        private Transform m_player;
        private RunState m_runState;
        private ObjectPool<Enemy> m_pool;
        private Vector3 m_encounterForward = Vector3.forward;

        private readonly List<Enemy> m_aliveEnemies = new(16);
        private readonly List<Enemy> m_pendingDeaths = new(16);
        private readonly List<Enemy> m_corpses = new(16);
        private bool m_clearQueued;
        private int m_currentWaveIndex;
        private bool m_isRunning;
        // Endless mode: instead of "clear wave -> pick upgrade -> next wave",
        // enemies stream in continuously and difficulty ramps on a timer. This
        // is the roguelike power-fantasy loop — the screen keeps filling and the
        // player keeps mowing it down with no menu interruptions.
        private bool m_endless;
        private float m_rampTimer;
        private float m_topUpTimer;
        private bool m_isTutorial;
        private bool m_isDemoEncounter;
        private bool m_isDemoElite;
        private int m_demoSpawnRemaining;
        private int m_demoTargetAlive;
        private float m_demoSpawnTimer;
        // Every this many seconds the virtual wave index ticks up, scaling enemy
        // health, archetype mix, and the on-screen target count.
        private const float k_rampIntervalSeconds = 12f;
        // How often we top the swarm back up toward its target size.
        private const float k_topUpIntervalSeconds = 0.7f;
        private const float k_demoTopUpIntervalSeconds = 1.4f;
        private const int k_demoNormalEnemyBudget = 18;
        private const int k_demoEliteEnemyBudget = 28;
        // Max enemies streamed in per top-up so they trickle in rather than
        // popping all at once.
        private const int k_spawnPerTopUp = 5;
        // One-shot multiplier consumed on the next SpawnNextWave call. Lets the
        // map system request a fatter Elite wave without changing config data.
        private float m_pendingCountMultiplier = 1f;

        // current wave index (1-based)
        public event Action<int> WaveStarted;
        // Raised when every enemy in the active wave is dead. The director shows
        // the upgrade pick before calling AdvanceToNextWave().
        public event Action<int> WaveCleared;

        public int CurrentWaveIndex => m_currentWaveIndex;

        public void Initialize(GameConfigSO config, Transform player, RunState runState)
        {
            m_config = config;
            m_player = player;
            m_runState = runState;

            m_pool ??= new ObjectPool<Enemy>(
                createFunc: CreateEnemy,
                actionOnGet: enemy => enemy.gameObject.SetActive(true),
                // Null-guard: during teardown Unity may have destroyed the
                // pooled GameObject before we get a chance to release it.
                actionOnRelease: enemy => { if (enemy != null) enemy.gameObject.SetActive(false); },
                actionOnDestroy: enemy => { if (enemy != null) Destroy(enemy.gameObject); },
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: 128);
        }

        // Begin from wave 1 with a fresh random layout.
        public void StartRun()
        {
            ClearAllEnemies();
            m_encounterForward = FrontApproach.CaptureForward(m_player);
            m_currentWaveIndex = 0;
            m_isRunning = true;
            m_endless = false;
            m_isTutorial = false;
            m_isDemoEncounter = false;
            SpawnNextWave();
        }

        public void StartEncounter(int floor, bool elite)
        {
            StopRun();
            m_encounterForward = FrontApproach.CaptureForward(m_player);
            m_currentWaveIndex = Mathf.Max(1, floor * 2 + 1);
            m_isRunning = true;
            m_isDemoEncounter = true;
            m_isDemoElite = elite;
            m_demoSpawnRemaining = Mathf.Clamp(8 + floor * 3 + (elite ? 6 : 0), 8, 30);
            m_demoTargetAlive = elite ? 5 : 4;
            m_demoSpawnTimer = 0f;
            TopUpDemoEncounter();
            WaveStarted?.Invoke(m_currentWaveIndex);
        }

        // Begin an endless, continuously-spawning run. Enemies stream in toward
        // a target swarm size that grows over time; there is no clear/upgrade
        // interruption. Used for the uninterrupted "just keep fighting" loop.
        public void StartEndless()
        {
            ClearAllEnemies();
            m_encounterForward = FrontApproach.CaptureForward(m_player);
            m_currentWaveIndex = 1;
            m_isRunning = true;
            m_endless = true;
            m_isTutorial = false;
            m_isDemoEncounter = false;
            m_rampTimer = 0f;
            m_topUpTimer = 0f;

            // Seed the arena immediately so the fight starts the moment combat
            // begins instead of waiting for the first top-up tick.
            TopUpSwarm();
            WaveStarted?.Invoke(m_currentWaveIndex);
        }

        // Training mode keeps one harmlessly small wave target available until
        // the director explicitly stops it. Clearing the target never advances
        // the run, which prevents an eager slash from skipping the left-hand
        // tutorial step.
        public void StartTutorialWave()
        {
            ClearAllEnemies();
            m_encounterForward = FrontApproach.CaptureForward(m_player);
            m_currentWaveIndex = 1;
            m_isRunning = true;
            m_endless = false;
            m_isTutorial = true;
            m_isDemoEncounter = false;
            SpawnTutorialTarget();
        }

        // Bounded, streamed encounter used by the vertical slice. Enemies arrive
        // in small beats instead of all popping at once; the encounter clears
        // only when its total budget has been defeated.
        public void StartDemoEncounter(bool isElite)
        {
            ClearAllEnemies();
            m_encounterForward = FrontApproach.CaptureForward(m_player);
            m_currentWaveIndex = isElite ? 7 : 4;
            m_isRunning = true;
            m_endless = false;
            m_isTutorial = false;
            m_isDemoEncounter = true;
            m_isDemoElite = isElite;
            m_demoSpawnRemaining = isElite
                ? k_demoEliteEnemyBudget
                : k_demoNormalEnemyBudget;
            m_demoTargetAlive = isElite ? 4 : 3;
            m_demoSpawnTimer = 0f;

            TopUpDemoEncounter();
            WaveStarted?.Invoke(m_currentWaveIndex);
        }

        private void Update()
        {
            if (!m_isRunning) return;

            if (m_isDemoEncounter)
            {
                UpdateDemoEncounter();
                return;
            }

            if (!m_endless) return;

            // Ramp difficulty on a timer: the virtual wave index drives enemy
            // health, archetype mix, and target swarm size upward.
            m_rampTimer += Time.deltaTime;
            if (m_rampTimer >= k_rampIntervalSeconds)
            {
                m_rampTimer -= k_rampIntervalSeconds;
                m_currentWaveIndex++;
                WaveStarted?.Invoke(m_currentWaveIndex);
            }

            // Trickle reinforcements in so the screen stays full as the player
            // mows enemies down.
            m_topUpTimer += Time.deltaTime;
            if (m_topUpTimer >= k_topUpIntervalSeconds)
            {
                m_topUpTimer -= k_topUpIntervalSeconds;
                TopUpSwarm();
            }
        }

        // Spawn enemies (up to a per-tick cap) until the live count reaches the
        // current wave's target swarm size.
        private void TopUpSwarm()
        {
            int target = m_config.GetEnemyCountForWave(m_currentWaveIndex);
            int deficit = target - m_aliveEnemies.Count;
            if (deficit <= 0) return;

            int toSpawn = Mathf.Min(deficit, k_spawnPerTopUp);
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne();
            }
        }

        public void StopRun()
        {
            m_isRunning = false;
            m_endless = false;
            m_isTutorial = false;
            m_isDemoEncounter = false;
            m_demoSpawnRemaining = 0;
            ClearAllEnemies();
        }

        // Called by the director after the player picks an upgrade for a
        // cleared wave. Spawns the next, harder, randomized wave.
        public void AdvanceToNextWave()
        {
            if (!m_isRunning) return;
            SpawnNextWave();
        }

        // Schedule the next spawned wave to be an elite encounter (more enemies,
        // tougher archetype mix). Consumed on the next SpawnNextWave call.
        public void RequestEliteNextWave()
        {
            m_pendingCountMultiplier = 1.5f;
        }

        private void SpawnNextWave()
        {
            m_currentWaveIndex++;
            int baseCount = m_config.GetEnemyCountForWave(m_currentWaveIndex);
            int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * m_pendingCountMultiplier));
            m_pendingCountMultiplier = 1f;

            for (int i = 0; i < count; i++)
            {
                SpawnOne();
            }

            WaveStarted?.Invoke(m_currentWaveIndex);
        }

        // Spawn a single enemy at a random ring position with an archetype
        // weighted by the current (virtual) wave index.
        private void SpawnOne()
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();
            SpawnOne(spawnPosition, m_currentWaveIndex, PickArchetype());
        }

        private void SpawnOne(
            Vector3 position,
            int waveIndex,
            EnemyArchetype archetype,
            bool isTutorialTarget = false)
        {
            Enemy enemy = m_pool.Get();
            enemy.Spawn(position, waveIndex, archetype, isTutorialTarget, m_encounterForward);
            m_aliveEnemies.Add(enemy);
        }

        private void SpawnTutorialTarget()
        {
            Vector3 position = FrontApproach.Position(m_player, m_encounterForward, 6f, 0f);
            SpawnOne(position, 1, EnemyArchetype.Drifter, true);
        }

        private void UpdateDemoEncounter()
        {
            if (m_demoSpawnRemaining <= 0) return;

            m_demoSpawnTimer -= Time.deltaTime;
            if (m_demoSpawnTimer > 0f) return;

            m_demoSpawnTimer = k_demoTopUpIntervalSeconds;
            TopUpDemoEncounter();
        }

        private void TopUpDemoEncounter()
        {
            int deficit = m_demoTargetAlive - m_aliveEnemies.Count;
            int toSpawn = Mathf.Min(deficit, m_demoSpawnRemaining);
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne(
                    GetRandomSpawnPosition(),
                    m_currentWaveIndex,
                    PickDemoArchetype());
                m_demoSpawnRemaining--;
            }
        }

        private EnemyArchetype PickDemoArchetype()
        {
            float roll = UnityEngine.Random.value;
            if (!m_isDemoElite)
            {
                if (roll < 0.55f) return EnemyArchetype.Drifter;
                if (roll < 0.85f) return EnemyArchetype.Charger;
                return EnemyArchetype.Tank;
            }

            if (roll < 0.2f) return EnemyArchetype.Drifter;
            if (roll < 0.45f) return EnemyArchetype.Charger;
            if (roll < 0.75f) return EnemyArchetype.Tank;
            return EnemyArchetype.Weaver;
        }

        // Weighted random archetype. Tougher variants become more likely on
        // later waves so difficulty escalates with run length.
        private EnemyArchetype PickArchetype()
        {
            float chargerWeight = 1f + m_currentWaveIndex * 0.4f;
            float tankWeight = Mathf.Max(0f, m_currentWaveIndex - 1) * 0.35f;
            // Weavers only enter the rotation from wave 3 onward so the player
            // has time to learn melee threats before ranged casters appear.
            float weaverWeight = Mathf.Max(0f, m_currentWaveIndex - 2) * 0.5f;
            const float drifterWeight = 3f;

            float total = drifterWeight + chargerWeight + tankWeight + weaverWeight;
            float roll = UnityEngine.Random.Range(0f, total);

            if (roll < drifterWeight) return EnemyArchetype.Drifter;
            if (roll < drifterWeight + chargerWeight) return EnemyArchetype.Charger;
            if (roll < drifterWeight + chargerWeight + tankWeight) return EnemyArchetype.Tank;
            return EnemyArchetype.Weaver;
        }

        private Vector3 GetRandomSpawnPosition()
        {
            float depth = UnityEngine.Random.Range(7f, 10f);
            float lane = UnityEngine.Random.Range(-0.4f, 0.4f) * depth;
            return FrontApproach.Position(m_player, m_encounterForward, depth, lane);
        }

        private Enemy CreateEnemy()
        {
            Enemy enemy = Instantiate(m_enemyPrefab);
            enemy.Initialize(m_config, m_player, HandleEnemyDied);
            return enemy;
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            // Damage callers publish hit/kill events before LateUpdate returns
            // objects to the pool or asks the director to display a result.
            if (!m_aliveEnemies.Contains(enemy) || m_pendingDeaths.Contains(enemy)) return;
            m_pendingDeaths.Add(enemy);
        }

        private void LateUpdate()
        {
            for (int i = m_corpses.Count - 1; i >= 0; i--)
            {
                Enemy corpse = m_corpses[i];
                if (corpse != null && !corpse.DeathComplete) continue;
                m_corpses.RemoveAt(i);
                if (corpse != null) m_pool.Release(corpse);
            }
            if (m_pendingDeaths.Count > 0) m_clearQueued = true;
            for (int i = 0; i < m_pendingDeaths.Count; i++)
            {
                Enemy enemy = m_pendingDeaths[i];
                if (m_aliveEnemies.Remove(enemy)) m_corpses.Add(enemy);
            }
            m_pendingDeaths.Clear();
            if (!m_clearQueued) return;

            if (m_isTutorial)
            {
                m_clearQueued = false;
                if (m_isRunning && m_aliveEnemies.Count == 0)
                {
                    SpawnTutorialTarget();
                }
                return;
            }

            if (m_aliveEnemies.Count > 0) { m_clearQueued = false; return; }
            // Keep the last death visible before a menu or next encounter clears the scene.
            if (m_corpses.Count > 0) return;
            m_clearQueued = false;
            if (m_isDemoEncounter)
            {
                if (m_demoSpawnRemaining > 0)
                {
                    return;
                }

                if (m_isRunning && m_aliveEnemies.Count == 0)
                {
                    m_isDemoEncounter = false;
                    m_runState?.Events.RaiseWaveCleared(m_currentWaveIndex);
                    WaveCleared?.Invoke(m_currentWaveIndex);
                }
                return;
            }

            // Endless mode never "clears" — reinforcements stream in from the
            // Update top-up, so we skip the wave-cleared / upgrade handoff.
            if (m_endless) return;

            if (m_isRunning && m_aliveEnemies.Count == 0)
            {
                // Hand off to the director for the upgrade pick; it calls
                // AdvanceToNextWave() once the player has chosen.
                m_runState?.Events.RaiseWaveCleared(m_currentWaveIndex);
                WaveCleared?.Invoke(m_currentWaveIndex);
            }
        }

        private void ClearAllEnemies()
        {
            m_clearQueued = false;
            m_pendingDeaths.Clear();
            foreach (Enemy corpse in m_corpses)
                if (corpse != null) m_pool?.Release(corpse);
            m_corpses.Clear();
            for (int i = m_aliveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = m_aliveEnemies[i];
                // Skip enemies Unity has already destroyed (e.g. during scene
                // teardown) — release would touch a missing GameObject.
                if (enemy == null) continue;
                m_pool.Release(enemy);
            }

            m_aliveEnemies.Clear();
        }

        private void OnDestroy()
        {
            ClearAllEnemies();
            m_pool?.Dispose();
            m_pool = null;
        }
    }
}
