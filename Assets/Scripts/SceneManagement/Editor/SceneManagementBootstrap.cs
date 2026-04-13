#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitBox.Library.Constants.Enums;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.SceneManagement.Editor
{
    public static class SceneManagementBootstrap
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string TitleMenuScenePath = "Assets/Scenes/TitleMenu.unity";
        private const string PlayersScenePath = "Assets/Scenes/Players.unity";
        private const string SystemsScenePath = "Assets/Scenes/Systems.unity";
        private const string HubWorldScenePath = "Assets/Scenes/HubWorld.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        public static SceneManagementConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SceneManagementConfig>(SceneManagementConfig.DefaultAssetPath);
            if (config == null)
            {
                config = CreateConfigAsset();
            }

            EnsureConfigHasDefaults(config);
            EnsureBuildSettingsContainConfiguredScenes(config);
            return config;
        }

        private static SceneManagementConfig CreateConfigAsset()
        {
            var config = ScriptableObject.CreateInstance<SceneManagementConfig>();
            PopulateDefaults(config);

            string directoryPath = Path.GetDirectoryName(SceneManagementConfig.DefaultAssetPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            AssetDatabase.CreateAsset(config, SceneManagementConfig.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created SceneManagementConfig at '{SceneManagementConfig.DefaultAssetPath}'.");
            return config;
        }

        private static void EnsureConfigHasDefaults(SceneManagementConfig config)
        {
            if (config == null)
            {
                return;
            }

            bool dirty = config.EnsureCollectionsInitialized();
            dirty |= EnsureSceneReference(config.BootstrapScene, BootstrapScenePath);

            if (config.GlobalBaseScenes.Count > 0)
            {
                dirty = true;
                config.GlobalBaseScenes.Clear();
            }

            if (config.GlobalUnmanagedScenes.Count == 0)
            {
                config.GlobalUnmanagedScenes.Add(CreateSceneReference(BootstrapScenePath));
                dirty = true;
            }

            dirty |= EnsureLogicalScene(
                config,
                MacroSceneType.TitleMenu,
                "Title Menu",
                "Loads the title menu and shared base scenes.",
                requiredScenePaths: new[] { TitleMenuScenePath }
            );

            dirty |= EnsureLogicalScene(
                config,
                MacroSceneType.CharacterSelection,
                "Character Selection",
                "Loads the player-character selection flow.",
                requiredScenePaths: new[] { PlayersScenePath }
            );

            dirty |= EnsureLogicalScene(
                config,
                MacroSceneType.HubWorld,
                "Hub World",
                "Loads the shared systems, players, and hub-world scenes.",
                requiredScenePaths: BuildGameplayRequiredScenePaths(HubWorldScenePath)
            );

            dirty |= EnsureLogicalScene(
                config,
                MacroSceneType.Sandbox,
                "Sandbox",
                "Loads the shared systems, players, and sandbox scenes.",
                requiredScenePaths: BuildGameplayRequiredScenePaths(SandboxScenePath)
            );

            dirty |= EnsureStartupBinding(
                config,
                "startup.bootstrap",
                "Title Menu",
                StartUpMode.TitleMenu,
                MacroSceneType.TitleMenu,
                showInDebugTools: true
            );

            dirty |= EnsureStartupBinding(
                config,
                "startup.hubworld",
                "Hub World",
                StartUpMode.HubWorld,
                MacroSceneType.HubWorld,
                showInDebugTools: true
            );

            config.SyncAllReferences();

            if (!dirty)
            {
                return;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void PopulateDefaults(SceneManagementConfig config)
        {
            config.EnsureCollectionsInitialized();
            config.BootstrapScene.AssignScenePath(BootstrapScenePath);

            config.GlobalBaseScenes.Clear();

            config.GlobalUnmanagedScenes.Clear();
            config.GlobalUnmanagedScenes.Add(CreateSceneReference(BootstrapScenePath));

            config.LogicalScenes.Clear();
            config.StartupBindings.Clear();

            EnsureConfigHasDefaults(config);
        }

        internal static bool EnsureBuildSettingsContainConfiguredScenes(SceneManagementConfig config)
        {
            if (config == null)
            {
                return false;
            }

            config.EnsureCollectionsInitialized();
            config.SyncAllReferences();

            List<string> configuredScenePaths = config.EnumerateAllSceneReferences()
                .Select(sceneReference => sceneReference?.ScenePath)
                .Where(scenePath => !string.IsNullOrWhiteSpace(scenePath))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<EditorBuildSettingsScene> buildSettingsScenes = EditorBuildSettings.scenes?.ToList()
                ?? new List<EditorBuildSettingsScene>();
            bool dirty = false;

            foreach (string scenePath in configuredScenePaths)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset == null)
                {
                    continue;
                }

                int existingIndex = buildSettingsScenes.FindIndex(scene =>
                    string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    if (buildSettingsScenes[existingIndex].enabled)
                    {
                        continue;
                    }

                    buildSettingsScenes[existingIndex] = new EditorBuildSettingsScene(scenePath, true);
                    dirty = true;
                    continue;
                }

                buildSettingsScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                dirty = true;
            }

            if (!dirty)
            {
                return false;
            }

            EditorBuildSettings.scenes = buildSettingsScenes.ToArray();
            return true;
        }

        private static bool EnsureSceneReference(SceneManagementConfig.SceneReference sceneReference, string scenePath)
        {
            if (sceneReference == null || string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            if (sceneReference.MatchesPath(scenePath))
            {
                sceneReference.SyncEditorCache();
                return false;
            }

            sceneReference.AssignScenePath(scenePath);
            return true;
        }

        private static SceneManagementConfig.SceneReference CreateSceneReference(string scenePath)
        {
            var sceneReference = new SceneManagementConfig.SceneReference();
            sceneReference.AssignScenePath(scenePath);
            return sceneReference;
        }

        private static string[] BuildGameplayRequiredScenePaths(string gameplayScenePath)
        {
            return new[]
            {
                SystemsScenePath,
                PlayersScenePath,
                gameplayScenePath
            };
        }

        private static bool EnsureLogicalScene(
            SceneManagementConfig config,
            MacroSceneType sceneType,
            string displayName,
            string description,
            IReadOnlyList<string> requiredScenePaths,
            IReadOnlyList<SceneManagementConfig.DynamicSceneMatchRule> dynamicRules = null
        )
        {
            var definition = config.LogicalScenes.FirstOrDefault(item => item != null && item.SceneType == sceneType);
            bool dirty = false;

            if (definition == null)
            {
                definition = new SceneManagementConfig.LogicalSceneDefinition
                {
                    SceneType = sceneType,
                    DisplayName = displayName,
                    Description = description,
                    ShowInDebugTools = true
                };
                config.LogicalScenes.Add(definition);
                dirty = true;
            }

            definition.EnsureCollectionsInitialized();

            if (string.IsNullOrWhiteSpace(definition.DisplayName) || definition.DisplayName == sceneType.ToString())
            {
                definition.DisplayName = displayName;
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(definition.Description))
            {
                definition.Description = description;
                dirty = true;
            }

            if (requiredScenePaths != null)
            {
                foreach (var scenePath in requiredScenePaths)
                {
                    bool alreadyPresent = definition.RequiredScenes.Any(sceneReference =>
                        sceneReference != null && sceneReference.MatchesPath(scenePath));

                    if (alreadyPresent)
                    {
                        continue;
                    }

                    definition.RequiredScenes.Add(CreateSceneReference(scenePath));
                    dirty = true;
                }
            }

            if ((definition.UnloadOnExitRules == null || definition.UnloadOnExitRules.Count == 0) && dynamicRules != null)
            {
                foreach (var rule in dynamicRules)
                {
                    definition.UnloadOnExitRules.Add(rule);
                }

                dirty = dynamicRules.Count > 0;
            }

            definition.SyncAllReferences();
            return dirty;
        }

        private static bool EnsureStartupBinding(
            SceneManagementConfig config,
            string id,
            string displayName,
            StartUpMode startUpMode,
            MacroSceneType targetScene,
            bool showInDebugTools
        )
        {
            var binding = config.StartupBindings.FirstOrDefault(item => item != null && item.StartUpMode == startUpMode);
            bool dirty = false;

            if (binding == null)
            {
                binding = new SceneManagementConfig.StartupModeBinding();
                config.StartupBindings.Add(binding);
                dirty = true;
            }

            if (binding.Id != id)
            {
                binding.Id = id;
                dirty = true;
            }

            if (binding.DisplayName != displayName)
            {
                binding.DisplayName = displayName;
                dirty = true;
            }

            if (binding.StartUpMode != startUpMode)
            {
                binding.StartUpMode = startUpMode;
                dirty = true;
            }

            if (binding.TargetScene != targetScene)
            {
                binding.TargetScene = targetScene;
                dirty = true;
            }

            if (binding.ShowInDebugTools != showInDebugTools)
            {
                binding.ShowInDebugTools = showInDebugTools;
                dirty = true;
            }

            return dirty;
        }
    }
}
#endif
