using System.Collections;
using System.Collections.Generic;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.SceneManagement;
using BitBox.Toymageddon.UserInterface;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon
{
    public class GameController : MonoBehaviourBase
    {
        public MacroSceneType CurrentMacroScene => _currentMacroSceneType;

        [SerializeField]
        [Required]
        private SceneManagementConfig _sceneManagementConfig;

        private readonly SceneTransitionPlanner _sceneTransitionPlanner = new SceneTransitionPlanner();
        private readonly SceneTransitionExecutor _sceneTransitionExecutor = new SceneTransitionExecutor();
        private MacroSceneType _currentMacroSceneType = MacroSceneType.None;
        private Coroutine _transitionCoroutine;

        protected override void OnAwakened()
        {
            StaticData.GameController = this;
            if (GetComponentInChildren<FrontendUiController>(true) == null)
            {
                var frontendUiRoot = new GameObject("FrontendUiRoot");
                frontendUiRoot.transform.SetParent(transform, false);
                frontendUiRoot.AddComponent<FrontendUiController>();
            }
        }

        protected override void OnEnabled()
        {
            _globalMessageBus.Subscribe<LoadMacroSceneEvent>(OnLoadMacroScene);
            _globalMessageBus.Subscribe<ReloadCurrentSceneEvent>(OnReloadCurrentScene);
            SceneManagementLog.CurrentLogLevel = logLevel;
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<LoadMacroSceneEvent>(OnLoadMacroScene);
            _globalMessageBus.Unsubscribe<ReloadCurrentSceneEvent>(OnReloadCurrentScene);
        }

        private void OnLoadMacroScene(LoadMacroSceneEvent @event)
        {
            BeginTransition(@event.SceneType, false);
        }

        private void OnReloadCurrentScene(ReloadCurrentSceneEvent @event)
        {
            if (_currentMacroSceneType == MacroSceneType.None)
            {
                LogWarning("ReloadCurrentSceneEvent was ignored because no macro scene has been loaded yet.");
                return;
            }

            BeginTransition(_currentMacroSceneType, true);
        }

        protected override void OnStarted()
        {
            SceneManagementLog.CurrentLogLevel = logLevel;

            MacroSceneType startupScene = ResolveStartupScene();
            if (startupScene == MacroSceneType.None)
            {
                LogWarning("No startup scene was resolved from SceneManagementConfig.");
                return;
            }

#if !UNITY_EDITOR
            LogInfo($"Loading startup scene '{startupScene}' for player build.");
            BeginTransition(startupScene, false);
#endif

#if UNITY_EDITOR
            string startupContext = DebugContext.HasActiveDebugLaunchTarget
                ? $"one-shot debug launch target '{DebugContext.ActiveDebugLaunchTarget}'"
                : $"debug start mode '{DebugContext.RequestedStartMode}'";
            LogInfo($"Loading startup scene '{startupScene}' for {startupContext}.");
            BeginTransition(startupScene, false);
#endif
        }

        private void BeginTransition(MacroSceneType targetScene, bool forceReload)
        {
            if (_sceneManagementConfig == null)
            {
                LogError("SceneManagementConfig is not assigned. Scene transition was skipped.");
                return;
            }

            if (_transitionCoroutine != null)
            {
                LogWarning($"A scene transition is already in progress. Ignored request for '{targetScene}'.");
                return;
            }

            _transitionCoroutine = StartCoroutine(LoadMacroSceneAsync(targetScene, forceReload));
        }

        private IEnumerator LoadMacroSceneAsync(MacroSceneType targetScene, bool forceReload)
        {
            if (_currentMacroSceneType == targetScene && !forceReload)
            {
                LogWarning($"Scene '{targetScene}' is already active. Use ReloadCurrentSceneEvent to force a reload.");
                _transitionCoroutine = null;
                yield break;
            }

            var loadedScenes = GetLoadedScenesSnapshot();
            SceneTransitionPlan plan;
            try
            {
                SceneManagementLog.Info(
                    "Controller",
                    $"Transition requested: {_currentMacroSceneType} -> {targetScene}. Force reload: {forceReload}."
                );
                plan = _sceneTransitionPlanner.BuildPlan(
                    _sceneManagementConfig,
                    _currentMacroSceneType,
                    targetScene,
                    loadedScenes,
                    forceReload
                );
            }
            catch (System.Exception exception)
            {
                LogError($"Failed to build a scene transition plan for '{targetScene}': {exception.Message}");
                _transitionCoroutine = null;
                yield break;
            }

            if (plan.IsNoOp)
            {
                LogWarning(plan.Summary);
                _transitionCoroutine = null;
                yield break;
            }

            LogDebug(plan.Summary);
            _globalMessageBus.Publish(new ShowLoadingScreenEvent());
            yield return null;

            var executionContext = new SceneTransitionExecutionContext(
                _sceneManagementConfig,
                (progress, progressText) => _globalMessageBus.Publish(new UpdateLoadingProgressEvent(progress, progressText)),
                $"Loading {targetScene} scene..."
            );

            yield return _sceneTransitionExecutor.ExecutePlan(plan, executionContext);

            List<string> missingLoadedScenePaths = GetMissingLoadedScenePaths(plan.ScenesToLoad);
            if (missingLoadedScenePaths.Count > 0)
            {
                _currentMacroSceneType = MacroSceneType.None;
                LogError(
                    $"Macro scene '{targetScene}' failed to finish loading. Missing loaded scenes: {string.Join(", ", missingLoadedScenePaths)}."
                );
                _globalMessageBus.Publish(new HideLoadingScreenEvent());
                _transitionCoroutine = null;
                yield break;
            }

            _currentMacroSceneType = targetScene;
            LogInfo($"Macro scene '{targetScene}' loaded successfully.");
            _globalMessageBus.Publish(new MacroSceneLoadedEvent(targetScene));
            _globalMessageBus.Publish(new HideLoadingScreenEvent());
            _transitionCoroutine = null;
        }

        private MacroSceneType ResolveStartupScene()
        {
            MacroSceneType defaultStartupScene = ResolveDefaultStartupScene();

#if UNITY_EDITOR
            if (ShouldLaunchTitleMenuFromBootstrap())
            {
                if (DebugContext.HasActiveDebugLaunchTarget)
                {
                    SceneManagementLog.Info(
                        "Controller",
                        $"Resolved active debug launch target '{DebugContext.ActiveDebugLaunchTarget}' from Bootstrap."
                    );
                    return DebugContext.ActiveDebugLaunchTarget;
                }

                if (DebugContext.RequestedStartMode != StartUpMode.TitleMenu)
                {
                    SceneManagementLog.Info(
                        "Controller",
                        $"Ignoring persisted startup override '{DebugContext.RequestedStartMode}' when launching Bootstrap. Defaulting to TitleMenu."
                    );
                    DebugContext.RequestedStartMode = StartUpMode.TitleMenu;
                }

                return defaultStartupScene;
            }

            if (_sceneManagementConfig != null
                && _sceneManagementConfig.TryGetStartupBinding(DebugContext.RequestedStartMode, out var binding))
            {
                SceneManagementLog.Info(
                    "Controller",
                    $"Resolved startup mode '{DebugContext.RequestedStartMode}' to macro scene '{binding.TargetScene}'."
                );
                return binding.TargetScene;
            }

            SceneManagementLog.Warning(
                "Controller",
                $"No startup binding exists for '{DebugContext.RequestedStartMode}'. Falling back to TitleMenu."
            );
            return defaultStartupScene;
#else
            return defaultStartupScene;
#endif
        }

        private MacroSceneType ResolveDefaultStartupScene()
        {
            if (_sceneManagementConfig != null
                && _sceneManagementConfig.TryGetStartupBinding(StartUpMode.TitleMenu, out var defaultBinding))
            {
                return defaultBinding.TargetScene;
            }

            return MacroSceneType.TitleMenu;
        }

#if UNITY_EDITOR
        private bool ShouldLaunchTitleMenuFromBootstrap()
        {
            if (_sceneManagementConfig?.BootstrapScene == null)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid()
                && activeScene.isLoaded
                && _sceneManagementConfig.BootstrapScene.MatchesPath(activeScene.path);
        }
#endif

        private static List<Scene> GetLoadedScenesSnapshot()
        {
            var loadedScenes = new List<Scene>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                loadedScenes.Add(SceneManager.GetSceneAt(i));
            }

            return loadedScenes;
        }

        private static List<string> GetMissingLoadedScenePaths(IEnumerable<string> expectedScenePaths)
        {
            var missingPaths = new List<string>();
            if (expectedScenePaths == null)
            {
                return missingPaths;
            }

            foreach (string scenePath in expectedScenePaths)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    continue;
                }

                missingPaths.Add(scenePath);
            }

            return missingPaths;
        }
    }
}
