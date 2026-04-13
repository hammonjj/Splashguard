using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Library.Localization
{
    [Serializable]
    [HideReferenceObjectPicker]
    public sealed class LocalizationEntry
    {
        [SerializeField, HideInInspector]
        private string _key = string.Empty;

        [SerializeField, HideInInspector]
        private List<LocalizedValue> _translations = new List<LocalizedValue>();

        [NonSerialized]
        private string[] _runtimeTranslations = Array.Empty<string>();

        [NonSerialized]
        private bool _hasDuplicateKey;

        [NonSerialized]
        private bool _hasEmptyKey;

        [NonSerialized]
        private bool _missingFallbackTranslation;

        [NonSerialized]
        private bool _hasMissingTranslations;

        [NonSerialized]
        private bool _hasOrphanedTranslations;

        [ShowInInspector, ReadOnly, TableColumnWidth(120, Resizable = true)]
        public string Group => ResolveGroup(_key);

        [ShowInInspector, TableColumnWidth(260, Resizable = true)]
        public string Key
        {
            get
            {
                return _key;
            }
            set
            {
                _key = value == null ? string.Empty : value.Trim();
            }
        }

        [ShowInInspector]
        [ListDrawerSettings(DraggableItems = false, ShowFoldout = true)]
        public List<LocalizedValue> SerializedTranslations => _translations;

        public bool SanitizeTranslations()
        {
            bool changed = false;

            if (_translations == null)
            {
                _translations = new List<LocalizedValue>();
                return true;
            }

            List<LocalizedValue> sanitizedTranslations = new List<LocalizedValue>(_translations.Count);
            HashSet<string> seenLanguageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < _translations.Count; index++)
            {
                LocalizedValue localizedValue = _translations[index];
                if (localizedValue == null)
                {
                    changed = true;
                    continue;
                }

                if (localizedValue.Sanitize())
                {
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(localizedValue.LanguageId))
                {
                    changed = true;
                    continue;
                }

                if (!seenLanguageIds.Add(localizedValue.LanguageId))
                {
                    changed = true;
                    continue;
                }

                sanitizedTranslations.Add(localizedValue);
            }

            if (changed || sanitizedTranslations.Count != _translations.Count)
            {
                _translations = sanitizedTranslations;
                changed = true;
            }

            _translations.Sort(CompareLocalizedValues);
            return changed;
        }

        public bool EnsureTranslationsForLanguages(IReadOnlyList<LocalizationLanguageDefinition> languages)
        {
            bool changed = SanitizeTranslations();

            if (languages == null)
            {
                return changed;
            }

            for (int index = 0; index < languages.Count; index++)
            {
                LocalizationLanguageDefinition language = languages[index];
                if (language == null || string.IsNullOrWhiteSpace(language.Id))
                {
                    continue;
                }

                if (FindTranslationIndex(language.Id) >= 0)
                {
                    continue;
                }

                _translations.Add(new LocalizedValue(language.Id, string.Empty));
                changed = true;
            }

            if (changed)
            {
                _translations.Sort(CompareLocalizedValues);
            }

            return changed;
        }

        public void PrepareForRuntime(Dictionary<string, int> languageIndexLookup, int languageCount)
        {
            SanitizeTranslations();

            if (_runtimeTranslations == null || _runtimeTranslations.Length != languageCount)
            {
                _runtimeTranslations = new string[languageCount];
            }
            else
            {
                Array.Clear(_runtimeTranslations, 0, _runtimeTranslations.Length);
            }

            for (int translationIndex = 0; translationIndex < _translations.Count; translationIndex++)
            {
                LocalizedValue localizedValue = _translations[translationIndex];
                if (localizedValue == null || string.IsNullOrWhiteSpace(localizedValue.LanguageId))
                {
                    continue;
                }

                if (!languageIndexLookup.TryGetValue(localizedValue.LanguageId, out int languageIndex))
                {
                    continue;
                }

                _runtimeTranslations[languageIndex] = localizedValue.Text;
            }
        }

        public bool TryGetTranslation(int languageIndex, out string translation)
        {
            if (_runtimeTranslations == null
                || languageIndex < 0
                || languageIndex >= _runtimeTranslations.Length)
            {
                translation = string.Empty;
                return false;
            }

            translation = _runtimeTranslations[languageIndex];
            return !string.IsNullOrWhiteSpace(translation);
        }

        public string GetSerializedTranslation(string languageId)
        {
            int translationIndex = FindTranslationIndex(languageId);
            if (translationIndex < 0)
            {
                return string.Empty;
            }

            LocalizedValue localizedValue = _translations[translationIndex];
            return localizedValue == null ? string.Empty : localizedValue.Text;
        }

        public void SetSerializedTranslation(string languageId, string value)
        {
            string normalizedLanguageId = LocalizationTable.NormalizeLanguageId(languageId);
            if (string.IsNullOrWhiteSpace(normalizedLanguageId))
            {
                return;
            }

            int translationIndex = FindTranslationIndex(normalizedLanguageId);
            if (translationIndex < 0)
            {
                _translations.Add(new LocalizedValue(normalizedLanguageId, value));
                _translations.Sort(CompareLocalizedValues);
                return;
            }

            LocalizedValue localizedValue = _translations[translationIndex];
            if (localizedValue == null)
            {
                _translations[translationIndex] = new LocalizedValue(normalizedLanguageId, value);
                return;
            }

            localizedValue.Text = value;
        }

        public bool RemoveTranslationsForLanguage(string languageId)
        {
            string normalizedLanguageId = LocalizationTable.NormalizeLanguageId(languageId);
            if (string.IsNullOrWhiteSpace(normalizedLanguageId) || _translations == null || _translations.Count == 0)
            {
                return false;
            }

            int removedCount = _translations.RemoveAll(localizedValue =>
                localizedValue != null
                && string.Equals(localizedValue.LanguageId, normalizedLanguageId, StringComparison.OrdinalIgnoreCase));

            return removedCount > 0;
        }

        public bool RemoveOrphanedTranslations(HashSet<string> validLanguageIds)
        {
            if (_translations == null || _translations.Count == 0)
            {
                return false;
            }

            int removedCount = _translations.RemoveAll(localizedValue =>
                localizedValue == null
                || string.IsNullOrWhiteSpace(localizedValue.LanguageId)
                || !validLanguageIds.Contains(localizedValue.LanguageId));

            return removedCount > 0;
        }

        public bool IsTranslationMissing(string languageId)
        {
            string translation = GetSerializedTranslation(languageId);
            return string.IsNullOrWhiteSpace(translation);
        }

        public List<string> GetMissingLanguageIds(IReadOnlyList<LocalizationLanguageDefinition> languages)
        {
            List<string> missingLanguageIds = new List<string>();
            if (languages == null)
            {
                return missingLanguageIds;
            }

            for (int index = 0; index < languages.Count; index++)
            {
                LocalizationLanguageDefinition language = languages[index];
                if (language == null || string.IsNullOrWhiteSpace(language.Id))
                {
                    continue;
                }

                if (IsTranslationMissing(language.Id))
                {
                    missingLanguageIds.Add(language.Id);
                }
            }

            return missingLanguageIds;
        }

        public List<string> GetOrphanedLanguageIds(HashSet<string> validLanguageIds)
        {
            List<string> orphanedLanguageIds = new List<string>();
            if (_translations == null || _translations.Count == 0)
            {
                return orphanedLanguageIds;
            }

            HashSet<string> addedLanguageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < _translations.Count; index++)
            {
                LocalizedValue localizedValue = _translations[index];
                if (localizedValue == null || string.IsNullOrWhiteSpace(localizedValue.LanguageId))
                {
                    continue;
                }

                if (validLanguageIds.Contains(localizedValue.LanguageId))
                {
                    continue;
                }

                if (addedLanguageIds.Add(localizedValue.LanguageId))
                {
                    orphanedLanguageIds.Add(localizedValue.LanguageId);
                }
            }

            orphanedLanguageIds.Sort(StringComparer.OrdinalIgnoreCase);
            return orphanedLanguageIds;
        }

        public void UpdateValidationState(
            bool hasDuplicateKey,
            bool hasEmptyKey,
            bool missingFallbackTranslation,
            bool hasMissingTranslations,
            bool hasOrphanedTranslations)
        {
            _hasDuplicateKey = hasDuplicateKey;
            _hasEmptyKey = hasEmptyKey;
            _missingFallbackTranslation = missingFallbackTranslation;
            _hasMissingTranslations = hasMissingTranslations;
            _hasOrphanedTranslations = hasOrphanedTranslations;
        }

        public bool HasContentIssues()
        {
            return _hasDuplicateKey
                || _hasEmptyKey
                || _missingFallbackTranslation
                || _hasMissingTranslations
                || _hasOrphanedTranslations;
        }

        public static string ResolveGroup(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "general";
            }

            int separatorIndex = key.IndexOf('.');
            if (separatorIndex <= 0)
            {
                return "general";
            }

            return key.Substring(0, separatorIndex);
        }

        private int FindTranslationIndex(string languageId)
        {
            string normalizedLanguageId = LocalizationTable.NormalizeLanguageId(languageId);
            if (string.IsNullOrWhiteSpace(normalizedLanguageId) || _translations == null)
            {
                return -1;
            }

            for (int index = 0; index < _translations.Count; index++)
            {
                LocalizedValue localizedValue = _translations[index];
                if (localizedValue == null)
                {
                    continue;
                }

                if (string.Equals(localizedValue.LanguageId, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CompareLocalizedValues(LocalizedValue left, LocalizedValue right)
        {
            string leftLanguageId = left == null ? string.Empty : left.LanguageId;
            string rightLanguageId = right == null ? string.Empty : right.LanguageId;
            return StringComparer.OrdinalIgnoreCase.Compare(leftLanguageId, rightLanguageId);
        }

        [Serializable]
        [HideReferenceObjectPicker]
        public sealed class LocalizedValue
        {
            [SerializeField]
            private string _languageId = string.Empty;

            [SerializeField]
            [TextArea(1, 4)]
            private string _text = string.Empty;

            public LocalizedValue(string languageId, string text)
            {
                _languageId = LocalizationTable.NormalizeLanguageId(languageId);
                _text = text ?? string.Empty;
            }

            [ShowInInspector, TableColumnWidth(100, Resizable = true)]
            public string LanguageId
            {
                get
                {
                    return _languageId;
                }
                set
                {
                    _languageId = LocalizationTable.NormalizeLanguageId(value);
                }
            }

            [ShowInInspector, TableColumnWidth(340, Resizable = true)]
            public string Text
            {
                get
                {
                    return _text;
                }
                set
                {
                    _text = value ?? string.Empty;
                }
            }

            public bool Sanitize()
            {
                bool changed = false;
                string normalizedLanguageId = LocalizationTable.NormalizeLanguageId(_languageId);
                if (!string.Equals(_languageId, normalizedLanguageId, StringComparison.Ordinal))
                {
                    _languageId = normalizedLanguageId;
                    changed = true;
                }

                if (_text == null)
                {
                    _text = string.Empty;
                    changed = true;
                }

                return changed;
            }
        }
    }
}
