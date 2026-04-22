using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.CameraUtils;
using BitBox.Library.Constants;
using BitBox.Library.Eventing.GlobalEvents;
using Bitbox.Splashguard.Nautical.Crane;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox.Splashguard.Nautical
{
    [DisallowMultipleComponent]
    public sealed class CargoBayControls : MonoBehaviourBase
    {
        private const string InteractionTriggerName = "InteractionTrigger";
        private static readonly Dictionary<int, CargoBayControls> ActiveCargoBaysByPlayerIndex = new();

        [Header("References")]
        [SerializeField] private GameObject _portDoor;
        [SerializeField] private GameObject _starboardDoor;
        [SerializeField] private Collider _interactionTrigger;
        [SerializeField] private Transform _stationAnchor;
        [SerializeField] private CraneControlRig _craneRig;

        [Header("Door Motion")]
        [SerializeField] private float _portOpenZDegrees = 90f;
        [SerializeField] private float _starboardOpenZDegrees = -90f;
        [SerializeField, Min(0.01f)] private float _openDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float _closeDuration = 0.45f;

        private readonly Dictionary<PlayerInput, int> _overlappingPlayers = new();
        private readonly List<ColliderState> _controlledPlayerColliderStates = new();
        private readonly List<PlayerInput> _staleOverlappingPlayers = new();

        private Transform _boatTransform;
        private PlayerInput _controllingPlayerInput;
        private PlayerMovement _controlledPlayerMovement;
        private InputAction _actionAction;
        private Vector3 _controlledPlayerLocalPosition;
        private Quaternion _controlledPlayerLocalRotation;
        private Quaternion _portClosedLocalRotation = Quaternion.identity;
        private Quaternion _starboardClosedLocalRotation = Quaternion.identity;
        private bool _doorsOpen;
        private bool _suppressExitUntilActionReleased;
        private bool _isPaused;

        public bool DoorsOpen => _doorsOpen;
        public bool HasControllingPlayer => _controllingPlayerInput != null;
        public CameraTargetAnchors CameraAnchors
        {
            get
            {
                _craneRig ??= ResolveCraneRig();
                return _craneRig != null ? _craneRig.CameraAnchors : null;
            }
        }

        public static bool TryGetActiveCargoBay(int playerIndex, out CargoBayControls controls)
        {
            return ActiveCargoBaysByPlayerIndex.TryGetValue(playerIndex, out controls) && controls != null;
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            if (!enabled)
            {
                return;
            }

            CacheClosedDoorRotations();
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnDisabled()
        {
            ReleaseControl(false, "disabled");
            _overlappingPlayers.Clear();
            _staleOverlappingPlayers.Clear();
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _doorsOpen = false;
            SnapDoorsClosed();
        }

        protected override void OnDestroyed()
        {
            ReleaseControl(false, "destroyed");
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
                TryHandleTakeoverRequest();
                AnimateDoors(Time.deltaTime);
                return;
            }

            ReadControlInputs();
            SyncControlledPlayerPose();
            AnimateDoors(Time.deltaTime);
        }

        protected override void OnLateUpdated()
        {
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

            if (_overlappingPlayers.TryGetValue(playerInput, out int overlapCount))
            {
                _overlappingPlayers[playerInput] = overlapCount + 1;
                return;
            }

            _overlappingPlayers[playerInput] = 1;
            LogInfo(
                $"Player entered cargo bay controls range. controls={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnTriggerExited(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput)
                || !_overlappingPlayers.ContainsKey(playerInput))
            {
                return;
            }

            if (IsPlayerOverlappingInteractionTrigger(playerInput))
            {
                return;
            }

            _overlappingPlayers.Remove(playerInput);

            if (playerInput == _controllingPlayerInput)
            {
                LogInfo(
                    $"Controlling player left cargo trigger while pose-locked. controls={name}, player={DescribePlayer(playerInput)}. Keeping cargo control active.");
                return;
            }

            LogInfo(
                $"Player left cargo bay controls range. controls={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        private void CacheReferences()
        {
            _boatTransform ??= ResolveBoatTransform();
            _interactionTrigger = ResolveInteractionTrigger();
            _craneRig ??= ResolveCraneRig();

            if (_interactionTrigger == null)
            {
                LogError(
                    $"Cargo bay controls could not resolve a trigger collider. controls={name}, root={transform.root?.name ?? "None"}. Assign a trigger collider on the cargo bay interaction volume.");
                enabled = false;
                return;
            }

            if (_portDoor == null || _starboardDoor == null)
            {
                LogError(
                    $"Cargo bay controls are missing door references. controls={name}, portDoor={_portDoor?.name ?? "None"}, starboardDoor={_starboardDoor?.name ?? "None"}.");
                enabled = false;
            }

            if (_craneRig == null)
            {
                LogWarning(
                    $"Cargo bay controls do not have a crane rig assigned. controls={name}. Assign an existing {nameof(CraneControlRig)} in the prefab hierarchy; runtime crane bootstrapping is intentionally disabled.");
            }
        }

        private Transform ResolveBoatTransform()
        {
            Rigidbody parentRigidbody = GetComponentInParent<Rigidbody>();
            if (parentRigidbody != null && parentRigidbody.transform != transform)
            {
                return parentRigidbody.transform;
            }

            return transform.root != null ? transform.root : transform;
        }

        private CraneControlRig ResolveCraneRig()
        {
            if (_craneRig != null)
            {
                return _craneRig;
            }

            return transform.root != null
                ? transform.root.GetComponentInChildren<CraneControlRig>(includeInactive: true)
                : GetComponentInChildren<CraneControlRig>(includeInactive: true);
        }

        private void CacheClosedDoorRotations()
        {
            if (_portDoor != null)
            {
                _portClosedLocalRotation = _portDoor.transform.localRotation;
            }

            if (_starboardDoor != null)
            {
                _starboardClosedLocalRotation = _starboardDoor.transform.localRotation;
            }
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
        }

        private void TryHandleTakeoverRequest()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null
                    || !IsPlayerOverlappingInteractionTrigger(playerInput)
                    || !CanPlayerTakeCargoBay(playerInput))
                {
                    continue;
                }

                if (!WasTakeCargoBayPressedThisFrame(playerInput))
                {
                    continue;
                }

                LogInfo(
                    $"Player requested cargo bay controls. controls={name}, player={DescribePlayer(playerInput)}.");
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

            InputActionMap craneControlsMap = playerInput.actions.FindActionMap(Strings.CraneControls, throwIfNotFound: false);
            Assert.IsNotNull(craneControlsMap, $"{nameof(CargoBayControls)} requires the '{Strings.CraneControls}' action map.");

            _actionAction = craneControlsMap.FindAction(Strings.ActionAction, throwIfNotFound: false);
            Assert.IsNotNull(_actionAction, $"{nameof(CargoBayControls)} requires the '{Strings.ActionAction}' action.");

            PlayerMovement playerMovement = playerInput.GetComponent<PlayerMovement>();
            if (playerMovement != null && !playerMovement.TryBeginScriptedMovement(this))
            {
                LogWarning(
                    $"Player could not take cargo bay controls because scripted movement is already owned. controls={name}, player={DescribePlayer(playerInput)}.");
                return;
            }

            ActivateInputMap(playerInput, Strings.CraneControls);
            _controllingPlayerInput = playerInput;
            _controlledPlayerMovement = playerMovement;
            CaptureControlledPlayerPose(playerInput.transform);
            _suppressExitUntilActionReleased = true;
            DisableControlledPlayerColliders(playerInput.transform);
            ActiveCargoBaysByPlayerIndex[playerInput.playerIndex] = this;
            OpenDoors();
            _craneRig?.BeginControl(playerInput);
            SyncControlledPlayerPose();
            _globalMessageBus.Publish(new PlayerEnteredCraneEvent(playerInput.playerIndex));

            LogInfo($"Player took cargo bay controls. controls={name}, player={DescribePlayer(playerInput)}.");
        }

        private void ReleaseControl(bool restoreInput = true, string reason = "released")
        {
            if (_controllingPlayerInput == null)
            {
                _suppressExitUntilActionReleased = false;
                RestoreControlledPlayerColliders();
                CloseDoors();
                _craneRig?.EndControl();
                return;
            }

            PlayerInput releasedPlayerInput = _controllingPlayerInput;
            int playerIndex = releasedPlayerInput.playerIndex;
            LogInfo($"Player released cargo bay controls. controls={name}, player={DescribePlayer(releasedPlayerInput)}, reason={reason}.");

            if (ActiveCargoBaysByPlayerIndex.TryGetValue(playerIndex, out CargoBayControls activeControls)
                && activeControls == this)
            {
                ActiveCargoBaysByPlayerIndex.Remove(playerIndex);
            }

            _controllingPlayerInput = null;
            _actionAction = null;
            _suppressExitUntilActionReleased = false;
            CloseDoors();
            _craneRig?.EndControl();
            RestoreControlledPlayerColliders();

            if (_controlledPlayerMovement != null)
            {
                _controlledPlayerMovement.EndScriptedMovement(this);
                _controlledPlayerMovement = null;
            }

            if (restoreInput)
            {
                ActivateInputMap(releasedPlayerInput, Strings.ThirdPersonControls);
            }

            _globalMessageBus.Publish(new PlayerExitedCraneEvent(playerIndex));
        }

        public Transform ResolveCraneCameraLookAtTarget()
        {
            _craneRig ??= ResolveCraneRig();
            Transform rigLookAt = _craneRig != null ? _craneRig.CameraLookAtTarget : null;
            if (rigLookAt != null)
            {
                return rigLookAt;
            }

            CameraTargetAnchors cameraAnchors = CameraAnchors;
            return cameraAnchors != null ? cameraAnchors.LookAtTarget : null;
        }

        private void ReadControlInputs()
        {
            if (_actionAction == null)
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
            }
        }

        private void OpenDoors()
        {
            _doorsOpen = true;
        }

        private void CloseDoors()
        {
            _doorsOpen = false;
        }

        private void AnimateDoors(float deltaTime)
        {
            if (_portDoor == null || _starboardDoor == null || deltaTime <= 0f)
            {
                return;
            }

            RotateDoorToward(
                _portDoor.transform,
                ResolvePortTargetRotation(),
                ResolveDoorDegreesPerSecond(_portClosedLocalRotation, ResolvePortOpenRotation()),
                deltaTime);
            RotateDoorToward(
                _starboardDoor.transform,
                ResolveStarboardTargetRotation(),
                ResolveDoorDegreesPerSecond(_starboardClosedLocalRotation, ResolveStarboardOpenRotation()),
                deltaTime);
        }

        private void RotateDoorToward(
            Transform door,
            Quaternion targetRotation,
            float degreesPerSecond,
            float deltaTime)
        {
            if (door == null)
            {
                return;
            }

            door.localRotation = Quaternion.RotateTowards(
                door.localRotation,
                targetRotation,
                degreesPerSecond * deltaTime);
        }

        private float ResolveDoorDegreesPerSecond(Quaternion closedRotation, Quaternion openRotation)
        {
            float angle = Quaternion.Angle(closedRotation, openRotation);
            float duration = _doorsOpen ? _openDuration : _closeDuration;
            return angle / Mathf.Max(0.01f, duration);
        }

        private Quaternion ResolvePortTargetRotation()
        {
            return _doorsOpen ? ResolvePortOpenRotation() : _portClosedLocalRotation;
        }

        private Quaternion ResolveStarboardTargetRotation()
        {
            return _doorsOpen ? ResolveStarboardOpenRotation() : _starboardClosedLocalRotation;
        }

        private Quaternion ResolvePortOpenRotation()
        {
            return _portClosedLocalRotation * Quaternion.Euler(0f, 0f, _portOpenZDegrees);
        }

        private Quaternion ResolveStarboardOpenRotation()
        {
            return _starboardClosedLocalRotation * Quaternion.Euler(0f, 0f, _starboardOpenZDegrees);
        }

        private void SnapDoorsClosed()
        {
            if (_portDoor != null)
            {
                _portDoor.transform.localRotation = _portClosedLocalRotation;
            }

            if (_starboardDoor != null)
            {
                _starboardDoor.transform.localRotation = _starboardClosedLocalRotation;
            }
        }

        private void CaptureControlledPlayerPose(Transform playerTransform)
        {
            if (_stationAnchor != null)
            {
                return;
            }

            Assert.IsNotNull(_boatTransform, $"{nameof(CargoBayControls)} requires a cached boat transform before taking control.");
            _controlledPlayerLocalPosition = _boatTransform.InverseTransformPoint(playerTransform.position);
            _controlledPlayerLocalRotation = Quaternion.Inverse(_boatTransform.rotation) * playerTransform.rotation;
        }

        private void SyncControlledPlayerPose()
        {
            if (_controllingPlayerInput == null)
            {
                return;
            }

            Transform playerTransform = _controllingPlayerInput.transform;
            Vector3 targetPosition;
            Quaternion targetRotation;
            if (_stationAnchor != null)
            {
                targetPosition = _stationAnchor.position;
                targetRotation = _stationAnchor.rotation;
            }
            else
            {
                targetPosition = _boatTransform.TransformPoint(_controlledPlayerLocalPosition);
                targetRotation = _boatTransform.rotation * _controlledPlayerLocalRotation;
            }

            if (_controlledPlayerMovement != null
                && _controlledPlayerMovement.TryApplyScriptedMovementPose(
                    this,
                    targetPosition,
                    targetRotation,
                    locomotionNormalized: 0f))
            {
                return;
            }

            playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
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

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private static bool CanPlayerTakeCargoBay(PlayerInput playerInput)
        {
            return playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls
                && !AnchorControls.IsPlayerInAnchorControlRange(playerInput)
                && !HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _)
                && !DeckMountedGunControl.TryGetActiveGun(playerInput.playerIndex, out _)
                && !TryGetActiveCargoBay(playerInput.playerIndex, out _);
        }

        private static bool WasTakeCargoBayPressedThisFrame(PlayerInput playerInput)
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

        private Collider ResolveInteractionTrigger()
        {
            if (_interactionTrigger != null && _interactionTrigger.isTrigger)
            {
                return _interactionTrigger;
            }

            Transform triggerTransform = FindChildByName(transform, InteractionTriggerName);
            if (triggerTransform != null)
            {
                Collider namedTrigger = ResolveTriggerFrom(triggerTransform.GetComponentsInChildren<Collider>(includeInactive: true));
                if (namedTrigger != null)
                {
                    return namedTrigger;
                }
            }

            return ResolveTriggerFrom(GetComponentsInChildren<Collider>(includeInactive: true));
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
