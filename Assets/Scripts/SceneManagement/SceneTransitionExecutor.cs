using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon.SceneManagement
{
    public sealed class SceneTransitionExecutionContext
    {
        public SceneTransitionExecutionContext(
            SceneManagementConfig config,
            Action<float, string> progressReporter,
            string progressText
        )
        {
            Config = config;
            ProgressReporter = progressReporter;
            ProgressText = progressText;
        }

        public SceneManagementConfig Config { get; }
        public Action<float, string> ProgressReporter { get; }
        public string ProgressText { get; }
    }

    public sealed class SceneTransitionExecutor
    {
        public IEnumerator ExecutePlan(SceneTransitionPlan plan, SceneTransitionExecutionContext context)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (plan.IsNoOp)
            {
                context.ProgressReporter?.Invoke(1f, context.ProgressText);
                yield break;
            }

            SceneManagementLog.Info("Executor", $"Executing transition plan for '{plan.TargetScene}'.");

            int totalOperationCount = Mathf.Max(1, plan.TotalOperationCount);
            int completedOperationCount = 0;

            var unloadOperations = QueueUnloadOperations(plan.GetCombinedUnloadPaths());
            if (unloadOperations.Count > 0)
            {
                yield return WaitForOperations(
                    unloadOperations,
                    completedOperationCount,
                    totalOperationCount,
                    context
                );
                completedOperationCount += unloadOperations.Count;
            }

            var loadOperations = QueueLoadOperations(plan.ScenesToLoad);
            if (loadOperations.Count > 0)
            {
                yield return WaitForOperations(
                    loadOperations,
                    completedOperationCount,
                    totalOperationCount,
                    context
                );
                completedOperationCount += loadOperations.Count;
            }

            context.ProgressReporter?.Invoke(1f, context.ProgressText);
            SceneManagementLog.Info("Executor", $"Finished transition plan for '{plan.TargetScene}'.");
        }

        private static List<AsyncOperation> QueueUnloadOperations(IReadOnlyList<string> scenePaths)
        {
            var operations = new List<AsyncOperation>();

            for (int i = 0; i < scenePaths.Count; i++)
            {
                string scenePath = scenePaths[i];
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid())
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    scene = SceneManager.GetSceneByName(sceneName);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    SceneManagementLog.Warning("Executor", $"Skipping unload for '{scenePath}' because it is not currently loaded.");
                    continue;
                }

                if (GetLoadedSceneCount() <= 1)
                {
                    SceneManagementLog.Warning("Executor", $"Skipping unload for '{scene.name}' because it appears to be the last loaded scene.");
                    continue;
                }

                SceneManagementLog.Info("Executor", $"Queueing unload for scene '{scene.name}'.");
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null)
                {
                    SceneManagementLog.Warning("Executor", $"Unity returned a null unload operation for '{scene.name}'.");
                    continue;
                }

                operations.Add(operation);
            }

            return operations;
        }

        private static List<AsyncOperation> QueueLoadOperations(IReadOnlyList<string> scenePaths)
        {
            var operations = new List<AsyncOperation>();

            for (int i = 0; i < scenePaths.Count; i++)
            {
                string scenePath = scenePaths[i];
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                Scene existing = SceneManager.GetSceneByPath(scenePath);
                if (existing.IsValid() && existing.isLoaded)
                {
                    SceneManagementLog.Debug("Executor", $"Skipping load for '{scenePath}' because it is already loaded.");
                    continue;
                }

                SceneManagementLog.Info("Executor", $"Queueing load for scene '{scenePath}'.");
                var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                if (operation == null)
                {
                    SceneManagementLog.Error("Executor", $"Unity returned a null load operation for '{scenePath}'.");
                    continue;
                }

                operations.Add(operation);
            }

            return operations;
        }

        private static IEnumerator WaitForOperations(
            List<AsyncOperation> operations,
            int completedOperationCount,
            int totalOperationCount,
            SceneTransitionExecutionContext context
        )
        {
            if (operations == null || operations.Count == 0)
            {
                yield break;
            }

            while (!operations.All(operation => operation == null || operation.isDone))
            {
                float progress = 0f;
                for (int i = 0; i < operations.Count; i++)
                {
                    var operation = operations[i];
                    if (operation == null)
                    {
                        progress += 1f;
                        continue;
                    }

                    progress += operation.isDone
                        ? 1f
                        : Mathf.Clamp01(operation.progress / 0.9f);
                }

                float normalizedProgress = (completedOperationCount + (progress / operations.Count) * operations.Count)
                    / Mathf.Max(1, totalOperationCount);

                context.ProgressReporter?.Invoke(normalizedProgress, context.ProgressText);
                yield return null;
            }
        }

        private static int GetLoadedSceneCount()
        {
            int loadedSceneCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    loadedSceneCount++;
                }
            }

            return loadedSceneCount;
        }
    }
}
