using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HoldTheCircle.Editor
{
    public static class FourFixBuild
    {
        private static string s_version = "0.1.5";
        private static string s_artifact = "HoldTheCircle-v0.1.5-front-bilingual.apk";
        private static string s_status = "four-fix-build.txt";

        [MenuItem("Tools/Hold The Circle/Build Four Fix APK")]
        public static void Schedule()
        {
            ScheduleBuild("0.1.5", "HoldTheCircle-v0.1.5-front-bilingual.apk", "four-fix-build.txt");
        }

        [MenuItem("Tools/Hold The Circle/Build Eye Hand Animated APK")]
        public static void ScheduleEyeHand()
        {
            ScheduleBuild("0.1.6", "HoldTheCircle-v0.1.6-eyehand-animated.apk", "eye-hand-build.txt");
        }

        private static void ScheduleBuild(string version, string artifact, string status)
        {
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play mode and finish compilation before building.");
            s_version = version;
            s_artifact = artifact;
            s_status = status;
            File.WriteAllText(StatusPath, "Scheduled");
            EditorApplication.update -= Build;
            EditorApplication.update += Build;
        }

        private static string StatusPath => Path.Combine(Path.GetTempPath(), "htc-review", s_status);
        private static void Build()
        {
            EditorApplication.update -= Build;
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            bool customKeystore = PlayerSettings.Android.useCustomKeystore;
            string version = PlayerSettings.bundleVersion;
            int code = PlayerSettings.Android.bundleVersionCode;
            try
            {
                File.WriteAllText(StatusPath, "Building");
                LocalizationBuilder.BuildContent();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.picosdk.holdthecircle.handrayfix");
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.bundleVersion = s_version;
                PlayerSettings.Android.bundleVersionCode = Math.Max(code + 1, 16);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    target = BuildTarget.Android,
                    locationPathName = s_artifact,
                    options = BuildOptions.Development
                });
                string result = $"{report.summary.result}; errors={report.summary.totalErrors}; size={report.summary.totalSize}; duration={report.summary.totalTime}";
                File.WriteAllText(StatusPath, result);
                Debug.Log("[FourFixBuild] " + result);
            }
            catch (Exception exception)
            {
                File.WriteAllText(StatusPath, "Failed: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, identifier);
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                PlayerSettings.bundleVersion = version;
                PlayerSettings.Android.bundleVersionCode = code;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
