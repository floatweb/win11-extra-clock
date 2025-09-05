using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Win11_Extra_Clock.Services
{
    public sealed class LocalizationManager
    {

        public sealed class LanguageInfo
        {
            public string Code { get; init; } = "en-US";
            public string Name { get; init; } = "English (US)";
            public Dictionary<string, string> Strings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static LocalizationManager? _instance;
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        public event EventHandler? LanguageChanged;

        private readonly Dictionary<string, LanguageInfo> _langs = new(StringComparer.OrdinalIgnoreCase);
        private string? _overrideCode;
        private string _effectiveCode = "en-US";
        private string _folder = "";

        public IReadOnlyCollection<LanguageInfo> Available => _langs.Values.OrderBy(l => l.Name).ToArray();

        public string EffectiveCode => _effectiveCode;
        public string? OverrideCode => _overrideCode;

        private LocalizationManager() { }

        public void Initialize(string folder, string? overrideCode = null)
        {
            Console.WriteLine($"Initialize called with folder: '{folder}'");
            _folder = folder;
            Directory.CreateDirectory(_folder);
            Console.WriteLine($"Language folder path: '{_folder}', exists: {Directory.Exists(_folder)}");

            _langs.Clear();

            foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
            {
                try
                {
                    Console.WriteLine($"Processing language file: {file}");
                    var json = File.ReadAllText(file);
                    var root = JsonSerializer.Deserialize<JsonElement>(json);

                    string code = "en-US";
                    string name = "English (US)";

                    if (root.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
                    {
                        code = meta.TryGetProperty("code", out var c) ? (c.GetString() ?? code) : code;
                        name = meta.TryGetProperty("name", out var n) ? (n.GetString() ?? code) : code;
                        Console.WriteLine($"Found metadata in _meta: {code} ({name})");
                    }
                    else
                    {
                        code = root.TryGetProperty("code", out var c2) ? (c2.GetString() ?? code) : code;
                        name = root.TryGetProperty("name", out var n2) ? (n2.GetString() ?? code) : code;
                        Console.WriteLine($"Found metadata in root: {code} ({name})");
                    }

                    var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (root.TryGetProperty("strings", out var s) && s.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var kv in s.EnumerateObject())
                            strings[kv.Name] = kv.Value.GetString() ?? "";
                        Console.WriteLine($"Loaded {strings.Count} strings from 'strings' property");
                    }
                    else
                    {
                        // fallback
                        foreach (var kv in root.EnumerateObject())
                        {
                            if (kv.NameEquals("_meta") || kv.NameEquals("code") || kv.NameEquals("name"))
                                continue;
                            if (kv.Value.ValueKind == JsonValueKind.String)
                                strings[kv.Name] = kv.Value.GetString() ?? "";
                        }
                        Console.WriteLine($"Loaded {strings.Count} strings from root properties");
                    }

                    _langs[code] = new LanguageInfo { Code = code, Name = name, Strings = strings };
                    Console.WriteLine($"Added language: {code} ({name}) with {strings.Count} strings");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                }

            }

            // fallback
            if (!HasAnyEnglish())
            {
                Console.WriteLine("No English language found, adding fallback");
                _langs["en-US"] = new LanguageInfo
                {
                    Code = "en-US",
                    Name = "English (US)",
                    Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                };
            }

            Console.WriteLine($"Loaded languages: {string.Join(", ", _langs.Keys)}");
            SetLanguage(overrideCode);
        }

        public void SetLanguage(string? codeOrAuto)
        {
            _overrideCode = (string.IsNullOrWhiteSpace(codeOrAuto) || 
                             codeOrAuto.Equals("auto", StringComparison.OrdinalIgnoreCase)) 
                            ? null 
                            : codeOrAuto;

            var newEffective = ResolveEffectiveCode();
            Console.WriteLine($"SetLanguage: override={_overrideCode ?? "auto"}, effective={newEffective}");
            
            if (!newEffective.Equals(_effectiveCode, StringComparison.OrdinalIgnoreCase))
            {
                _effectiveCode = newEffective;
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string ResolveEffectiveCode()
        {
            string engFallback = DetermineEnglishFallback();

            // 1) override
            if (!string.IsNullOrWhiteSpace(_overrideCode))
            {
                if (_langs.ContainsKey(_overrideCode!)) return _overrideCode!;
                var pref = _overrideCode!.Split('-')[0];
                if (_langs.ContainsKey(pref)) return pref;
                var anyPref = _langs.Keys.FirstOrDefault(k => k.StartsWith(pref + "-", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(anyPref)) return anyPref!;
                return engFallback;
            }

            // 2) auto (system)
            var ui = CultureInfo.CurrentUICulture;
            var full = ui.Name;
            var two = ui.TwoLetterISOLanguageName;

            if (_langs.ContainsKey(full)) return full;
            if (_langs.ContainsKey(two)) return two;

            return engFallback;
        }

        private string DetermineEnglishFallback()
        {
            if (_langs.ContainsKey("en-US")) return "en-US";
            if (_langs.ContainsKey("en")) return "en";
            var anyEn = _langs.Keys.FirstOrDefault(k => k.StartsWith("en-", StringComparison.OrdinalIgnoreCase));
            return anyEn ?? "en-US";
        }

        private bool HasAnyEnglish()
        {
            return _langs.Keys.Any(k => k.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                                   k.StartsWith("en-", StringComparison.OrdinalIgnoreCase));
        }

        public string T(string key, string? fallback = null)
        {
            // Current language
            if (_langs.TryGetValue(_effectiveCode, out var lang) && 
                lang.Strings.TryGetValue(key, out var val) && 
                !string.IsNullOrEmpty(val))
                return val;

            // En fallback
            var engFallback = DetermineEnglishFallback();
            if (!engFallback.Equals(_effectiveCode, StringComparison.OrdinalIgnoreCase) &&
                _langs.TryGetValue(engFallback, out var engLang) &&
                engLang.Strings.TryGetValue(key, out var engVal) &&
                !string.IsNullOrEmpty(engVal))
                return engVal;

            return fallback ?? key;
        }
    }

}

