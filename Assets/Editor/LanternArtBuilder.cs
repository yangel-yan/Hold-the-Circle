using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoldTheCircle.Editor
{
    /// <summary>Builds runtime references from imported art without Inspector wiring.</summary>
    [InitializeOnLoad]
    public static class LanternArtBuilder
    {
        private const string k_root = "Assets/Game/Resources/Art";
        private const string k_skyTexture = "Assets/Game/Textures/LanternNightSky.png";
        private const string k_skyMaterial = k_root + "/LanternNightSky.mat";

        static LanternArtBuilder()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        private static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Material>(k_skyMaterial) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(k_skyTexture) != null)
            {
                BuildNightSkybox();
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(k_root + "/RiteAtlas.mat") == null ||
                AssetDatabase.LoadAssetAtPath<Material>(k_root + "/RiteRibbon.mat") == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(k_root + "/RiteQuad.asset") == null)
            {
                BuildRiteAssets();
            }
        }

        [MenuItem("Tools/Hold The Circle/Build Rite Assets")]
        public static void BuildRiteAssets()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(k_root + "/RiteInk.shader");
            if (shader == null || AssetDatabase.LoadAssetAtPath<Texture2D>(k_root + "/RiteAtlas.png") == null)
            {
                return;
            }

            ConfigureRiteTexture(k_root + "/RiteAtlas.png", 2048, true);
            ConfigureRiteTexture(k_root + "/RiteRibbon.png", 256, true);
            foreach (RiteMotif motif in System.Enum.GetValues(typeof(RiteMotif)))
            {
                ConfigureRiteTexture(k_root + "/Icons/" + motif + ".png", 256, false);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { k_root + "/Relics" }))
            {
                ConfigureRiteTexture(AssetDatabase.GUIDToAssetPath(guid), 256, false);
            }
            BuildRiteMaterial("RiteAtlas", shader);
            BuildRiteMaterial("RiteRibbon", shader);
            if (TMPro.TMP_Settings.defaultFontAsset != null)
            {
                string path = k_root + "/RiteNumbers.mat";
                Material numbers = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (numbers == null)
                {
                    numbers = new Material(TMPro.TMP_Settings.defaultFontAsset.material);
                    AssetDatabase.CreateAsset(numbers, path);
                }
                numbers.SetColor("_OutlineColor", new Color(0.025f, 0.04f, 0.07f));
                numbers.SetFloat("_OutlineWidth", 0.16f);
                numbers.EnableKeyword("OUTLINE_ON");
                EditorUtility.SetDirty(numbers);
            }

            if (AssetDatabase.LoadAssetAtPath<Mesh>(k_root + "/RiteQuad.asset") == null)
            {
                var mesh = new Mesh { name = "Shared Rite Quad" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
                };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, k_root + "/RiteQuad.asset");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Rite assets built: static atlas, shared quad/materials and Android ASTC textures.");
        }

        private static void BuildRiteMaterial(string name, Shader shader)
        {
            string path = k_root + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_root + "/" + name + ".png");
            material.SetColor("_Tint", Color.white);
            material.SetVector("_UvRect", new Vector4(0, 0, 1, 1));
            material.SetFloat("_Fill", 1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureRiteTexture(string path, int size, bool mipmaps)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = size,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            });
            importer.SaveAndReimport();
        }

        [MenuItem("Tools/Hold The Circle/Build Night Skybox")]
        public static void BuildNightSkybox()
        {
            var importer = AssetImporter.GetAtPath(k_skyTexture) as TextureImporter;
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (importer == null || shader == null)
            {
                return;
            }

            // Repeat longitude, clamp latitude and keep mipmaps for stable headset sampling.
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 4096,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            });
            importer.SaveAndReimport();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(k_skyMaterial);
            if (material == null)
            {
                System.IO.Directory.CreateDirectory(k_root);
                AssetDatabase.Refresh();
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, k_skyMaterial);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(k_skyTexture));
            material.SetColor("_Tint", Color.gray);
            material.SetFloat("_Exposure", 0.8f);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_Layout", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log("Lantern night skybox built: static panorama, Android ASTC 6x6, no runtime animation.");
        }

        [MenuItem("Tools/Hold The Circle/Rebuild Art Assets")]
        public static void Rebuild()
        {
            LanternRigBuilder.Build();
            BuildNightSkybox();
            BuildRiteAssets();
        }
    }
}
