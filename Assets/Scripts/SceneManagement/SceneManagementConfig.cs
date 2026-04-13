using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitBox.Library.Constants.Enums;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BitBox.Toymageddon.SceneManagement
{
    [CreateAssetMenu(
        fileName = "SceneManagementConfig",
        menuName = "Scene Management/Scene Management Config"
    )]
    public sealed class SceneManagementConfig : SerializedScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Config/SceneManagement/SceneManagementConfig.asset";
        private const string PreserveInfoMessage = "Preserve scenes stay loaded only if already present. They are not auto-loaded.";

        [TitleGroup("Global")]
        [PropertyOrder(0)]
        [SerializeField]
        private SceneReference _bootstrapScene = new SceneReference();

        [TitleGroup("Global")]
        [PropertyOrder(1)]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        private List<SceneReference> _globalBaseScenes = new List<SceneReference>();

        [TitleGroup("Global")]
        [PropertyOrder(2)]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        private List<SceneReference> _globalUnmanagedScenes = new List<SceneReference>();

        [TitleGroup("Logical Scenes")]
        [PropertyOrder(10)]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        private List<LogicalSceneDefinition> _logicalScenes = new List<LogicalSceneDefinition>();

        [TitleGroup("Startup Modes")]
        [PropertyOrder(20)]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        private List<StartupModeBinding> _startupBindings = new List<StartupModeBinding>();

        [PropertyOrder(90)]
        [ShowInInspector]
        [ReadOnly]
        [MultiLineProperty(8)]
        [LabelText("Validation Summary")]
        private string ValidationSummary
        {
            get
            {
#if UNITY_EDITOR
                return BuildEditorValidationSummary();
#else
                return "Validation is available in the Unity Editor.";
#endif
            }
        }

#if UNITY_EDITOR
        [PropertyOrder(91)]
        [Button(ButtonSizes.Small)]
        private void LogValidationResults()
        {
            Debug.Log(BuildEditorValidationSummary());
        }

        [PropertyOrder(92)]
        [Button(ButtonSizes.Small)]
        private void OpenSceneManagementWindow()
        {
            EditorApplication.ExecuteMenuItem("Tools/Scene Management");
        }
#endif

        public SceneReference BootstrapScene => _bootstrapScene;
        public List<SceneReference> GlobalBaseScenes => _globalBaseScenes;
        public List<SceneReference> GlobalUnmanagedScenes => _globalUnmanagedScenes;
        public List<LogicalSceneDefinition> LogicalScenes => _logicalScenes;
        public List<StartupModeBinding> StartupBindings => _startupBindings;

        public bool EnsureCollectionsInitialized()
        {
            bool dirty = false;

            if (_bootstrapScene == null)
            {
                _bootstrapScene = new SceneReference();
                dirty = true;
            }

            if (_globalBaseScenes == null)
            {
                _globalBaseScenes = new List<SceneReference>();
                dirty = true;
            }

            if (_globalUnmanagedScenes == null)
            {
                _globalUnmanagedScenes = new List<SceneReference>();
                dirty = true;
            }

            if (_logicalScenes == null)
            {
                _logicalScenes = new List<LogicalSceneDefinition>();
                dirty = true;
            }

            if (_startupBindings == null)
            {
                _startupBindings = new List<StartupModeBinding>();
                dirty = true;
            }

            for (int i = 0; i < _logicalScenes.Count; i++)
            {
                if (_logicalScenes[i] == null)
                {
                    _logicalScenes[i] = new LogicalSceneDefinition();
                    dirty = true;
                }

                dirty |= _logicalScenes[i].EnsureCollectionsInitialized();
            }

            for (int i = 0; i < _startupBindings.Count; i++)
            {
                if (_startupBindings[i] == null)
                {
                    _startupBindings[i] = new StartupModeBinding();
                    dirty = true;
                }
            }

            return dirty;
        }

        public bool TryGetLogicalScene(MacroSceneType sceneType, out LogicalSceneDefinition definition)
        {
            definition = _logicalScenes.FirstOrDefault(item => item != null && item.SceneType == sceneType);
            return definition != null;
        }

        public LogicalSceneDefinition GetLogicalSceneOrNull(MacroSceneType sceneType)
        {
            TryGetLogicalScene(sceneType, out var definition);
            return definition;
        }

        public bool TryGetStartupBinding(StartUpMode startUpMode, out StartupModeBinding binding)
        {
            binding = _startupBindings.FirstOrDefault(item => item != null && item.StartUpMode == startUpMode);
            return binding != null;
        }

        public IEnumerable<SceneReference> EnumerateAllSceneReferences()
        {
            if (_bootstrapScene != null)
            {
                yield return _bootstrapScene;
            }

            foreach (var sceneReference in _globalBaseScenes)
            {
                if (sceneReference != null)
                {
                    yield return sceneReference;
                }
            }

            foreach (var sceneReference in _globalUnmanagedScenes)
            {
                if (sceneReference != null)
                {
                    yield return sceneReference;
                }
            }

            foreach (var definition in _logicalScenes)
            {
                if (definition == null)
                {
                    continue;
                }

                foreach (var sceneReference in definition.RequiredScenes)
                {
                    if (sceneReference != null)
                    {
                        yield return sceneReference;
                    }
                }

                foreach (var sceneReference in definition.PreserveIfLoadedScenes)
                {
                    if (sceneReference != null)
                    {
                        yield return sceneReference;
                    }
                }
            }
        }

        public IEnumerable<string> GetGlobalBaseScenePaths()
        {
            return GetDistinctScenePaths(_globalBaseScenes);
        }

        public IEnumerable<string> GetGlobalUnmanagedScenePaths()
        {
            return GetDistinctScenePaths(_globalUnmanagedScenes)
                .Concat(GetDistinctScenePaths(new[] { _bootstrapScene }))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> GetRequiredScenePaths(MacroSceneType sceneType)
        {
            var target = GetLogicalSceneOrNull(sceneType);
            if (target == null)
            {
                return Enumerable.Empty<string>();
            }

            return GetGlobalBaseScenePaths()
                .Concat(GetDistinctScenePaths(target.RequiredScenes))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> GetPreserveScenePaths(MacroSceneType sceneType)
        {
            var target = GetLogicalSceneOrNull(sceneType);
            if (target == null)
            {
                return Enumerable.Empty<string>();
            }

            return GetDistinctScenePaths(target.PreserveIfLoadedScenes);
        }

        public HashSet<string> BuildManagedScenePathSet()
        {
            var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in GetGlobalBaseScenePaths())
            {
                managed.Add(path);
            }

            foreach (var definition in _logicalScenes)
            {
                if (definition == null)
                {
                    continue;
                }

                foreach (var path in GetDistinctScenePaths(definition.RequiredScenes))
                {
                    managed.Add(path);
                }

                foreach (var path in GetDistinctScenePaths(definition.PreserveIfLoadedScenes))
                {
                    managed.Add(path);
                }
            }

            foreach (var path in GetGlobalUnmanagedScenePaths())
            {
                managed.Remove(path);
            }

            return managed;
        }

        public string GetSceneDisplayName(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return "(Missing Scene Path)";
            }

            foreach (var sceneReference in EnumerateAllSceneReferences())
            {
                if (sceneReference == null || !sceneReference.MatchesPath(scenePath))
                {
                    continue;
                }

                return sceneReference.DisplayNameOrFallback;
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }

        public IReadOnlyList<MacroSceneType> GetDefinedSceneTypes()
        {
            return _logicalScenes
                .Where(definition => definition != null)
                .Select(definition => definition.SceneType)
                .ToList();
        }

        public void SyncAllReferences()
        {
            EnsureCollectionsInitialized();
            _bootstrapScene?.SyncEditorCache();

            foreach (var sceneReference in _globalBaseScenes)
            {
                sceneReference?.SyncEditorCache();
            }

            foreach (var sceneReference in _globalUnmanagedScenes)
            {
                sceneReference?.SyncEditorCache();
            }

            foreach (var definition in _logicalScenes)
            {
                definition?.SyncAllReferences();
            }
        }

        private static IEnumerable<string> GetDistinctScenePaths(IEnumerable<SceneReference> sceneReferences)
        {
            return sceneReferences
                .Where(sceneReference => sceneReference != null && !string.IsNullOrWhiteSpace(sceneReference.ScenePath))
                .Select(sceneReference => sceneReference.ScenePath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void OnValidate()
        {
            EnsureCollectionsInitialized();
            SyncAllReferences();
        }

#if UNITY_EDITOR
        private string BuildEditorValidationSummary()
        {
            var messages = new List<string>();

            if (_bootstrapScene == null || string.IsNullOrWhiteSpace(_bootstrapScene.ScenePath))
            {
                messages.Add("Bootstrap scene is not assigned.");
            }

            var sceneTypes = _logicalScenes
                .Where(definition => definition != null)
                .Select(definition => definition.SceneType)
                .ToList();

            foreach (var duplicateSceneType in sceneTypes
                .GroupBy(sceneType => sceneType)
                .Where(group => group.Key != MacroSceneType.None && group.Count() > 1)
                .Select(group => group.Key))
            {
                messages.Add($"Duplicate logical scene definition for '{duplicateSceneType}'.");
            }

            foreach (var definition in _logicalScenes.Where(definition => definition != null))
            {
                if (definition.SceneType == MacroSceneType.None)
                {
                    messages.Add("Logical scene definitions cannot use MacroSceneType.None.");
                }

                foreach (var sceneReference in definition.RequiredScenes.Where(sceneReference => sceneReference != null))
                {
                    if (string.IsNullOrWhiteSpace(sceneReference.ScenePath))
                    {
                        messages.Add($"{definition.SceneType} has a required scene with no path assigned.");
                    }
                }

                foreach (var sceneReference in definition.PreserveIfLoadedScenes.Where(sceneReference => sceneReference != null))
                {
                    if (string.IsNullOrWhiteSpace(sceneReference.ScenePath))
                    {
                        messages.Add($"{definition.SceneType} has a preserved scene with no path assigned.");
                    }
                }
            }

            foreach (var startupBinding in _startupBindings.Where(binding => binding != null))
            {
                if (!_logicalScenes.Any(definition => definition != null && definition.SceneType == startupBinding.TargetScene))
                {
                    messages.Add($"Startup mode '{startupBinding.StartUpMode}' targets '{startupBinding.TargetScene}', which is not defined.");
                }
            }

            if (messages.Count == 0)
            {
                return "No inline config issues found. Use Tools/Scene Management for full validation.";
            }

            return string.Join("\n", messages);
        }
#endif

        [Serializable]
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class LogicalSceneDefinition
        {
            [BoxGroup("Definition")]
            [SerializeField]
            private MacroSceneType _sceneType = MacroSceneType.None;

            [BoxGroup("Definition")]
            [SerializeField]
            private string _displayName = string.Empty;

            [BoxGroup("Definition")]
            [SerializeField]
            [MultiLineProperty(2)]
            private string _description = string.Empty;

            [BoxGroup("Definition")]
            [SerializeField]
            private bool _showInDebugTools = true;

            [BoxGroup("Required Scenes")]
            [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
            [SerializeField]
            private List<SceneReference> _requiredScenes = new List<SceneReference>();

            [BoxGroup("Preserve If Loaded")]
            [InfoBox(PreserveInfoMessage)]
            [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
            [SerializeField]
            private List<SceneReference> _preserveIfLoadedScenes = new List<SceneReference>();

            [BoxGroup("Unload On Exit")]
            [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, DefaultExpandedState = true)]
            [SerializeField]
            private List<DynamicSceneMatchRule> _unloadOnExitRules = new List<DynamicSceneMatchRule>();

            public MacroSceneType SceneType
            {
                get => _sceneType;
                set => _sceneType = value;
            }

            public string DisplayName
            {
                get => string.IsNullOrWhiteSpace(_displayName) ? _sceneType.ToString() : _displayName;
                set => _displayName = value;
            }

            public string Description
            {
                get => _description;
                set => _description = value;
            }

            public bool ShowInDebugTools
            {
                get => _showInDebugTools;
                set => _showInDebugTools = value;
            }

            public List<SceneReference> RequiredScenes => _requiredScenes;
            public List<SceneReference> PreserveIfLoadedScenes => _preserveIfLoadedScenes;
            public List<DynamicSceneMatchRule> UnloadOnExitRules => _unloadOnExitRules;

            public bool EnsureCollectionsInitialized()
            {
                bool dirty = false;

                if (_requiredScenes == null)
                {
                    _requiredScenes = new List<SceneReference>();
                    dirty = true;
                }

                if (_preserveIfLoadedScenes == null)
                {
                    _preserveIfLoadedScenes = new List<SceneReference>();
                    dirty = true;
                }

                if (_unloadOnExitRules == null)
                {
                    _unloadOnExitRules = new List<DynamicSceneMatchRule>();
                    dirty = true;
                }

                for (int i = 0; i < _requiredScenes.Count; i++)
                {
                    if (_requiredScenes[i] == null)
                    {
                        _requiredScenes[i] = new SceneReference();
                        dirty = true;
                    }
                }

                for (int i = 0; i < _preserveIfLoadedScenes.Count; i++)
                {
                    if (_preserveIfLoadedScenes[i] == null)
                    {
                        _preserveIfLoadedScenes[i] = new SceneReference();
                        dirty = true;
                    }
                }

                for (int i = 0; i < _unloadOnExitRules.Count; i++)
                {
                    if (_unloadOnExitRules[i] == null)
                    {
                        _unloadOnExitRules[i] = new DynamicSceneMatchRule();
                        dirty = true;
                    }
                }

                return dirty;
            }

            public void SyncAllReferences()
            {
                EnsureCollectionsInitialized();

                foreach (var sceneReference in _requiredScenes)
                {
                    sceneReference?.SyncEditorCache();
                }

                foreach (var sceneReference in _preserveIfLoadedScenes)
                {
                    sceneReference?.SyncEditorCache();
                }
            }
        }

        [Serializable]
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class SceneReference
        {
            [SerializeField]
            [HideInInspector]
            private string _sceneGuid = string.Empty;

            [SerializeField]
            [HideInInspector]
            private string _scenePath = string.Empty;

            [SerializeField]
            [HideInInspector]
            private string _displayName = string.Empty;

#if UNITY_EDITOR
            [ShowInInspector]
            [HorizontalGroup("SceneReference", Width = 0.5f)]
            [LabelText("Scene")]
            [AssetSelector(Paths = "Assets/Scenes")]
            private SceneAsset EditorSceneAsset
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(_scenePath))
                    {
                        return null;
                    }

                    return AssetDatabase.LoadAssetAtPath<SceneAsset>(_scenePath);
                }
                set
                {
                    if (value == null)
                    {
                        Clear();
                        return;
                    }

                    AssignScenePath(AssetDatabase.GetAssetPath(value));
                }
            }
#endif

            [ShowInInspector]
            [ReadOnly]
            [HorizontalGroup("SceneReference", Width = 0.35f)]
            [LabelText("Path")]
            public string ScenePath => _scenePath;

            [ShowInInspector]
            [ReadOnly]
            [HorizontalGroup("SceneReference", Width = 0.15f)]
            [LabelText("Name")]
            public string DisplayNameOrFallback => string.IsNullOrWhiteSpace(_displayName)
                ? Path.GetFileNameWithoutExtension(_scenePath)
                : _displayName;

            public string SceneGuid => _sceneGuid;

            public bool MatchesPath(string scenePath)
            {
                return string.Equals(_scenePath, scenePath, StringComparison.OrdinalIgnoreCase);
            }

            public void Clear()
            {
                _sceneGuid = string.Empty;
                _scenePath = string.Empty;
                _displayName = string.Empty;
            }

            public void AssignScenePath(string scenePath)
            {
                _scenePath = scenePath ?? string.Empty;
                _displayName = string.IsNullOrWhiteSpace(_scenePath)
                    ? string.Empty
                    : Path.GetFileNameWithoutExtension(_scenePath);

#if UNITY_EDITOR
                _sceneGuid = string.IsNullOrWhiteSpace(_scenePath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(_scenePath);
#endif
            }

            public void SyncEditorCache()
            {
#if UNITY_EDITOR
                if (string.IsNullOrWhiteSpace(_scenePath))
                {
                    return;
                }

                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_scenePath);
                if (asset == null)
                {
                    return;
                }

                _sceneGuid = AssetDatabase.AssetPathToGUID(_scenePath);
                _displayName = Path.GetFileNameWithoutExtension(_scenePath);
#endif
            }
        }

        [Serializable]
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class DynamicSceneMatchRule
        {
            [SerializeField]
            private string _label = string.Empty;

            [SerializeField]
            private DynamicSceneMatchKind _matchKind = DynamicSceneMatchKind.PathPrefix;

            [SerializeField]
            private string _pathPrefix = string.Empty;

            public string Label
            {
                get => string.IsNullOrWhiteSpace(_label) ? _matchKind.ToString() : _label;
                set => _label = value;
            }

            public DynamicSceneMatchKind MatchKind
            {
                get => _matchKind;
                set => _matchKind = value;
            }

            [ShowIf(nameof(UsesPathPrefix))]
            public string PathPrefix
            {
                get => _pathPrefix;
                set => _pathPrefix = value;
            }

            public bool UsesPathPrefix()
            {
                return _matchKind == DynamicSceneMatchKind.PathPrefix;
            }

            public bool Matches(string scenePath)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return false;
                }

                switch (_matchKind)
                {
                    case DynamicSceneMatchKind.PathPrefix:
                        return !string.IsNullOrWhiteSpace(_pathPrefix)
                            && scenePath.StartsWith(_pathPrefix, StringComparison.OrdinalIgnoreCase);
                    default:
                        return false;
                }
            }
        }

        [Serializable]
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class StartupModeBinding
        {
            [SerializeField]
            private string _id = string.Empty;

            [SerializeField]
            private string _displayName = string.Empty;

            [SerializeField]
            private StartUpMode _startUpMode = StartUpMode.TitleMenu;

            [SerializeField]
            private MacroSceneType _targetScene = MacroSceneType.TitleMenu;

            [SerializeField]
            private bool _showInDebugTools = true;

            public string Id
            {
                get => string.IsNullOrWhiteSpace(_id)
                    ? $"startup.{_startUpMode.ToString().ToLowerInvariant()}"
                    : _id;
                set => _id = value;
            }

            public string DisplayName
            {
                get => string.IsNullOrWhiteSpace(_displayName) ? _startUpMode.ToString() : _displayName;
                set => _displayName = value;
            }

            public StartUpMode StartUpMode
            {
                get => _startUpMode;
                set => _startUpMode = value;
            }

            public MacroSceneType TargetScene
            {
                get => _targetScene;
                set => _targetScene = value;
            }

            public bool ShowInDebugTools
            {
                get => _showInDebugTools;
                set => _showInDebugTools = value;
            }
        }
    }

    public enum DynamicSceneMatchKind
    {
        PathPrefix
    }
}
