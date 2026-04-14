using System;
using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.CameraUtils;
using BitBox.Library.Constants;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.Settings;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class DeckMountedGunControl : MonoBehaviourBase
    {
        private const string RotationPivotName = "RotationPivot";
        private const string AuthoredPitchPivotName = "PitchlPivot";
        private const string CorrectedPitchPivotName = "PitchPivot";

        private static readonly Dictionary<int, DeckMountedGunControl> ActiveGunsByPlayerIndex = new();

        [Header("References")]
        [SerializeField, Required, InlineEditor] private BoatGunData _gunData;
        [SerializeField] private Transform _rotationPivot;
        [SerializeField] private Transform _pitchPivot;
        [SerializeField] private Transform _seatAnchor;
        [SerializeField] private Collider _interactionTrigger;
        [SerializeField] private CameraTargetAnchors _cameraAnchors;

        private readonly Dictionary<PlayerInput, int> _overlappingPlayers = new();
        private readonly Dictionary<int, PlayerInput> _playersBlockedFromRetakeUntilExit = new();
        private readonly List<ColliderState> _controlledPlayerColliderStates = new();
        private readonly List<PlayerInput> _staleOverlappingPlayers = new();
        private readonly List<int> _retakeBlocksToRemove = new();

        private Transform _boatTransform;
        private MessageBus _localMessageBus;
        private PlayerInput _controllingPlayerInput;
        private InputAction _aimAction;
        private InputAction _actionAction;
        private InputAction _zoomAction;
        private InputAction _fireAction;
        private Quaternion _rotationPivotInitialLocalRotation;
        private Quaternion _pitchPivotInitialLocalRotation;
        private float _yawDegrees;
        private float _pitchDegrees;
        private bool _suppressExitUntilActionReleased;
        private bool _isPaused;
        private bool _lastFireHeld;

        public BoatGunData GunData => _gunData;
        public CameraTargetAnchors CameraAnchors => _cameraAnchors;
        public bool IsZoomHeld => _zoomAction != null && _zoomAction.IsPressed();

        public static bool TryGetActiveGun(int playerIndex, out DeckMountedGunControl gun)
        {
            return ActiveGunsByPlayerIndex.TryGetValue(playerIndex, out gun) && gun != null;
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            if (!enabled)
            {
                return;
            }

            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnDisabled()
        {
            ReleaseControl(false, "disabled");
            _overlappingPlayers.Clear();
            _playersBlockedFromRetakeUntilExit.Clear();
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (_isPaused)
            {
                SyncControlledPlayerPose();
                return;
            }

            if (_controllingPlayerInput == null)
            {
                RefreshInteractionState();
                TryHandleGunTakeoverRequest();
                return;
            }

            ReadControlInputs();
            SyncControlledPlayerPose();
        }

        protected override void OnTriggerEntered(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput))
            {
                return;
            }

            if (!IsPlayerOverlappingInteractionTrigger(playerInput))
            {
                return;
            }

            if (IsBlockedFromRetake(playerInput))
            {
                return;
            }

            if (_overlappingPlayers.TryGetValue(playerInput, out int overlapCount))
            {
                _overlappingPlayers[playerInput] = overlapCount + 1;
                return;
            }

            _overlappingPlayers[playerInput] = 1;
            LogInfo(
                $"Player entered boat-gun range. gun={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnTriggerExited(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput)
                || !_overlappingPlayers.ContainsKey(playerInput))
            {
                if (playerInput != null && !IsPlayerOverlappingInteractionTrigger(playerInput))
                {
                    _playersBlockedFromRetakeUntilExit.Remove(playerInput.playerIndex);
                }

                return;
            }

            if (IsPlayerOverlappingInteractionTrigger(playerInput))
            {
                return;
            }

            _overlappingPlayers.Remove(playerInput);
            _playersBlockedFromRetakeUntilExit.Remove(playerInput.playerIndex);
            LogInfo(
                $"Player left boat-gun range. gun={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        private void CacheReferences()
        {
            ConfigureMountedRigidbody();
            _boatTransform ??= ResolveBoatTransform();
            _localMessageBus ??= GetComponent<MessageBus>();
            if (_localMessageBus == null)
            {
                _localMessageBus = gameObject.AddComponent<MessageBus>();
            }

            Assert.IsNotNull(_gunData, $"{nameof(DeckMountedGunControl)} requires {nameof(BoatGunData)}.");

            _interactionTrigger = ResolveInteractionTrigger();
            if (_interactionTrigger == null)
            {
                LogError(
                    $"Boat gun could not resolve a trigger collider. gun={name}, root={transform.root?.name ?? "None"}. Assign a trigger collider on the gun interaction volume.");
                enabled = false;
                return;
            }

            _rotationPivot ??= FindChildByName(transform, RotationPivotName);
            _pitchPivot ??= FindChildByName(transform, AuthoredPitchPivotName) ?? FindChildByName(transform, CorrectedPitchPivotName);
            _cameraAnchors ??= GetComponentInChildren<CameraTargetAnchors>(includeInactive: true);

            if (_rotationPivot == null || _pitchPivot == null || _seatAnchor == null || _cameraAnchors == null)
            {
                LogError(
                    $"Boat gun is missing required references. gun={name}, rotationPivot={_rotationPivot?.name ?? "None"}, pitchPivot={_pitchPivot?.name ?? "None"}, seatAnchor={_seatAnchor?.name ?? "None"}, cameraAnchors={_cameraAnchors?.name ?? "None"}.");
                enabled = false;
                return;
            }

            _rotationPivotInitialLocalRotation = _rotationPivot.localRotation;
            _pitchPivotInitialLocalRotation = _pitchPivot.localRotation;
            AttachSeatAndCameraAnchorsToPitchPivot();
            AttachPhysicalCollidersToPitchPivot();
            IgnorePhysicalColliderContactsWithBoat();
            _yawDegrees = 0f;
            _pitchDegrees = Mathf.Clamp(
                NormalizeAngle(_pitchPivot.localEulerAngles.x),
                _gunData.MinPitch,
                _gunData.MaxPitch);
            ApplyPivotRotations();
        }

        private Transform ResolveBoatTransform()
        {
            Rigidbody[] parentRigidbodies = GetComponentsInParent<Rigidbody>(includeInactive: true);
            for (int i = 0; i < parentRigidbodies.Length; i++)
            {
                Rigidbody parentRigidbody = parentRigidbodies[i];
                if (parentRigidbody != null && parentRigidbody.transform != transform)
                {
                    return parentRigidbody.transform;
                }
            }

            if (parentRigidbodies.Length > 0 && parentRigidbodies[0] != null)
            {
                return parentRigidbodies[0].transform;
            }

            return transform.root != null ? transform.root : transform;
        }

        private void ConfigureMountedRigidbody()
        {
            Rigidbody mountedRigidbody = GetComponent<Rigidbody>();
            if (mountedRigidbody == null)
            {
                return;
            }

            mountedRigidbody.useGravity = false;
            mountedRigidbody.isKinematic = true;
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
            if (_isPaused)
            {
                PublishFireInputIfChanged(false);
            }
        }

        private void TryHandleGunTakeoverRequest()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null
                    || !IsPlayerOverlappingInteractionTrigger(playerInput)
                    || IsBlockedFromRetake(playerInput)
                    || !CanPlayerTakeGun(playerInput))
                {
                    continue;
                }

                if (!WasTakeGunPressedThisFrame(playerInput))
                {
                    continue;
                }

                LogInfo($"Player requested boat-gun takeover. gun={name}, player={DescribePlayer(playerInput)}.");
                AssumeControl(playerInput);
                return;
            }
        }

        private void AssumeControl(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput == _controllingPlayerInput)
            {
                return;
            }

            InputActionMap boatGunnerMap = playerInput.actions.FindActionMap(Strings.BoatGunner, throwIfNotFound: false);
            Assert.IsNotNull(boatGunnerMap, $"{nameof(DeckMountedGunControl)} requires the '{Strings.BoatGunner}' action map.");

            _aimAction = boatGunnerMap.FindAction(Strings.AimAction, throwIfNotFound: false);
            _actionAction = boatGunnerMap.FindAction(Strings.ActionAction, throwIfNotFound: false);
            _zoomAction = boatGunnerMap.FindAction(Strings.ZoomAction, throwIfNotFound: false);
            _fireAction = boatGunnerMap.FindAction(Strings.FireAction, throwIfNotFound: false);

            Assert.IsNotNull(_aimAction, $"{nameof(DeckMountedGunControl)} requires the '{Strings.AimAction}' action.");
            Assert.IsNotNull(_actionAction, $"{nameof(DeckMountedGunControl)} requires the '{Strings.ActionAction}' action.");
            Assert.IsNotNull(_zoomAction, $"{nameof(DeckMountedGunControl)} requires the '{Strings.ZoomAction}' action.");
            Assert.IsNotNull(_fireAction, $"{nameof(DeckMountedGunControl)} requires the '{Strings.FireAction}' action.");

            ActivateInputMap(playerInput, Strings.BoatGunner);
            _controllingPlayerInput = playerInput;
            _suppressExitUntilActionReleased = true;
            DisableControlledPlayerColliders(playerInput.transform);
            ActiveGunsByPlayerIndex[playerInput.playerIndex] = this;
            SyncControlledPlayerPose();

            LogInfo($"Player took boat gun. gun={name}, player={DescribePlayer(playerInput)}, actionMap={playerInput.currentActionMap?.name ?? "None"}, fireActionEnabled={_fireAction.enabled}, pitch={_pitchDegrees:0.##}, yaw={_yawDegrees:0.##}.");
            _localMessageBus.Publish(new WeaponControlAcquiredEvent(
                playerInput.playerIndex,
                playerInput.gameObject,
                gameObject,
                _boatTransform != null ? _boatTransform.gameObject : gameObject));
            _globalMessageBus.Publish(new PlayerEnteredBoatGunEvent(playerInput.playerIndex));
        }

        private void ReleaseControl(bool publishEvent = true, string reason = "released")
        {
            if (_controllingPlayerInput == null)
            {
                return;
            }

            PlayerInput releasedPlayerInput = _controllingPlayerInput;
            int playerIndex = releasedPlayerInput.playerIndex;
            LogInfo($"Player released boat gun. gun={name}, player={DescribePlayer(releasedPlayerInput)}, reason={reason}.");
            PublishFireInputIfChanged(false);
            _localMessageBus.Publish(new WeaponControlReleasedEvent(playerIndex, reason));
            BlockPlayerFromRetakeUntilExit(releasedPlayerInput);

            if (ActiveGunsByPlayerIndex.TryGetValue(playerIndex, out DeckMountedGunControl activeGun)
                && activeGun == this)
            {
                ActiveGunsByPlayerIndex.Remove(playerIndex);
            }

            _controllingPlayerInput = null;
            _aimAction = null;
            _actionAction = null;
            _zoomAction = null;
            _fireAction = null;
            _suppressExitUntilActionReleased = false;
            _lastFireHeld = false;
            RestoreControlledPlayerColliders();
            ActivateInputMap(releasedPlayerInput, Strings.ThirdPersonControls);

            if (publishEvent)
            {
                _globalMessageBus.Publish(new PlayerExitedBoatGunEvent(playerIndex));
            }
        }

        private void ReadControlInputs()
        {
            if (_actionAction == null || _aimAction == null)
            {
                return;
            }

            if (_suppressExitUntilActionReleased)
            {
                _suppressExitUntilActionReleased = _actionAction.IsPressed();
            }
            else if (_actionAction.WasPressedThisFrame())
            {
                ReleaseControl(reason: "action_exit");
                return;
            }

            Vector2 aimValue = _aimAction.ReadValue<Vector2>();
            ApplyAim(aimValue);
            PublishFireInputIfChanged(_fireAction != null && _fireAction.IsPressed());
        }

        private void PublishFireInputIfChanged(bool isHeld)
        {
            if (_lastFireHeld == isHeld)
            {
                return;
            }

            _lastFireHeld = isHeld;
            if (_controllingPlayerInput == null || _localMessageBus == null)
            {
                return;
            }

            LogInfo($"Boat-gun fire input changed. gun={name}, player={DescribePlayer(_controllingPlayerInput)}, held={isHeld}, actionMap={_controllingPlayerInput.currentActionMap?.name ?? "None"}, fireActionEnabled={_fireAction != null && _fireAction.enabled}.");
            _localMessageBus.Publish(new WeaponFireInputEvent(_controllingPlayerInput.playerIndex, isHeld));
        }

        private void ApplyAim(Vector2 aimValue)
        {
            if (_gunData == null || aimValue == Vector2.zero)
            {
                return;
            }

            bool isGamepad = string.Equals(
                _controllingPlayerInput.currentControlScheme,
                Strings.GamepadControlScheme,
                StringComparison.Ordinal);

            if (isGamepad && aimValue.sqrMagnitude < _gunData.AimDeadZone * _gunData.AimDeadZone)
            {
                return;
            }

            float zoomMultiplier = IsZoomHeld ? _gunData.ZoomAimMultiplier : 1f;
            float verticalSign = IsInvertVerticalAimEnabled() ? -1f : 1f;

            if (isGamepad)
            {
                _yawDegrees += aimValue.x * _gunData.GamepadYawDegreesPerSecond * zoomMultiplier * Time.deltaTime;
                _pitchDegrees += aimValue.y * _gunData.GamepadPitchDegreesPerSecond * verticalSign * zoomMultiplier * Time.deltaTime;
            }
            else
            {
                _yawDegrees += aimValue.x * _gunData.MouseYawDegreesPerPixel * zoomMultiplier;
                _pitchDegrees += aimValue.y * _gunData.MousePitchDegreesPerPixel * verticalSign * zoomMultiplier;
            }

            _pitchDegrees = _gunData.ClampPitch(_pitchDegrees);
            ApplyPivotRotations();
        }

        private void ApplyPivotRotations()
        {
            if (_rotationPivot == null || _pitchPivot == null)
            {
                return;
            }

            _rotationPivot.localRotation = _rotationPivotInitialLocalRotation * Quaternion.Euler(0f, _yawDegrees, 0f);
            _pitchPivot.localRotation = _pitchPivotInitialLocalRotation * Quaternion.Euler(_pitchDegrees, 0f, 0f);
        }

        private void AttachSeatAndCameraAnchorsToPitchPivot()
        {
            if (_pitchPivot == null)
            {
                return;
            }

            if (_seatAnchor != null && _seatAnchor.parent != _pitchPivot)
            {
                _seatAnchor.SetParent(_pitchPivot, true);
            }

            Transform cameraAnchorRoot = _cameraAnchors != null ? _cameraAnchors.transform : null;
            if (cameraAnchorRoot != null && cameraAnchorRoot.parent != _pitchPivot)
            {
                cameraAnchorRoot.SetParent(_pitchPivot, true);
            }
        }

        private void AttachPhysicalCollidersToPitchPivot()
        {
            if (_pitchPivot == null)
            {
                return;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider gunCollider = colliders[i];
                if (gunCollider == null
                    || gunCollider == _interactionTrigger
                    || gunCollider.isTrigger
                    || gunCollider.transform == transform
                    || gunCollider.transform.IsChildOf(_pitchPivot))
                {
                    continue;
                }

                gunCollider.transform.SetParent(_pitchPivot, true);
            }
        }

        private void IgnorePhysicalColliderContactsWithBoat()
        {
            if (_boatTransform == null || _boatTransform == transform)
            {
                return;
            }

            Collider[] gunColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            Collider[] boatColliders = _boatTransform.GetComponentsInChildren<Collider>(includeInactive: true);

            for (int gunIndex = 0; gunIndex < gunColliders.Length; gunIndex++)
            {
                Collider gunCollider = gunColliders[gunIndex];
                if (!IsPhysicalGunCollider(gunCollider))
                {
                    continue;
                }

                for (int boatIndex = 0; boatIndex < boatColliders.Length; boatIndex++)
                {
                    Collider boatCollider = boatColliders[boatIndex];
                    if (!IsPhysicalBoatCollider(boatCollider))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(gunCollider, boatCollider, true);
                }
            }
        }

        private void SyncControlledPlayerPose()
        {
            if (_controllingPlayerInput == null || _seatAnchor == null)
            {
                return;
            }

            _controllingPlayerInput.transform.SetPositionAndRotation(_seatAnchor.position, _seatAnchor.rotation);
        }

        private void DisableControlledPlayerColliders(Transform playerRoot)
        {
            RestoreControlledPlayerColliders();

            if (playerRoot == null)
            {
                return;
            }

            Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null)
                {
                    continue;
                }

                _controlledPlayerColliderStates.Add(new ColliderState(playerCollider, playerCollider.enabled));
                playerCollider.enabled = false;
            }
        }

        private void RestoreControlledPlayerColliders()
        {
            for (int i = 0; i < _controlledPlayerColliderStates.Count; i++)
            {
                ColliderState colliderState = _controlledPlayerColliderStates[i];
                if (colliderState.Collider != null)
                {
                    colliderState.Collider.enabled = colliderState.WasEnabled;
                }
            }

            _controlledPlayerColliderStates.Clear();
        }

        private void BlockPlayerFromRetakeUntilExit(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            _overlappingPlayers.Remove(playerInput);
            _playersBlockedFromRetakeUntilExit[playerInput.playerIndex] = playerInput;
        }

        private void RefreshInteractionState()
        {
            if (_interactionTrigger == null)
            {
                return;
            }

            _staleOverlappingPlayers.Clear();
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null || !IsPlayerOverlappingInteractionTrigger(playerInput))
                {
                    _staleOverlappingPlayers.Add(playerInput);
                }
            }

            for (int i = 0; i < _staleOverlappingPlayers.Count; i++)
            {
                _overlappingPlayers.Remove(_staleOverlappingPlayers[i]);
            }

            _retakeBlocksToRemove.Clear();
            foreach (var blockedEntry in _playersBlockedFromRetakeUntilExit)
            {
                PlayerInput playerInput = blockedEntry.Value;
                if (playerInput == null || !IsPlayerOverlappingInteractionTrigger(playerInput))
                {
                    _retakeBlocksToRemove.Add(blockedEntry.Key);
                }
            }

            for (int i = 0; i < _retakeBlocksToRemove.Count; i++)
            {
                _playersBlockedFromRetakeUntilExit.Remove(_retakeBlocksToRemove[i]);
            }
        }

        private bool IsPlayerOverlappingInteractionTrigger(PlayerInput playerInput)
        {
            if (playerInput == null || _interactionTrigger == null)
            {
                return false;
            }

            Collider[] playerColliders = playerInput.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider != null
                    && playerCollider.enabled
                    && playerCollider.bounds.Intersects(_interactionTrigger.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlockedFromRetake(PlayerInput playerInput)
        {
            return playerInput != null
                && _playersBlockedFromRetakeUntilExit.ContainsKey(playerInput.playerIndex);
        }

        private static bool IsInvertVerticalAimEnabled()
        {
            return GameSettingsService.Instance != null
                && GameSettingsService.Instance.CurrentSettings.InvertVerticalAim;
        }

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private static bool CanPlayerTakeGun(PlayerInput playerInput)
        {
            return playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls
                && !AnchorControls.IsPlayerInAnchorControlRange(playerInput)
                && !HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _);
        }

        private static bool WasTakeGunPressedThisFrame(PlayerInput playerInput)
        {
            InputActionMap thirdPersonMap = playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            InputAction actionAction = thirdPersonMap?.FindAction(Strings.ActionAction, throwIfNotFound: false);
            return actionAction != null && actionAction.WasPressedThisFrame();
        }

        private static void ActivateInputMap(PlayerInput playerInput, string actionMapName)
        {
            if (playerInput == null || string.IsNullOrWhiteSpace(actionMapName))
            {
                return;
            }

            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(actionMapName);
            playerInput.ActivateInput();
        }

        private static Collider ResolveTriggerFrom(Collider[] candidateColliders)
        {
            for (int i = 0; i < candidateColliders.Length; i++)
            {
                Collider candidateCollider = candidateColliders[i];
                if (candidateCollider != null && candidateCollider.isTrigger)
                {
                    return candidateCollider;
                }
            }

            return null;
        }

        private Collider ResolveInteractionTrigger()
        {
            if (_interactionTrigger != null && _interactionTrigger.isTrigger)
            {
                return _interactionTrigger;
            }

            return ResolveTriggerFrom(GetComponentsInChildren<Collider>(includeInactive: true));
        }

        private bool IsPhysicalGunCollider(Collider candidateCollider)
        {
            return candidateCollider != null
                && candidateCollider != _interactionTrigger
                && !candidateCollider.isTrigger
                && candidateCollider.transform.IsChildOf(transform);
        }

        private bool IsPhysicalBoatCollider(Collider candidateCollider)
        {
            return candidateCollider != null
                && !candidateCollider.isTrigger
                && !candidateCollider.transform.IsChildOf(transform);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nestedChild = FindChildByName(child, childName);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static string DescribePlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return "null";
            }

            return $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }

        private static float NormalizeAngle(float angleDegrees)
        {
            return angleDegrees > 180f
                ? angleDegrees - 360f
                : angleDegrees;
        }

        private readonly struct ColliderState
        {
            public ColliderState(Collider collider, bool wasEnabled)
            {
                Collider = collider;
                WasEnabled = wasEnabled;
            }

            public Collider Collider { get; }
            public bool WasEnabled { get; }
        }
    }
}
