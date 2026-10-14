/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.

NOTICE：All information contained herein is, and remains the property of
PICO Technology Co., Ltd. The intellectual and technical concepts
contained herein are proprietary to PICO Technology Co., Ltd. and may be
covered by patents, patents in process, and are protected by trade secret or
copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd.
*******************************************************************************/

// PicoGen_AssetImporter.cs
// Downloads a generated result file and writes it into the project. Models (GLB/
// FBX) follow the project convention Assets/Game/Models/{name}/; textures (PNG)
// go under Assets/Game/Textures/{name}/. glTFast (in the manifest) imports .glb
// under URP automatically. Non-destructive: an existing file is suffixed, not
// overwritten.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.Generation.Editor
{
    public static class PicoGen_AssetImporter
    {
        public const string ModelsRoot   = "Assets/Game/Models";
        public const string TexturesRoot = "Assets/Game/Textures";

        // Download `file` and import it under the right root for `modality`.
        // Returns the project-relative asset path, or null on failure (reason out).
        public static string Import(PicoGenResultFile file, string name, PicoGenModality modality, out string error)
        {
            error = null;
            if (file == null || string.IsNullOrEmpty(file.Url))
            {
                error = "no result file url";
                return null;
            }

            var safeName = Sanitize(string.IsNullOrWhiteSpace(name) ? DefaultName(modality) : name);
            var ext = ResolveExtension(file.Type, modality);
            var root = modality == PicoGenModality.Texture ? TexturesRoot : ModelsRoot;

            var bytes = PicoGen_HttpJson.DownloadBytes(file.Url, out var dlErr);
            if (bytes == null)
            {
                error = "download failed: " + dlErr;
                return null;
            }

            var relDir = root + "/" + safeName;
            Directory.CreateDirectory(ToAbsolute(relDir));

            var relPath = AssetDatabase.GenerateUniqueAssetPath(relDir + "/" + safeName + ext);
            try { File.WriteAllBytes(ToAbsolute(relPath), bytes); }
            catch (Exception e)
            {
                error = "write failed: " + e.Message;
                return null;
            }

            AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
            return relPath;
        }

        static string DefaultName(PicoGenModality m) => m switch
        {
            PicoGenModality.Object    => "GeneratedModel",
            PicoGenModality.Texture   => "GeneratedTexture",
            PicoGenModality.Animation => "GeneratedMotion",
            _                          => "GeneratedAsset",
        };

        static string ResolveExtension(string type, PicoGenModality modality)
        {
            switch ((type ?? "").ToUpperInvariant())
            {
                case "GLB":  return ".glb";
                case "OBJ":  return ".obj";
                case "FBX":  return ".fbx";
                case "USDZ": return ".usdz";
                case "STL":  return ".stl";
                case "PNG":  return ".png";
                case "JPG":
                case "JPEG": return ".jpg";
                case "WEBP": return ".webp";
                default:
                    return modality == PicoGenModality.Texture ? ".png"
                         : modality == PicoGenModality.Animation ? ".fbx"
                         : ".glb";
            }
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static string ToAbsolute(string projectRelative)
        {
            var root = Directory.GetParent(Application.dataPath)!.FullName.Replace("\\", "/");
            return root + "/" + projectRelative;
        }
    }
}
