using System;
using System.Collections.Generic;
using UnityEngine;

namespace BitBox.Library.Localization
{
    public static class GameText
    {
        private static readonly HashSet<string> MissingKeys = new HashSet<string>(StringComparer.Ordinal);

        private static LocalizationTable _table;
        private static int _currentLanguageIndex = -1;
        private static int _fallbackLanguageIndex = -1;
        private static string _fallbackLanguageId = string.Empty;

        public static event Action<string> LanguageChanged;

        public static string CurrentLanguageId { get; private set; } = string.Empty;

        public static void Initialize(LocalizationTable table, string initialLanguageId = null)
        {
            _table = table;
            MissingKeys.Clear();

            if (_table == null)
            {
                _fallbackLanguageId = string.Empty;
                _fallbackLanguageIndex = -1;
                SetCurrentLanguageWithoutEvent(initialLanguageId);
                Debug.LogWarning("GameText was initialized without a LocalizationTable.");
                return;
            }

            _table.RebuildLookup();
            _fallbackLanguageId = _table.ResolveFallbackLanguageId();
            _fallbackLanguageIndex = ResolveLanguageIndex(_fallbackLanguageId);
            SetCurrentLanguageWithoutEvent(initialLanguageId);
        }

        public static void SetLanguage(string languageId)
        {
            string previousLanguageId = CurrentLanguageId;
            SetCurrentLanguageWithoutEvent(languageId);

            if (string.Equals(previousLanguageId, CurrentLanguageId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Action<string> handler = LanguageChanged;
            if (handler != null)
            {
                handler(CurrentLanguageId);
            }
        }

        public static void ClearMissingKeyWarnings()
        {
            MissingKeys.Clear();
        }

        public static string Get(string key)
        {
            return ResolveText(key);
        }

        public static string Get(string key, params object[] args)
        {
            string text = ResolveText(key);
            if (args == null || args.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(text, args);
            }
            catch (FormatException exception)
            {
                Debug.LogWarning($"Localization formatting failed for key '{key}': {exception.Message}");
                return text;
            }
        }

        private static void SetCurrentLanguageWithoutEvent(string requestedLanguageId)
        {
            if (_table == null)
            {
                CurrentLanguageId = LocalizationTable.NormalizeLanguageId(requestedLanguageId);
                _currentLanguageIndex = -1;
                return;
            }

            CurrentLanguageId = _table.ResolveLanguageId(requestedLanguageId);
            _currentLanguageIndex = ResolveLanguageIndex(CurrentLanguageId);
            _fallbackLanguageId = _table.ResolveFallbackLanguageId();
            _fallbackLanguageIndex = ResolveLanguageIndex(_fallbackLanguageId);
        }

        private static int ResolveLanguageIndex(string languageId)
        {
            if (_table == null)
            {
                return -1;
            }

            return _table.TryGetLanguageIndex(languageId, out int languageIndex)
                ? languageIndex
                : -1;
        }

        private static string ResolveText(string key)
        {
            string sanitizedKey = key ?? string.Empty;

            if (_table != null && _table.TryGetEntry(sanitizedKey, out LocalizationEntry entry))
            {
                if (_currentLanguageIndex >= 0 && entry.TryGetTranslation(_currentLanguageIndex, out string localizedText))
                {
                    return localizedText;
                }

                if (_fallbackLanguageIndex >= 0
                    && _fallbackLanguageIndex != _currentLanguageIndex
                    && entry.TryGetTranslation(_fallbackLanguageIndex, out string fallbackText))
                {
                    return fallbackText;
                }
            }

            return BuildMissingValue(sanitizedKey);
        }

        private static string BuildMissingValue(string key)
        {
            if (MissingKeys.Add(key))
            {
                if (_table == null)
                {
                    Debug.LogWarning($"Localization table is not initialized. Missing key '{key}'.");
                }
                else
                {
                    string languageLabel = string.IsNullOrWhiteSpace(CurrentLanguageId) ? "<unset>" : CurrentLanguageId;
                    Debug.LogWarning($"Missing localization key '{key}' for language '{languageLabel}'.");
                }
            }

            return $"[MISSING:{key}]";
        }
    }
}
