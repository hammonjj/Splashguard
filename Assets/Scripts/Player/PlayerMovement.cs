using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.PlayerEvents;
using Bitbox.Toymageddon.Nautical;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MessageBus))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerDataReference))]
    public class PlayerMovement : MonoBehaviourBase
    {
        private const string TerrainLayerName = "Terrain";
        private const float GroundedGracePeriodSeconds = 0.12f;
        private const float SupportProbeMargin = 0.02f;
        private const float MinimumSupportProbeDistance = 0.12f;
        private static readonly string[] GameplaySpawnTags =
        {
            Tags.PlayerOneSpawnPoint,
            Tags.PlayerTwoSpawnPoint,
            Tags.PlayerThreeSpawnPoint,
            Tags.PlayerFourSpawnPoint,
        };

        private CharacterController _characterController;
        private MessageBus _localMessageBus;
        private PlayerInput _playerInput;
        private PlayerDataReference _playerDataReference;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;
        private bool _isPaused;
        private float _verticalVelocity;
        private float _lastGroundedTime = float.NegativeInfinity;
        private Transform _activeSupportTransform;
        private Vector3 _activeSupportLocalPoint;
        private bool _isFloatingInWater;
        private object _scriptedMovementOwner;

        protected override void OnAwakened()
        {
            CacheReferences();
            BindGameplayActions();
            LogInfo(
                $"Movement initialized. playerIndex={_playerInput.playerIndex}, scheme={_playerInput.currentControlScheme}, actionMap={_playerInput.currentActionMap?.name ?? "None"}, gameplayCamera={GameplayCameraTransform.name}, visualTarget={VisualFacingTarget.name}.");
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            BindGameplayActions();

            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);

            if (_currentMacroScene.IsGameplayScene())
            {
                _isPaused = false;
                SnapToGameplaySpawn(_currentMacroScene);
            }

            LogInfo(
                $"Movement enabled. playerIndex={_playerInput.playerIndex}, scene={_currentMacroScene}, scheme={_playerInput.currentControlScheme}, actionMap={_playerInput.currentActionMap?.name ?? "None"}, locomotionSubscribers={_localMessageBus.GetSubscriberCount<PlayerLocomotionAnimationEvent>()}.");
        }

        protected override void OnDisabled()
        {
            _verticalVelocity = 0f;
            _lastGroundedTime = float.NegativeInfinity;
            _isFloatingInWater = false;
            _scriptedMovementOwner = null;
            ClearActiveSupport();
            PublishGroundedIdleAnimation("disabled");
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (!_currentMacroScene.IsGameplayScene() || _isPaused)
            {
                _isFloatingInWater = false;
                ClearActiveSupport();
                return;
            }

            if (_scriptedMovementOwner != null)
            {
                _verticalVelocity = 0f;
                _lastGroundedTime = Time.time;
                _isFloatingInWater = false;
                ClearActiveSupport();
                PublishGroundedIdleAnimation("scripted_movement");
                return;
            }

            if (!IsGameplayMovementActive())
            {
                _verticalVelocity = 0f;
                _lastGroundedTime = Time.time;
                _isFloatingInWater = false;
                ClearActiveSupport();
                PublishGroundedIdleAnimation("movement_suspended");
                return;
            }

            PublishAnimationSnapshot(UpdateMovement(), "frame_update");
        }

        public bool TryBeginScriptedMovement(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            if (_scriptedMovementOwner != null && !ReferenceEquals(_scriptedMovementOwner, owner))
            {
                return false;
            }

            _scriptedMovementOwner = owner;
            _verticalVelocity = 0f;
            _lastGroundedTime = Time.time;
            _isFloatingInWater = false;
            ClearActiveSupport();
            PublishGroundedIdleAnimation("scripted_movement_started");
            return true;
        }

        public void EndScriptedMovement(object owner)
        {
            if (owner == null || !ReferenceEquals(_scriptedMovementOwner, owner))
            {
                return;
            }

            _scriptedMovementOwner = null;
            _verticalVelocity = 0f;
            _lastGroundedTime = Time.time;
            _isFloatingInWater = false;
            ClearActiveSupport();
            PublishGroundedIdleAnimation("scripted_movement_ended");
        }

        public bool IsScriptedMovementOwnedBy(object owner)
        {
            return owner != null && ReferenceEquals(_scriptedMovementOwner, owner);
        }

        public bool TryApplyScriptedMovementPose(
            object owner,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float locomotionNormalized)
        {
            if (!IsScriptedMovementOwnedBy(owner))
            {
                return false;
            }

            bool controllerWasEnabled = _characterController != null && _characterController.enabled;
            if (controllerWasEnabled)
            {
                _characterController.enabled = false;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (VisualFacingTarget != null)
            {
                VisualFacingTarget.rotation = worldRotation;
            }

            if (controllerWasEnabled)
            {
                _characterController.enabled = true;
            }

            PublishAnimationSnapshot(Mathf.Abs(locomotionNormalized), true, 0f, false, "scripted_movement_pose");
            return true;
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;
            _isPaused = false;

            if (!@event.SceneType.IsGameplayScene())
            {
                _verticalVelocity = 0f;
                _isFloatingInWater = false;
                ClearActiveSupport();
                PublishGroundedIdleAnimation($"macro_scene_loaded:{@event.SceneType}");
                return;
            }

            SnapToGameplaySpawn(@event.SceneType);
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;

            if (_isPaused || !_currentMacroScene.IsGameplayScene())
            {
                _verticalVelocity = 0f;
                _isFloatingInWater = false;
                ClearActiveSupport();
                PublishGroundedIdleAnimation(@event.IsPaused ? "pause_entered" : "pause_released_outside_gameplay");
            }
        }

        private void CacheReferences()
        {
            _characterController ??= GetComponent<CharacterController>();
            _localMessageBus ??= GetComponent<MessageBus>();
            _playerInput ??= GetComponent<PlayerInput>();
            _playerDataReference ??= GetComponent<PlayerDataReference>();

            Assert.IsNotNull(_characterController, $"{nameof(PlayerMovement)} requires a {nameof(CharacterController)}.");
            Assert.IsNotNull(_localMessageBus, $"{nameof(PlayerMovement)} requires a local {nameof(MessageBus)}.");
            Assert.IsNotNull(_playerInput, $"{nameof(PlayerMovement)} requires a {nameof(PlayerInput)}.");
            Assert.IsNotNull(_playerDataReference, $"{nameof(PlayerMovement)} requires a {nameof(PlayerDataReference)}.");
            Assert.IsNotNull(_playerDataReference.GameplayData, $"{nameof(PlayerDataReference)} requires {nameof(PlayerGameplayData)}.");
            Assert.IsNotNull(_playerDataReference.VisualFacingTarget, $"{nameof(PlayerDataReference)} requires a visual facing target.");
            Assert.IsNotNull(_playerDataReference.CameraTarget, $"{nameof(PlayerDataReference)} requires a camera target.");
            Assert.IsNotNull(_playerDataReference.GameplayCamera, $"{nameof(PlayerDataReference)} requires a gameplay camera.");
            Assert.AreNotEqual(_playerDataReference.VisualFacingTarget, _playerDataReference.CameraTarget, $"{nameof(PlayerDataReference)} camera target must not match the visual facing target.");
            AssertCharacterControllerCollisionConfiguration();
        }

        private void BindGameplayActions()
        {
            Assert.IsNotNull(_playerInput.actions, $"{nameof(PlayerMovement)} requires an input actions asset.");

            InputActionMap thirdPersonMap = _playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            Assert.IsNotNull(thirdPersonMap, $"{nameof(PlayerMovement)} requires the '{Strings.ThirdPersonControls}' action map.");

            _moveAction = thirdPersonMap.FindAction(Strings.MoveAction, throwIfNotFound: false);
            _jumpAction = thirdPersonMap.FindAction(Strings.JumpAction, throwIfNotFound: false);

            Assert.IsNotNull(_moveAction, $"{nameof(PlayerMovement)} requires the '{Strings.MoveAction}' action.");
            Assert.IsNotNull(_jumpAction, $"{nameof(PlayerMovement)} requires the '{Strings.JumpAction}' action.");
        }

        private void SnapToGameplaySpawn(MacroSceneType sceneType)
        {
            Transform spawnPoint = ResolveGameplaySpawnPoint(sceneType);

            _characterController.enabled = false;
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            VisualFacingTarget.rotation = spawnPoint.rotation;
            _characterController.enabled = true;

            _verticalVelocity = 0f;
            _lastGroundedTime = Time.time;
            _isFloatingInWater = false;
            ClearActiveSupport();
            PublishGroundedIdleAnimation($"gameplay_spawn_snap:{sceneType}");
            LogInfo(
                $"Snapped to gameplay spawn. playerIndex={_playerInput.playerIndex}, scene={sceneType}, spawnPoint={spawnPoint.name}, position={spawnPoint.position}, rotation={spawnPoint.rotation.eulerAngles}, controllerHeight={_characterController.height}, controllerRadius={_characterController.radius}, controllerCenter={_characterController.center}, locomotionSubscribers={_localMessageBus.GetSubscriberCount<PlayerLocomotionAnimationEvent>()}.");
        }

        private Transform ResolveGameplaySpawnPoint(MacroSceneType sceneType)
        {
            if (sceneType == MacroSceneType.HubWorld)
            {
                GameObject coordinatorObject = GameObject.FindWithTag(Tags.HubWorldCoordinator);
                Assert.IsNotNull(coordinatorObject, $"No GameObject tagged '{Tags.HubWorldCoordinator}' is loaded for hub-world spawn resolution.");

                HubWorldCoordinator coordinator = coordinatorObject.GetComponent<HubWorldCoordinator>();
                Assert.IsNotNull(coordinator, $"GameObject tagged '{Tags.HubWorldCoordinator}' must have a {nameof(HubWorldCoordinator)} component.");

                return coordinator.ResolveSpawnPoint(_playerInput.playerIndex);
            }

            Assert.IsTrue(
                _playerInput.playerIndex >= 0 && _playerInput.playerIndex < GameplaySpawnTags.Length,
                $"{nameof(PlayerMovement)} does not have an authored gameplay spawn tag for player index {_playerInput.playerIndex}.");

            string spawnTag = GameplaySpawnTags[_playerInput.playerIndex];
            GameObject spawnObject = GameObject.FindWithTag(spawnTag);
            Assert.IsNotNull(
                spawnObject,
                $"No GameObject tagged '{spawnTag}' is loaded for gameplay spawn resolution in scene {sceneType}.");

            return spawnObject.transform;
        }

        private PlayerLocomotionAnimationEvent UpdateMovement()
        {
            PlayerGameplayData gameplayData = _playerDataReference.GameplayData;
            ControlSchemeKind controlSchemeKind = ResolveControlSchemeKind();
            Vector2 rawMoveInput = _moveAction.ReadValue<Vector2>();
            Vector2 moveInput = ResolveGameplayMoveInput(rawMoveInput, controlSchemeKind, gameplayData.MoveInputDeadZone);
            Vector3 moveDirection = CalculateCameraRelativeMove(moveInput, GameplayCameraTransform);
            bool hasGroundSupportBeforeMove = TryResolveGroundSupport(out _);
            bool rawGroundedBeforeMove = _characterController.isGrounded || hasGroundSupportBeforeMove;
            bool isGroundedBeforeMove = ResolveGroundedState(rawGroundedBeforeMove, false);
            WaterFloatFrame waterFloatBeforeMove = ResolveWaterFloatFrame(gameplayData, rawGroundedBeforeMove, allowEnter: true);
            bool isFloatingBeforeMove = waterFloatBeforeMove.IsFloating;
            Vector3 supportDisplacement = isFloatingBeforeMove ? Vector3.zero : ResolveSupportDisplacement();
            if (isFloatingBeforeMove)
            {
                ClearActiveSupport();
            }

            RotateVisualFacingTarget(moveDirection, gameplayData.RotationSharpness);
            bool jumpStartedThisFrame = UpdateVerticalVelocity(
                gameplayData,
                isGroundedBeforeMove,
                isFloatingBeforeMove,
                waterFloatBeforeMove);

            float movementSpeed = gameplayData.WalkSpeed * (isFloatingBeforeMove ? gameplayData.WaterMoveSpeedMultiplier : 1f);
            Vector3 velocity = moveDirection * movementSpeed;
            velocity.y = _verticalVelocity;
            CollisionFlags collisionFlags = _characterController.Move(supportDisplacement + (velocity * Time.deltaTime));
            bool hasGroundSupportAfterMove = TryResolveGroundSupport(out Transform supportTransform);
            bool rawGroundedAfterMove = (collisionFlags & CollisionFlags.Below) != 0 || _characterController.isGrounded || hasGroundSupportAfterMove;
            bool isGroundedAfterMove = ResolveGroundedState(rawGroundedAfterMove, jumpStartedThisFrame);
            WaterFloatFrame waterFloatAfterMove = ResolveWaterFloatFrame(gameplayData, rawGroundedAfterMove, allowEnter: !jumpStartedThisFrame);
            bool isFloatingAfterMove = !jumpStartedThisFrame && waterFloatAfterMove.IsFloating;

            if (isGroundedAfterMove && _verticalVelocity < 0f)
            {
                _verticalVelocity = gameplayData.GroundedVerticalVelocity;
            }

            if (isFloatingAfterMove && _verticalVelocity < -gameplayData.WaterFloatSinkSpeed)
            {
                _verticalVelocity = -gameplayData.WaterFloatSinkSpeed;
            }

            UpdateActiveSupport(isGroundedAfterMove && !isFloatingAfterMove, supportTransform);
            bool isGroundedForAnimation = isGroundedAfterMove || isFloatingAfterMove;

            return new PlayerLocomotionAnimationEvent(
                ResolveLocomotionAnimationValue(moveInput, controlSchemeKind),
                isGroundedForAnimation,
                _verticalVelocity,
                jumpStartedThisFrame);
        }

        private bool UpdateVerticalVelocity(
            PlayerGameplayData gameplayData,
            bool isGrounded,
            bool isFloatingInWater,
            WaterFloatFrame waterFloatFrame)
        {
            if (isGrounded && !isFloatingInWater && _verticalVelocity < 0f)
            {
                _verticalVelocity = gameplayData.GroundedVerticalVelocity;
            }

            float riseGravity = gameplayData.Gravity * gameplayData.JumpRiseGravityMultiplier;
            bool jumpStartedThisFrame = false;
            if ((isGrounded || isFloatingInWater) && _jumpAction.WasPressedThisFrame())
            {
                _lastGroundedTime = float.NegativeInfinity;
                _verticalVelocity = isFloatingInWater
                    ? PlayerWaterFloatUtility.CalculateWaterJumpVelocity(
                        gameplayData.JumpHeight,
                        gameplayData.Gravity,
                        gameplayData.JumpRiseGravityMultiplier,
                        gameplayData.WaterJumpHeightMultiplier)
                    : Mathf.Sqrt(gameplayData.JumpHeight * 2f * riseGravity);
                _isFloatingInWater = false;
                jumpStartedThisFrame = true;
            }

            if (isFloatingInWater && !jumpStartedThisFrame && waterFloatFrame.HasWaterSample)
            {
                _verticalVelocity = PlayerWaterFloatUtility.CalculateFloatVerticalVelocity(
                    transform.position.y,
                    waterFloatFrame.WaterHeight,
                    gameplayData.WaterSurfaceRootOffset,
                    gameplayData.WaterFloatCorrectionSharpness,
                    gameplayData.WaterFloatRiseSpeed,
                    gameplayData.WaterFloatSinkSpeed);
                return false;
            }

            float appliedGravityMultiplier = _verticalVelocity > 0f
                ? gameplayData.JumpRiseGravityMultiplier
                : gameplayData.JumpFallGravityMultiplier;
            _verticalVelocity -= gameplayData.Gravity * appliedGravityMultiplier * Time.deltaTime;
            return jumpStartedThisFrame;
        }

        private WaterFloatFrame ResolveWaterFloatFrame(
            PlayerGameplayData gameplayData,
            bool rawIsGrounded,
            bool allowEnter)
        {
            if (!gameplayData.WaterFloatEnabled)
            {
                _isFloatingInWater = false;
                return WaterFloatFrame.Empty;
            }

            Vector3 controllerBottomPoint = CalculateControllerBottomPoint();
            bool hasWaterSample = WaterQuery.TrySample(controllerBottomPoint, out WaterSample waterSample);
            float waterHeight = hasWaterSample ? waterSample.Height : 0f;

            if (PlayerWaterFloatUtility.ShouldExitFloat(
                    _isFloatingInWater,
                    hasWaterSample,
                    rawIsGrounded,
                    transform.position.y,
                    waterHeight,
                    gameplayData.WaterExitHeight))
            {
                _isFloatingInWater = false;
            }

            if (!_isFloatingInWater
                && allowEnter
                && PlayerWaterFloatUtility.ShouldEnterFloat(
                    gameplayData.WaterFloatEnabled,
                    hasWaterSample,
                    rawIsGrounded,
                    _verticalVelocity,
                    controllerBottomPoint.y,
                    waterHeight,
                    gameplayData.WaterEnterDepth))
            {
                _isFloatingInWater = true;
            }

            return new WaterFloatFrame(_isFloatingInWater, hasWaterSample, waterHeight);
        }

        private bool ResolveGroundedState(bool rawIsGrounded, bool jumpStartedThisFrame)
        {
            if (jumpStartedThisFrame || _verticalVelocity > 0f)
            {
                return false;
            }

            if (rawIsGrounded)
            {
                _lastGroundedTime = Time.time;
                return true;
            }

            return Time.time <= _lastGroundedTime + GroundedGracePeriodSeconds;
        }

        private void RotateVisualFacingTarget(Vector3 moveDirection, float rotationSharpness)
        {
            if (moveDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            float interpolationFactor = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            VisualFacingTarget.rotation = Quaternion.Slerp(VisualFacingTarget.rotation, targetRotation, interpolationFactor);
        }

        private bool IsGameplayMovementActive()
        {
            return string.Equals(
                _playerInput.currentActionMap?.name,
                Strings.ThirdPersonControls,
                System.StringComparison.Ordinal);
        }

        private Vector3 ResolveSupportDisplacement()
        {
            if (_activeSupportTransform == null || IsPlayerVesselSupport(_activeSupportTransform))
            {
                return Vector3.zero;
            }

            Vector3 targetPosition = _activeSupportTransform.TransformPoint(_activeSupportLocalPoint);
            return targetPosition - transform.position;
        }

        private void UpdateActiveSupport(bool isGrounded, Transform supportTransform)
        {
            if (!isGrounded || supportTransform == null || IsPlayerVesselSupport(supportTransform))
            {
                ClearActiveSupport();
                return;
            }

            _activeSupportTransform = supportTransform;
            _activeSupportLocalPoint = _activeSupportTransform.InverseTransformPoint(transform.position);
        }

        private void ClearActiveSupport()
        {
            _activeSupportTransform = null;
            _activeSupportLocalPoint = Vector3.zero;
        }

        private static bool IsPlayerVesselSupport(Transform supportTransform)
        {
            return supportTransform != null
                && supportTransform.GetComponentInParent<PlayerVesselRoot>() != null;
        }

        private bool TryResolveGroundSupport(out Transform supportTransform)
        {
            supportTransform = null;

            if (_characterController == null || !_characterController.enabled)
            {
                return false;
            }

            Vector3 controllerCenter = transform.TransformPoint(_characterController.center);
            float halfHeight = Mathf.Max(_characterController.height * 0.5f, _characterController.radius);
            Vector3 bottomCenter = controllerCenter + (Vector3.down * (halfHeight - _characterController.radius));
            float sphereRadius = Mathf.Max(_characterController.radius - SupportProbeMargin, _characterController.radius * 0.5f);
            float probeDistance = Mathf.Max(_characterController.skinWidth + _characterController.stepOffset + SupportProbeMargin, MinimumSupportProbeDistance);
            Vector3 probeOrigin = bottomCenter + (Vector3.up * (sphereRadius + SupportProbeMargin));

            if (!Physics.SphereCast(
                    probeOrigin,
                    sphereRadius,
                    Vector3.down,
                    out RaycastHit hit,
                    probeDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.transform == null || hit.transform.IsChildOf(transform))
            {
                return false;
            }

            supportTransform = hit.rigidbody != null
                ? hit.rigidbody.transform
                : hit.collider.transform;

            return supportTransform != null;
        }

        private Vector3 CalculateControllerBottomPoint()
        {
            Vector3 controllerCenter = transform.TransformPoint(_characterController.center);
            float bottomY = PlayerWaterFloatUtility.CalculateControllerBottomY(
                controllerCenter,
                _characterController.height,
                _characterController.radius);
            return new Vector3(controllerCenter.x, bottomY, controllerCenter.z);
        }

        private Transform GameplayCameraTransform => _playerDataReference.GameplayCamera.transform;
        private Transform VisualFacingTarget => _playerDataReference.VisualFacingTarget;

        private void AssertCharacterControllerCollisionConfiguration()
        {
            int terrainLayer = LayerMask.NameToLayer(TerrainLayerName);
            Assert.IsTrue(terrainLayer >= 0, $"Expected a '{TerrainLayerName}' layer to exist.");

            int terrainMask = 1 << terrainLayer;
            Assert.IsTrue(_characterController.detectCollisions, $"{nameof(CharacterController)} must have collision detection enabled.");
            Assert.IsTrue(
                (_characterController.includeLayers.value & terrainMask) == terrainMask,
                $"{nameof(CharacterController)} include layers must include '{TerrainLayerName}'.");
            Assert.IsTrue(
                (_characterController.excludeLayers.value & terrainMask) == 0,
                $"{nameof(CharacterController)} exclude layers must not exclude '{TerrainLayerName}'.");
        }

        private void PublishAnimationSnapshot(PlayerLocomotionAnimationEvent animationEvent, string reason)
        {
            Assert.IsNotNull(animationEvent, $"{nameof(PlayerMovement)} requires an animation event payload to publish.");
            PublishAnimationSnapshot(
                animationEvent.LocomotionNormalized,
                animationEvent.IsGrounded,
                animationEvent.VerticalVelocity,
                animationEvent.JumpStartedThisFrame,
                reason);
        }

        private void PublishGroundedIdleAnimation(string reason)
        {
            PublishAnimationSnapshot(0f, true, 0f, false, reason);
        }

        private void PublishAnimationSnapshot(float locomotionNormalized, bool isGrounded, float verticalVelocity, bool jumpStartedThisFrame, string reason)
        {
            Assert.IsNotNull(_localMessageBus, $"{nameof(PlayerMovement)} requires a local {nameof(MessageBus)} before publishing locomotion animation.");

            var normalizedLocomotion = Mathf.Clamp01(locomotionNormalized);
            _localMessageBus.Publish(new PlayerLocomotionAnimationEvent(normalizedLocomotion, isGrounded, verticalVelocity, jumpStartedThisFrame));
        }

        private ControlSchemeKind ResolveControlSchemeKind()
        {
            string controlScheme = _playerInput.currentControlScheme;
            Assert.IsFalse(string.IsNullOrWhiteSpace(controlScheme), $"{nameof(PlayerMovement)} requires an active control scheme while updating gameplay movement.");

            if (string.Equals(controlScheme, Strings.KeyboardControlScheme, System.StringComparison.OrdinalIgnoreCase))
            {
                return ControlSchemeKind.KeyboardMouse;
            }

            if (string.Equals(controlScheme, Strings.GamepadControlScheme, System.StringComparison.OrdinalIgnoreCase))
            {
                return ControlSchemeKind.Gamepad;
            }

            string message = $"{nameof(PlayerMovement)} does not support control scheme '{controlScheme}'.";
            Assert.IsTrue(false, message);
            throw new System.InvalidOperationException(message);
        }

        private static Vector2 ResolveGameplayMoveInput(Vector2 rawMoveInput, ControlSchemeKind controlSchemeKind, float moveInputDeadZone)
        {
            if (rawMoveInput.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            if (controlSchemeKind == ControlSchemeKind.KeyboardMouse)
            {
                return rawMoveInput.sqrMagnitude > 1f
                    ? rawMoveInput.normalized
                    : rawMoveInput;
            }

            float magnitude = rawMoveInput.magnitude;
            if (magnitude <= moveInputDeadZone)
            {
                return Vector2.zero;
            }

            float remappedMagnitude = Mathf.Clamp01((magnitude - moveInputDeadZone) / (1f - moveInputDeadZone));
            return rawMoveInput.normalized * remappedMagnitude;
        }

        private static float ResolveLocomotionAnimationValue(Vector2 resolvedMoveInput, ControlSchemeKind controlSchemeKind)
        {
            float magnitude = resolvedMoveInput.magnitude;
            if (magnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            return controlSchemeKind == ControlSchemeKind.KeyboardMouse
                ? 1f
                : Mathf.Clamp01(magnitude);
        }

        private static Vector3 CalculateCameraRelativeMove(Vector2 moveInput, Transform gameplayCameraTransform)
        {
            Vector3 forward = gameplayCameraTransform.forward;
            forward.y = 0f;
            Assert.IsTrue(forward.sqrMagnitude > Mathf.Epsilon, $"{nameof(PlayerMovement)} requires the gameplay camera forward vector to remain planar.");
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            return moveDirection;
        }

        private enum ControlSchemeKind
        {
            KeyboardMouse,
            Gamepad
        }

        private readonly struct WaterFloatFrame
        {
            public static readonly WaterFloatFrame Empty = new(false, false, 0f);

            public WaterFloatFrame(bool isFloating, bool hasWaterSample, float waterHeight)
            {
                IsFloating = isFloating;
                HasWaterSample = hasWaterSample;
                WaterHeight = waterHeight;
            }

            public bool IsFloating { get; }
            public bool HasWaterSample { get; }
            public float WaterHeight { get; }
        }
    }
}
