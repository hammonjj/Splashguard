using System;
using System.Collections.Generic;
using System.Linq;
using BitBox.Library.Constants.Enums;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon.SceneManagement
{
    public sealed class SceneTransitionPlanner
    {
        public SceneTransitionPlan BuildPlan(
            SceneManagementConfig config,
            MacroSceneType current,
            MacroSceneType target,
            IReadOnlyList<Scene> loadedScenes,
            bool forceReload = false
        )
        {
            var loadedPaths = new List<string>();
            if (loadedScenes != null)
            {
                for (int i = 0; i < loadedScenes.Count; i++)
                {
                    var scene = loadedScenes[i];
                    if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
                    {
                        continue;
                    }

                    loadedPaths.Add(scene.path);
                }
            }

            return BuildPlanFromScenePaths(config, current, target, loadedPaths, forceReload);
        }

        public SceneTransitionPlan BuildPlanFromScenePaths(
            SceneManagementConfig config,
            MacroSceneType current,
            MacroSceneType target,
            IReadOnlyList<string> loadedScenePaths,
            bool forceReload = false
        )
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!config.TryGetLogicalScene(target, out var targetDefinition))
            {
                throw new InvalidOperationException($"No logical scene definition exists for target '{target}'.");
            }

            config.TryGetLogicalScene(current, out var currentDefinition);

            var loadedSet = new HashSet<string>(
                (loadedScenePaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.OrdinalIgnoreCase
            );
            var unmanagedPaths = config.GetGlobalUnmanagedScenePaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var managedPaths = config.BuildManagedScenePathSet();
            var requiredPaths = config.GetRequiredScenePaths(target).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var preservePaths = config.GetPreserveScenePaths(target).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var managedLoadedPaths = loadedSet
                .Where(path => managedPaths.Contains(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var scenesToLoad = requiredPaths
                .Where(path => forceReload || !loadedSet.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var scenesPreserved = loadedSet
                .Where(path => preservePaths.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var scenesToUnload = managedLoadedPaths
                .Where(path => !requiredPaths.Contains(path) && !preservePaths.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (forceReload)
            {
                foreach (var path in requiredPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (!loadedSet.Contains(path))
                    {
                        continue;
                    }

                    if (!scenesToUnload.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        scenesToUnload.Add(path);
                    }
                }

                scenesToUnload = scenesToUnload
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var dynamicScenesToUnload = new List<string>();
            if (!forceReload && currentDefinition != null)
            {
                foreach (var rule in currentDefinition.UnloadOnExitRules.Where(rule => rule != null))
                {
                    foreach (var path in loadedSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!rule.Matches(path))
                        {
                            continue;
                        }

                        if (requiredPaths.Contains(path) || preservePaths.Contains(path) || unmanagedPaths.Contains(path))
                        {
                            SceneManagementLog.Debug(
                                "Planner",
                                $"Skipped dynamic unload for '{config.GetSceneDisplayName(path)}' because the target still requires or preserves it."
                            );
                            continue;
                        }

                        if (!dynamicScenesToUnload.Contains(path, StringComparer.OrdinalIgnoreCase)
                            && !scenesToUnload.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            dynamicScenesToUnload.Add(path);
                        }
                    }
                }
            }

            dynamicScenesToUnload = dynamicScenesToUnload
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            scenesToUnload.RemoveAll(path => unmanagedPaths.Contains(path));
            dynamicScenesToUnload.RemoveAll(path => unmanagedPaths.Contains(path));
            scenesToLoad.RemoveAll(path => unmanagedPaths.Contains(path));

            bool isNoOp = !forceReload
                && current == target
                && scenesToLoad.Count == 0
                && scenesToUnload.Count == 0
                && dynamicScenesToUnload.Count == 0;

            string summary = BuildSummary(
                config,
                current,
                target,
                scenesToLoad,
                scenesToUnload,
                scenesPreserved,
                dynamicScenesToUnload,
                forceReload
            );

            SceneManagementLog.Debug(
                "Planner",
                $"Loaded scenes: {FormatSceneList(config, loadedSet)}"
            );
            SceneManagementLog.Debug(
                "Planner",
                $"Required scenes for {target}: {FormatSceneList(config, requiredPaths)}"
            );
            SceneManagementLog.Info("Planner", summary);

            if (isNoOp)
            {
                SceneManagementLog.Debug(
                    "Planner",
                    $"No-op transition detected for '{target}'. Current scene already matches the target definition."
                );
            }

            return new SceneTransitionPlan(
                current,
                target,
                scenesToLoad,
                scenesToUnload,
                scenesPreserved,
                dynamicScenesToUnload,
                isNoOp,
                summary
            );
        }

        private static string BuildSummary(
            SceneManagementConfig config,
            MacroSceneType current,
            MacroSceneType target,
            IReadOnlyCollection<string> scenesToLoad,
            IReadOnlyCollection<string> scenesToUnload,
            IReadOnlyCollection<string> scenesPreserved,
            IReadOnlyCollection<string> dynamicScenesToUnload,
            bool forceReload
        )
        {
            string reloadText = forceReload ? " [Reload]" : string.Empty;
            return $"Scene plan {current} -> {target}{reloadText} | "
                + $"Load: {FormatSceneList(config, scenesToLoad)} | "
                + $"Unload: {FormatSceneList(config, scenesToUnload)} | "
                + $"Preserve: {FormatSceneList(config, scenesPreserved)} | "
                + $"Dynamic Unload: {FormatSceneList(config, dynamicScenesToUnload)}";
        }

        internal static string FormatSceneList(SceneManagementConfig config, IEnumerable<string> scenePaths)
        {
            var paths = scenePaths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => config.GetSceneDisplayName(path))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths == null || paths.Count == 0)
            {
                return "None";
            }

            return string.Join(", ", paths);
        }
    }
}
