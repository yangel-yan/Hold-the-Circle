using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TextCore.LowLevel;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace HoldTheCircle.Editor
{
    /// <summary>Idempotent authoring of the code-built game's bilingual tables and fonts.</summary>
    public static class LocalizationBuilder
    {
        [Serializable] private sealed class Dataset { public Translation[] entries; }
        [Serializable] private sealed class Translation { public string en; public string zh; }
        private const string k_directory = "Assets/Game/Localization";

        [MenuItem("Tools/Hold The Circle/Build Localization")]
        public static void Build()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                string guid = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" }).FirstOrDefault();
                settings = guid != null ? AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(guid))
                    : ScriptableObject.CreateInstance<LocalizationSettings>();
                if (!AssetDatabase.Contains(settings))
                    AssetDatabase.CreateAsset(settings, k_directory + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
                settings.GetStartupLocaleSelectors().Clear();
                settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = "zh-Hans" });
            }
            LocalizationSettings.InitializeSynchronously = true;
            foreach (string code in new[] { "en", "zh-Hans" })
            {
                if (LocalizationEditorSettings.GetLocales().Any(locale => locale.Identifier.Code == code)) continue;
                Locale locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, k_directory + "/" + code + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
            var locales = LocalizationEditorSettings.GetLocales().Where(locale =>
                locale.Identifier.Code == "en" || locale.Identifier.Code == "zh-Hans").ToList();
            var collection = LocalizationEditorSettings.GetStringTableCollection(L10n.StringTable)
                ?? LocalizationEditorSettings.CreateStringTableCollection(L10n.StringTable, k_directory, locales);
            Dataset data = JsonUtility.FromJson<Dataset>(File.ReadAllText(k_directory + "/GameStrings.json"));
            foreach (Translation entry in data.entries)
            {
                var key = collection.SharedData.GetEntry(entry.en) ?? collection.SharedData.AddKey(entry.en);
                foreach (var table in collection.StringTables)
                {
                    string value = table.LocaleIdentifier.Code == "zh-Hans" ? entry.zh : entry.en;
                    if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Empty translation: " + entry.en);
                    var existing = table.GetEntry(key.Id) ?? table.AddEntry(key.Id, value);
                    existing.Value = value;
                    EditorUtility.SetDirty(table);
                }
            }
            collection.SetPreloadTableFlag(true);
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(typeof(LocalizationBuilder), collection);

            Font source = AssetDatabase.LoadAssetAtPath<Font>(k_directory + "/Fonts/NotoSansCJKsc-Regular.otf");
            if (source == null) throw new InvalidOperationException("Noto Sans CJK source font has not been imported.");
            var fonts = LocalizationEditorSettings.GetAssetTableCollection(L10n.AssetTable)
                ?? LocalizationEditorSettings.CreateAssetTableCollection(L10n.AssetTable, k_directory, locales);
            string latin = new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray()) + "—×";
            string chinese = new string((latin + string.Concat(data.entries.Select(entry => entry.zh)) +
                "中文简体使用界面").Distinct().ToArray());
            foreach (Locale locale in locales)
            {
                string code = locale.Identifier.Code;
                string chars = code == "zh-Hans" ? chinese : latin;
                string uiPath = k_directory + "/Fonts/" + code + "-UIToolkit.asset";
                var ui = AssetDatabase.LoadAssetAtPath<FontAsset>(uiPath);
                if (ui == null)
                {
                    ui = FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                        UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
                    ui.name = code + "-UIToolkit";
                    ui.TryAddCharacters(chars, out string missing);
                    if (!string.IsNullOrEmpty(missing)) throw new InvalidOperationException("UI font missing: " + missing);
                    SaveFont(ui, ui.material, ui.atlasTextures, uiPath);
                }
                string tmpPath = k_directory + "/Fonts/" + code + "-TextMeshPro.asset";
                var tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpPath);
                if (tmp == null)
                {
                    tmp = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                        TMPro.AtlasPopulationMode.Dynamic, true);
                    tmp.name = code + "-TextMeshPro";
                    tmp.TryAddCharacters(chars, out string missing);
                    if (!string.IsNullOrEmpty(missing)) throw new InvalidOperationException("TMP font missing: " + missing);
                    SaveFont(tmp, tmp.material, tmp.atlasTextures, tmpPath);
                }
                fonts.AddAssetToTable(locale.Identifier, "UIToolkit", ui);
                fonts.AddAssetToTable(locale.Identifier, "TextMeshPro", tmp);
            }
            fonts.SetPreloadTableFlag(true);
            EditorUtility.SetDirty(fonts);
            EditorUtility.SetDirty(fonts.SharedData);
            foreach (var table in fonts.AssetTables) EditorUtility.SetDirty(table);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(typeof(LocalizationBuilder), fonts);
            EditorUtility.SetDirty(settings);
            var sky = Resources.Load<Material>(ArenaSkyEnvironment.MaterialResource);
            sky.shaderKeywords = Array.Empty<string>();
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            Debug.Log("[LocalizationBuilder] Created bilingual tables and locale-specific UI/TMP fonts. " + Validate());
        }

        private static void SaveFont(UnityEngine.Object font, Material material, Texture2D[] textures, string path)
        {
            AssetDatabase.CreateAsset(font, path);
            foreach (Texture2D texture in textures)
            {
                texture.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(texture);
            }
            material.name = font.name + " Material";
            material.mainTexture = textures[0];
            AssetDatabase.AddObjectToAsset(material, font);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(font);
        }

        public static string Validate()
        {
            int count = 0;
            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
            foreach (var key in collection.SharedData.Entries)
            foreach (var table in collection.StringTables)
            {
                count++;
                if (string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value))
                    throw new InvalidOperationException($"Missing {table.LocaleIdentifier.Code}: {key.Key}");
            }
            if (count == 0) throw new InvalidOperationException("No entries checked.");
            return $"{count} entries checked; zero gaps.";
        }

        [MenuItem("Tools/Hold The Circle/Build Localization Content")]
        public static void BuildContent()
        {
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("[LocalizationBuilder] Addressables built successfully.");
        }
    }
}
