using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RepositoryCleanupTemp
{
    private const string Work = "C:/Users/Admin/AppData/Local/Temp/htc-cleanup";
    private static string s_last;

    static RepositoryCleanupTemp()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        string request = Work + "/request.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlaying) return;
        string command = File.ReadAllText(request).Trim();
        if (command == s_last) return;
        s_last = command;
        try
        {
            if (command == "retire")
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/Scenes/SampleScene.unity" || scene.isDirty)
                    throw new InvalidOperationException("Expected the saved SampleScene.");
                string archive = File.ReadAllText(Work + "/archive.txt").Trim();
                Directory.CreateDirectory(archive + "/Assets/Scenes");
                File.Copy(scene.path, archive + "/" + scene.path, false);
                File.Copy(scene.path + ".meta", archive + "/" + scene.path + ".meta", false);
                var retired = new List<string>();
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name == "WeaverOrb" || root.name.StartsWith("BossSentinel") ||
                        root.name == "BossHealthBar")
                    {
                        retired.Add(root.name);
                        Undo.DestroyObjectImmediate(root);
                    }
                    if (root.name == "Game")
                    {
                        var guide = root.transform.Find("VerticalSliceGuide");
                        if (guide != null)
                        {
                            retired.Add("Game/VerticalSliceGuide");
                            Undo.DestroyObjectImmediate(guide.gameObject);
                        }
                    }
                }
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Scene save failed.");
                File.WriteAllLines(Work + "/retired.txt", retired);
                DumpDependencies();
                File.WriteAllText(Work + "/retire.done", string.Join("\n", retired));
            }
            else if (command == "delete")
            {
                var deleted = new List<string>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string path in File.ReadAllLines(Work + "/delete.txt"))
                    {
                        if (!path.StartsWith("Assets/") || path.Contains(".."))
                            throw new InvalidOperationException("Invalid deletion path: " + path);
                        if (!AssetDatabase.DeleteAsset(path))
                            throw new InvalidOperationException("DeleteAsset failed: " + path);
                        deleted.Add(path);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.Refresh();
                File.WriteAllLines(Work + "/deleted.txt", deleted);
                File.WriteAllText(Work + "/delete.done", deleted.Count.ToString());
            }
            else if (command == "verify")
            {
                DumpDependencies();
                var scene = SceneManager.GetActiveScene();
                var missing = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                            missing.Add(transform.name);
                File.WriteAllText(Work + "/verify.done",
                    "scene=" + scene.path + "\nmissingScripts=" + missing.Count +
                    "\nscriptCompilationFailed=" + EditorUtility.scriptCompilationFailed +
                    "\n" + string.Join("\n", missing));
            }
            else if (command == "runtime")
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("HoldTheCircle.Editor.ReworkValidation"))
                    .First(t => t != null);
                type.GetField("s_output", System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic).SetValue(null, Work + "/Runtime");
                type.GetMethod("Begin").Invoke(null, null);
                File.WriteAllText(Work + "/runtime.started", DateTime.Now.ToString("O"));
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Work + "/" + command + ".error", error.ToString());
            Debug.LogException(error);
        }
    }

    private static void DumpDependencies()
    {
        string[] paths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var roots = new HashSet<string>(EditorBuildSettings.scenes
            .Where(s => s.enabled).Select(s => s.path));
        foreach (string path in paths)
            if (path.Contains("/Resources/") || path.StartsWith("Assets/AddressableAssetsData/") ||
                path.StartsWith("Assets/Game/Localization/") || path.StartsWith("Assets/Game/Relics/") ||
                path.StartsWith("Assets/Game/Specializations/") || path.StartsWith("Assets/Settings/") ||
                path.StartsWith("Assets/XR/") || path.StartsWith("Assets/XRI/"))
                roots.Add(path);
        File.WriteAllLines(Work + "/assets.txt", paths);
        File.WriteAllLines(Work + "/roots.txt", roots);
        File.WriteAllLines(Work + "/dependencies.txt", AssetDatabase.GetDependencies(roots.ToArray(), true));
    }
}
