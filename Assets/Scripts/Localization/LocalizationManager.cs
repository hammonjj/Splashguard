using System.Collections.Generic;
using BitBox.Library.Localization;
using UnityEngine;
#if UNITY_EDITOR
using BitBox.Toymageddon.Debugging;
#endif

namespace BitBox.Toymageddon.Localization
{
    [DefaultExecutionOrder(-1000)]
    public sealed class LocalizationManager : MonoBehaviour
    {
        public const string SelectedLanguagePlayerPrefsKey = "Toymageddon.Localization.SelectedLanguageId";

        private static LocalizationManager _instance;

        [SerializeField]
        private LocalizationTable _table;

        [SerializeField]
        private string _defaultLanguageId = LocalizationTable.EnglishLanguageId;

        [SerializeField]
        private bool _persistSelection = true;

        public static LocalizationManager Instance => _instance;

        public static string CurrentLanguageId => GameText.CurrentLanguageId;

        public IReadOnlyList<LocalizationLanguageDefinition> AvailableLanguages
        {
            get
            {
                if (_table == null)
                {
                    return null;
                }

                _table.RebuildLookup();
                return _table.RuntimeLanguages;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Duplicate LocalizationManager detected. The new instance was disabled.");
                enabled = false;
                return;
            }

            _instance = this;

            string configuredDefaultLanguageId = ResolveConfiguredDefaultLanguageId();
            string startupLanguageId = ResolveStartupLanguageId(configuredDefaultLanguageId);
            GameText.Initialize(_table, configuredDefaultLanguageId);
            ApplyLanguage(startupLanguageId, persistSelection: false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void SetLanguage(string languageId)
        {
            ApplyLanguage(languageId, persistSelection: true);
        }

        public static void SetCurrentLanguage(string languageId)
        {
            if (_instance == null)
            {
                Debug.LogWarning("LocalizationManager has not been initialized. Updating GameText without persistence.");
                GameText.SetLanguage(languageId);
                return;
            }

            _instance.ApplyLanguage(languageId, persistSelection: true);
        }

        private void ApplyLanguage(string languageId, bool persistSelection)
        {
            GameText.SetLanguage(languageId);

            if (!persistSelection || !_persistSelection)
            {
                return;
            }

            PlayerPrefs.SetString(SelectedLanguagePlayerPrefsKey, GameText.CurrentLanguageId);
            PlayerPrefs.Save();
        }

        private string ResolveConfiguredDefaultLanguageId()
        {
            string normalizedDefaultLanguageId = LocalizationTable.NormalizeLanguageId(_defaultLanguageId);
            if (!string.IsNullOrWhiteSpace(normalizedDefaultLanguageId))
            {
                return normalizedDefaultLanguageId;
            }

            if (_table == null)
            {
                return string.Empty;
            }

            _table.RebuildLookup();
            return _table.DefaultLanguageId;
        }

        private string ResolveStartupLanguageId(string configuredDefaultLanguageId)
        {
#if UNITY_EDITOR
            if (DebugContext.HasRequestedLanguage)
            {
                return DebugContext.RequestedLanguageId;
            }
#endif

            if (_persistSelection && PlayerPrefs.HasKey(SelectedLanguagePlayerPrefsKey))
            {
                return PlayerPrefs.GetString(SelectedLanguagePlayerPrefsKey, configuredDefaultLanguageId);
            }

            return configuredDefaultLanguageId;
        }
    }
}
