using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerDataReference))]
    public class PlayerContainer : MonoBehaviourBase
    {
        private const string HudSortingLayerName = "UIOverlay";
        private const int HudSortingOrder = 1000;

        [Header("UI Containers")]
        [SerializeField, Required] private GameObject _inGameUIContainer;
        [SerializeField, Required] private GameObject _pauseMenuContainer;

        private MacroSceneType _currentMacroScene = MacroSceneType.None;
        private PlayerInput _playerInput;
        private PlayerDataReference _playerDataReference;
        private InputAction _thirdPersonPauseAction;
        private InputAction _navalPauseAction;
        private InputAction _boatGunnerPauseAction;
        private bool _isPaused;

        protected override void OnAwakened()
        {
            CacheReferences();
            EnsureUiCanvasBoundToPlayerCamera();
            UpdateShellVisibility();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            EnsureUiCanvasBoundToPlayerCamera();
            BindPauseAction();

            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);

            UpdateShellVisibility();
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (!_currentMacroScene.IsGameplayScene()
                || _isPaused
                || !WasPausePressedThisFrame())
            {
                return;
            }

            _globalMessageBus.Publish(new PauseGameEvent(true, GetPlayerIndex()));
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;
            _isPaused = false;

            CacheReferences();
            EnsureUiCanvasBoundToPlayerCamera();
            UpdateShellVisibility();
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
            UpdateShellVisibility();
        }

        private void CacheReferences()
        {
            _playerInput ??= GetComponent<PlayerInput>();
            _playerDataReference ??= GetComponent<PlayerDataReference>();
            _inGameUIContainer ??= transform.Find("UiCanvas/ViewportRoot/GameplayHudRoot")?.gameObject;
            _pauseMenuContainer ??= transform.Find("UiCanvas/ViewportRoot/PauseMenuRoot")?.gameObject;
            Assert.IsNotNull(_playerInput, $"{nameof(PlayerContainer)} requires {nameof(PlayerInput)}.");
            Assert.IsNotNull(_playerDataReference, $"{nameof(PlayerContainer)} requires {nameof(PlayerDataReference)}.");
            Assert.IsNotNull(_playerDataReference.GameplayCamera, $"{nameof(PlayerDataReference)} requires a gameplay camera.");
        }

        private void BindPauseAction()
        {
            Assert.IsNotNull(_playerInput, $"{nameof(PlayerContainer)} requires {nameof(PlayerInput)}.");
            Assert.IsNotNull(_playerInput.actions, $"{nameof(PlayerContainer)} requires an input actions asset.");

            InputActionMap thirdPersonMap = _playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            InputActionMap navalNavigationMap = _playerInput.actions.FindActionMap(Strings.NavalNavigation, throwIfNotFound: false);
            InputActionMap boatGunnerMap = _playerInput.actions.FindActionMap(Strings.BoatGunner, throwIfNotFound: false);
            Assert.IsNotNull(thirdPersonMap, $"{nameof(PlayerContainer)} requires the '{Strings.ThirdPersonControls}' action map.");
            Assert.IsNotNull(navalNavigationMap, $"{nameof(PlayerContainer)} requires the '{Strings.NavalNavigation}' action map.");
            Assert.IsNotNull(boatGunnerMap, $"{nameof(PlayerContainer)} requires the '{Strings.BoatGunner}' action map.");

            _thirdPersonPauseAction = thirdPersonMap.FindAction(Strings.PauseAction, throwIfNotFound: false);
            _navalPauseAction = navalNavigationMap.FindAction(Strings.PauseAction, throwIfNotFound: false);
            _boatGunnerPauseAction = boatGunnerMap.FindAction(Strings.PauseAction, throwIfNotFound: false);

            Assert.IsNotNull(_thirdPersonPauseAction, $"{nameof(PlayerContainer)} requires the '{Strings.PauseAction}' action on '{Strings.ThirdPersonControls}'.");
            Assert.IsNotNull(_navalPauseAction, $"{nameof(PlayerContainer)} requires the '{Strings.PauseAction}' action on '{Strings.NavalNavigation}'.");
            Assert.IsNotNull(_boatGunnerPauseAction, $"{nameof(PlayerContainer)} requires the '{Strings.PauseAction}' action on '{Strings.BoatGunner}'.");
        }

        private void UpdateShellVisibility()
        {
            bool showHudShell = _currentMacroScene.IsGameplayScene() && !_isPaused;

            if (_inGameUIContainer != null)
            {
                _inGameUIContainer.SetActive(showHudShell);
            }

            if (_pauseMenuContainer != null)
            {
                _pauseMenuContainer.SetActive(false);
            }
        }

        private void EnsureUiCanvasBoundToPlayerCamera()
        {
            Canvas uiCanvas = GetComponentInChildren<Canvas>(true);
            Camera playerCamera = _playerDataReference != null ? _playerDataReference.GameplayCamera : null;
            if (uiCanvas == null || playerCamera == null)
            {
                return;
            }

            uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            uiCanvas.worldCamera = playerCamera;
            uiCanvas.targetDisplay = playerCamera.targetDisplay;
            uiCanvas.planeDistance = Mathf.Max(playerCamera.nearClipPlane + 0.05f, 0.5f);
            uiCanvas.overrideSorting = true;
            uiCanvas.sortingLayerName = HudSortingLayerName;
            uiCanvas.sortingOrder = HudSortingOrder;
        }

        private int GetPlayerIndex()
        {
            Assert.IsNotNull(_playerInput, $"{nameof(PlayerContainer)} requires {nameof(PlayerInput)}.");
            return _playerInput.playerIndex;
        }

        private bool WasPausePressedThisFrame()
        {
            return (_thirdPersonPauseAction != null && _thirdPersonPauseAction.WasPressedThisFrame())
                || (_navalPauseAction != null && _navalPauseAction.WasPressedThisFrame())
                || (_boatGunnerPauseAction != null && _boatGunnerPauseAction.WasPressedThisFrame());
        }
    }
}
