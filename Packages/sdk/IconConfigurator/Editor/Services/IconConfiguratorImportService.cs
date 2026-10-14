using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.IconConfigurator.Editor
{
    public class IconConfiguratorImportService
    {
        private const string k_OutputExtension = ".png";
        private const string k_SupportedFormatText = "PNG, JPG, JPEG, or WEBP";

        public static string[] GetSupportedImageFileFilters()
        {
            return new[] { "Image files", "png,jpg,jpeg,webp" };
        }

        public IconLayerConfig ImportLayerFromDialog(IconLayerKind layerKind)
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                $"Select {layerKind} image",
                string.Empty,
                GetSupportedImageFileFilters());

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            return ImportLayer(sourcePath, layerKind);
        }

        public bool TryImportLayer(string sourcePath, IconLayerKind layerKind, out IconLayerConfig importedLayer, out string errorMessage)
        {
            importedLayer = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                errorMessage = "File path is empty.";
                return false;
            }

            if (!File.Exists(sourcePath))
            {
                errorMessage = "File does not exist.";
                return false;
            }

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!IsSupportedImageExtension(extension))
            {
                errorMessage = $"Only {k_SupportedFormatText} files are supported.";
                return false;
            }

            try
            {
                IconLayerConfig layer = ImportLayer(sourcePath, layerKind);
                if (layer?.Texture == null)
                {
                    errorMessage = "Failed to import texture.";
                    return false;
                }

                if (layerKind == IconLayerKind.Background && !HasOpaqueContent(layer.Texture))
                {
                    if (!string.IsNullOrWhiteSpace(layer.AssetPath))
                    {
                        AssetDatabase.DeleteAsset(layer.AssetPath);
                    }

                    errorMessage = "Background layer must contain opaque pixels.";
                    return false;
                }

                importedLayer = layer;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        public IconLayerConfig ImportLayer(string sourcePath, IconLayerKind layerKind)
        {
            EnsureFolder(IconConfiguratorPaths.RootDirectory);
            EnsureFolder(IconConfiguratorPaths.ImportedDirectory);

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!IsSupportedImageExtension(extension))
            {
                throw new InvalidOperationException($"Only {k_SupportedFormatText} files are supported.");
            }

            Texture2D sourceTexture = LoadSourceTexture(sourcePath, sourceBytes, extension);

            Texture2D normalizedTexture = IconTextureUtility.NormalizeToSquare(sourceTexture);
            byte[] normalizedBytes = normalizedTexture.EncodeToPNG();
            string hash = ComputeHash(normalizedBytes);

            string fileName = $"{layerKind.ToString().ToLowerInvariant()}_{hash.Substring(0, 8)}{k_OutputExtension}";
            string assetPath = $"{IconConfiguratorPaths.ImportedDirectory}/{fileName}";

            if (!File.Exists(assetPath))
            {
                File.WriteAllBytes(assetPath, normalizedBytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureTextureImporter(assetPath);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            UnityEngine.Object.DestroyImmediate(normalizedTexture);
            UnityEngine.Object.DestroyImmediate(sourceTexture);

            return new IconLayerConfig
            {
                LayerKind = layerKind,
                AssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                AssetPath = assetPath,
                OriginalFileName = Path.GetFileName(sourcePath),
                ContentHash = hash,
                DisplayName = Path.GetFileNameWithoutExtension(sourcePath),
                SourceWidth = texture != null ? texture.width : 0,
                SourceHeight = texture != null ? texture.height : 0,
                Texture = texture,
            };
        }

        private static bool HasOpaqueContent(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.99f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSupportedImageExtension(string extension)
        {
            return extension == ".png"
                || extension == ".jpg"
                || extension == ".jpeg"
                || extension == ".webp";
        }

        private static Texture2D LoadSourceTexture(string sourcePath, byte[] sourceBytes, string extension)
        {
            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (sourceTexture.LoadImage(sourceBytes))
            {
                return sourceTexture;
            }

            UnityEngine.Object.DestroyImmediate(sourceTexture);

            // Some editor-supported formats, such as WEBP, may not be supported by Texture2D.LoadImage.
            string temporaryAssetPath = $"{IconConfiguratorPaths.ImportedDirectory}/__source_{Guid.NewGuid():N}{extension}";
            File.Copy(sourcePath, temporaryAssetPath);
            AssetDatabase.ImportAsset(temporaryAssetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(temporaryAssetPath);

            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(temporaryAssetPath);
            if (importedTexture == null)
            {
                AssetDatabase.DeleteAsset(temporaryAssetPath);
                throw new InvalidOperationException($"Failed to decode image. Supported formats are {k_SupportedFormatText}.");
            }

            Texture2D readableCopy = new Texture2D(importedTexture.width, importedTexture.height, TextureFormat.RGBA32, false);
            readableCopy.SetPixels(importedTexture.GetPixels());
            readableCopy.Apply();
            AssetDatabase.DeleteAsset(temporaryAssetPath);
            return readableCopy;
        }

        private static string ComputeHash(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void ConfigureTextureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                return;
            }

            bool changed = false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string folderName = path.Substring(path.LastIndexOf('/') + 1);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
