using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Localization;
using BitBox.Library.UI.Toolkit;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    public class FrontendUiController : MonoBehaviourBase
    {
        private const string FrontendPanelSettingsResourcePath = "Frontend/FrontendRuntimePanelSettings";
        private const string TitleMenuSceneName = "TitleMenu";
        private const string LegacyTitleCanvasObjectName = "TitleMenuCanvas";
        private const string LegacyTitleEventSystemObjectName = "EventSystem";

        private readonly UiBindingScope _bindings = new UiBindingScope();
        private readonly FrontendUiBuilder _uiBuilder = new FrontendUiBuilder();

        private UIDocument _uiDocument;
        private FrontendUiRuntime _runtime;
        private TitleFlowController _titleFlowController;
        private SettingsOverlayController _settingsOverlayController;
        private PauseOverlayController _pauseOverlayController;
        private LoadingOverlayController _loadingOverlayController;
        private bool _uiBuilt;
        private InputAction _frontendCancelAction;
        private GameSettingsService _gameSettingsService;

        protected override void OnAwakened()
        {
            EnsureUiDocument();
            BuildUiIfNeeded();
        }

        protected override void OnEnabled()
        {
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            _globalMessageBus.Subscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
            _globalMessageBus.Subscribe<UpdateLoadingProgressEvent>(OnUpdateLoadingProgress);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<QuitToMainMenuEvent>(OnQuitToMainMenu);
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameText.LanguageChanged += OnLanguageChanged;

            EnsureCancelAction();
            _frontendCancelAction.Enable();
            SubscribeToGameSettingsService();
            BuildUiIfNeeded();
            RefreshLocalizedText();
            DisableLegacyTitleUiInLoadedScenes();
        }

        protected override void OnStarted()
        {
            BuildUiIfNeeded();
        }

        protected override void OnUpdated()
        {
            if (_uiBuilt && _frontendCancelAction != null && _frontendCancelAction.WasPressedThisFrame())
            {
                if (HandleFrontendCancel())
                {
                    return;
                }
            }

            _titleFlowController?.Tick(Time.unscaledTime);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus?.Unsubscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            _globalMessageBus?.Unsubscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
            _globalMessageBus?.Unsubscribe<UpdateLoadingProgressEvent>(OnUpdateLoadingProgress);
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus?.Unsubscribe<QuitToMainMenuEvent>(OnQuitToMainMenu);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameText.LanguageChanged -= OnLanguageChanged;
            _frontendCancelAction?.Disable();
            UnsubscribeFromGameSettingsService();
        }

        protected override void OnDestroyed()
        {
            _pauseOverlayController?.Dispose();
            _bindings?.Dispose();
            _frontendCancelAction?.Dispose();
            _frontendCancelAction = null;
        }

        private void EnsureUiDocument()
        {
            if (_uiDocument != null)
            {
                return;
            }

            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                _uiDocument = gameObject.AddComponent<UIDocument>();
            }

            var panelSettings = Resources.Load<PanelSettings>(FrontendPanelSettingsResourcePath);
            Assert.IsNotNull(panelSettings, $"Missing PanelSettings resource at '{FrontendPanelSettingsResourcePath}'.");
            _uiDocument.panelSettings = panelSettings;
        }

        private void BuildUiIfNeeded()
        {
            if (_uiBuilt)
            {
                return;
            }

            EnsureUiDocument();
            _runtime = _uiBuilder.Build(_uiDocument);

            _loadingOverlayController = new LoadingOverlayController(_runtime.LoadingScreen, _runtime.ScreenHost);
            _settingsOverlayController = new SettingsOverlayController(
                _runtime.SettingsScreen,
                _runtime.ScreenHost,
                _bindings,
                ResolveGameSettingsService,
                ResolveCurrentSettingsSnapshot,
                HandlePlayerInvincibilityChanged,
                CloseSettingsOverlay);
            _pauseOverlayController = new PauseOverlayController(
                _runtime.PauseScreen,
                _runtime.ScreenHost,
                _bindings,
                this,
                gameObject,
                ResolvePlayerInputs,
                () => OpenSettingsOverlay(FrontendSettingsReturnTarget.Pause),
                ownerPlayerIndex => _globalMessageBus.Publish(new PauseGameEvent(false, ownerPlayerIndex)),
                ownerPlayerIndex =>
                {
                    _globalMessageBus.Publish(new PauseGameEvent(false, ownerPlayerIndex));
                    _globalMessageBus.Publish(new QuitToMainMenuEvent());
                },
                message => LogWarning(message));
            _titleFlowController = new TitleFlowController(
                _runtime,
                _bindings,
                () => OpenSettingsOverlay(FrontendSettingsReturnTarget.Title),
                OnJoinPromptAccepted,
                message => LogInfo(message));

            _settingsOverlayController.Initialize();
            _pauseOverlayController.Initialize();
            _titleFlowController.Initialize();
            _loadingOverlayController.SetProgress(0f, "Loading...");

            _uiBuilt = true;
            ApplyFrontendUiScale(ResolveCurrentSettingsSnapshot().UiScale);
            RefreshLocalizedText();
        }

        private void OnLanguageChanged(string languageId)
        {
            RefreshLocalizedText();
            _settingsOverlayController?.RefreshValues(ResolveCurrentSettingsSnapshot());
        }

        private void RefreshLocalizedText()
        {
            if (!_uiBuilt)
            {
                return;
            }

            _titleFlowController.RefreshLocalizedText();
            _settingsOverlayController.RefreshText();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TitleMenuSceneName)
            {
                DisableLegacyTitleUi(scene);
            }
        }

        private void DisableLegacyTitleUiInLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && scene.name == TitleMenuSceneName)
                {
                    DisableLegacyTitleUi(scene);
                }
            }
        }

        private void DisableLegacyTitleUi(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == LegacyTitleCanvasObjectName
                    || root.name == LegacyTitleEventSystemObjectName)
                {
                    if (root.activeSelf)
                    {
                        root.SetActive(false);
                        LogInfo($"Disabled legacy title UI root '{root.name}' in scene '{scene.name}'.");
                    }
                }
            }
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            BuildUiIfNeeded();

            if (@event.SceneType == MacroSceneType.TitleMenu)
            {
                StaticData.PendingInitialJoinRequest = null;
                _settingsOverlayController.Hide();
                _pauseOverlayController.Close();
                _titleFlowController.ShowTitle();
                return;
            }

            _titleFlowController.Hide();
            _runtime.ScreenHost.HideAllBaseScreens();
            _settingsOverlayController.Hide();
            _pauseOverlayController.Close();
        }

        private void OnShowLoadingScreen(ShowLoadingScreenEvent @event)
        {
            BuildUiIfNeeded();
            _loadingOverlayController.Show();
        }

        private void OnHideLoadingScreen(HideLoadingScreenEvent @event)
        {
            if (!_uiBuilt)
            {
                return;
            }

            _loadingOverlayController.Hide();
            RestoreFocusAfterOverlayChange();
        }

        private void OnUpdateLoadingProgress(UpdateLoadingProgressEvent @event)
        {
            BuildUiIfNeeded();
            _loadingOverlayController.SetProgress(@event.Progress, @event.ProgressText);
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            BuildUiIfNeeded();

            if (!@event.IsPaused)
            {
                _settingsOverlayController.Hide();
                _pauseOverlayController.Close();
                RestoreFocusAfterOverlayChange();
                return;
            }

            _pauseOverlayController.ShowForPlayer(@event.InitiatingPlayerIndex);
        }

        private void OnQuitToMainMenu(QuitToMainMenuEvent @event)
        {
            if (!_uiBuilt)
            {
                return;
            }

            _titleFlowController.Hide();
            _settingsOverlayController.Hide();
            _pauseOverlayController.Close();
            _runtime.ScreenHost.HideAllBaseScreens();
        }

        private void OpenSettingsOverlay(FrontendSettingsReturnTarget returnTarget)
        {
            BuildUiIfNeeded();

            if (returnTarget == FrontendSettingsReturnTarget.Pause)
            {
                _pauseOverlayController.PrepareForSettings();
            }
            else
            {
                _runtime.ScreenHost.HideAllBaseScreens();
            }

            _settingsOverlayController.Open(returnTarget);
        }

        private void CloseSettingsOverlay()
        {
            if (!_uiBuilt)
            {
                return;
            }

            FrontendSettingsReturnTarget returnTarget = _settingsOverlayController.ReturnTarget;
            _settingsOverlayController.Close();

            if (returnTarget == FrontendSettingsReturnTarget.Pause && _pauseOverlayController.RestoreFromSettings())
            {
                return;
            }

            _titleFlowController.ShowTitle();
        }

        private void EnsureCancelAction()
        {
            if (_frontendCancelAction != null)
            {
                return;
            }

            _frontendCancelAction = new InputAction("FrontendCancel", InputActionType.Button, "*/{Cancel}");
        }

        private bool HandleFrontendCancel()
        {
            if (!_uiBuilt)
            {
                return false;
            }

            if (_loadingOverlayController.IsVisible)
            {
                return false;
            }

            if (_settingsOverlayController.IsVisible)
            {
                LogInfo($"Frontend cancel routed to settings back. returnTarget={_settingsOverlayController.ReturnTarget}.");
                CloseSettingsOverlay();
                return true;
            }

            return _titleFlowController.HandleCancel();
        }

        private void RestoreFocusAfterOverlayChange()
        {
            if (!_uiBuilt)
            {
                return;
            }

            if (_settingsOverlayController.IsVisible)
            {
                _settingsOverlayController.FocusDefault();
                return;
            }

            if (_pauseOverlayController.IsVisible)
            {
                _pauseOverlayController.FocusDefault();
                return;
            }

            _titleFlowController.RestoreFocus();
        }

        private void OnJoinPromptAccepted(PendingPlayerJoinRequest pendingJoinRequest)
        {
            StaticData.PendingInitialJoinRequest = pendingJoinRequest;
            BuildUiIfNeeded();
            _titleFlowController.Hide();
            _settingsOverlayController.Hide();
            _runtime.ScreenHost.HideAllBaseScreens();
            _globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.CharacterSelection));
        }

        private void HandlePlayerInvincibilityChanged(bool isInvincible)
        {
            DebugContext.PlayerInvincible = isInvincible;
        }

        private IReadOnlyList<PlayerInput> ResolvePlayerInputs()
        {
            return StaticData.PlayerInputCoordinator?.PlayerInputs;
        }

        private void SubscribeToGameSettingsService()
        {
            if (_gameSettingsService != null)
            {
                return;
            }

            _gameSettingsService = GameSettingsService.Instance;
            if (_gameSettingsService == null)
            {
                return;
            }

            _gameSettingsService.SettingsChanged += OnGameSettingsChanged;
        }

        private void UnsubscribeFromGameSettingsService()
        {
            if (_gameSettingsService == null)
            {
                return;
            }

            _gameSettingsService.SettingsChanged -= OnGameSettingsChanged;
            _gameSettingsService = null;
        }

        private void OnGameSettingsChanged(GameSettingsSnapshot settingsSnapshot)
        {
            ApplyFrontendUiScale(settingsSnapshot.UiScale);
            _settingsOverlayController?.RefreshValues(settingsSnapshot);
        }

        private GameSettingsService ResolveGameSettingsService()
        {
            if (_gameSettingsService == null)
            {
                SubscribeToGameSettingsService();
            }

            return _gameSettingsService;
        }

        private GameSettingsSnapshot ResolveCurrentSettingsSnapshot()
        {
            GameSettingsService settingsService = ResolveGameSettingsService();
            if (settingsService != null)
            {
                return settingsService.CurrentSettings;
            }

            return new GameSettingsSnapshot(
                1f,
                1f,
                1f,
                ResolveCurrentLanguageId(),
                UiScaleSettings.DefaultScale,
                GameSettingsService.DefaultInvertVerticalAim);
        }

        private string ResolveCurrentLanguageId()
        {
            return string.IsNullOrWhiteSpace(GameText.CurrentLanguageId)
                ? DebugContext.RequestedLanguageId
                : GameText.CurrentLanguageId;
        }

        private void ApplyFrontendUiScale(float uiScale)
        {
            if (_runtime?.FrontendRootElement == null)
            {
                return;
            }

            UiScaleVisualElementUtility.ApplyScale(_runtime.FrontendRootElement, UiScaleSettings.Clamp(uiScale));
        }
    }
}
