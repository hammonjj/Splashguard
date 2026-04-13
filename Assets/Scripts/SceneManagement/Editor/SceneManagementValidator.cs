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
    public sealed class SceneManagementValidationReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public string GetSummary()
        {
            if (Errors.Count == 0 && Warnings.Count == 0)
            {
                return "No validation issues found.";
            }

            var sections = new List<string>();
            if (Errors.Count > 0)
            {
                sections.Add("Errors:\n- " + string.Join("\n- ", Errors));
            }

            if (Warnings.Count > 0)
            {
                sections.Add("Warnings:\n- " + string.Join("\n- ", Warnings));
            }

            return string.Join("\n\n", sections);
        }
    }

    public static class SceneManagementValidator
    {
        public static SceneManagementValidationReport Validate(SceneManagementConfig config)
        {
            var report = new SceneManagementValidationReport();
            if (config == null)
            {
                report.Errors.Add("SceneManagementConfig is null.");
                return report;
            }

            config.EnsureCollectionsInitialized();

            var buildSettingsPaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ValidateSceneReference(report, config.BootstrapScene, "Bootstrap scene", buildSettingsPaths);

            var globalBasePaths = ValidateSceneReferenceList(
                report,
                config.GlobalBaseScenes,
                "Global Base Scenes",
                buildSettingsPaths
            );

            var globalUnmanagedPaths = ValidateSceneReferenceList(
                report,
                config.GlobalUnmanagedScenes,
                "Global Unmanaged Scenes",
                buildSettingsPaths
            );

            foreach (var overlap in globalBasePaths.Intersect(globalUnmanagedPaths, StringComparer.OrdinalIgnoreCase))
            {
                report.Errors.Add($"Scene '{config.GetSceneDisplayName(overlap)}' appears in both global base scenes and global unmanaged scenes.");
            }

            if (config.BootstrapScene != null && !string.IsNullOrWhiteSpace(config.BootstrapScene.ScenePath))
            {
                if (globalBasePaths.Contains(config.BootstrapScene.ScenePath))
                {
                    report.Errors.Add("Bootstrap scene cannot also appear in Global Base Scenes.");
                }
            }

            var definitionGroups = config.LogicalScenes
                .Where(definition => definition != null)
                .GroupBy(definition => definition.SceneType)
                .ToList();

            foreach (var group in definitionGroups)
            {
                if (group.Key == MacroSceneType.None)
                {
                    report.Errors.Add("Logical scene definitions cannot use MacroSceneType.None.");
                    continue;
                }

                if (group.Count() > 1)
                {
                    report.Errors.Add($"Logical scene '{group.Key}' has {group.Count()} definitions. Only one definition is allowed per MacroSceneType.");
                }
            }

            foreach (MacroSceneType sceneType in Enum.GetValues(typeof(MacroSceneType)))
            {
                if (sceneType == MacroSceneType.None)
                {
                    continue;
                }

                if (!config.LogicalScenes.Any(definition => definition != null && definition.SceneType == sceneType))
                {
                    report.Warnings.Add($"No logical scene definition exists for '{sceneType}'.");
                }
            }

            foreach (var definition in config.LogicalScenes.Where(definition => definition != null))
            {
                var requiredPaths = ValidateSceneReferenceList(
                    report,
                    definition.RequiredScenes,
                    $"{definition.SceneType}.RequiredScenes",
                    buildSettingsPaths
                );

                var preservePaths = ValidateSceneReferenceList(
                    report,
                    definition.PreserveIfLoadedScenes,
                    $"{definition.SceneType}.PreserveIfLoadedScenes",
                    buildSettingsPaths
                );

                foreach (var overlap in requiredPaths.Intersect(preservePaths, StringComparer.OrdinalIgnoreCase))
                {
                    report.Errors.Add(
                        $"Scene '{config.GetSceneDisplayName(overlap)}' appears in both required and preserve lists for logical scene '{definition.SceneType}'."
                    );
                }

                if (config.BootstrapScene != null
                    && !string.IsNullOrWhiteSpace(config.BootstrapScene.ScenePath)
                    && requiredPaths.Contains(config.BootstrapScene.ScenePath))
                {
                    report.Errors.Add($"Logical scene '{definition.SceneType}' requires the bootstrap scene, which should remain unmanaged.");
                }

                foreach (var rule in definition.UnloadOnExitRules.Where(rule => rule != null))
                {
                    if (rule.MatchKind == DynamicSceneMatchKind.PathPrefix
                        && string.IsNullOrWhiteSpace(rule.PathPrefix))
                    {
                        report.Errors.Add($"Logical scene '{definition.SceneType}' has a PathPrefix unload rule with an empty prefix.");
                    }
                }
            }

            var startupModeGroups = config.StartupBindings
                .Where(binding => binding != null)
                .GroupBy(binding => binding.StartUpMode)
                .ToList();

            foreach (var group in startupModeGroups.Where(group => group.Count() > 1))
            {
                report.Errors.Add($"Startup mode '{group.Key}' is bound {group.Count()} times. Only one binding is allowed per StartUpMode.");
            }

            foreach (var binding in config.StartupBindings.Where(binding => binding != null))
            {
                if (!config.TryGetLogicalScene(binding.TargetScene, out _))
                {
                    report.Warnings.Add($"Startup mode '{binding.StartUpMode}' targets '{binding.TargetScene}', but no logical scene definition exists for that target.");
                }
            }

            return report;
        }

        public static void LogReport(SceneManagementValidationReport report, string contextLabel)
        {
            if (report == null)
            {
                Debug.LogError($"{contextLabel}: validation report is null.");
                return;
            }

            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
            {
                Debug.Log($"{contextLabel}: no validation issues found.");
                return;
            }

            foreach (var error in report.Errors)
            {
                Debug.LogError($"{contextLabel}: {error}");
            }

            foreach (var warning in report.Warnings)
            {
                Debug.LogWarning($"{contextLabel}: {warning}");
            }
        }

        private static HashSet<string> ValidateSceneReferenceList(
            SceneManagementValidationReport report,
            IEnumerable<SceneManagementConfig.SceneReference> sceneReferences,
            string scopeLabel,
            HashSet<string> buildSettingsPaths
        )
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sceneReferences == null)
            {
                return paths;
            }

            foreach (var sceneReference in sceneReferences)
            {
                if (sceneReference == null)
                {
                    report.Errors.Add($"{scopeLabel} contains a null scene reference.");
                    continue;
                }

                ValidateSceneReference(report, sceneReference, scopeLabel, buildSettingsPaths);
                if (!string.IsNullOrWhiteSpace(sceneReference.ScenePath))
                {
                    if (!paths.Add(sceneReference.ScenePath))
                    {
                        report.Warnings.Add($"{scopeLabel} contains duplicate scene path '{sceneReference.ScenePath}'.");
                    }
                }
            }

            return paths;
        }

        private static void ValidateSceneReference(
            SceneManagementValidationReport report,
            SceneManagementConfig.SceneReference sceneReference,
            string scopeLabel,
            HashSet<string> buildSettingsPaths
        )
        {
            if (sceneReference == null)
            {
                report.Errors.Add($"{scopeLabel} is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneReference.ScenePath))
            {
                report.Errors.Add($"{scopeLabel} is missing a scene path.");
                return;
            }

            if (!sceneReference.ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                report.Errors.Add($"{scopeLabel} must reference a .unity scene path. Found '{sceneReference.ScenePath}'.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneReference.ScenePath) == null || !File.Exists(sceneReference.ScenePath))
            {
                report.Errors.Add($"{scopeLabel} references a missing scene asset at '{sceneReference.ScenePath}'.");
                return;
            }

            if (!buildSettingsPaths.Contains(sceneReference.ScenePath))
            {
                report.Warnings.Add($"{scopeLabel} references '{sceneReference.ScenePath}', which is not enabled in build settings.");
            }
        }
    }
}
#endif
