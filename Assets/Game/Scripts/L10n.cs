using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace HoldTheCircle
{
    /// <summary>Uses Unity Localization for locale, tables and font assets.</summary>
    public static class L10n
    {
        public const string StringTable = "GameStrings";
        public const string AssetTable = "GameFonts";
        private sealed class Source { public string Text; }
        private readonly struct Template
        {
            public readonly Regex Pattern;
            public readonly string Translation;
            public Template(string source, string translation)
            {
                string pattern = Regex.Escape(source);
                pattern = Regex.Replace(pattern, @"\\\{(\d+)\}", m => $"(?<arg{m.Groups[1].Value}>.+?)");
                Pattern = new Regex("^" + pattern + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                Translation = translation;
            }
        }

        private static readonly ConditionalWeakTable<TextElement, Source> s_sources = new();
        private static readonly Dictionary<string, string> s_strings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> s_cache = new(StringComparer.Ordinal);
        private static readonly List<Template> s_templates = new();
        private static readonly Regex s_fragments = new(@"(<[^>]+>|\n)", RegexOptions.Compiled);
        public static event Action Changed;
        public static FontAsset UIFont { get; private set; }
        public static TMP_FontAsset TMPFont { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Changed = null;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            string saved = PlayerPrefs.GetString("HTC.Locale", "");
            if (!string.IsNullOrEmpty(saved))
            {
                Locale locale = LocalizationSettings.AvailableLocales.GetLocale(saved);
                if (locale != null) LocalizationSettings.SelectedLocale = locale;
            }
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            HandleLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        public static void SetLocale(string code)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return;
            PlayerPrefs.SetString("HTC.Locale", code);
            PlayerPrefs.Save();
            LocalizationSettings.SelectedLocale = locale;
        }

        private static void HandleLocaleChanged(Locale locale)
        {
            s_strings.Clear();
            s_templates.Clear();
            s_cache.Clear();
            var english = LocalizationSettings.StringDatabase.GetTable(StringTable,
                LocalizationSettings.AvailableLocales.GetLocale("en"));
            var selected = LocalizationSettings.StringDatabase.GetTable(StringTable, locale);
            if (english != null && selected != null)
            {
                foreach (var entry in english.Values)
                {
                    var translated = selected.GetEntry(entry.KeyId);
                    if (translated == null || string.IsNullOrEmpty(translated.Value)) continue;
                    s_strings[entry.Value] = translated.Value;
                    if (entry.Value.Contains("{0}"))
                        s_templates.Add(new Template(entry.Value, translated.Value));
                }
                // More specific templates win before generic "{0} shards" patterns.
                s_templates.Sort((a, b) => b.Pattern.ToString().Length.CompareTo(a.Pattern.ToString().Length));
            }
            UIFont = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>(AssetTable, "UIToolkit", locale);
            TMPFont = LocalizationSettings.AssetDatabase.GetLocalizedAsset<TMP_FontAsset>(AssetTable, "TextMeshPro", locale);
            Changed?.Invoke();
            Debug.Log($"[Localization] locale={locale.Identifier.Code}, entries={s_strings.Count}, uiFont={UIFont != null}, tmpFont={TMPFont != null}");
        }

        public static string Text(string english)
        {
            if (string.IsNullOrEmpty(english)) return english ?? "";
            if (LocalizationSettings.SelectedLocale.Identifier.Code == "en") return english;
            if (s_cache.TryGetValue(english, out string cached)) return cached;
            string translated = Translate(english);
            if (s_cache.Count >= 512) s_cache.Clear();
            s_cache[english] = translated;
            return translated;
        }

        private static string Translate(string source)
        {
            if (s_strings.TryGetValue(source, out string exact)) return exact;
            foreach (Template template in s_templates)
            {
                Match match = template.Pattern.Match(source);
                if (!match.Success) continue;
                var arguments = new object[4];
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = Text(match.Groups["arg" + i].Value);
                return string.Format(template.Translation, arguments);
            }
            // Composition boundaries preserve rich-text tags and authored line breaks.
            string[] pieces = s_fragments.Split(source);
            if (pieces.Length == 1) return source;
            for (int i = 0; i < pieces.Length; i++)
                if (!pieces[i].StartsWith("<") && pieces[i] != "\n") pieces[i] = Text(pieces[i]);
            return string.Concat(pieces);
        }

        public static void Set(TextElement element, string english)
        {
            s_sources.GetValue(element, _ => new Source()).Text = english;
            element.text = Text(english);
        }

        public static string SourceText(TextElement element) =>
            s_sources.TryGetValue(element, out Source source) ? source.Text : element.text;

        public static IDisposable RegisterRoot(VisualElement root)
        {
            void Refresh()
            {
                if (UIFont != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(UIFont);
                root.EnableInClassList("locale-zh", LocalizationSettings.SelectedLocale.Identifier.Code == "zh-Hans");
                root.Query<TextElement>().ForEach(element =>
                {
                    Source source = s_sources.GetValue(element, _ => new Source());
                    source.Text ??= element.text;
                    element.text = Text(source.Text);
                });
            }
            Changed += Refresh;
            Refresh();
            return new Subscription(() => Changed -= Refresh);
        }

        public static void Set(TMP_Text element, string english)
        {
            var binding = element.GetComponent<LocalizedRiteText>();
            if (binding == null) binding = element.gameObject.AddComponent<LocalizedRiteText>();
            binding.Set(english);
        }

        private sealed class Subscription : IDisposable
        {
            private Action m_dispose;
            public Subscription(Action dispose) => m_dispose = dispose;
            public void Dispose() { m_dispose?.Invoke(); m_dispose = null; }
        }
    }

    internal sealed class LocalizedRiteText : MonoBehaviour
    {
        private TMP_Text m_text;
        private string m_source = "";
        public void Set(string source) { m_source = source; Refresh(); }
        private void OnEnable() { m_text = GetComponent<TMP_Text>(); L10n.Changed += Refresh; Refresh(); }
        private void OnDisable() => L10n.Changed -= Refresh;
        private void Refresh()
        {
            if (m_text == null) return;
            if (L10n.TMPFont != null) m_text.font = L10n.TMPFont;
            m_text.text = L10n.Text(m_source);
        }
    }
}
