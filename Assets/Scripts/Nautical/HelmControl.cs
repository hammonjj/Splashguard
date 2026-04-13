using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.CameraUtils;
using BitBox.Library.Constants;
using BitBox.Library.Eventing.GlobalEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using Bitbox.Toymageddon.Nautical;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public class HelmControl : MonoBehaviourBase
    {
        private const string TerrainLayerName = "Terrain";
        private static readonly Dictionary<int, HelmControl> ActiveHelmsByPlayerIndex = new();

        [Header("References")]
        [SerializeField, Required, InlineEditor] private BoatNavigationData _boatNavigationData;
        [SerializeField] private Transform _driveTransform;
        [SerializeField] private Collider _interactionTrigger;
        [SerializeField] private CameraTargetAnchors _cameraAnchors;

        private readonly Dictionary<PlayerInput, int> _overlappingPlayers = new();

        private Rigidbody _rigidbody;
        private Transform _boatTransform;
        private PlayerInput _controllingPlayerInput;
        private InputAction _throttleAction;
        private InputAction _steeringAction;
        private InputAction _actionAction;
        private InputAction _killThrottleAction;
        private Vector3 _controlledPlayerLocalPosition;
        private Quaternion _controlledPlayerLocalRotation;
        private float _throttleSetting;
        private float _steeringInput;
        private bool _isPaused;
        private int _terrainLayerMask;

        public float ThrottleSettingNormalized => _throttleSetting;
        public float SignedForwardSpeed => _rigidbody != null && _driveTransform != null
            ? Vector3.Dot(_rigidbody.linearVelocity, _driveTransform.forward)
            : 0f;
        public float MaxForwardSpeed => _boatNavigationData != null ? _boatNavigationData.MaxForwardSpeed : 0f;
        public float MaxReverseSpeed => _boatNavigationData != null ? _boatNavigationData.MaxReverseSpeed : 0f;
        public CameraTargetAnchors CameraAnchors => _cameraAnchors;

        public static bool TryGetActiveHelm(int playerIndex, out HelmControl helm)
        {
            return ActiveHelmsByPlayerIndex.TryGetValue(playerIndex, out helm) && helm != null;
        }

        protected override void OnEnabled()
        {
            CacheReferences();

            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnDisabled()
        {
            ReleaseControl(false, "disabled");
            _overlappingPlayers.Clear();

            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (_isPaused)
            {
                _steeringInput = 0f;
                SyncControlledPlayerPose();
                return;
            }

            if (_controllingPlayerInput == null)
            {
                _steeringInput = 0f;
                TryHandleHelmTakeoverRequest();
                return;
            }

            ReadControlInputs();
            SyncControlledPlayerPose();
        }

        protected override void OnFixedUpdated()
        {
            if (_rigidbody == null || _isPaused)
            {
                return;
            }

            ApplyThrottleForce();
            ApplySteeringTorque();
        }

        protected override void OnTriggerEntered(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput))
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
                $"Player entered helm range. helm={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnTriggerExited(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput)
                || !_overlappingPlayers.TryGetValue(playerInput, out int overlapCount))
            {
                return;
            }

            if (overlapCount > 1)
            {
                _overlappingPlayers[playerInput] = overlapCount - 1;
                return;
            }

            _overlappingPlayers.Remove(playerInput);
            LogInfo(
                $"Player left helm range. helm={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");

            if (playerInput == _controllingPlayerInput)
            {
                ReleaseControl(reason: "left_range");
            }
        }

        protected override void OnDrawnGizmos()
        {
            if (_driveTransform == null || _boatNavigationData == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                GetTerrainProbeOrigin(),
                GetTerrainProbeOrigin() + (Vector3.down * (_boatNavigationData.TerrainProbeHeight + _boatNavigationData.TerrainProbeDepth)));
        }

        private void CacheReferences()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponentInParent<Rigidbody>();
            }

            if (_rigidbody == null)
            {
                LogError(
                    $"Helm could not resolve a parent rigidbody. helm={name}, parent={transform.parent?.name ?? "None"}, root={transform.root?.name ?? "None"}.");
                enabled = false;
                return;
            }

            Assert.IsNotNull(_boatNavigationData, $"{nameof(HelmControl)} requires {nameof(BoatNavigationData)}.");

            _boatTransform = _rigidbody.transform;

            if (_driveTransform == null)
            {
                _driveTransform = _boatTransform;
            }

            _interactionTrigger = ResolveInteractionTrigger();
            if (_interactionTrigger == null)
            {
                LogError(
                    $"Helm could not resolve a trigger collider. helm={name}, root={_boatTransform.name}. Assign a trigger collider on the helm interaction volume.");
                enabled = false;
                return;
            }

            _cameraAnchors ??= GetComponentInChildren<CameraTargetAnchors>(includeInactive: true);

            _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
            Assert.IsTrue(_terrainLayerMask != 0, $"Expected a '{TerrainLayerName}' layer to exist.");
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
            _steeringInput = 0f;
        }

        private void AssumeControl(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput == _controllingPlayerInput)
            {
                return;
            }

            InputActionMap navalNavigationMap = playerInput.actions.FindActionMap(Strings.NavalNavigation, throwIfNotFound: false);
            Assert.IsNotNull(navalNavigationMap, $"{nameof(HelmControl)} requires the '{Strings.NavalNavigation}' action map.");

            _throttleAction = navalNavigationMap.FindAction(Strings.ThrottleAction, throwIfNotFound: false);
            _steeringAction = navalNavigationMap.FindAction(Strings.SteeringAction, throwIfNotFound: false);
            _actionAction = navalNavigationMap.FindAction(Strings.ActionAction, throwIfNotFound: false);
            _killThrottleAction = navalNavigationMap.FindAction(Strings.KillThrottleAction, throwIfNotFound: false);

            Assert.IsNotNull(_throttleAction, $"{nameof(HelmControl)} requires the '{Strings.ThrottleAction}' action.");
            Assert.IsNotNull(_steeringAction, $"{nameof(HelmControl)} requires the '{Strings.SteeringAction}' action.");
            Assert.IsNotNull(_actionAction, $"{nameof(HelmControl)} requires the '{Strings.ActionAction}' action.");
            Assert.IsNotNull(_killThrottleAction, $"{nameof(HelmControl)} requires the '{Strings.KillThrottleAction}' action.");

            _controllingPlayerInput = playerInput;
            CaptureControlledPlayerPose(playerInput.transform);
            ActiveHelmsByPlayerIndex[playerInput.playerIndex] = this;
            LogInfo(
                $"Player took helm. helm={name}, player={DescribePlayer(playerInput)}, throttle={_throttleSetting:0.00}.");
            _globalMessageBus.Publish(new PlayerEnteredHelmEvent(playerInput.playerIndex));
        }

        private void TryHandleHelmTakeoverRequest()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null || !CanPlayerTakeHelm(playerInput))
                {
                    continue;
                }

                if (!WasTakeHelmPressedThisFrame(playerInput))
                {
                    continue;
                }

                LogInfo(
                    $"Player requested helm takeover. helm={name}, player={DescribePlayer(playerInput)}.");
                AssumeControl(playerInput);
                return;
            }
        }

        private void ReleaseControl(bool publishEvent = true, string reason = "released")
        {
            if (_controllingPlayerInput == null)
            {
                _steeringInput = 0f;
                return;
            }

            PlayerInput releasedPlayerInput = _controllingPlayerInput;
            int playerIndex = _controllingPlayerInput.playerIndex;
            LogInfo(
                $"Player released helm. helm={name}, player={DescribePlayer(releasedPlayerInput)}, reason={reason}, throttle={_throttleSetting:0.00}.");

            if (ActiveHelmsByPlayerIndex.TryGetValue(playerIndex, out HelmControl activeHelm)
                && activeHelm == this)
            {
                ActiveHelmsByPlayerIndex.Remove(playerIndex);
            }

            _controllingPlayerInput = null;
            _throttleAction = null;
            _steeringAction = null;
            _actionAction = null;
            _killThrottleAction = null;
            _steeringInput = 0f;

            if (publishEvent)
            {
                _globalMessageBus.Publish(new PlayerExitedHelmEvent(playerIndex));
            }
        }

        private void ReadControlInputs()
        {
            float throttleInput = Mathf.Clamp(_throttleAction.ReadValue<float>(), -1f, 1f);
            _steeringInput = Mathf.Clamp(_steeringAction.ReadValue<float>(), -1f, 1f);

            if (_killThrottleAction.WasPressedThisFrame())
            {
                _throttleSetting = 0f;
            }

            if (_actionAction.WasPressedThisFrame())
            {
                ReleaseControl(reason: "action_exit");
                return;
            }

            if (Mathf.Abs(throttleInput) <= Mathf.Epsilon)
            {
                return;
            }

            _throttleSetting = Mathf.Clamp(
                _throttleSetting + (throttleInput * _boatNavigationData.ThrottleChangeRate * Time.deltaTime),
                -1f,
                1f);
        }

        private void ApplyThrottleForce()
        {
            if (!WaterSurface.TryFindSurface(_rigidbody.worldCenterOfMass, out _, out _))
            {
                return;
            }

            Vector3 forward = _driveTransform.forward;
            float currentForwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, forward);
            float targetSpeed = ResolveTargetSpeed();
            float speedError = targetSpeed - currentForwardSpeed;
            float desiredAcceleration = Mathf.Clamp(
                speedError * _boatNavigationData.SpeedResponse,
                -_boatNavigationData.DriveDeceleration,
                _boatNavigationData.DriveAcceleration);

            _rigidbody.AddForce(forward * desiredAcceleration, ForceMode.Acceleration);

            if (targetSpeed <= 0f || currentForwardSpeed <= 0f)
            {
                return;
            }

            if (!IsTerrainBlockingAhead())
            {
                return;
            }

            float brakingAcceleration = Mathf.Min(
                currentForwardSpeed * _boatNavigationData.TerrainBrakeAcceleration,
                _boatNavigationData.TerrainBrakeAcceleration);
            _rigidbody.AddForce(-forward * brakingAcceleration, ForceMode.Acceleration);
        }

        private void ApplySteeringTorque()
        {
            float currentForwardSpeed = Mathf.Abs(Vector3.Dot(_rigidbody.linearVelocity, _driveTransform.forward));
            float steeringAuthority = Mathf.Clamp01(
                Mathf.Max(currentForwardSpeed, Mathf.Abs(_throttleSetting) * _boatNavigationData.MaxForwardSpeed)
                / _boatNavigationData.SteeringAuthoritySpeed);
            float steeringTorque = _steeringInput * _boatNavigationData.SteeringTorque * steeringAuthority;
            float dampingTorque = -_rigidbody.angularVelocity.y * _boatNavigationData.SteeringDamping;

            _rigidbody.AddTorque(Vector3.up * (steeringTorque + dampingTorque), ForceMode.Acceleration);
        }

        private float ResolveTargetSpeed()
        {
            float targetSpeed = _throttleSetting >= 0f
                ? _throttleSetting * _boatNavigationData.MaxForwardSpeed
                : _throttleSetting * _boatNavigationData.MaxReverseSpeed;

            if (targetSpeed > 0f && IsTerrainBlockingAhead())
            {
                return 0f;
            }

            return targetSpeed;
        }

        private bool IsTerrainBlockingAhead()
        {
            Vector3 probePoint = _driveTransform.position + (_driveTransform.forward * _boatNavigationData.TerrainProbeForwardDistance);
            Vector3 probeOrigin = probePoint + (Vector3.up * _boatNavigationData.TerrainProbeHeight);

            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    _boatNavigationData.TerrainProbeHeight + _boatNavigationData.TerrainProbeDepth,
                    _terrainLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (!WaterSurface.TryFindSurface(probePoint, out _, out WaterSample waterSample))
            {
                return true;
            }

            return hit.point.y >= waterSample.Height - _boatNavigationData.TerrainClearance;
        }

        private Vector3 GetTerrainProbeOrigin()
        {
            return _driveTransform.position
                + (_driveTransform.forward * _boatNavigationData.TerrainProbeForwardDistance)
                + (Vector3.up * _boatNavigationData.TerrainProbeHeight);
        }

        private void CaptureControlledPlayerPose(Transform playerTransform)
        {
            Assert.IsNotNull(_boatTransform, $"{nameof(HelmControl)} requires a cached boat transform before taking control.");

            _controlledPlayerLocalPosition = _boatTransform.InverseTransformPoint(playerTransform.position);
            _controlledPlayerLocalRotation = Quaternion.Inverse(_boatTransform.rotation) * playerTransform.rotation;
        }

        private void SyncControlledPlayerPose()
        {
            if (_controllingPlayerInput == null || _boatTransform == null)
            {
                return;
            }

            Transform playerTransform = _controllingPlayerInput.transform;
            playerTransform.SetPositionAndRotation(
                _boatTransform.TransformPoint(_controlledPlayerLocalPosition),
                _boatTransform.rotation * _controlledPlayerLocalRotation);
        }

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private static bool CanPlayerTakeHelm(PlayerInput playerInput)
        {
            return playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls;
        }

        private static bool WasTakeHelmPressedThisFrame(PlayerInput playerInput)
        {
            InputActionMap thirdPersonMap = playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            InputAction actionAction = thirdPersonMap?.FindAction(Strings.ActionAction, throwIfNotFound: false);
            return actionAction != null && actionAction.WasPressedThisFrame();
        }

        private static string DescribePlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return "null";
            }

            return $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }

        private Collider ResolveInteractionTrigger()
        {
            if (_interactionTrigger != null && _interactionTrigger.isTrigger)
            {
                return _interactionTrigger;
            }

            Collider[] candidateColliders = GetComponentsInChildren<Collider>(includeInactive: true);
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
    }
}
