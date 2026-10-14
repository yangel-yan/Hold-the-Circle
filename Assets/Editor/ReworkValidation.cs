using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoldTheCircle.Editor
{
    /// <summary>Editor-only, cross-frame checks for menu input and encounter handoffs.</summary>
    [InitializeOnLoad]
    public static class ReworkValidation
    {
        private static string s_output = "Docs/VisualRework/Evidence/FrontBilingual";
        private static readonly List<string> s_checks = new();
        private static readonly string[] s_metaKeys = { "HTC.MetaShards", "HTC.RunsCompleted", "HTC.BossKills" };
        private static GameDirector s_director;
        private static Transform[] s_hands;
        private static Vector3[] s_handPositions;
        private static int s_stage;
        private static int s_stageFrame;
        private static int s_lastFrame = -1;
        private static int s_ticks;
        private static int s_kills;
        private static float s_integrity;
        private static float s_bossHealth;
        private static Vector3 s_chainPosition;
        private static Quaternion s_chainRotation;
        private static bool s_checkingBossChain;
        private static double s_nextStep;
        private static bool s_running;
        public static string Status => $"Stage {s_stage}, frames {s_ticks}, kills {s_kills}";

        [MenuItem("Tools/Hold The Circle/Validation Status")]
        public static void WriteStatus()
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "htc-review", "validation-status.txt"),
                $"{DateTime.Now:O}; project={Application.dataPath}; playing={EditorApplication.isPlaying}; " +
                $"compiling={EditorApplication.isCompiling}; running={s_running}; session={SessionState.GetBool("HTC.Review.Running", false)}; {Status}");
        }

        static ReworkValidation()
        {
            s_output = SessionState.GetString("HTC.Review.Output", s_output);
            s_running = SessionState.GetBool("HTC.Review.Running", false);
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Hold The Circle/Validate Rework")]
        public static void Begin()
        {
            if (EditorApplication.isCompiling || s_running && EditorApplication.isPlaying)
            {
                WriteStatus();
                return;
            }
            if (s_running)
            {
                // A failed compilation can cancel entering Play mode after the
                // fixture saved its state. Restore that setup before retrying.
                var previousXR = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                    .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (previousXR != null) previousXR.InitManagerOnStart = SessionState.GetBool("HTC.Review.XRInit", true);
                s_running = false;
                SessionState.SetBool("HTC.Review.Running", false);
            }
            for (int i = 0; i < s_metaKeys.Length; i++)
            {
                SessionState.SetBool("HTC.Review.Exists." + i, PlayerPrefs.HasKey(s_metaKeys[i]));
                SessionState.SetInt("HTC.Review.Meta." + i, PlayerPrefs.GetInt(s_metaKeys[i], 0));
            }
            Directory.CreateDirectory(s_output);
            SessionState.SetString("HTC.Review.Output", s_output);
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            SessionState.SetBool("HTC.Review.XRInit", xr != null && xr.InitManagerOnStart);
            if (xr != null) xr.InitManagerOnStart = false;
            s_checks.Clear();
            s_stage = 0;
            s_stageFrame = 0;
            s_lastFrame = -1;
            s_ticks = 0;
            s_kills = 0;
            s_checkingBossChain = false;
            s_director = null;
            SessionState.SetBool("HTC.Review.Running", true);
            s_running = true;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Hold The Circle/Validate Eye Hand Animated")]
        public static void BeginEyeHand()
        {
            s_output = "Docs/VisualRework/Evidence/EyeHandAnimated";
            Begin();
        }

        [MenuItem("Tools/Hold The Circle/Validate Original Art")]
        public static void BeginOriginalArt()
        {
            s_output = "Docs/VisualRework/Evidence/ShareableArt";
            Begin();
        }

        private static void Tick()
        {
            if (!s_running || !SessionState.GetBool("HTC.Review.Running", false) ||
                !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < s_nextStep) return;
            s_nextStep = EditorApplication.timeSinceStartup + 0.03;
            try
            {
                if (Time.frameCount != s_lastFrame)
                {
                    s_lastFrame = Time.frameCount;
                    s_ticks++;
                    Time.captureDeltaTime = 1f / 60f;
                    Advance();
                }
                if (s_running) EditorApplication.Step();
            }
            catch (Exception exception)
            {
                s_checks.Add("FAIL: " + exception);
                Finish(false);
            }
        }

        private static void Advance()
        {
            if (s_ticks > 4500) throw new InvalidOperationException("Validation exceeded frame budget.");
            if (s_director == null)
            {
                s_director = UnityEngine.Object.FindFirstObjectByType<GameDirector>();
                if (s_director == null || WorldMenu.Current == null) return;
                var casters = UnityEngine.Object.FindObjectsByType<HandWaveCaster>(FindObjectsSortMode.None);
                s_hands = new Transform[casters.Length];
                s_handPositions = new Vector3[casters.Length];
                for (int i = 0; i < casters.Length; i++)
                {
                    s_hands[i] = casters[i].transform;
                    s_handPositions[i] = casters[i].transform.position;
                }
            }

            int elapsed = s_ticks - s_stageFrame;
            WorldMenu menu = WorldMenu.Current;
            switch (s_stage)
            {
                case 0:
                    if (elapsed < 5) return;
                    Check(s_director.State == GameState.Lobby, "Boot enters lobby");
                    FourFixValidation.Run();
                    Check(menu.GetComponent<UIDocument>().rootVisualElement.Q<Label>()
                        .resolvedStyle.height > 0, "World-space theme renders text");
                    Capture("Lobby");
                    ValidateMapAndRelics();
                    menu.Select(0);
                    Next();
                    break;
                case 1:
                    if (elapsed < 5) return;
                    Check(menu.GetComponent<UIDocument>().rootVisualElement
                        .Q(className: "world-menu__confirm").enabledSelf, "Menu selection enables explicit confirmation");
                    menu.Confirm();
                    Next();
                    break;
                case 2:
                    if (MenuTitle() == "KEEP THE LANTERN LIT")
                    {
                        if (elapsed > 90) throw new InvalidOperationException("Menu confirmation failed.");
                        return;
                    }
                    RestoreHands();
                    Check(MenuTitle().Contains("HAND RITE"), "Menu confirmation starts run");
                    Next();
                    break;
                case 3:
                    if (elapsed < 4) return;
                    Choose(0);
                    if (MenuTitle().Contains("HAND RITE")) return;
                    Check(s_director.State == GameState.MapSelect, "Specializations lead to route");
                    Next();
                    break;
                case 4:
                    if (elapsed < 4) return;
                    Capture("Route");
                    Choose(0);
                    Check(Enemy.Active.Count == 4, "First encounter has four concurrent enemies");
                    ArrangeEnemies();
                    Next();
                    break;
                case 5:
                    if (elapsed < 5) return;
                    Capture("PaintedEnemies");
                    var waves = UnityEngine.Object.FindFirstObjectByType<WaveSystem>();
                    foreach (Enemy enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                        enemy.enabled = true;
                    waves.StopRun();
                    waves.StartEncounter(0, false);
                    s_integrity = UnityEngine.Object.FindFirstObjectByType<CircleIntegrity>().Ratio;
                    s_director.PauseRun();
                    HitstopManager.Kill();
                    Next();
                    break;
                case 6:
                    if (elapsed < 10) return;
                    Check(HitstopManager.IsPaused && Time.timeScale == 0f,
                        "Hitstop cannot resume a paused encounter");
                    Check(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<CircleIntegrity>().Ratio -
                        s_integrity) < 0.001f, "Pause does not restore seal integrity");
                    Capture("Pause");
                    Choose(0);
                    Check(Time.timeScale == 1f, "Resume restores game time");
                    Next();
                    break;
                case 7:
                    if (elapsed < 4) return;
                    if (elapsed == 4)
                        Check(!menu.IsOpen && !menu.GetComponent<UIRenderer>().enabled,
                            "Menu renderer closes on resume");
                    if (s_director.State == GameState.Dead)
                        throw new InvalidOperationException("Player died during accelerated route.");
                    s_director.RunState.PlayerHealth.Heal(1000);
                    if (menu.IsOpen)
                    {
                        // Route, relic, merchant and event all use the same explicit confirmation.
                        int index = MenuTitle() == "THE LANTERN KEEPER" ? menu.ChoiceCount - 1 : 0;
                        Choose(index);
                        return;
                    }
                    KillCurrentEnemies();
                    UnityEngine.Object.FindFirstObjectByType<WaveSystem>()
                        .SendMessage("TopUpDemoEncounter", SendMessageOptions.RequireReceiver);
                    var boss = UnityEngine.Object.FindFirstObjectByType<BossSystem>();
                    if (boss != null && boss.IsActive)
                    {
                        var twins = UnityEngine.Object.FindObjectsByType<BossSentinel>(FindObjectsSortMode.None);
                        Check(twins.Length == 2, "Final node spawns twin sentinels");
                        var chain = UnityEngine.Object.FindFirstObjectByType<ChainLightningCaster>();
                        if (!s_checkingBossChain)
                        {
                            s_bossHealth = twins[0].CurrentHealth + twins[1].CurrentHealth;
                            s_chainPosition = chain.transform.position;
                            s_chainRotation = chain.transform.rotation;
                            chain.transform.position = twins[0].AimPosition + Vector3.back;
                            chain.transform.rotation = Quaternion.identity;
                            chain.ResetForNewRun();
                            s_checkingBossChain = true;
                            return;
                        }
                        Check(twins[0].CurrentHealth + twins[1].CurrentHealth < s_bossHealth,
                            "Left-hand chain damages the boss");
                        chain.transform.SetPositionAndRotation(s_chainPosition, s_chainRotation);
                        foreach (var twin in twins)
                        {
                            if (twin.ApplyDamage(10000))
                            {
                                s_director.RunState.Events.RaiseEnemyKilled(new EnemyKilledEvent
                                {
                                    Position = twin.transform.position
                                });
                                s_kills++;
                            }
                        }
                        Next();
                    }
                    else if (s_director.State == GameState.MapSelect)
                    {
                        Choose(0);
                    }
                    break;
                case 8:
                    if (elapsed < 4) return;
                    if (s_director.State != GameState.Dead && elapsed < 210) return;
                    Check(s_director.State == GameState.Dead && MenuTitle() == "THE LANTERN ENDURES",
                        "Simultaneous twin kills finish the six-floor route");
                    Check(s_director.GetComponent<MapSystem>().Graph.CurrentFloor == 6, "Six floors resolved");
                    Check(s_director.RunState.Kills == s_kills, "Result includes the final kill");
                    Check(Enemy.Active.Count == 0, "Result has no active enemies");
                    Check(UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
                        .Length == 0, "Result has no hostile projectiles");
                    Capture("Result");
                    Choose(0);
                    Next();
                    break;
                case 9:
                    if (elapsed < 4) return;
                    Check(s_director.State == GameState.Lobby, "Result returns to lobby");
                    Choose(2);
                    Next();
                    break;
                case 10:
                    if (elapsed < 4) return;
                    var circle = UnityEngine.Object.FindFirstObjectByType<CircleIntegrity>();
                    circle.Restore(-100);
                    Next();
                    break;
                case 11:
                    if (elapsed < 4) return;
                    Check(s_director.State == GameState.Dead && MenuTitle() == "THE SEAL HAS FALLEN",
                        "Seal failure ends an endless run");
                    Choose(0);
                    Next();
                    break;
                case 12:
                    if (elapsed < 4) return;
                    Choose(1);
                    s_kills = 0;
                    Next();
                    break;
                case 13:
                    if (elapsed < 4) return;
                    s_director.RunState.Events.RaiseCombatAction(new CombatActionEvent
                    {
                        HandIndex = 1,
                        Kind = CombatActionKind.SlashWave
                    });
                    s_director.RunState.Events.RaiseCombatAction(new CombatActionEvent
                    {
                        HandIndex = 0,
                        Kind = CombatActionKind.ChainLightning
                    });
                    Next();
                    break;
                case 14:
                    if (elapsed < 35) return;
                    Check(UnityEngine.Object.FindFirstObjectByType<WaveSystem>().CurrentWaveIndex == 4,
                        "Tutorial actions lead to normal encounter");
                    if (menu.IsOpen)
                    {
                        Capture("Relic");
                        Choose(0);
                        Next();
                        return;
                    }
                    s_director.RunState.PlayerHealth.Heal(1000);
                    KillCurrentEnemies();
                    UnityEngine.Object.FindFirstObjectByType<WaveSystem>()
                        .SendMessage("TopUpDemoEncounter", SendMessageOptions.RequireReceiver);
                    break;
                case 15:
                    if (elapsed < 4) return;
                    s_director.RunState.PlayerHealth.Heal(1000);
                    if (s_director.State != GameState.Dead)
                    {
                        KillCurrentEnemies();
                        UnityEngine.Object.FindFirstObjectByType<WaveSystem>()
                            .SendMessage("TopUpDemoEncounter", SendMessageOptions.RequireReceiver);
                        return;
                    }
                    Check(MenuTitle() == "THE RITES COMPLETE", "Tutorial ends on a separate result page");
                    Check(s_director.RunState.Kills == 46 && s_kills == 46,
                        "Tutorial includes all 18 normal and 28 elite kills");
                    Capture("TutorialResult");
                    s_checks.Add($"Observed {s_ticks} simulation frames; six-floor route and 46-kill tutorial completed.");
                    Finish(true);
                    break;
            }
        }

        private static void KillCurrentEnemies()
        {
            var enemies = new List<Enemy>(Enemy.Active);
            int killsBefore = s_director.RunState.Kills;
            foreach (Enemy enemy in enemies)
            {
                if (!enemy.ApplyDamage(10000)) continue;
                s_director.RunState.Events.RaiseEnemyHit(new EnemyHitEvent { Enemy = enemy, Damage = 10000 });
                s_director.RunState.Events.RaiseEnemyKilled(new EnemyKilledEvent
                {
                    Enemy = enemy,
                    Position = enemy.transform.position
                });
                s_kills++;
            }
            Check(s_director.RunState.Kills == killsBefore + enemies.Count,
                "Every simulated killing blow is counted", false);
        }

        private static void ArrangeEnemies()
        {
            Camera camera = Camera.main;
            Vector3 forward = camera.transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            int i = 0;
            foreach (Enemy enemy in new List<Enemy>(Enemy.Active))
            {
                enemy.Spawn(new Vector3(camera.transform.position.x, 0, camera.transform.position.z) +
                    forward * 3.3f + right * ((i - 1.5f) * 1.05f), 1, (EnemyArchetype)i, true);
                enemy.transform.rotation = Quaternion.LookRotation(-forward);
                enemy.enabled = false;
                i++;
            }
        }

        private static void ValidateMapAndRelics()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var graph = new MapGraph();
                graph.Generate(Vector3.zero, Vector3.forward);
                for (int floor = 0; floor < graph.FloorCount; floor++)
                {
                    List<MapNode> choices = graph.GetCurrentChoices();
                    Check(choices != null && choices.Count > 0, $"Route sample {seed} floor {floor} reachable", false);
                    graph.AdvanceTo(new MapNode { Floor = floor });
                    Check(graph.CurrentFloor == floor, "Invalid route node rejected", false);
                    graph.AdvanceTo(choices[0]);
                }
                Check(graph.CurrentFloor == 6, "Route reaches final floor", false);
            }
            s_checks.Add("PASS: 20 generated routes remain reachable and reject foreign nodes.");
            var state = new RunState();
            state.Initialize(2);
            var relic = ScriptableObject.CreateInstance<RelicSO>();
            state.AddRelic(relic);
            state.AddDamage(0.5f);
            state.RemoveRelic(state.Relics[0]);
            Check(Mathf.Abs(state.GetHand(0).DamageMultiplier - 1.5f) < 0.001f,
                "Relic removal preserves unrelated bonuses");
            state.Reset();
            Check(state.GetHand(0).DamageMultiplier == 1f && state.Relics.Count == 0, "Run reset clears relics");
            UnityEngine.Object.Destroy(relic);
        }

        private static void MoveHandTo(VisualElement element)
        {
            Vector2 point = element.worldBound.center;
            s_hands[0].position = WorldMenu.Current.transform.TransformPoint(new Vector3(point.x, -point.y, 0));
        }

        private static void RestoreHands()
        {
            for (int i = 0; i < s_hands.Length; i++) s_hands[i].position = s_handPositions[i];
        }

        private static void Choose(int index)
        {
            WorldMenu.Current.Select(index);
            WorldMenu.Current.Confirm();
        }

        private static string MenuTitle() => L10n.SourceText(WorldMenu.Current.GetComponent<UIDocument>()
            .rootVisualElement.Q<Label>(className: "world-menu__title"));

        private static void Next()
        {
            s_stage++;
            s_stageFrame = s_ticks;
        }

        private static void Check(bool condition, string description, bool report = true)
        {
            if (!condition) throw new InvalidOperationException(description);
            if (report) s_checks.Add("PASS: " + description);
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            var target = new RenderTexture(1600, 1000, 24);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes(s_output + "/" + name + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Finish(bool success)
        {
            s_running = false;
            SessionState.SetBool("HTC.Review.Running", false);
            Time.captureDeltaTime = 0;
            HitstopManager.SetPaused(false);
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr != null)
            {
                xr.InitManagerOnStart = SessionState.GetBool("HTC.Review.XRInit", true);
                EditorUtility.SetDirty(xr);
                AssetDatabase.SaveAssetIfDirty(xr);
            }
            for (int i = 0; i < s_metaKeys.Length; i++)
            {
                if (SessionState.GetBool("HTC.Review.Exists." + i, false))
                    PlayerPrefs.SetInt(s_metaKeys[i], SessionState.GetInt("HTC.Review.Meta." + i, 0));
                else
                    PlayerPrefs.DeleteKey(s_metaKeys[i]);
            }
            PlayerPrefs.Save();
            File.WriteAllText(s_output + "/EditorValidation.md",
                "# Editor rework validation\n\n" + (success ? "PASS" : "FAIL") +
                "\n\n" + string.Join("\n\n", s_checks) +
                "\n\nInput and damage are simulated. Screenshots are rendered by the actual Unity camera. " +
                "Editor frame stepping is used because continuous preview does not advance in this environment. " +
                "Headset comfort, MR composition and device performance are not validated.\n");
            Debug.Log("Rework validation " + (success ? "PASS" : "FAIL") + ". See " + s_output);
            EditorApplication.isPlaying = false;
        }
    }
}
