using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ByteDance.PICO.XR;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace HoldTheCircle.Editor
{
    public static class FourFixValidation
    {
        private static readonly List<string> s_checks = new();
        private const BindingFlags k_private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            s_checks.Clear();
            Check(LocalizationBuilder.Validate().Contains("zero gaps"), "410 localized table entries have values");
            Check(L10n.Text("WAVE 17") == "第 17 波", "Dynamic wave counter translates");
            Check(L10n.Text("Wind Shard — ready to confirm") == "已选择「风之碎片」，请确认", "Relic selection translates");
            Check(L10n.Text("Score 1,250  /  46 defeated\n+31 shards  /  Total 90") ==
                "得分 1,250  /  击败 46 个敌人\n获得 31 碎片  /  累计 90", "Composed result translates");
            Check(L10n.Text("<b>SWIPE YOUR RIGHT HAND</b>\n<size=62%>A fast horizontal swipe throws an energy wave.</size>") ==
                "<b>挥动右手</b>\n<size=62%>快速水平挥动右手，释放能量波。</size>", "Tutorial rich-text tags survive translation");
            // Dynamic atlases only contain glyphs requested by a rendered string in this session.
            L10n.UIFont.TryAddCharacters("守");
            L10n.TMPFont.TryAddCharacters("守");
            Check(L10n.UIFont.HasCharacter('守') && L10n.TMPFont.HasCharacter('守'), "Both font systems contain Chinese glyphs");
            var root = WorldMenu.Current.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Q<Label>(className: "world-menu__title").text == "守住不灭的灯火", "Lobby renders Chinese");
            string before = LocalizationSettings.SelectedLocale.Identifier.Code;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
            Check(root.Q<Label>(className: "world-menu__title").text == "KEEP THE LANTERN LIT", "Open menu refreshes to English");
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(before);
            Check(root.Q<Label>(className: "world-menu__title").text == "守住不灭的灯火", "Open menu refreshes back to Chinese");
            Check(root.resolvedStyle.unityFontDefinition.fontAsset == L10n.UIFont, "UI Toolkit uses locale font asset");
            var camera = Camera.main;
            ArenaSkyEnvironment.Apply(camera, false);
            Check(camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null, "Night mode renders the skybox");
            ArenaSkyEnvironment.Apply(camera, true);
            Check(camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor.a == 0,
                "MR mode preserves transparent background");
            ArenaSkyEnvironment.Apply(camera, ArenaSkyEnvironment.UsePassthrough);
            ValidateFront();
            ValidateHandInput();
            Directory.CreateDirectory("Docs/VisualRework/Evidence/FrontBilingual");
            File.WriteAllText("Docs/VisualRework/Evidence/FrontBilingual/FourFixChecks.md",
                "# Four fixes: Editor checks\n\n" + string.Join("\n\n", s_checks) +
                "\n\nSynthetic XRHands and SDK native state enter the real input path. Physical hand tracking and MR composition require headset verification.");
            Debug.Log("[FourFixValidation] " + s_checks.Count + " checks passed.");
        }

        private static void ValidateFront()
        {
            WaveSystem waves = UnityEngine.Object.FindFirstObjectByType<WaveSystem>();
            Transform player = Camera.main.transform;
            Quaternion original = player.rotation;
            try
            {
                player.rotation = Quaternion.Euler(0, 73, 0);
                Vector3 forward = player.forward;
                waves.StartEncounter(0, true);
                var sample = typeof(WaveSystem).GetMethod("GetRandomSpawnPosition", k_private);
                float closest = float.MaxValue;
                for (int i = 0; i < 1000; i++)
                {
                    Vector3 position = (Vector3)sample.Invoke(waves, null);
                    Vector3 offset = position - player.position;
                    float depth = Vector3.Dot(offset, forward);
                    float lane = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)));
                    Check(depth >= 7f - 0.001f && depth <= 10.001f && lane <= depth * 0.401f,
                        "Front spawn sample", false);
                    closest = Mathf.Min(closest, depth);
                }
                player.rotation = Quaternion.Euler(0, 253, 0);
                Vector3 next = (Vector3)sample.Invoke(waves, null);
                Check(Vector3.Dot(next - player.position, forward) >= 7f - 0.001f,
                    "1000 distant spawns stay in the captured front, even after looking behind");
                var movement = typeof(Enemy).GetMethod("UpdateMovement", k_private);
                foreach (Enemy enemy in Enemy.Active)
                for (int i = 0; i < 600; i++)
                {
                    movement.Invoke(enemy, new object[] { 1f / 60f });
                    Check(Vector3.Dot(enemy.transform.position - player.position, forward) >= 1.199f,
                        "Enemy front movement", false);
                }
                Check(Enemy.Active.All(enemy => Vector3.Distance(enemy.transform.position, player.position) < 6f),
                    "All sampled archetypes approach from distance and stay in front");
                waves.StopRun();
                player.rotation = Quaternion.Euler(0, 73, 0);
                BossSystem boss = UnityEngine.Object.FindFirstObjectByType<BossSystem>();
                boss.StartEncounter();
                Check(BossSentinel.Active.Count == 2 &&
                    BossSentinel.Active.All(twin => Vector3.Dot(twin.transform.position - player.position, forward) >= 8.99f),
                    "Both bosses start nine metres in the encounter heading");
                var update = typeof(BossSentinel).GetMethod("Update", k_private);
                foreach (BossSentinel twin in BossSentinel.Active)
                {
                    twin.Activate(3.5f, 80f, 99f, 0f, 0f, 0f);
                    for (int i = 0; i < 1200; i++)
                    {
                        update.Invoke(twin, null);
                        Check(Vector3.Dot(twin.transform.position - player.position, forward) >= 1.199f,
                            "Boss front motion", false);
                    }
                }
                Check(true, "Boss motion remains in front through repeated lateral cycles");
                boss.StopEncounter();
                EnemyProjectile.ClearAll();
            }
            finally
            {
                waves.StopRun();
                player.rotation = original;
            }
        }

        private static void ValidateHandInput()
        {
            var type = typeof(GameDirector).Assembly.GetType("HoldTheCircle.HandMenuPointer");
            var poll = type.GetMethod("Poll");
            var reset = type.GetMethod("Reset");
            object pointer = Activator.CreateInstance(type, new object[] { HandSide.Right });
            var space = new GameObject("Test Tracking Offset");
            space.transform.SetPositionAndRotation(new Vector3(3, 1, -4), Quaternion.Euler(0, 73, 0));
            space.transform.localScale = Vector3.one * 1.4f;
            type.GetMethod("SetTrackingSpace").Invoke(pointer, new object[] { space.transform });
            PicoHandInteraction original = PicoHandInteraction.right;
            PicoHandInteraction device = PicoHandInteraction.CreateHand(InputDeviceCharacteristics.Right);
            PicoHandInteraction.right = device;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(device);
            XRHandSubsystem hands = null;
            try
            {
                MethodInfo update = typeof(PicoHandInteraction).GetMethods(k_private).Single(method =>
                    method.Name == "UpdateHand" && method.GetParameters()[0].ParameterType == typeof(bool));
                void Native(bool tracked, float pinch, Quaternion rotation)
                {
                    Type stateType = update.GetParameters()[1].ParameterType.GetElementType();
                    object state = Activator.CreateInstance(stateType);
                    object poseState = Activator.CreateInstance(stateType.GetField("aimPose").FieldType);
                    var nativePose = new Posef
                    {
                        Position = new Vector3f { x = 0.15f, y = 0.4f, z = -0.3f },
                        Orientation = new Quatf { x = rotation.x, y = rotation.y, z = -rotation.z, w = -rotation.w }
                    };
                    poseState.GetType().GetField("pose").SetValue(poseState, nativePose);
                    poseState.GetType().GetField("isActive").SetValue(poseState, tracked);
                    poseState.GetType().GetField("locationFlags").SetValue(poseState, 15UL);
                    stateType.GetField("aimPose").SetValue(state, poseState);
                    stateType.GetField("gripPose").SetValue(state, poseState);
                    stateType.GetField("pinchValue").SetValue(state, pinch);
                    update.Invoke(device, new[] { (object)tracked, state });
                    InputSystem.Update();
                }
                (bool tracked, Ray ray, bool pressed) Read()
                {
                    object[] arguments = { default(Ray), false };
                    bool tracked = (bool)poll.Invoke(pointer, arguments);
                    return (tracked, (Ray)arguments[0], (bool)arguments[1]);
                }
                Quaternion aim = Quaternion.Euler(-12, 25, 0);
                Native(true, 1f, aim);
                var sample = Read();
                Check(sample.tracked && !sample.pressed,
                    $"SDK aim alone remains available; held pinch on open is ignored (enabled={device.enabled}, tracked={device.isTracked.ReadValue()}, flags={device.compatAimFlags}, rotation={device.pointer.rotation.ReadValue()})");
                Check(Vector3.Angle(sample.ray.direction, space.transform.TransformDirection(aim * Vector3.forward)) < 0.01f &&
                    Vector3.Distance(sample.ray.origin, space.transform.TransformPoint(new Vector3(0.15f, 0.4f, 0.3f))) < 0.001f,
                    "Native pose converts once through rotated and scaled tracking space");
                Native(true, 0f, aim); Read();
                Native(true, 1f, aim);
                Check(Read().pressed && !Read().pressed, "SDK pinch emits one press per release");
                Native(false, 0f, aim);
                Check(!Read().tracked, "Native tracking loss clears the pointer");
                Native(true, 1f, aim);
                Check(!Read().pressed, "Held pinch after native tracking recovery does not click");

                XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
                {
                    id = "HTC Four Fix Test Hands", providerType = typeof(TestHandProvider)
                });
                var descriptors = new List<XRHandSubsystemDescriptor>();
                SubsystemManager.GetSubsystemDescriptors(descriptors);
                hands = descriptors.Single(descriptor => descriptor.id == "HTC Four Fix Test Hands").Create();
                hands.Start();
                TestHandProvider.Tracked = true;
                TestHandProvider.Pinched = false;
                TestHandProvider.Rotation = Quaternion.identity;
                hands.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                Native(true, 0f, aim);
                reset.Invoke(pointer, null);
                sample = Read();
                Read(); // Source changes clear the latch; the next open sample arms it.
                Check(Vector3.Angle(sample.ray.direction, space.transform.forward) < 0.01f,
                    "Tracked index overrides shoulder-biased SDK aim angle");
                TestHandProvider.Pinched = true;
                hands.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                Native(true, 1f, aim);
                sample = Read();
                Check(sample.pressed && Vector3.Angle(sample.ray.direction, space.transform.forward) < 0.01f,
                    "Curling the index into a pinch preserves the original aim and selects once");
                Native(false, 0f, aim);
                TestHandProvider.Pinched = false;
                hands.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                reset.Invoke(pointer, null);
                sample = Read();
                Check(sample.tracked && !sample.pressed, "XRHands-only path works without a valid PICO aim");
                TestHandProvider.Tracked = false;
                hands.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                Check(!Read().tracked, "All tracking loss hides the joint pointer");
            }
            finally
            {
                hands?.Stop();
                hands?.Destroy();
                PicoHandInteraction.right = original;
                InputSystem.RemoveDevice(device);
                InputSystem.settings.backgroundBehavior = background;
                type.GetMethod("Dispose").Invoke(pointer, null);
                UnityEngine.Object.DestroyImmediate(space);
            }
        }

        private static void Check(bool passed, string text, bool report = true)
        {
            if (!passed) throw new InvalidOperationException("[FourFixValidation] " + text);
            if (report) s_checks.Add("PASS: " + text);
        }
    }

    public sealed class TestHandProvider : XRHandSubsystemProvider
    {
        public static bool Tracked;
        public static bool Pinched;
        public static Quaternion Rotation;
        public override void Start() { }
        public override void Stop() { }
        public override void Destroy() { }
        public override void GetHandLayout(NativeArray<bool> layout)
        {
            for (int i = 0; i < layout.Length; i++) layout[i] = true;
        }
        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType type,
            ref Pose leftRoot, NativeArray<XRHandJoint> left, ref Pose rightRoot, NativeArray<XRHandJoint> right)
        {
            if (!Tracked) return XRHandSubsystem.UpdateSuccessFlags.None;
            Vector3 origin = new(0.15f, 0.4f, 0.3f);
            for (int i = 0; i < right.Length; i++)
            {
                var id = XRHandJointIDUtility.FromIndex(i);
                Vector3 position = id switch
                {
                    XRHandJointID.Wrist => origin - Vector3.forward * 0.1f,
                    XRHandJointID.IndexIntermediate => origin + (Pinched ? Vector3.up : Vector3.forward) * 0.03f,
                    XRHandJointID.IndexTip => origin + Vector3.forward * 0.08f,
                    XRHandJointID.ThumbTip => origin + Vector3.forward * 0.08f + (Pinched ? Vector3.zero : Vector3.up * 0.06f),
                    _ => origin
                };
                right[i] = XRHandProviderUtility.CreateJoint(Handedness.Right, XRHandJointTrackingState.Pose,
                    id, new Pose(position, Rotation), 0.01f);
            }
            rightRoot = new Pose(origin, Rotation);
            return XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
        }
    }
}
