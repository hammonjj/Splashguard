using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BitBox.Library.Localization
{
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "BitBox/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Config/Localization/LocalizationTable.asset";
        public const string EnglishLanguageId = "en";
        public const string SpanishLanguageId = "es";

        private static readonly StringComparer KeyComparer = StringComparer.Ordinal;
        private static readonly StringComparer LanguageComparer = StringComparer.OrdinalIgnoreCase;

        [SerializeField]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true)]
        private List<LocalizationLanguageDefinition> _languages = new List<LocalizationLanguageDefinition>();

        [SerializeField]
        [ValueDropdown(nameof(GetLanguageDropdownItems))]
        private string _defaultLanguageId = EnglishLanguageId;

        [SerializeField]
        [ValueDropdown(nameof(GetLanguageDropdownItems))]
        private string _fallbackLanguageId = EnglishLanguageId;

        [SerializeField]
        [Searchable]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true)]
        private List<LocalizationEntry> _entries = new List<LocalizationEntry>();

        [NonSerialized]
        private Dictionary<string, LocalizationEntry> _entryLookup;

        [NonSerialized]
        private Dictionary<string, int> _languageIndexLookup;

        [NonSerialized]
        private List<LocalizationLanguageDefinition> _runtimeLanguages;

        [NonSerialized]
        private string _validationSummary = "Validation has not been run.";

        [NonSerialized]
        private List<string> _languageIssues;

        [NonSerialized]
        private List<string> _duplicateKeyIssues;

        [NonSerialized]
        private List<string> _emptyKeyIssues;

        [NonSerialized]
        private List<string> _missingFallbackIssues;

        [NonSerialized]
        private List<string> _missingTranslationIssues;

        [NonSerialized]
        private List<string> _orphanedTranslationIssues;

        [ShowInInspector, ReadOnly, PropertyOrder(100), LabelText("Languages")]
        public int LanguageCount => _runtimeLanguages == null ? 0 : _runtimeLanguages.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(101), LabelText("Entries")]
        public int EntryCount => _entries == null ? 0 : _entries.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(102), LabelText("Language Issues")]
        public int LanguageIssueCount => _languageIssues == null ? 0 : _languageIssues.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(103), LabelText("Duplicate Keys")]
        public int DuplicateKeyCount => _duplicateKeyIssues == null ? 0 : _duplicateKeyIssues.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(104), LabelText("Empty Keys")]
        public int EmptyKeyCount => _emptyKeyIssues == null ? 0 : _emptyKeyIssues.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(105), LabelText("Missing Fallback")]
        public int MissingFallbackCount => _missingFallbackIssues == null ? 0 : _missingFallbackIssues.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(106), LabelText("Missing Translations")]
        public int MissingTranslationCount => _missingTranslationIssues == null ? 0 : _missingTranslationIssues.Count;

        [ShowInInspector, ReadOnly, PropertyOrder(107), LabelText("Orphaned Translations")]
        public int OrphanedTranslationCount => _orphanedTranslationIssues == null ? 0 : _orphanedTranslationIssues.Count;

        [ShowInInspector, ReadOnly, MultiLineProperty(8), PropertyOrder(108), LabelText("Validation Summary")]
        public string ValidationSummary => _validationSummary;

        public IReadOnlyList<LocalizationLanguageDefinition> Languages => _languages;

        public IReadOnlyList<LocalizationLanguageDefinition> RuntimeLanguages => _runtimeLanguages;

        public IReadOnlyList<LocalizationEntry> Entries => _entries;

        public IReadOnlyList<string> LanguageIssues => _languageIssues;

        public IReadOnlyList<string> DuplicateKeyIssues => _duplicateKeyIssues;

        public IReadOnlyList<string> EmptyKeyIssues => _emptyKeyIssues;

        public IReadOnlyList<string> MissingFallbackIssues => _missingFallbackIssues;

        public IReadOnlyList<string> MissingTranslationIssues => _missingTranslationIssues;

        public IReadOnlyList<string> OrphanedTranslationIssues => _orphanedTranslationIssues;

        public string DefaultLanguageId => ResolveDefaultLanguageId();

        public string FallbackLanguageId => ResolveFallbackLanguageId();

        public bool HasValidationIssues =>
            LanguageIssueCount > 0
            || DuplicateKeyCount > 0
            || EmptyKeyCount > 0
            || MissingFallbackCount > 0
            || MissingTranslationCount > 0
            || OrphanedTranslationCount > 0;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public static string NormalizeLanguageId(string languageId)
        {
            return string.IsNullOrWhiteSpace(languageId)
                ? string.Empty
                : languageId.Trim().ToLowerInvariant();
        }

        public bool EnsureCollectionsInitialized()
        {
            bool changed = false;

            if (_languages == null)
            {
                _languages = new List<LocalizationLanguageDefinition>();
                changed = true;
            }

            if (_entries == null)
            {
                _entries = new List<LocalizationEntry>();
                changed = true;
            }

            return changed;
        }

        public void EnsureSeedData()
        {
            bool changed = EnsureCollectionsInitialized();

            if (!HasSerializedLanguage(EnglishLanguageId))
            {
                _languages.Add(new LocalizationLanguageDefinition(EnglishLanguageId, "English"));
                changed = true;
            }

            if (!HasSerializedLanguage(SpanishLanguageId))
            {
                _languages.Add(new LocalizationLanguageDefinition(SpanishLanguageId, "Spanish", "Espanol"));
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(_defaultLanguageId))
            {
                _defaultLanguageId = EnglishLanguageId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(_fallbackLanguageId))
            {
                _fallbackLanguageId = EnglishLanguageId;
                changed = true;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                LocalizationEntry entry = _entries[index];
                if (entry == null)
                {
                    entry = new LocalizationEntry();
                    _entries[index] = entry;
                    changed = true;
                }

                if (entry.EnsureTranslationsForLanguages(_languages))
                {
                    changed = true;
                }
            }

            RebuildLookup();

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void RebuildLookup()
        {
            bool structureChanged = EnsureCollectionsInitialized();

            EnsureValidationCollections();
            _languageIssues.Clear();
            _duplicateKeyIssues.Clear();
            _emptyKeyIssues.Clear();
            _missingFallbackIssues.Clear();
            _missingTranslationIssues.Clear();
            _orphanedTranslationIssues.Clear();

            _entryLookup = new Dictionary<string, LocalizationEntry>(_entries.Count, KeyComparer);
            _languageIndexLookup = new Dictionary<string, int>(LanguageComparer);
            _runtimeLanguages = new List<LocalizationLanguageDefinition>(_languages.Count);

            for (int languageIndex = 0; languageIndex < _languages.Count; languageIndex++)
            {
                LocalizationLanguageDefinition language = _languages[languageIndex];
                if (language == null)
                {
                    language = new LocalizationLanguageDefinition();
                    _languages[languageIndex] = language;
                    structureChanged = true;
                }

                if (language.Sanitize())
                {
                    structureChanged = true;
                }

                if (string.IsNullOrWhiteSpace(language.Id))
                {
                    _languageIssues.Add($"Language row {languageIndex + 1} has an empty id.");
                    continue;
                }

                if (_languageIndexLookup.ContainsKey(language.Id))
                {
                    _languageIssues.Add($"Duplicate language id '{language.Id}'.");
                    continue;
                }

                _languageIndexLookup.Add(language.Id, _runtimeLanguages.Count);
                _runtimeLanguages.Add(language);
            }

            string resolvedDefaultLanguageId = ResolveConfiguredDefaultLanguageId();
            if (!string.Equals(_defaultLanguageId, resolvedDefaultLanguageId, StringComparison.Ordinal))
            {
                _defaultLanguageId = resolvedDefaultLanguageId;
                structureChanged = true;
            }

            string resolvedFallbackLanguageId = ResolveConfiguredFallbackLanguageId();
            if (!string.Equals(_fallbackLanguageId, resolvedFallbackLanguageId, StringComparison.Ordinal))
            {
                _fallbackLanguageId = resolvedFallbackLanguageId;
                structureChanged = true;
            }

            if (_runtimeLanguages.Count == 0)
            {
                _languageIssues.Add("No valid languages are configured.");
            }

            Dictionary<string, int> keyUsage = new Dictionary<string, int>(KeyComparer);
            HashSet<string> validLanguageIds = new HashSet<string>(LanguageComparer);
            HashSet<string> loggedDuplicateKeys = new HashSet<string>(KeyComparer);

            for (int index = 0; index < _runtimeLanguages.Count; index++)
            {
                validLanguageIds.Add(_runtimeLanguages[index].Id);
            }

            for (int entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                LocalizationEntry entry = _entries[entryIndex];
                if (entry == null)
                {
                    entry = new LocalizationEntry();
                    _entries[entryIndex] = entry;
                    structureChanged = true;
                }

                if (entry.EnsureTranslationsForLanguages(_runtimeLanguages))
                {
                    structureChanged = true;
                }

                if (entry.SanitizeTranslations())
                {
                    structureChanged = true;
                }

                string key = entry.Key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (keyUsage.ContainsKey(key))
                {
                    keyUsage[key]++;
                }
                else
                {
                    keyUsage.Add(key, 1);
                }
            }

            for (int entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                LocalizationEntry entry = _entries[entryIndex];
                string key = entry.Key;
                bool hasEmptyKey = string.IsNullOrWhiteSpace(key);
                bool hasDuplicateKey = !hasEmptyKey && keyUsage.TryGetValue(key, out int keyCount) && keyCount > 1;
                bool missingFallback = !string.IsNullOrWhiteSpace(_fallbackLanguageId) && entry.IsTranslationMissing(_fallbackLanguageId);
                List<string> missingLanguageIds = entry.GetMissingLanguageIds(_runtimeLanguages);
                List<string> orphanedLanguageIds = entry.GetOrphanedLanguageIds(validLanguageIds);

                if (hasEmptyKey)
                {
                    _emptyKeyIssues.Add($"Entry row {entryIndex + 1} has an empty key.");
                }

                if (hasDuplicateKey)
                {
                    _duplicateKeyIssues.Add($"Key '{key}' is duplicated.");

                    if (Application.isPlaying && loggedDuplicateKeys.Add(key))
                    {
                        Debug.LogWarning(
                            $"LocalizationTable '{name}' contains duplicate key '{key}'. The first entry will be used.",
                            this);
                    }
                }

                if (missingFallback)
                {
                    _missingFallbackIssues.Add(
                        $"{DescribeEntryKey(key, entryIndex)} is missing fallback '{_fallbackLanguageId}'.");
                }

                if (missingLanguageIds.Count > 0)
                {
                    _missingTranslationIssues.Add(
                        $"{DescribeEntryKey(key, entryIndex)} is missing translations for {JoinLanguageIds(missingLanguageIds)}.");
                }

                if (orphanedLanguageIds.Count > 0)
                {
                    _orphanedTranslationIssues.Add(
                        $"{DescribeEntryKey(key, entryIndex)} contains orphaned translations for {JoinLanguageIds(orphanedLanguageIds)}.");
                }

                entry.UpdateValidationState(
                    hasDuplicateKey,
                    hasEmptyKey,
                    missingFallback,
                    missingLanguageIds.Count > 0,
                    orphanedLanguageIds.Count > 0);

                entry.PrepareForRuntime(_languageIndexLookup, _runtimeLanguages.Count);

                if (hasEmptyKey || _entryLookup.ContainsKey(key))
                {
                    continue;
                }

                _entryLookup.Add(key, entry);
            }

            _validationSummary = BuildValidationSummary();

#if UNITY_EDITOR
            if (structureChanged && !Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public bool TryGetEntry(string key, out LocalizationEntry entry)
        {
            if (_entryLookup == null)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                entry = null;
                return false;
            }

            return _entryLookup.TryGetValue(key, out entry);
        }

        public bool TryGetLanguageIndex(string languageId, out int languageIndex)
        {
            if (_languageIndexLookup == null)
            {
                RebuildLookup();
            }

            return _languageIndexLookup.TryGetValue(NormalizeLanguageId(languageId), out languageIndex);
        }

        public bool HasLanguage(string languageId)
        {
            return TryGetLanguageIndex(languageId, out _);
        }

        public string ResolveLanguageId(string requestedLanguageId)
        {
            if (_languageIndexLookup == null)
            {
                RebuildLookup();
            }

            string normalizedLanguageId = NormalizeLanguageId(requestedLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            string defaultLanguageId = ResolveDefaultLanguageId();
            if (!string.IsNullOrWhiteSpace(defaultLanguageId))
            {
                return defaultLanguageId;
            }

            string fallbackLanguageId = ResolveFallbackLanguageId();
            if (!string.IsNullOrWhiteSpace(fallbackLanguageId))
            {
                return fallbackLanguageId;
            }

            return string.Empty;
        }

        public string ResolveDefaultLanguageId()
        {
            if (_languageIndexLookup == null)
            {
                RebuildLookup();
            }

            string normalizedLanguageId = NormalizeLanguageId(_defaultLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            string fallbackLanguageId = ResolveFallbackLanguageId();
            if (!string.IsNullOrWhiteSpace(fallbackLanguageId))
            {
                return fallbackLanguageId;
            }

            return _runtimeLanguages.Count > 0 ? _runtimeLanguages[0].Id : string.Empty;
        }

        public string ResolveFallbackLanguageId()
        {
            if (_languageIndexLookup == null)
            {
                RebuildLookup();
            }

            string normalizedLanguageId = NormalizeLanguageId(_fallbackLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            normalizedLanguageId = NormalizeLanguageId(_defaultLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            return _runtimeLanguages.Count > 0 ? _runtimeLanguages[0].Id : string.Empty;
        }

        public string GetLanguageDisplayName(string languageId)
        {
            string normalizedLanguageId = NormalizeLanguageId(languageId);
            if (string.IsNullOrWhiteSpace(normalizedLanguageId))
            {
                return string.Empty;
            }

            for (int index = 0; index < _languages.Count; index++)
            {
                LocalizationLanguageDefinition language = _languages[index];
                if (language == null)
                {
                    continue;
                }

                if (string.Equals(language.Id, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
                {
                    return language.EditorLabel;
                }
            }

            return normalizedLanguageId;
        }

        public void SetDefaultLanguageId(string languageId)
        {
            _defaultLanguageId = NormalizeLanguageId(languageId);
        }

        public void SetFallbackLanguageId(string languageId)
        {
            _fallbackLanguageId = NormalizeLanguageId(languageId);
        }

        public LocalizationLanguageDefinition AddLanguage(string suggestedLanguageId, string suggestedDisplayName, string nativeName = "")
        {
            EnsureCollectionsInitialized();

            string uniqueLanguageId = GenerateUniqueLanguageId(suggestedLanguageId);
            string displayName = string.IsNullOrWhiteSpace(suggestedDisplayName)
                ? uniqueLanguageId.ToUpperInvariant()
                : suggestedDisplayName.Trim();

            LocalizationLanguageDefinition language = new LocalizationLanguageDefinition(uniqueLanguageId, displayName, nativeName);
            _languages.Add(language);

            for (int index = 0; index < _entries.Count; index++)
            {
                LocalizationEntry entry = _entries[index];
                if (entry == null)
                {
                    entry = new LocalizationEntry();
                    _entries[index] = entry;
                }

                entry.SetSerializedTranslation(uniqueLanguageId, entry.GetSerializedTranslation(uniqueLanguageId));
            }

            if (string.IsNullOrWhiteSpace(_defaultLanguageId))
            {
                _defaultLanguageId = uniqueLanguageId;
            }

            if (string.IsNullOrWhiteSpace(_fallbackLanguageId))
            {
                _fallbackLanguageId = uniqueLanguageId;
            }

            RebuildLookup();
            return language;
        }

        public bool MoveLanguage(int fromIndex, int toIndex)
        {
            if (_languages == null || fromIndex < 0 || fromIndex >= _languages.Count)
            {
                return false;
            }

            if (toIndex < 0 || toIndex >= _languages.Count || fromIndex == toIndex)
            {
                return false;
            }

            LocalizationLanguageDefinition language = _languages[fromIndex];
            _languages.RemoveAt(fromIndex);
            _languages.Insert(toIndex, language);
            RebuildLookup();
            return true;
        }

        public bool RemoveLanguage(string languageId)
        {
            string normalizedLanguageId = NormalizeLanguageId(languageId);
            if (string.IsNullOrWhiteSpace(normalizedLanguageId))
            {
                return false;
            }

            if (string.Equals(normalizedLanguageId, ResolveFallbackLanguageId(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int removedCount = _languages.RemoveAll(language =>
                language != null
                && string.Equals(language.Id, normalizedLanguageId, StringComparison.OrdinalIgnoreCase));

            if (removedCount == 0)
            {
                return false;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                LocalizationEntry entry = _entries[index];
                if (entry == null)
                {
                    continue;
                }

                entry.RemoveTranslationsForLanguage(normalizedLanguageId);
            }

            if (string.Equals(_defaultLanguageId, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
            {
                _defaultLanguageId = string.Empty;
            }

            RebuildLookup();
            return true;
        }

        public LocalizationEntry AddEntry(string suggestedKey = "")
        {
            EnsureCollectionsInitialized();

            LocalizationEntry entry = new LocalizationEntry
            {
                Key = suggestedKey
            };

            entry.EnsureTranslationsForLanguages(_languages);
            _entries.Add(entry);
            RebuildLookup();
            return entry;
        }

        public bool RemoveEntry(LocalizationEntry entry)
        {
            if (entry == null || _entries == null)
            {
                return false;
            }

            bool removed = _entries.Remove(entry);
            if (removed)
            {
                RebuildLookup();
            }

            return removed;
        }

        public bool RemoveOrphanedTranslations()
        {
            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            HashSet<string> validLanguageIds = new HashSet<string>(LanguageComparer);
            for (int index = 0; index < _runtimeLanguages.Count; index++)
            {
                validLanguageIds.Add(_runtimeLanguages[index].Id);
            }

            bool changed = false;
            for (int index = 0; index < _entries.Count; index++)
            {
                LocalizationEntry entry = _entries[index];
                if (entry == null)
                {
                    continue;
                }

                if (entry.RemoveOrphanedTranslations(validLanguageIds))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildLookup();
            }

            return changed;
        }

        [Button(ButtonSizes.Small), PropertyOrder(0), LabelText("Rebuild Lookup")]
        public void RebuildLookupButton()
        {
            RebuildLookup();

#if UNITY_EDITOR
            Debug.Log($"Rebuilt localization lookup for '{name}'.\n{_validationSummary}", this);
#endif
        }

        [Button(ButtonSizes.Small), PropertyOrder(1), LabelText("Validate")]
        public void ValidateTable()
        {
            RebuildLookup();

#if UNITY_EDITOR
            if (HasValidationIssues)
            {
                Debug.LogWarning($"Localization validation found issues in '{name}'.\n{_validationSummary}", this);
                return;
            }

            Debug.Log($"Localization validation passed for '{name}'.\n{_validationSummary}", this);
#endif
        }

        [Button(ButtonSizes.Small), PropertyOrder(2), LabelText("Sort Keys")]
        public void SortKeys()
        {
            if (_entries == null)
            {
                _entries = new List<LocalizationEntry>();
            }

            _entries.Sort(CompareEntriesByKey);
            RebuildLookup();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        [Button(ButtonSizes.Small), PropertyOrder(3), LabelText("Prune Orphans")]
        public void RemoveOrphanedTranslationsButton()
        {
            if (!RemoveOrphanedTranslations())
            {
                RebuildLookup();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private IEnumerable<ValueDropdownItem<string>> GetLanguageDropdownItems()
        {
            EnsureCollectionsInitialized();

            if (_languages.Count == 0)
            {
                yield break;
            }

            for (int index = 0; index < _languages.Count; index++)
            {
                LocalizationLanguageDefinition language = _languages[index];
                if (language == null)
                {
                    continue;
                }

                string languageId = NormalizeLanguageId(language.Id);
                if (string.IsNullOrWhiteSpace(languageId))
                {
                    continue;
                }

                yield return new ValueDropdownItem<string>(language.EditorLabel, languageId);
            }
        }

        private void EnsureValidationCollections()
        {
            _languageIssues ??= new List<string>();
            _duplicateKeyIssues ??= new List<string>();
            _emptyKeyIssues ??= new List<string>();
            _missingFallbackIssues ??= new List<string>();
            _missingTranslationIssues ??= new List<string>();
            _orphanedTranslationIssues ??= new List<string>();
        }

        private bool HasSerializedLanguage(string languageId)
        {
            string normalizedLanguageId = NormalizeLanguageId(languageId);

            for (int index = 0; index < _languages.Count; index++)
            {
                LocalizationLanguageDefinition language = _languages[index];
                if (language == null)
                {
                    continue;
                }

                if (string.Equals(language.Id, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GenerateUniqueLanguageId(string suggestedLanguageId)
        {
            string baseLanguageId = NormalizeLanguageId(suggestedLanguageId);
            if (string.IsNullOrWhiteSpace(baseLanguageId))
            {
                baseLanguageId = "lang";
            }

            string candidateLanguageId = baseLanguageId;
            int suffix = 2;
            while (HasSerializedLanguage(candidateLanguageId))
            {
                candidateLanguageId = $"{baseLanguageId}{suffix}";
                suffix++;
            }

            return candidateLanguageId;
        }

        private string ResolveConfiguredDefaultLanguageId()
        {
            string normalizedLanguageId = NormalizeLanguageId(_defaultLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            normalizedLanguageId = NormalizeLanguageId(_fallbackLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            return _runtimeLanguages.Count > 0 ? _runtimeLanguages[0].Id : string.Empty;
        }

        private string ResolveConfiguredFallbackLanguageId()
        {
            string normalizedLanguageId = NormalizeLanguageId(_fallbackLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            normalizedLanguageId = NormalizeLanguageId(_defaultLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedLanguageId) && _languageIndexLookup.ContainsKey(normalizedLanguageId))
            {
                return normalizedLanguageId;
            }

            return _runtimeLanguages.Count > 0 ? _runtimeLanguages[0].Id : string.Empty;
        }

        private string BuildValidationSummary()
        {
            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine($"Table: {name}");
            builder.AppendLine($"Languages: {LanguageCount}");
            builder.AppendLine($"Entries: {EntryCount}");
            builder.AppendLine($"Language issues: {LanguageIssueCount}");
            builder.AppendLine($"Duplicate keys: {DuplicateKeyCount}");
            builder.AppendLine($"Empty keys: {EmptyKeyCount}");
            builder.AppendLine($"Missing fallback: {MissingFallbackCount}");
            builder.AppendLine($"Missing translations: {MissingTranslationCount}");
            builder.AppendLine($"Orphaned translations: {OrphanedTranslationCount}");

            if (!HasValidationIssues)
            {
                builder.Append("Status: OK");
                return builder.ToString();
            }

            builder.AppendLine("Status: Issues found");
            AppendIssues(builder, _languageIssues);
            AppendIssues(builder, _duplicateKeyIssues);
            AppendIssues(builder, _emptyKeyIssues);
            AppendIssues(builder, _missingFallbackIssues);
            AppendIssues(builder, _missingTranslationIssues);
            AppendIssues(builder, _orphanedTranslationIssues);
            return builder.ToString().TrimEnd();
        }

        private static void AppendIssues(StringBuilder builder, List<string> issues)
        {
            if (issues == null)
            {
                return;
            }

            for (int index = 0; index < issues.Count; index++)
            {
                builder.Append("- ");
                builder.AppendLine(issues[index]);
            }
        }

        private static string DescribeEntryKey(string key, int entryIndex)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return $"Key '{key}'";
            }

            return $"Entry row {entryIndex + 1}";
        }

        private string JoinLanguageIds(IReadOnlyList<string> languageIds)
        {
            StringBuilder builder = new StringBuilder();

            for (int index = 0; index < languageIds.Count; index++)
            {
                string languageId = languageIds[index];
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append('\'');
                builder.Append(GetLanguageDisplayName(languageId));
                builder.Append('\'');
            }

            return builder.ToString();
        }

        private static int CompareEntriesByKey(LocalizationEntry left, LocalizationEntry right)
        {
            string leftKey = left == null ? string.Empty : left.Key;
            string rightKey = right == null ? string.Empty : right.Key;
            return StringComparer.OrdinalIgnoreCase.Compare(leftKey, rightKey);
        }
    }

    [Serializable]
    [HideReferenceObjectPicker]
    public sealed class LocalizationLanguageDefinition
    {
        [SerializeField]
        private string _id = string.Empty;

        [SerializeField]
        private string _displayName = string.Empty;

        [SerializeField]
        private string _nativeName = string.Empty;

        public LocalizationLanguageDefinition()
        {
        }

        public LocalizationLanguageDefinition(string id, string displayName, string nativeName = "")
        {
            _id = LocalizationTable.NormalizeLanguageId(id);
            _displayName = displayName == null ? string.Empty : displayName.Trim();
            _nativeName = nativeName == null ? string.Empty : nativeName.Trim();
        }

        [ShowInInspector, TableColumnWidth(100, Resizable = true)]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = LocalizationTable.NormalizeLanguageId(value);
            }
        }

        [ShowInInspector, TableColumnWidth(180, Resizable = true)]
        public string DisplayName
        {
            get
            {
                return _displayName;
            }
            set
            {
                _displayName = value == null ? string.Empty : value.Trim();
            }
        }

        [ShowInInspector, TableColumnWidth(180, Resizable = true)]
        public string NativeName
        {
            get
            {
                return _nativeName;
            }
            set
            {
                _nativeName = value == null ? string.Empty : value.Trim();
            }
        }

        [ShowInInspector, ReadOnly]
        public string EditorLabel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_displayName))
                {
                    return string.IsNullOrWhiteSpace(_id) ? "<missing id>" : _id;
                }

                if (string.IsNullOrWhiteSpace(_nativeName))
                {
                    return $"{_displayName} [{_id}]";
                }

                return $"{_displayName} / {_nativeName} [{_id}]";
            }
        }

        public string DropdownLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_displayName))
                {
                    return _displayName;
                }

                return string.IsNullOrWhiteSpace(_id) ? "<missing id>" : _id;
            }
        }

        public bool Sanitize()
        {
            bool changed = false;
            string normalizedId = LocalizationTable.NormalizeLanguageId(_id);
            if (!string.Equals(_id, normalizedId, StringComparison.Ordinal))
            {
                _id = normalizedId;
                changed = true;
            }

            if (_displayName == null)
            {
                _displayName = string.Empty;
                changed = true;
            }
            else
            {
                string trimmedDisplayName = _displayName.Trim();
                if (!string.Equals(_displayName, trimmedDisplayName, StringComparison.Ordinal))
                {
                    _displayName = trimmedDisplayName;
                    changed = true;
                }
            }

            if (_nativeName == null)
            {
                _nativeName = string.Empty;
                changed = true;
            }
            else
            {
                string trimmedNativeName = _nativeName.Trim();
                if (!string.Equals(_nativeName, trimmedNativeName, StringComparison.Ordinal))
                {
                    _nativeName = trimmedNativeName;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
