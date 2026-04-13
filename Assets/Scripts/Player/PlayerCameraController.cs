using System;
using BitBox.Library;
using BitBox.Library.CameraUtils;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using Bitbox.Toymageddon.Nautical;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerDataReference))]
    public sealed class PlayerCameraController : MonoBehaviourBase
    {
        private const string ThirdPersonCameraName = "ThirdPersonOrbitCamera";
        private const string HelmCameraName = "HelmOrbitCamera";
        private const string BoatGunnerCameraName = "BoatGunnerCamera";
        private const string LookOrbitXAxisName = "Look Orbit X";
        private const string LookOrbitYAxisName = "Look Orbit Y";
        private const string RuntimeHelmLookAtTargetName = "RuntimeHelmLookAtTarget";
        private const float FallbackBoatGunnerDefaultFieldOfView = 60f;
        private const float FallbackBoatGunnerZoomFieldOfView = 48f;
        private const float FallbackBoatGunnerZoomTransitionSpeed = 12f;
        private const float MinHelmLookAtSeparation = 0.25f;
        private static readonly Vector3 HelmLookAtFallbackOffset = new(0f, -0.1f, 1.6f);

        [Header("Look Tuning")]
        [SerializeField, Min(0f)] private float _mouseLookSensitivity = 0.2f;
        [SerializeField, Min(0f)] private float _gamepadLookSensitivity = 180f;
        [SerializeField, Min(0f)] private float _helmLookSensitivityMultiplier = 1.35f;

        private PlayerInput _playerInput;
        private PlayerDataReference _playerDataReference;
        private CinemachineBrain _cinemachineBrain;
        private CinemachineCamera _thirdPersonCamera;
        private CinemachineCamera _helmCamera;
        private CinemachineCamera _gunnerCamera;
        private CinemachineOrbitalFollow _thirdPersonOrbitalFollow;
        private CinemachineOrbitalFollow _helmOrbitalFollow;
        private CinemachineFollow _gunnerFollow;
        private CinemachineInputAxisController _thirdPersonInputController;
        private CinemachineInputAxisController _helmInputController;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;
        private bool _isPaused;
        private bool _needsThirdPersonOrbitAlignment = true;
        private bool _needsHelmOrbitAlignment;
        private bool _prioritiesInitialized;
        private int _thirdPersonBasePriority;
        private int _helmBasePriority;
        private int _gunnerBasePriority;
        private int _activePriority;
        private HelmControl _activeHelm;
        private DeckMountedGunControl _activeGun;
        private Transform _runtimeHelmLookAtTarget;
        private CameraMode _activeCameraMode = CameraMode.ThirdPerson;
        private string _lastControlScheme;
        private string _lastActionMapName;

        protected override void OnAwakened()
        {
            CacheReferences();
            ConfigureChannelsAndInputs();
            ApplyLookInputTuning("awake");
            RestorePlayerCameraTargets();
            ActivateThirdPersonCamera();
            QueueThirdPersonOrbitAlignment();
            LogInfo(
                $"Camera controller initialized. playerIndex={_playerInput.playerIndex}, scheme={_playerInput.currentControlScheme}, gameplayCamera={GameplayCamera.name}, thirdPersonVcam={_thirdPersonCamera.name}, helmVcam={_helmCamera.name}, gunnerVcam={_gunnerCamera.name}, cameraTarget={CameraTarget.name}, channel={ResolveOutputChannel(_playerInput.playerIndex)}.");
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            ConfigureChannelsAndInputs();
            ApplyLookInputTuning("enable");

            _playerInput.onControlsChanged += OnControlsChanged;

            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Subscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Subscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Subscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);

            if (_currentMacroScene.IsGameplayScene())
            {
                _isPaused = false;
                QueueThirdPersonOrbitAlignment();
                RefreshCameraMode();
            }
        }

        protected override void OnDisabled()
        {
            if (_playerInput != null)
            {
                _playerInput.onControlsChanged -= OnControlsChanged;
            }

            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Unsubscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Unsubscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Unsubscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Unsubscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);
        }

        protected override void OnUpdated()
        {
            if (DidInputContextChange())
            {
                ConfigureChannelsAndInputs();
                ApplyLookInputTuning("input_context_changed");
            }

            if (_needsThirdPersonOrbitAlignment)
            {
                AlignThirdPersonOrbitToVisualFacingTarget();
                _needsThirdPersonOrbitAlignment = false;
            }

            if (_needsHelmOrbitAlignment)
            {
                AlignHelmOrbitToTrackingTarget();
                _needsHelmOrbitAlignment = false;
            }

            if (_currentMacroScene.IsGameplayScene()
                && !_isPaused
                && _activeCameraMode == CameraMode.Helm
                && !TryResolveActiveHelm(out _))
            {
                ActivateThirdPersonCamera();
            }

            if (_currentMacroScene.IsGameplayScene()
                && !_isPaused
                && _activeCameraMode == CameraMode.Gunner
                && !TryResolveActiveGun(out _))
            {
                ActivateThirdPersonCamera();
                return;
            }

            if (_activeCameraMode == CameraMode.Gunner)
            {
                UpdateGunnerCameraZoom();
            }
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;
            _isPaused = false;
            ConfigureChannelsAndInputs();
            ApplyLookInputTuning($"scene_loaded_{@event.SceneType}");

            if (!@event.SceneType.IsGameplayScene())
            {
                ActivateThirdPersonCamera();
                return;
            }

            QueueThirdPersonOrbitAlignment();
            RefreshCameraMode();
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
        }

        private void OnControlsChanged(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput != _playerInput)
            {
                return;
            }

            ConfigureChannelsAndInputs();
            ApplyLookInputTuning("controls_changed");
        }

        private void OnPlayerEnteredHelm(PlayerEnteredHelmEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            LogInfo($"Received helm-enter event. playerIndex={@event.PlayerIndex}.");
            ActivateHelmCamera();
        }

        private void OnPlayerExitedHelm(PlayerExitedHelmEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            LogInfo($"Received helm-exit event. playerIndex={@event.PlayerIndex}.");
            ActivateThirdPersonCamera();
        }

        private void OnPlayerEnteredBoatGun(PlayerEnteredBoatGunEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            LogInfo($"Received boat-gun-enter event. playerIndex={@event.PlayerIndex}.");
            ActivateGunnerCamera();
        }

        private void OnPlayerExitedBoatGun(PlayerExitedBoatGunEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            LogInfo($"Received boat-gun-exit event. playerIndex={@event.PlayerIndex}.");
            ActivateThirdPersonCamera();
        }

        private void CacheReferences()
        {
            _playerInput ??= GetComponent<PlayerInput>();
            _playerDataReference ??= GetComponent<PlayerDataReference>();
            _cinemachineBrain ??= GameplayCamera != null ? GameplayCamera.GetComponent<CinemachineBrain>() : null;

            if (CameraRigRoot != null)
            {
                CinemachineCamera[] authoredCameras = CameraRigRoot.GetComponentsInChildren<CinemachineCamera>(true);
                _thirdPersonCamera ??= FindAuthoredCamera(authoredCameras, ThirdPersonCameraName);
                _helmCamera ??= FindAuthoredCamera(authoredCameras, HelmCameraName);
                _gunnerCamera ??= FindAuthoredCamera(authoredCameras, BoatGunnerCameraName);
            }

            _thirdPersonOrbitalFollow ??= _thirdPersonCamera != null ? _thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>() : null;
            _helmOrbitalFollow ??= _helmCamera != null ? _helmCamera.GetComponent<CinemachineOrbitalFollow>() : null;
            _gunnerFollow ??= _gunnerCamera != null ? _gunnerCamera.GetComponent<CinemachineFollow>() : null;
            _thirdPersonInputController ??= _thirdPersonCamera != null ? _thirdPersonCamera.GetComponent<CinemachineInputAxisController>() : null;
            _helmInputController ??= _helmCamera != null ? _helmCamera.GetComponent<CinemachineInputAxisController>() : null;

            Assert.IsNotNull(_playerInput, $"{nameof(PlayerCameraController)} requires a {nameof(PlayerInput)}.");
            Assert.IsNotNull(_playerDataReference, $"{nameof(PlayerCameraController)} requires a {nameof(PlayerDataReference)}.");
            Assert.IsNotNull(_playerDataReference.CameraTarget, $"{nameof(PlayerDataReference)} requires a camera target.");
            Assert.IsNotNull(_playerDataReference.GameplayCamera, $"{nameof(PlayerDataReference)} requires a gameplay camera.");
            Assert.IsNotNull(_cinemachineBrain, $"{nameof(PlayerCameraController)} requires a {nameof(CinemachineBrain)} on the gameplay camera.");
            Assert.IsNotNull(CameraRigRoot, $"{nameof(PlayerCameraController)} requires the gameplay camera to have a parent transform.");
            Assert.IsNotNull(_thirdPersonCamera, $"{nameof(PlayerCameraController)} requires an authored {ThirdPersonCameraName} under the gameplay camera rig.");
            Assert.IsNotNull(_helmCamera, $"{nameof(PlayerCameraController)} requires an authored {HelmCameraName} under the gameplay camera rig.");
            Assert.IsNotNull(_gunnerCamera, $"{nameof(PlayerCameraController)} requires an authored {BoatGunnerCameraName} under the gameplay camera rig.");
            Assert.IsNotNull(_thirdPersonOrbitalFollow, $"{nameof(PlayerCameraController)} requires an authored {nameof(CinemachineOrbitalFollow)} on {ThirdPersonCameraName}.");
            Assert.IsNotNull(_helmOrbitalFollow, $"{nameof(PlayerCameraController)} requires an authored {nameof(CinemachineOrbitalFollow)} on {HelmCameraName}.");
            Assert.IsNotNull(_gunnerFollow, $"{nameof(PlayerCameraController)} requires an authored {nameof(CinemachineFollow)} on {BoatGunnerCameraName}.");
            Assert.IsNotNull(_thirdPersonInputController, $"{nameof(PlayerCameraController)} requires an authored {nameof(CinemachineInputAxisController)} on {ThirdPersonCameraName}.");
            Assert.IsNotNull(_helmInputController, $"{nameof(PlayerCameraController)} requires an authored {nameof(CinemachineInputAxisController)} on {HelmCameraName}.");

            if (!_prioritiesInitialized)
            {
                _thirdPersonBasePriority = _thirdPersonCamera.Priority.Value;
                _helmBasePriority = _helmCamera.Priority.Value;
                _gunnerBasePriority = _gunnerCamera.Priority.Value;
                _activePriority = Mathf.Max(_thirdPersonBasePriority, Mathf.Max(_helmBasePriority, _gunnerBasePriority)) + 1;
                _prioritiesInitialized = true;
            }
        }

        private void ConfigureChannelsAndInputs()
        {
            OutputChannels outputChannel = ResolveOutputChannel(_playerInput.playerIndex);
            _cinemachineBrain.ChannelMask = outputChannel;
            ConfigureCamera(outputChannel, _thirdPersonCamera, _thirdPersonInputController);
            ConfigureCamera(outputChannel, _helmCamera, _helmInputController);
            ConfigureCamera(outputChannel, _gunnerCamera, null);
        }

        private void ConfigureCamera(
            OutputChannels outputChannel,
            CinemachineCamera camera,
            CinemachineInputAxisController inputController)
        {
            if (camera != null)
            {
                camera.OutputChannel = outputChannel;
            }

            if (inputController != null)
            {
                inputController.PlayerIndex = _playerInput.playerIndex;
            }
        }

        private bool DidInputContextChange()
        {
            string controlScheme = _playerInput != null ? _playerInput.currentControlScheme ?? string.Empty : string.Empty;
            string actionMapName = _playerInput != null ? _playerInput.currentActionMap?.name ?? string.Empty : string.Empty;

            bool changed = !string.Equals(_lastControlScheme, controlScheme, StringComparison.Ordinal)
                || !string.Equals(_lastActionMapName, actionMapName, StringComparison.Ordinal);

            _lastControlScheme = controlScheme;
            _lastActionMapName = actionMapName;
            return changed;
        }

        private void ApplyLookInputTuning(string reason)
        {
            if (_playerInput == null)
            {
                return;
            }

            bool useGamepadTuning = string.Equals(_playerInput.currentControlScheme, Strings.GamepadControlScheme, StringComparison.Ordinal);
            float baseSensitivity = useGamepadTuning ? _gamepadLookSensitivity : _mouseLookSensitivity;
            float helmSensitivity = baseSensitivity * _helmLookSensitivityMultiplier;
            bool cancelDeltaTime = !useGamepadTuning;

            ApplyLookInputTuning(_thirdPersonInputController, baseSensitivity, cancelDeltaTime);
            ApplyLookInputTuning(_helmInputController, helmSensitivity, cancelDeltaTime);
            LogInputDiagnostics(reason, baseSensitivity, helmSensitivity, cancelDeltaTime);
        }

        private static void ApplyLookInputTuning(
            CinemachineInputAxisController inputController,
            float sensitivity,
            bool cancelDeltaTime)
        {
            if (inputController == null)
            {
                return;
            }

            ApplyLookAxisTuning(inputController, LookOrbitXAxisName, sensitivity, cancelDeltaTime);
            ApplyLookAxisTuning(inputController, LookOrbitYAxisName, -sensitivity, cancelDeltaTime);
        }

        private static void ApplyLookAxisTuning(
            CinemachineInputAxisController inputController,
            string axisName,
            float gain,
            bool cancelDeltaTime)
        {
            CinemachineInputAxisController.Controller controller = inputController.GetController(axisName);
            if (controller == null || controller.Input == null)
            {
                return;
            }

            controller.Input.Gain = gain;
            controller.Input.CancelDeltaTime = cancelDeltaTime;
        }

        private void LogInputDiagnostics(string reason, float thirdPersonSensitivity, float helmSensitivity, bool cancelDeltaTime)
        {
            InputAction rotateCameraAction = _playerInput != null && _playerInput.currentActionMap != null
                ? _playerInput.currentActionMap.FindAction(Strings.RotateCameraAction, throwIfNotFound: false)
                : null;
            Vector2 rotateCameraValue = rotateCameraAction != null ? rotateCameraAction.ReadValue<Vector2>() : Vector2.zero;
            float thirdPersonLookX = GetControllerInputValue(_thirdPersonInputController, LookOrbitXAxisName);
            float thirdPersonLookY = GetControllerInputValue(_thirdPersonInputController, LookOrbitYAxisName);
            float helmLookX = GetControllerInputValue(_helmInputController, LookOrbitXAxisName);
            float helmLookY = GetControllerInputValue(_helmInputController, LookOrbitYAxisName);

            LogDebug(
                $"Camera input diagnostics [{reason}]. playerIndex={_playerInput.playerIndex}, scheme={_playerInput.currentControlScheme ?? "None"}, actionMap={_playerInput.currentActionMap?.name ?? "None"}, activeCameraMode={_activeCameraMode}, rotateActionId={rotateCameraAction?.id.ToString() ?? "None"}, rotateActionEnabled={(rotateCameraAction != null && rotateCameraAction.enabled)}, rotateValue=({rotateCameraValue.x:0.###}, {rotateCameraValue.y:0.###}), thirdPersonSensitivity={thirdPersonSensitivity:0.###}, helmSensitivity={helmSensitivity:0.###}, cancelDeltaTime={cancelDeltaTime}, thirdPersonInput=({thirdPersonLookX:0.###}, {thirdPersonLookY:0.###}), helmInput=({helmLookX:0.###}, {helmLookY:0.###}).");
        }

        private static float GetControllerInputValue(CinemachineInputAxisController inputController, string axisName)
        {
            CinemachineInputAxisController.Controller controller = inputController?.GetController(axisName);
            return controller != null ? controller.InputValue : 0f;
        }

        private void QueueThirdPersonOrbitAlignment()
        {
            _needsThirdPersonOrbitAlignment = true;
        }

        private void QueueHelmOrbitAlignment()
        {
            _needsHelmOrbitAlignment = true;
        }

        private void AlignThirdPersonOrbitToVisualFacingTarget()
        {
            SetCameraTargets(_thirdPersonCamera, CameraTarget, CameraTarget);
            float yaw = NormalizeAngle(VisualFacingTarget.eulerAngles.y);
            _thirdPersonOrbitalFollow.HorizontalAxis.Value = _thirdPersonOrbitalFollow.HorizontalAxis.ClampValue(yaw);
            _thirdPersonOrbitalFollow.VerticalAxis.Value = _thirdPersonOrbitalFollow.VerticalAxis.ClampValue(_thirdPersonOrbitalFollow.VerticalAxis.Center);
            _thirdPersonCamera.PreviousStateIsValid = false;
        }

        private void AlignHelmOrbitToTrackingTarget()
        {
            if (_activeHelm == null)
            {
                return;
            }

            Transform trackingTarget = _activeHelm.CameraAnchors != null
                ? _activeHelm.CameraAnchors.TrackingTarget
                : _activeHelm.transform;
            float yaw = NormalizeAngle(trackingTarget.eulerAngles.y);
            _helmOrbitalFollow.HorizontalAxis.Value = _helmOrbitalFollow.HorizontalAxis.ClampValue(yaw);
            _helmOrbitalFollow.VerticalAxis.Value = _helmOrbitalFollow.VerticalAxis.ClampValue(_helmOrbitalFollow.VerticalAxis.Center);
            _helmCamera.PreviousStateIsValid = false;
        }

        private void RefreshCameraMode()
        {
            if (TryResolveActiveGun(out _))
            {
                ActivateGunnerCamera();
                return;
            }

            if (TryResolveActiveHelm(out _))
            {
                ActivateHelmCamera();
                return;
            }

            ActivateThirdPersonCamera();
        }

        private void ActivateHelmCamera()
        {
            _activeGun = null;

            if (!TryResolveActiveHelm(out HelmControl helm))
            {
                ActivateThirdPersonCamera();
                return;
            }

            _activeHelm = helm;
            BindHelmCameraTargets(helm);
            ActivateCameraMode(CameraMode.Helm);
            QueueHelmOrbitAlignment();
        }

        private void ActivateGunnerCamera()
        {
            _activeHelm = null;

            if (!TryResolveActiveGun(out DeckMountedGunControl gun))
            {
                ActivateThirdPersonCamera();
                return;
            }

            _activeGun = gun;
            BindGunnerCameraTargets(gun);
            ResetGunnerCameraFieldOfView(gun);
            ActivateCameraMode(CameraMode.Gunner);
            _gunnerCamera.PreviousStateIsValid = false;
        }

        private void ActivateThirdPersonCamera()
        {
            _activeHelm = null;
            _activeGun = null;
            RestorePlayerCameraTargets();
            ActivateCameraMode(CameraMode.ThirdPerson);
        }

        private void ActivateCameraMode(CameraMode cameraMode)
        {
            CameraMode previousCameraMode = _activeCameraMode;
            _activeCameraMode = cameraMode;

            _thirdPersonCamera.Priority.Value = cameraMode == CameraMode.ThirdPerson
                ? _activePriority
                : _thirdPersonBasePriority;
            _helmCamera.Priority.Value = cameraMode == CameraMode.Helm
                ? _activePriority
                : _helmBasePriority;
            _gunnerCamera.Priority.Value = cameraMode == CameraMode.Gunner
                ? _activePriority
                : _gunnerBasePriority;

            if (cameraMode == CameraMode.ThirdPerson)
            {
                QueueThirdPersonOrbitAlignment();
            }

            if (previousCameraMode == cameraMode)
            {
                return;
            }

            CinemachineCamera activeCamera = cameraMode switch
            {
                CameraMode.Helm => _helmCamera,
                CameraMode.Gunner => _gunnerCamera,
                _ => _thirdPersonCamera
            };
            string helmName = _activeHelm != null ? _activeHelm.name : "None";
            string gunName = _activeGun != null ? _activeGun.name : "None";

            LogInfo(
                $"Camera mode changed. playerIndex={_playerInput.playerIndex}, from={previousCameraMode}, to={cameraMode}, activeCamera={activeCamera.name}, helm={helmName}, gun={gunName}, follow={activeCamera.Follow?.name ?? "None"}, lookAt={activeCamera.LookAt?.name ?? "None"}, thirdPersonPriority={_thirdPersonCamera.Priority.Value}, helmPriority={_helmCamera.Priority.Value}, gunnerPriority={_gunnerCamera.Priority.Value}.");
            ApplyLookInputTuning($"camera_mode_{cameraMode}");
        }

        private static OutputChannels ResolveOutputChannel(int playerIndex)
        {
            return playerIndex switch
            {
                0 => OutputChannels.Default,
                1 => OutputChannels.Channel01,
                2 => OutputChannels.Channel02,
                3 => OutputChannels.Channel03,
                _ => throw new InvalidOperationException($"{nameof(PlayerCameraController)} only supports player indices 0 through 3. Received {playerIndex}.")
            };
        }

        private void BindHelmCameraTargets(HelmControl helm)
        {
            CameraTargetAnchors cameraAnchors = helm.CameraAnchors;
            Transform trackingTarget = cameraAnchors != null ? cameraAnchors.TrackingTarget : helm.transform;
            Transform authoredLookAtTarget = cameraAnchors != null ? cameraAnchors.LookAtTarget : trackingTarget;
            Transform resolvedLookAtTarget = ResolveHelmLookAtTarget(helm, cameraAnchors, trackingTarget, authoredLookAtTarget);
            SetCameraTargets(_helmCamera, trackingTarget, resolvedLookAtTarget);
            LogHelmCameraTargets("bind", helm, trackingTarget, authoredLookAtTarget, resolvedLookAtTarget);
        }

        private void BindGunnerCameraTargets(DeckMountedGunControl gun)
        {
            CameraTargetAnchors cameraAnchors = gun.CameraAnchors;
            Transform trackingTarget = cameraAnchors != null ? cameraAnchors.TrackingTarget : gun.transform;
            Transform lookAtTarget = cameraAnchors != null ? cameraAnchors.LookAtTarget : trackingTarget;
            SetCameraTargets(_gunnerCamera, trackingTarget, lookAtTarget);
            LogInfo(
                $"Boat-gunner camera targets [bind]. gun={gun.name}, tracking={trackingTarget?.name ?? "None"}, lookAt={lookAtTarget?.name ?? "None"}, fov={_gunnerCamera.Lens.FieldOfView:0.##}.");
        }

        private void RestorePlayerCameraTargets()
        {
            SetCameraTargets(_thirdPersonCamera, CameraTarget, CameraTarget);
            SetCameraTargets(_helmCamera, CameraTarget, CameraTarget);
            SetCameraTargets(_gunnerCamera, CameraTarget, CameraTarget);
        }

        private static void SetCameraTargets(CinemachineCamera camera, Transform trackingTarget, Transform lookAtTarget)
        {
            camera.Follow = trackingTarget;
            camera.LookAt = lookAtTarget != null ? lookAtTarget : trackingTarget;
        }

        private Transform ResolveHelmLookAtTarget(
            HelmControl helm,
            CameraTargetAnchors cameraAnchors,
            Transform trackingTarget,
            Transform authoredLookAtTarget)
        {
            if (trackingTarget == null)
            {
                return helm != null ? helm.transform : null;
            }

            if (authoredLookAtTarget != null
                && authoredLookAtTarget != trackingTarget
                && Vector3.Distance(trackingTarget.position, authoredLookAtTarget.position) >= MinHelmLookAtSeparation)
            {
                return authoredLookAtTarget;
            }

            Transform anchorRoot = cameraAnchors != null ? cameraAnchors.transform : helm.transform;
            Transform fallbackLookAtTarget = EnsureRuntimeHelmLookAtTarget(anchorRoot);
            Vector3 trackingLocalPosition = anchorRoot != null
                ? anchorRoot.InverseTransformPoint(trackingTarget.position)
                : Vector3.zero;
            fallbackLookAtTarget.localPosition = trackingLocalPosition + HelmLookAtFallbackOffset;
            fallbackLookAtTarget.localRotation = Quaternion.identity;

            float authoredSeparation = authoredLookAtTarget != null
                ? Vector3.Distance(trackingTarget.position, authoredLookAtTarget.position)
                : 0f;
            LogWarning(
                $"Helm camera look-at target was invalid. helm={helm?.name ?? "None"}, tracking={trackingTarget.name}, authoredLookAt={authoredLookAtTarget?.name ?? "None"}, authoredSeparation={authoredSeparation:0.###}. Using runtime fallback target at localOffset={HelmLookAtFallbackOffset}.");
            return fallbackLookAtTarget;
        }

        private Transform EnsureRuntimeHelmLookAtTarget(Transform anchorRoot)
        {
            if (_runtimeHelmLookAtTarget == null)
            {
                var runtimeTarget = new GameObject(RuntimeHelmLookAtTargetName);
                runtimeTarget.hideFlags = HideFlags.HideAndDontSave;
                _runtimeHelmLookAtTarget = runtimeTarget.transform;
            }

            if (_runtimeHelmLookAtTarget.parent != anchorRoot)
            {
                _runtimeHelmLookAtTarget.SetParent(anchorRoot, false);
            }

            return _runtimeHelmLookAtTarget;
        }

        private void LogHelmCameraTargets(
            string reason,
            HelmControl helm,
            Transform trackingTarget,
            Transform authoredLookAtTarget,
            Transform resolvedLookAtTarget)
        {
            if (trackingTarget == null || resolvedLookAtTarget == null)
            {
                return;
            }

            Transform anchorRoot = helm != null && helm.CameraAnchors != null
                ? helm.CameraAnchors.transform
                : helm != null ? helm.transform : null;
            Vector3 trackingLocalPosition = anchorRoot != null
                ? anchorRoot.InverseTransformPoint(trackingTarget.position)
                : trackingTarget.localPosition;
            Vector3 authoredLookAtLocalPosition = authoredLookAtTarget != null && anchorRoot != null
                ? anchorRoot.InverseTransformPoint(authoredLookAtTarget.position)
                : authoredLookAtTarget != null ? authoredLookAtTarget.localPosition : Vector3.zero;
            Vector3 resolvedLookAtLocalPosition = anchorRoot != null
                ? anchorRoot.InverseTransformPoint(resolvedLookAtTarget.position)
                : resolvedLookAtTarget.localPosition;

            LogInfo(
                $"Helm camera targets [{reason}]. helm={helm?.name ?? "None"}, tracking={trackingTarget.name}, authoredLookAt={authoredLookAtTarget?.name ?? "None"}, resolvedLookAt={resolvedLookAtTarget.name}, trackingLocal=({trackingLocalPosition.x:0.###}, {trackingLocalPosition.y:0.###}, {trackingLocalPosition.z:0.###}), authoredLookAtLocal=({authoredLookAtLocalPosition.x:0.###}, {authoredLookAtLocalPosition.y:0.###}, {authoredLookAtLocalPosition.z:0.###}), resolvedLookAtLocal=({resolvedLookAtLocalPosition.x:0.###}, {resolvedLookAtLocalPosition.y:0.###}, {resolvedLookAtLocalPosition.z:0.###}), targetSeparation={Vector3.Distance(trackingTarget.position, resolvedLookAtTarget.position):0.###}, orbitStyle={_helmOrbitalFollow.OrbitStyle}, sphereRadius={_helmOrbitalFollow.Radius:0.###}, targetOffset=({_helmOrbitalFollow.TargetOffset.x:0.###}, {_helmOrbitalFollow.TargetOffset.y:0.###}, {_helmOrbitalFollow.TargetOffset.z:0.###}), centerOrbit=({_helmOrbitalFollow.Orbits.Center.Radius:0.###} radius, {_helmOrbitalFollow.Orbits.Center.Height:0.###} height), horizontalAxis={_helmOrbitalFollow.HorizontalAxis.Value:0.###}, verticalAxis={_helmOrbitalFollow.VerticalAxis.Value:0.###}.");
        }

        private bool TryResolveActiveHelm(out HelmControl helm)
        {
            return HelmControl.TryGetActiveHelm(_playerInput.playerIndex, out helm) && helm != null;
        }

        private bool TryResolveActiveGun(out DeckMountedGunControl gun)
        {
            return DeckMountedGunControl.TryGetActiveGun(_playerInput.playerIndex, out gun) && gun != null;
        }

        private bool IsLocalPlayerEvent(int playerIndex)
        {
            return _playerInput != null && _playerInput.playerIndex == playerIndex;
        }

        private static CinemachineCamera FindAuthoredCamera(CinemachineCamera[] authoredCameras, string cameraName)
        {
            if (authoredCameras == null)
            {
                return null;
            }

            for (int i = 0; i < authoredCameras.Length; i++)
            {
                CinemachineCamera camera = authoredCameras[i];
                if (camera != null && camera.name == cameraName)
                {
                    return camera;
                }
            }

            return null;
        }

        private static float NormalizeAngle(float angleDegrees)
        {
            return angleDegrees > 180f
                ? angleDegrees - 360f
                : angleDegrees;
        }

        private void ResetGunnerCameraFieldOfView(DeckMountedGunControl gun)
        {
            _gunnerCamera.Lens.FieldOfView = gun != null && gun.GunData != null
                ? gun.GunData.DefaultFieldOfView
                : FallbackBoatGunnerDefaultFieldOfView;
        }

        private void UpdateGunnerCameraZoom()
        {
            if (_gunnerCamera == null || _activeGun == null)
            {
                return;
            }

            BoatGunData gunData = _activeGun.GunData;
            float defaultFieldOfView = gunData != null
                ? gunData.DefaultFieldOfView
                : FallbackBoatGunnerDefaultFieldOfView;
            float zoomFieldOfView = gunData != null
                ? gunData.ZoomFieldOfView
                : FallbackBoatGunnerZoomFieldOfView;
            float zoomTransitionSpeed = gunData != null
                ? gunData.ZoomTransitionSpeed
                : FallbackBoatGunnerZoomTransitionSpeed;
            float targetFieldOfView = _activeGun.IsZoomHeld ? zoomFieldOfView : defaultFieldOfView;

            _gunnerCamera.Lens.FieldOfView = Mathf.MoveTowards(
                _gunnerCamera.Lens.FieldOfView,
                targetFieldOfView,
                zoomTransitionSpeed * Time.deltaTime * Mathf.Abs(defaultFieldOfView - zoomFieldOfView));
        }

        private Transform CameraRigRoot => GameplayCamera.transform.parent;
        private Transform CameraTarget => _playerDataReference.CameraTarget;
        private Camera GameplayCamera => _playerDataReference.GameplayCamera;
        private Transform VisualFacingTarget => _playerDataReference.VisualFacingTarget;

        private enum CameraMode
        {
            ThirdPerson,
            Helm,
            Gunner
        }
    }
}
