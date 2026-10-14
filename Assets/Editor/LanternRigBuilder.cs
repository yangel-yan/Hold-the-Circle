using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoldTheCircle.Editor
{
    /// <summary>Prepares original rigged art and retargets generated motion to Unity Humanoid.</summary>
    public static class LanternRigBuilder
    {
        private const string k_output = "Assets/Game/Resources/Art/LanternGuardians/";
        private static readonly string[] k_characters = { "OriginalLanternWarden", "OriginalLanternBulwark" };

        [MenuItem("Tools/Hold The Circle/Build Animated Enemies")]
        public static void Build()
        {
            Directory.CreateDirectory(k_output);
            AssetDatabase.Refresh();
            foreach (string name in k_characters)
                ImportHumanoid($"Assets/Game/Models/{name}/{name}.fbx", false);
            foreach (string name in new[] { "RiteSentinelRun", "RiteSentinelAttack", "RiteSentinelDeath" })
                ImportHumanoid($"Assets/Game/Models/{name}/{name}.fbx", true);

            foreach (string path in k_characters.SelectMany(name =>
                Directory.GetFiles($"Assets/Game/Models/{name}/", "*.png")))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = path.Contains("BaseColor") || path.Contains("Emission");
                importer.textureType = path.EndsWith("Normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.maxTextureSize = 1024;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, maxTextureSize = 1024,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 100
                });
                importer.SaveAndReimport();
            }

            var run = Clip("RiteSentinelRun");
            var attack = Clip("RiteSentinelAttack");
            var death = Clip("RiteSentinelDeath");
            if (!run.isHumanMotion || !attack.isHumanMotion || !death.isHumanMotion)
                throw new InvalidOperationException("Retargeting failed: all motion clips must be Humanoid.");
            AnimationClip idle = BuildIdle();
            string controllerPath = k_output + "LanternGuardian.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            AddState(machine, "Idle", idle, 1f);
            AddState(machine, "Walk", run, 0.48f);
            AddState(machine, "Run", run, 1f);
            AddState(machine, "Attack", attack, 1f);
            AddState(machine, "Death", death, 1f);
            machine.defaultState = machine.states.First(x => x.state.name == "Idle").state;

            Shader shader = Shader.Find("HoldTheCircle/Lantern Bone");
            if (shader == null) throw new InvalidOperationException("Missing Lantern Bone shader");
            foreach (string character in k_characters)
            {
                string role = character.Replace("OriginalLantern", "");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Models/{character}/{character}.fbx");
                string materialPath = k_output + role + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", Texture(character, "BaseColor"));
                material.SetTexture("_BumpMap", Texture(character, "Normal"));
                material.SetTexture("_EmissionMap", Texture(character, "Emission"));
                material.SetTexture("_MaskMap", Texture(character, "Mask"));
                material.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.35f, 1f));
                material.SetColor("_AccentColor", new Color(0.6f, 0.8f, 1f));
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                GameObject root = new GameObject("Lantern " + role);
                try
                {
                    var body = UnityEngine.Object.Instantiate(source, root.transform);
                    var animator = body.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                        throw new InvalidOperationException("Character must have a valid humanoid avatar");
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    var renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMesh.subMeshCount).ToArray();
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        renderer.quality = SkinQuality.Bone2;
                        renderer.updateWhenOffscreen = false;
                        var baked = new Mesh();
                        // FBX import may retain a 100x mesh transform for centimetres.
                        // Bake into the renderer's local space and expand by two world metres.
                        renderer.BakeMesh(baked, true);
                        var bounds = baked.bounds;
                        UnityEngine.Object.DestroyImmediate(baked);
                        float meshScale = Mathf.Max(0.0001f, renderer.transform.lossyScale.x);
                        bounds.Expand(2f / meshScale);
                        renderer.localBounds = bounds;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, k_output + role + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[LanternRig] Prepared two original PBR characters, own rigs, humanoid run/attack/death and shared animator.");
        }

        private static Texture2D Texture(string character, string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Models/{character}/{name}.png");

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"Assets/Game/Models/{name}/{name}.fbx")
                .OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview"));

        private static void AddState(AnimatorStateMachine machine, string name, Motion motion, float speed)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            state.speed = speed;
            state.writeDefaultValues = true;
        }

        private static AnimationClip BuildIdle()
        {
            string path = k_output + "Breathing.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.name = "Breathing";
            var reference = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Models/{k_characters[0]}/{k_characters[0]}.fbx"));
            var animator = reference.GetComponent<Animator>();
            animator.Rebind();
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            handler.Dispose();
            UnityEngine.Object.DestroyImmediate(reference);
            for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
            {
                string muscle = HumanTrait.MuscleName[i];
                float value = muscle.Contains("Arm Down-Up") ? -0.8f :
                    muscle.Contains("Forearm Stretch") ? 0.8f : pose.muscles[i];
                var curve = AnimationCurve.Constant(0, 2.4f, value);
                if (muscle == "Chest Front-Back" || muscle == "Spine Front-Back")
                    curve = new AnimationCurve(new Keyframe(0, 0.015f), new Keyframe(1.2f, -0.015f), new Keyframe(2.4f, 0.015f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
            }
            // Humanoid body translation is normalized by humanScale. An omitted RootT puts the hips at the floor.
            foreach (var axis in new[] { "x", "y", "z" })
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis),
                    AnimationCurve.Constant(0, 2.4f, axis == "y" ? pose.bodyPosition.y : 0f));
            foreach (var axis in new[] { "x", "y", "z", "w" })
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axis),
                    AnimationCurve.Constant(0, 2.4f, axis == "w" ? 1f : 0f));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ImportHumanoid(string path, bool motion)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Missing skeletal asset", path);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var map = new Dictionary<string, string>();
            void Map(string human, string bone) => map[human] = bone;
            Map("Hips", "Pelvis");
            Map("Spine", "Spine1");
            Map("Chest", "Spine2");
            Map("UpperChest", "Spine3");
            Map("Neck", "Neck");
            Map("Head", "Head");
            foreach (string side in new[] { "Left", "Right" })
            {
                string p = side == "Left" ? "L" : "R";
                Map(side + "UpperLeg", $"{p}_Hip");
                Map(side + "LowerLeg", $"{p}_Knee");
                Map(side + "Foot", $"{p}_Ankle");
                Map(side + "Toes", $"{p}_Foot");
                Map(side + "Shoulder", $"{p}_Collar");
                Map(side + "UpperArm", $"{p}_Shoulder");
                Map(side + "LowerArm", $"{p}_Elbow");
                Map(side + "Hand", $"{p}_Wrist");
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    for (int i = 1; i <= 3; i++)
                    {
                        string joint = i == 1 ? "Proximal" : i == 2 ? "Intermediate" : "Distal";
                        string hy = finger == "Little" ? "Pinky" : finger;
                        Map(side + finger + joint, $"{p}_{hy}{i}");
                    }
            }
            var names = source.GetComponentsInChildren<Transform>().Select(x => x.name).ToHashSet();
            HumanDescription description = importer.humanDescription;
            description.human = map.Where(pair => names.Contains(pair.Value)).Select(pair => new HumanBone
            {
                humanName = pair.Key, boneName = pair.Value, limit = new HumanLimit { useDefaultValues = true }
            }).ToArray();
            description.skeleton = source.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
            {
                name = t.name, position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            }).ToArray();
            description.upperArmTwist = description.lowerArmTwist = description.upperLegTwist = description.lowerLegTwist = 0.5f;
            description.armStretch = description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.importCameras = importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            // Unity 6.3 generates indexed LODs while retaining the same material and skin.
            if (!motion) importer.generateMeshLods = true;
            importer.SaveAndReimport();
            if (!motion) return;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = path.Contains("Run");
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = false;
                clip.lockRootPositionXZ = false;
                clip.keepOriginalPositionXZ = false;
                clip.keepOriginalOrientation = false;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
