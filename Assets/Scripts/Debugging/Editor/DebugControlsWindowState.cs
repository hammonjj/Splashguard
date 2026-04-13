#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BitBox.Library.Constants.Enums;
using UnityEditor;
using UnityEngine;

namespace Bitbox.Toymageddon.Debugging.Editor
{
    [FilePath("UserSettings/DebugControlsWindowState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class DebugControlsWindowState : ScriptableSingleton<DebugControlsWindowState>
    {
        private const string LegacyTrackedScenesEditorPrefsKey = "DebugControls.TrackedScenes";
        private const string LegacyTrackedPrefabsEditorPrefsKey = "DebugControls.TrackedPrefabs";
        private const char LegacyTrackedItemsSeparator = '|';
        private const float TileMinWidthLowerBound = 0f;
        private const float TileChromeWidthLowerBound = 72f;
        private const int TileLabelCharacterLowerBound = 8;
        private const int TileLabelCharacterUpperBound = 40;

        [SerializeField]
        private List<string> _trackedSceneGuids = new List<string>();

        [SerializeField]
        private List<string> _trackedPrefabGuids = new List<string>();

        [SerializeField]
        private float _sceneTileMinWidth = 120f;

        [SerializeField]
        private float _sceneTileChromeWidth = 92f;

        [SerializeField]
        private int _sceneMaxLabelCharacters = 20;

        [SerializeField]
        private float _prefabTileMinWidth = 120f;

        [SerializeField]
        private float _prefabTileChromeWidth = 92f;

        [SerializeField]
        private int _prefabMaxLabelCharacters = 20;

        [SerializeField]
        private bool _showLaunchSection = true;

        [SerializeField]
        private bool _showGameplayFlagsSection = true;

        [SerializeField]
        private MacroSceneType _debugLauncherScene = MacroSceneType.HubWorld;

        [SerializeField]
        private string _debugLauncherInputSelectionId = string.Empty;

        public List<string> TrackedSceneGuids
        {
            get
            {
                EnsureCollectionsInitialized();
                return _trackedSceneGuids;
            }
        }

        public List<string> TrackedPrefabGuids
        {
            get
            {
                EnsureCollectionsInitialized();
                return _trackedPrefabGuids;
            }
        }

        public float SceneTileMinWidth
        {
            get => Mathf.Max(TileMinWidthLowerBound, _sceneTileMinWidth);
            set => _sceneTileMinWidth = Mathf.Max(TileMinWidthLowerBound, value);
        }

        public float SceneTileChromeWidth
        {
            get => Mathf.Max(TileChromeWidthLowerBound, _sceneTileChromeWidth);
            set => _sceneTileChromeWidth = Mathf.Max(TileChromeWidthLowerBound, value);
        }

        public int SceneMaxLabelCharacters
        {
            get => Mathf.Clamp(_sceneMaxLabelCharacters, TileLabelCharacterLowerBound, TileLabelCharacterUpperBound);
            set => _sceneMaxLabelCharacters = Mathf.Clamp(value, TileLabelCharacterLowerBound, TileLabelCharacterUpperBound);
        }

        public float PrefabTileMinWidth
        {
            get => Mathf.Max(TileMinWidthLowerBound, _prefabTileMinWidth);
            set => _prefabTileMinWidth = Mathf.Max(TileMinWidthLowerBound, value);
        }

        public float PrefabTileChromeWidth
        {
            get => Mathf.Max(TileChromeWidthLowerBound, _prefabTileChromeWidth);
            set => _prefabTileChromeWidth = Mathf.Max(TileChromeWidthLowerBound, value);
        }

        public int PrefabMaxLabelCharacters
        {
            get => Mathf.Clamp(_prefabMaxLabelCharacters, TileLabelCharacterLowerBound, TileLabelCharacterUpperBound);
            set => _prefabMaxLabelCharacters = Mathf.Clamp(value, TileLabelCharacterLowerBound, TileLabelCharacterUpperBound);
        }

        public bool ShowLaunchSection
        {
            get => _showLaunchSection;
            set => _showLaunchSection = value;
        }

        public bool ShowGameplayFlagsSection
        {
            get => _showGameplayFlagsSection;
            set => _showGameplayFlagsSection = value;
        }

        public MacroSceneType DebugLauncherScene
        {
            get
            {
                Assert.IsTrue(IsSupportedDebugLauncherScene(_debugLauncherScene),
                    $"Unsupported debug launcher scene '{_debugLauncherScene}'.");
                return _debugLauncherScene;
            }
            set
            {
                Assert.IsTrue(IsSupportedDebugLauncherScene(value),
                    $"Unsupported debug launcher scene '{value}'.");
                _debugLauncherScene = value;
            }
        }

        public string DebugLauncherInputSelectionId
        {
            get => _debugLauncherInputSelectionId ?? string.Empty;
            set => _debugLauncherInputSelectionId = value ?? string.Empty;
        }

        private void OnEnable()
        {
            EnsureCollectionsInitialized();
            bool hasChanges = EnsureSerializedStateInitialized();
            hasChanges |= MigrateLegacyEditorPrefsIfNeeded();

            if (hasChanges)
            {
                SaveState();
            }
        }

        public void SaveState()
        {
            Save(true);
        }

        private void EnsureCollectionsInitialized()
        {
            _trackedSceneGuids ??= new List<string>();
            _trackedPrefabGuids ??= new List<string>();
        }

        // Store tracked items per project instead of global EditorPrefs keys.
        private bool EnsureSerializedStateInitialized()
        {
            if (IsSupportedDebugLauncherScene(_debugLauncherScene))
            {
                return false;
            }

            _debugLauncherScene = MacroSceneType.HubWorld;
            return true;
        }

        private static bool IsSupportedDebugLauncherScene(MacroSceneType sceneType)
        {
            return sceneType == MacroSceneType.HubWorld || sceneType == MacroSceneType.Sandbox;
        }

        private bool MigrateLegacyEditorPrefsIfNeeded()
        {
            bool hasChanges = false;

            if (_trackedSceneGuids.Count == 0)
            {
                hasChanges |= TryMigrateLegacyGuids(LegacyTrackedScenesEditorPrefsKey, _trackedSceneGuids);
            }

            if (_trackedPrefabGuids.Count == 0)
            {
                hasChanges |= TryMigrateLegacyGuids(LegacyTrackedPrefabsEditorPrefsKey, _trackedPrefabGuids);
            }
            return hasChanges;
        }

        private static bool TryMigrateLegacyGuids(string editorPrefsKey, List<string> targetList)
        {
            if (!EditorPrefs.HasKey(editorPrefsKey))
            {
                return false;
            }

            string rawState = EditorPrefs.GetString(editorPrefsKey);
            if (string.IsNullOrWhiteSpace(rawState))
            {
                EditorPrefs.DeleteKey(editorPrefsKey);
                return false;
            }

            bool hasChanges = false;
            string[] trackedGuids = rawState.Split(new[] { LegacyTrackedItemsSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string trackedGuid in trackedGuids)
            {
                if (string.IsNullOrWhiteSpace(trackedGuid) || targetList.Contains(trackedGuid))
                {
                    continue;
                }

                targetList.Add(trackedGuid);
                hasChanges = true;
            }

            if (hasChanges)
            {
                EditorPrefs.DeleteKey(editorPrefsKey);
            }

            return hasChanges;
        }
    }
}
#endif
