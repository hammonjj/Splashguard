#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace BitBox.Library.Localization.Editor
{
    public sealed class LocalizationWorkspaceWindow : OdinMenuEditorWindow
    {
        [NonSerialized]
        private LocalizationTable _table;

        [MenuItem("Tools/Localization/Workspace")]
        public static void ShowWindow()
        {
            LocalizationWorkspaceWindow window = GetWindow<LocalizationWorkspaceWindow>();
            window.titleContent = new GUIContent("Localization");
            window.minSize = new Vector2(1100f, 700f);
            window.Show();
        }

        internal LocalizationTable Table => _table;

        protected override OdinMenuTree BuildMenuTree()
        {
            _table = LoadTable();

            OdinMenuTree tree = new OdinMenuTree
            {
                Config =
                {
                    DrawSearchToolbar = true
                }
            };

            tree.Add("Setup", new LocalizationSetupPage(this));
            tree.Add("Languages", new LocalizationLanguagesPage(this));
            tree.Add("Strings", new LocalizationStringsPage(this));
            tree.Add("Validation", new LocalizationValidationPage(this));
            return tree;
        }

        protected override void OnBeginDrawEditors()
        {
            DrawToolbar();
            EditorGUI.BeginChangeCheck();
            base.OnBeginDrawEditors();
        }

        protected override void OnEndDrawEditors()
        {
            base.OnEndDrawEditors();

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            PersistTableChanges();
        }

        internal LocalizationTable GetOrCreateTable()
        {
            if (_table != null)
            {
                return _table;
            }

            _table = LoadTable();
            if (_table != null)
            {
                return _table;
            }

            string directoryPath = Path.GetDirectoryName(LocalizationTable.DefaultAssetPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _table.EnsureSeedData();
            _table.SetDefaultLanguageId(LocalizationTable.EnglishLanguageId);
            _table.SetFallbackLanguageId(LocalizationTable.EnglishLanguageId);

            AssetDatabase.CreateAsset(_table, LocalizationTable.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.SetDirty(_table);
            Selection.activeObject = _table;
            EditorGUIUtility.PingObject(_table);
            return _table;
        }

        internal void PersistTableChanges()
        {
            if (_table == null)
            {
                return;
            }

            _table.RebuildLookup();
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        internal void ReloadTable()
        {
            AssetDatabase.Refresh();
            _table = LoadTable();
            ForceMenuTreeRebuild();
        }

        internal void FocusTableAsset()
        {
            if (_table == null)
            {
                return;
            }

            Selection.activeObject = _table;
            EditorGUIUtility.PingObject(_table);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                PersistTableChanges();
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                ReloadTable();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static LocalizationTable LoadTable()
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocalizationTable.DefaultAssetPath);
            if (table != null)
            {
                table.RebuildLookup();
            }

            return table;
        }
    }

    [Serializable]
    internal sealed class LocalizationSetupPage
    {
        private readonly LocalizationWorkspaceWindow _window;

        public LocalizationSetupPage(LocalizationWorkspaceWindow window)
        {
            _window = window;
        }

        [ShowInInspector, ReadOnly]
        [LabelText("Asset Path")]
        public string AssetPath => LocalizationTable.DefaultAssetPath;

        [ShowInInspector, ReadOnly]
        [LabelText("Status")]
        public string Status => _window.Table == null
            ? "LocalizationTable asset has not been created yet."
            : $"Loaded '{_window.Table.name}' with {_window.Table.LanguageCount} valid languages.";

        [Button(ButtonSizes.Large)]
        [PropertyOrder(0)]
        [LabelText("Create Or Refresh Default Asset")]
        public void CreateOrRefreshDefaultAsset()
        {
            LocalizationTable table = _window.GetOrCreateTable();
            table.EnsureSeedData();
            table.SetDefaultLanguageId(LocalizationTable.EnglishLanguageId);
            table.SetFallbackLanguageId(LocalizationTable.EnglishLanguageId);
            _window.PersistTableChanges();
            _window.FocusTableAsset();
        }

        [Button(ButtonSizes.Medium)]
        [PropertyOrder(1)]
        [LabelText("Select Asset")]
        public void SelectAsset()
        {
            LocalizationTable table = _window.GetOrCreateTable();
            if (table == null)
            {
                return;
            }

            _window.FocusTableAsset();
        }

        [OnInspectorGUI]
        private void DrawSetupNotes()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "This workspace owns only the generic localization asset and authoring flow. " +
                "Game-specific scene hookup, startup policy, and settings UI stay outside the library.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "Use 'Create Or Refresh Default Asset' to ensure the default asset exists at the standard path, " +
                "seed 'en' and 'es', and set both default and fallback to 'en'.",
                MessageType.None);
        }
    }

    [Serializable]
    internal sealed class LocalizationLanguagesPage
    {
        private readonly LocalizationWorkspaceWindow _window;
        private Vector2 _scrollPosition;

        public LocalizationLanguagesPage(LocalizationWorkspaceWindow window)
        {
            _window = window;
        }

        [OnInspectorGUI]
        private void DrawLanguagesPage()
        {
            LocalizationTable table = _window.Table;
            if (table == null)
            {
                EditorGUILayout.HelpBox(
                    $"No LocalizationTable asset was found at '{LocalizationTable.DefaultAssetPath}'. Use the Setup page first.",
                    MessageType.Info);
                return;
            }

            table.RebuildLookup();

            DrawLanguageSelectionHeader(table);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Add Language", GUILayout.Width(140f)))
            {
                table.AddLanguage("lang", "New Language");
                _window.PersistTableChanges();
            }

            EditorGUILayout.Space(6f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int languageIndex = 0; languageIndex < table.Languages.Count; languageIndex++)
            {
                LocalizationLanguageDefinition language = table.Languages[languageIndex];
                DrawLanguageRow(table, language, languageIndex);
            }

            EditorGUILayout.EndScrollView();

            if (table.LanguageIssues.Count > 0)
            {
                EditorGUILayout.Space(8f);
                DrawIssueList("Language Issues", table.LanguageIssues);
            }
        }

        private void DrawLanguageSelectionHeader(LocalizationTable table)
        {
            IReadOnlyList<LocalizationLanguageDefinition> languages = table.RuntimeLanguages;
            if (languages == null || languages.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one valid language id to configure defaults.", MessageType.Warning);
                return;
            }

            string[] labels = BuildLanguageLabels(languages);
            int currentDefaultIndex = ResolveLanguageIndex(languages, table.DefaultLanguageId);
            int currentFallbackIndex = ResolveLanguageIndex(languages, table.FallbackLanguageId);

            int nextDefaultIndex = EditorGUILayout.Popup("Default Language", Mathf.Max(currentDefaultIndex, 0), labels);
            if (nextDefaultIndex >= 0 && nextDefaultIndex < languages.Count && nextDefaultIndex != currentDefaultIndex)
            {
                table.SetDefaultLanguageId(languages[nextDefaultIndex].Id);
                _window.PersistTableChanges();
            }

            int nextFallbackIndex = EditorGUILayout.Popup("Fallback Language", Mathf.Max(currentFallbackIndex, 0), labels);
            if (nextFallbackIndex >= 0 && nextFallbackIndex < languages.Count && nextFallbackIndex != currentFallbackIndex)
            {
                table.SetFallbackLanguageId(languages[nextFallbackIndex].Id);
                _window.PersistTableChanges();
            }
        }

        private void DrawLanguageRow(LocalizationTable table, LocalizationLanguageDefinition language, int languageIndex)
        {
            if (language == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Language {languageIndex + 1}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(languageIndex <= 0))
            {
                if (GUILayout.Button("Up", GUILayout.Width(48f)) && table.MoveLanguage(languageIndex, languageIndex - 1))
                {
                    _window.PersistTableChanges();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            using (new EditorGUI.DisabledScope(languageIndex >= table.Languages.Count - 1))
            {
                if (GUILayout.Button("Down", GUILayout.Width(56f)) && table.MoveLanguage(languageIndex, languageIndex + 1))
                {
                    _window.PersistTableChanges();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                if (string.Equals(language.Id, table.FallbackLanguageId, StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog(
                        "Cannot Remove Fallback Language",
                        "Assign a different fallback language before removing this language.",
                        "OK");
                }
                else if (table.RemoveLanguage(language.Id))
                {
                    _window.PersistTableChanges();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            string nextId = EditorGUILayout.DelayedTextField("Id", language.Id);
            string nextDisplayName = EditorGUILayout.TextField("Display Name", language.DisplayName);
            string nextNativeName = EditorGUILayout.TextField("Native Name", language.NativeName);
            if (EditorGUI.EndChangeCheck())
            {
                language.Id = nextId;
                language.DisplayName = nextDisplayName;
                language.NativeName = nextNativeName;
                _window.PersistTableChanges();
            }

            EditorGUILayout.LabelField("Preview", language.EditorLabel);
            EditorGUILayout.EndVertical();
        }

        private static string[] BuildLanguageLabels(IReadOnlyList<LocalizationLanguageDefinition> languages)
        {
            string[] labels = new string[languages.Count];
            for (int index = 0; index < languages.Count; index++)
            {
                labels[index] = languages[index].EditorLabel;
            }

            return labels;
        }

        private static int ResolveLanguageIndex(IReadOnlyList<LocalizationLanguageDefinition> languages, string languageId)
        {
            for (int index = 0; index < languages.Count; index++)
            {
                LocalizationLanguageDefinition language = languages[index];
                if (language == null)
                {
                    continue;
                }

                if (string.Equals(language.Id, languageId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DrawIssueList(string title, IReadOnlyList<string> issues)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int index = 0; index < issues.Count; index++)
            {
                EditorGUILayout.HelpBox(issues[index], MessageType.Warning);
            }
        }
    }

    [Serializable]
    internal sealed class LocalizationStringsPage
    {
        private const float KeyColumnWidth = 240f;
        private const float TranslationColumnWidth = 260f;
        private const float RemoveButtonWidth = 72f;
        private const float TranslationRowHeight = 42f;

        private readonly LocalizationWorkspaceWindow _window;
        private Vector2 _scrollPosition;
        private string _searchText = string.Empty;

        public LocalizationStringsPage(LocalizationWorkspaceWindow window)
        {
            _window = window;
        }

        [OnInspectorGUI]
        private void DrawStringsPage()
        {
            LocalizationTable table = _window.Table;
            if (table == null)
            {
                EditorGUILayout.HelpBox(
                    $"No LocalizationTable asset was found at '{LocalizationTable.DefaultAssetPath}'. Use the Setup page first.",
                    MessageType.Info);
                return;
            }

            table.RebuildLookup();
            DrawToolbar(table);

            IReadOnlyList<LocalizationLanguageDefinition> languages = table.RuntimeLanguages;
            if (languages == null || languages.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one valid language before authoring strings.", MessageType.Warning);
                return;
            }

            DrawHeaderRow(languages);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            LocalizationEntry entryToRemove = null;
            string previousGroup = null;

            for (int entryIndex = 0; entryIndex < table.Entries.Count; entryIndex++)
            {
                LocalizationEntry entry = table.Entries[entryIndex];
                if (entry == null || !MatchesSearch(entry, languages, _searchText))
                {
                    continue;
                }

                string group = entry.Group;
                if (!string.Equals(previousGroup, group, StringComparison.OrdinalIgnoreCase))
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                    previousGroup = group;
                }

                bool removeRequested;
                if (DrawEntryRow(entry, languages, out removeRequested))
                {
                    _window.PersistTableChanges();
                }

                if (removeRequested)
                {
                    entryToRemove = entry;
                }
            }

            EditorGUILayout.EndScrollView();

            if (entryToRemove != null && table.RemoveEntry(entryToRemove))
            {
                _window.PersistTableChanges();
            }
        }

        private void DrawToolbar(LocalizationTable table)
        {
            EditorGUILayout.BeginHorizontal();
            _searchText = EditorGUILayout.TextField("Search", _searchText);

            if (GUILayout.Button("Add String", GUILayout.Width(110f)))
            {
                table.AddEntry();
                _window.PersistTableChanges();
            }

            if (GUILayout.Button("Sort Keys", GUILayout.Width(96f)))
            {
                table.SortKeys();
                _window.PersistTableChanges();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHeaderRow(IReadOnlyList<LocalizationLanguageDefinition> languages)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Key", EditorStyles.miniBoldLabel, GUILayout.Width(KeyColumnWidth));

            for (int index = 0; index < languages.Count; index++)
            {
                GUILayout.Label(languages[index].EditorLabel, EditorStyles.miniBoldLabel, GUILayout.Width(TranslationColumnWidth));
            }

            GUILayout.Label("Action", EditorStyles.miniBoldLabel, GUILayout.Width(RemoveButtonWidth));
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawEntryRow(
            LocalizationEntry entry,
            IReadOnlyList<LocalizationLanguageDefinition> languages,
            out bool removeRequested)
        {
            bool changed = false;
            removeRequested = false;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            string nextKey = EditorGUILayout.DelayedTextField(entry.Key, GUILayout.Width(KeyColumnWidth));
            if (EditorGUI.EndChangeCheck())
            {
                entry.Key = nextKey;
                changed = true;
            }

            for (int languageIndex = 0; languageIndex < languages.Count; languageIndex++)
            {
                LocalizationLanguageDefinition language = languages[languageIndex];
                string currentValue = entry.GetSerializedTranslation(language.Id);

                Color previousColor = GUI.color;
                if (string.IsNullOrWhiteSpace(currentValue))
                {
                    GUI.color = new Color(1.0f, 0.92f, 0.70f);
                }

                EditorGUI.BeginChangeCheck();
                string nextValue = EditorGUILayout.TextArea(
                    currentValue,
                    GUILayout.Width(TranslationColumnWidth),
                    GUILayout.Height(TranslationRowHeight));
                if (EditorGUI.EndChangeCheck())
                {
                    entry.SetSerializedTranslation(language.Id, nextValue);
                    changed = true;
                }

                GUI.color = previousColor;
            }

            removeRequested = GUILayout.Button(
                "Remove",
                GUILayout.Width(RemoveButtonWidth),
                GUILayout.Height(22f));
            EditorGUILayout.EndHorizontal();
            return changed;
        }

        private static bool MatchesSearch(
            LocalizationEntry entry,
            IReadOnlyList<LocalizationLanguageDefinition> languages,
            string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            if (ContainsOrdinalIgnoreCase(entry.Key, searchText) || ContainsOrdinalIgnoreCase(entry.Group, searchText))
            {
                return true;
            }

            for (int index = 0; index < languages.Count; index++)
            {
                string translation = entry.GetSerializedTranslation(languages[index].Id);
                if (ContainsOrdinalIgnoreCase(translation, searchText))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string searchText)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [Serializable]
    internal sealed class LocalizationValidationPage
    {
        private readonly LocalizationWorkspaceWindow _window;
        private Vector2 _scrollPosition;

        public LocalizationValidationPage(LocalizationWorkspaceWindow window)
        {
            _window = window;
        }

        [OnInspectorGUI]
        private void DrawValidationPage()
        {
            LocalizationTable table = _window.Table;
            if (table == null)
            {
                EditorGUILayout.HelpBox(
                    $"No LocalizationTable asset was found at '{LocalizationTable.DefaultAssetPath}'. Use the Setup page first.",
                    MessageType.Info);
                return;
            }

            table.RebuildLookup();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild Lookup", GUILayout.Width(120f)))
            {
                table.RebuildLookup();
                _window.PersistTableChanges();
            }

            if (GUILayout.Button("Validate", GUILayout.Width(90f)))
            {
                table.ValidateTable();
                _window.PersistTableChanges();
            }

            if (GUILayout.Button("Prune Orphans", GUILayout.Width(110f)))
            {
                table.RemoveOrphanedTranslations();
                _window.PersistTableChanges();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                table.ValidationSummary,
                table.HasValidationIssues ? MessageType.Warning : MessageType.Info);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawIssueSection("Language Issues", table.LanguageIssues);
            DrawIssueSection("Duplicate Keys", table.DuplicateKeyIssues);
            DrawIssueSection("Empty Keys", table.EmptyKeyIssues);
            DrawIssueSection("Missing Fallback", table.MissingFallbackIssues);
            DrawIssueSection("Missing Translations", table.MissingTranslationIssues);
            DrawIssueSection("Orphaned Translations", table.OrphanedTranslationIssues);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawIssueSection(string title, IReadOnlyList<string> issues)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.HelpBox("None", MessageType.None);
                return;
            }

            for (int index = 0; index < issues.Count; index++)
            {
                EditorGUILayout.HelpBox(issues[index], MessageType.Warning);
            }
        }
    }
}
#endif
