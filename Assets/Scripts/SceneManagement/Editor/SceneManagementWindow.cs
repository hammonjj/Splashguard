#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BitBox.Library.Constants.Enums;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BitBox.Toymageddon.SceneManagement.Editor
{
    public sealed class SceneManagementWindow : OdinMenuEditorWindow
    {
        [NonSerialized] private SceneManagementConfig _config;

        [MenuItem("Tools/Scene Management")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneManagementWindow>();
            window.titleContent = new GUIContent("Scene Management");
            window.minSize = new Vector2(900f, 600f);
            window.Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            _config = SceneManagementBootstrap.GetOrCreateConfig();

            var tree = new OdinMenuTree
            {
                Config =
                {
                    DrawSearchToolbar = true
                }
            };

            tree.Add("Overview", new SceneManagementOverviewPage(_config));
            tree.Add("Global Scenes", new SceneManagementGlobalScenesPage(_config));
            tree.Add("Startup Modes", new SceneManagementStartupModesPage(_config));
            tree.Add("Validation", new SceneManagementValidationPage(_config));

            foreach (var definition in _config.LogicalScenes
                .Where(definition => definition != null)
                .OrderBy(definition => definition.SceneType))
            {
                tree.Add($"Logical Scenes/{definition.SceneType}", new SceneManagementLogicalScenePage(_config, definition));
            }

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

            PersistConfigChanges();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                PersistConfigChanges();
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                ReloadConfig();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void PersistConfigChanges()
        {
            if (_config == null)
            {
                return;
            }

            _config.EnsureCollectionsInitialized();
            _config.SyncAllReferences();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void ReloadConfig()
        {
            AssetDatabase.Refresh();
            _config = SceneManagementBootstrap.GetOrCreateConfig();
            ForceMenuTreeRebuild();
        }
    }

    [Serializable]
    internal sealed class SceneManagementOverviewPage
    {
        private readonly SceneManagementConfig _config;
        private readonly SceneTransitionPlanner _planner = new SceneTransitionPlanner();

        [ValueDropdown(nameof(GetSceneOptions))]
        [OnValueChanged(nameof(RebuildPreview))]
        [LabelText("Source")]
        public MacroSceneType PreviewSource = MacroSceneType.None;

        [ValueDropdown(nameof(GetSceneOptions))]
        [OnValueChanged(nameof(RebuildPreview))]
        [LabelText("Target")]
        public MacroSceneType PreviewTarget = MacroSceneType.TitleMenu;

        [ShowInInspector]
        [TableList(AlwaysExpanded = true)]
        [PropertyOrder(0)]
        public List<LogicalSceneOverviewRow> LogicalScenes => BuildRows();

        [ShowInInspector]
        [ReadOnly]
        [MultiLineProperty(5)]
        [PropertyOrder(10)]
        [LabelText("Transition Preview")]
        public string PreviewSummary => _previewPlan?.Summary ?? "Select a source and target scene to preview the transition plan.";

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(11)]
        public string PreviewLoads => _previewPlan == null
            ? "None"
            : SceneManagementWindowHelpers.FormatSceneList(_config, _previewPlan.ScenesToLoad);

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(12)]
        public string PreviewUnloads => _previewPlan == null
            ? "None"
            : SceneManagementWindowHelpers.FormatSceneList(_config, _previewPlan.ScenesToUnload);

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(13)]
        public string PreviewPreserved => _previewPlan == null
            ? "None"
            : SceneManagementWindowHelpers.FormatSceneList(_config, _previewPlan.ScenesPreserved);

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(14)]
        public string PreviewDynamicUnloads => _previewPlan == null
            ? "None"
            : SceneManagementWindowHelpers.FormatSceneList(_config, _previewPlan.DynamicScenesToUnload);

        private SceneTransitionPlan _previewPlan;

        public SceneManagementOverviewPage(SceneManagementConfig config)
        {
            _config = config;
            PreviewTarget = config.LogicalScenes.FirstOrDefault(definition => definition != null)?.SceneType ?? MacroSceneType.TitleMenu;
            RebuildPreview();
        }

        [Button(ButtonSizes.Small)]
        public void RebuildPreview()
        {
            if (_config == null || !_config.TryGetLogicalScene(PreviewTarget, out _))
            {
                _previewPlan = null;
                return;
            }

            var loadedPaths = PreviewSource == MacroSceneType.None
                ? _config.GetGlobalUnmanagedScenePaths().ToList()
                : _config.GetRequiredScenePaths(PreviewSource)
                    .Concat(_config.GetGlobalUnmanagedScenePaths())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _previewPlan = _planner.BuildPlanFromScenePaths(
                _config,
                PreviewSource,
                PreviewTarget,
                loadedPaths
            );
        }

        private IEnumerable<ValueDropdownItem<MacroSceneType>> GetSceneOptions()
        {
            yield return new ValueDropdownItem<MacroSceneType>("None", MacroSceneType.None);
            foreach (var definition in _config.LogicalScenes
                .Where(definition => definition != null)
                .OrderBy(definition => definition.SceneType))
            {
                yield return new ValueDropdownItem<MacroSceneType>(
                    definition.DisplayName,
                    definition.SceneType
                );
            }
        }

        private List<LogicalSceneOverviewRow> BuildRows()
        {
            var report = SceneManagementValidator.Validate(_config);
            return _config.LogicalScenes
                .Where(definition => definition != null)
                .OrderBy(definition => definition.SceneType)
                .Select(definition => new LogicalSceneOverviewRow(_config, definition, report))
                .ToList();
        }
    }

    [Serializable]
    internal sealed class LogicalSceneOverviewRow
    {
        public LogicalSceneOverviewRow(
            SceneManagementConfig config,
            SceneManagementConfig.LogicalSceneDefinition definition,
            SceneManagementValidationReport report
        )
        {
            SceneType = definition.SceneType;
            DisplayName = definition.DisplayName;
            RequiredScenes = definition.RequiredScenes.Count;
            PreservedScenes = definition.PreserveIfLoadedScenes.Count;
            DynamicRules = definition.UnloadOnExitRules.Count;
            HasConfigIssues = report.Errors.Any(error => SceneManagementWindowHelpers.ContainsOrdinal(error, definition.SceneType.ToString()))
                || report.Warnings.Any(warning => SceneManagementWindowHelpers.ContainsOrdinal(warning, definition.SceneType.ToString()));
            BuildSettingsStatus = definition.RequiredScenes.Count == 0
                ? "No static scenes"
                : string.Join(
                    ", ",
                    definition.RequiredScenes.Select(sceneReference => config.GetSceneDisplayName(sceneReference.ScenePath))
                );
        }

        [TableColumnWidth(120)]
        public MacroSceneType SceneType;

        [TableColumnWidth(160)]
        public string DisplayName;

        [TableColumnWidth(70)]
        public int RequiredScenes;

        [TableColumnWidth(80)]
        public int PreservedScenes;

        [TableColumnWidth(70)]
        public int DynamicRules;

        [TableColumnWidth(80)]
        public bool HasConfigIssues;

        public string BuildSettingsStatus;
    }

    [Serializable]
    internal sealed class SceneManagementGlobalScenesPage
    {
        [ShowInInspector]
        [PropertyOrder(0)]
        [LabelText("Bootstrap Scene")]
        public SceneManagementConfig.SceneReference BootstrapScene;

        [ShowInInspector]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [PropertyOrder(1)]
        [LabelText("Global Base Scenes")]
        public List<SceneManagementConfig.SceneReference> GlobalBaseScenes;

        [ShowInInspector]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [PropertyOrder(2)]
        [LabelText("Global Unmanaged Scenes")]
        public List<SceneManagementConfig.SceneReference> GlobalUnmanagedScenes;

        public SceneManagementGlobalScenesPage(SceneManagementConfig config)
        {
            BootstrapScene = config.BootstrapScene;
            GlobalBaseScenes = config.GlobalBaseScenes;
            GlobalUnmanagedScenes = config.GlobalUnmanagedScenes;
        }
    }

    [Serializable]
    internal sealed class SceneManagementLogicalScenePage
    {
        private readonly SceneManagementConfig _config;
        private readonly SceneTransitionPlanner _planner = new SceneTransitionPlanner();

        [ValueDropdown(nameof(GetSourceOptions))]
        [OnValueChanged(nameof(RebuildPreview))]
        [LabelText("Preview From")]
        public MacroSceneType PreviewSource = MacroSceneType.None;

        [ShowInInspector]
        [InlineProperty]
        [HideLabel]
        [PropertyOrder(0)]
        public SceneManagementConfig.LogicalSceneDefinition Definition;

        [ShowInInspector]
        [ReadOnly]
        [MultiLineProperty(5)]
        [PropertyOrder(10)]
        [LabelText("Transition Preview")]
        public string PreviewSummary => _previewPlan?.Summary ?? "Select a source scene to preview the transition into this logical scene.";

        [ShowInInspector]
        [ReadOnly]
        [MultiLineProperty(5)]
        [PropertyOrder(11)]
        [LabelText("Validation Notes")]
        public string ValidationSummary => BuildValidationSummary();

        private SceneTransitionPlan _previewPlan;

        public SceneManagementLogicalScenePage(
            SceneManagementConfig config,
            SceneManagementConfig.LogicalSceneDefinition definition
        )
        {
            _config = config;
            Definition = definition;
            RebuildPreview();
        }

        [Button(ButtonSizes.Small)]
        [PropertyOrder(20)]
        public void RebuildPreview()
        {
            var loadedPaths = PreviewSource == MacroSceneType.None
                ? _config.GetGlobalUnmanagedScenePaths().ToList()
                : _config.GetRequiredScenePaths(PreviewSource)
                    .Concat(_config.GetGlobalUnmanagedScenePaths())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _previewPlan = _planner.BuildPlanFromScenePaths(
                _config,
                PreviewSource,
                Definition.SceneType,
                loadedPaths
            );
        }

        [Button(ButtonSizes.Small)]
        [PropertyOrder(21)]
        public void PingReferencedScenes()
        {
            foreach (var sceneReference in Definition.RequiredScenes.Concat(Definition.PreserveIfLoadedScenes))
            {
                if (sceneReference == null || string.IsNullOrWhiteSpace(sceneReference.ScenePath))
                {
                    continue;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneReference.ScenePath);
                if (sceneAsset != null)
                {
                    EditorGUIUtility.PingObject(sceneAsset);
                }
            }
        }

        [Button(ButtonSizes.Small)]
        [PropertyOrder(22)]
        public void OpenFirstRequiredScene()
        {
            var firstRequiredScene = Definition.RequiredScenes
                .FirstOrDefault(sceneReference => sceneReference != null && !string.IsNullOrWhiteSpace(sceneReference.ScenePath));

            if (firstRequiredScene == null)
            {
                Debug.LogWarning($"Logical scene '{Definition.SceneType}' does not define a primary required scene to open.");
                return;
            }

            EditorSceneManager.OpenScene(firstRequiredScene.ScenePath, OpenSceneMode.Single);
        }

        private IEnumerable<ValueDropdownItem<MacroSceneType>> GetSourceOptions()
        {
            yield return new ValueDropdownItem<MacroSceneType>("None", MacroSceneType.None);
            foreach (var definition in _config.LogicalScenes
                .Where(definition => definition != null && definition.SceneType != Definition.SceneType)
                .OrderBy(definition => definition.SceneType))
            {
                yield return new ValueDropdownItem<MacroSceneType>(
                    definition.DisplayName,
                    definition.SceneType
                );
            }
        }

        private string BuildValidationSummary()
        {
            var report = SceneManagementValidator.Validate(_config);
            var notes = report.Errors
                .Concat(report.Warnings)
                .Where(message => SceneManagementWindowHelpers.ContainsOrdinal(message, Definition.SceneType.ToString()))
                .ToList();

            if (notes.Count == 0)
            {
                return "No scene-specific validation issues found.";
            }

            return string.Join("\n", notes);
        }
    }

    [Serializable]
    internal sealed class SceneManagementStartupModesPage
    {
        [ShowInInspector]
        [TableList(AlwaysExpanded = true)]
        public List<SceneManagementConfig.StartupModeBinding> StartupBindings;

        public SceneManagementStartupModesPage(SceneManagementConfig config)
        {
            StartupBindings = config.StartupBindings;
        }
    }

    [Serializable]
    internal sealed class SceneManagementValidationPage
    {
        private readonly SceneManagementConfig _config;
        private SceneManagementValidationReport _report;

        public SceneManagementValidationPage(SceneManagementConfig config)
        {
            _config = config;
            Revalidate();
        }

        [Button(ButtonSizes.Small)]
        public void Revalidate()
        {
            _report = SceneManagementValidator.Validate(_config);
        }

        [Button(ButtonSizes.Small)]
        public void LogValidation()
        {
            SceneManagementValidator.LogReport(_report, "Scene Management Window");
        }

        [ShowInInspector]
        [ReadOnly]
        [ListDrawerSettings(IsReadOnly = true, ShowFoldout = true, DefaultExpandedState = true)]
        public List<string> Errors => _report?.Errors ?? new List<string>();

        [ShowInInspector]
        [ReadOnly]
        [ListDrawerSettings(IsReadOnly = true, ShowFoldout = true, DefaultExpandedState = true)]
        public List<string> Warnings => _report?.Warnings ?? new List<string>();
    }

    internal static class SceneManagementWindowHelpers
    {
        internal static bool ContainsOrdinal(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return false;
            }

            return source.IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        internal static string FormatSceneList(SceneManagementConfig config, IEnumerable<string> scenePaths)
        {
            if (config == null || scenePaths == null)
            {
                return "None";
            }

            var sceneNames = scenePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(config.GetSceneDisplayName)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            return sceneNames.Count == 0 ? "None" : string.Join(", ", sceneNames);
        }
    }
}
#endif
