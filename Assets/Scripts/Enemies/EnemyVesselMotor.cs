using BitBox.Library;
using BitBox.Library.Constants;
using Bitbox.Toymageddon.Nautical;
using Sirenix.OdinInspector;
using UnityEngine;
using BitBox.Library.Utilities;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyVesselMotor : MonoBehaviourBase, IEnemyMovementAgent
    {
        private const string TerrainLayerName = "Terrain";
        private const string DefaultStopReason = "none";

        [SerializeField, InlineEditor] private EnemyVesselData _enemyData;
        [SerializeField] private Transform _driveTransform;

        private EnemyBrain _brain;
        private Rigidbody _rigidbody;
        private Vector3 _destination;
        private float _acceptanceRadius = 1f;
        private bool _hasDestination;
        private int _terrainLayerMask;
        private string _lastStopReason = DefaultStopReason;
        private EnemyMovementStatus _currentStatus;
        private float _nextDiagnosticTime;
        private float _nextMoveOrderLogTime;
        private float _nextReferenceDiagnosticTime;
        private Vector3 _lastLoggedMoveDestination;
        private bool _hasLoggedMoveDestination;

        private Vector3 _lastCenterProbePoint;
        private Vector3 _lastLeftProbePoint;
        private Vector3 _lastRightProbePoint;
        private bool _lastCenterProbeBlocked;
        private bool _lastLeftProbeBlocked;
        private bool _lastRightProbeBlocked;

        public bool HasDestination => _hasDestination;
        public Vector3 CurrentDestination => _destination;
        public EnemyMovementStatus CurrentStatus => _currentStatus;
        public float CurrentSpeed
        {
            get
            {
                if (_rigidbody == null)
                {
                    return 0f;
                }

                Vector3 velocity = _rigidbody.linearVelocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            SetStatus(EnemyMovementState.Idle, Vector3.zero, Vector3.zero, 0f, 0f, 0f, false, false);
            LogInfo(
                $"Enemy vessel motor enabled. object={name}, rigidbody={DescribeRigidbody()}, driveTransform={_driveTransform?.name ?? "None"}, data={ResolveData()?.name ?? "None"}, terrainLayerMask={_terrainLayerMask}.");
        }

        protected override void OnFixedUpdated()
        {
            EnemyVesselData data = ResolveData();
            if (_rigidbody == null || _driveTransform == null || data == null)
            {
                SetStatus(EnemyMovementState.MissingReferences, Vector3.zero, Vector3.zero, 0f, 0f, 0f, false, false);
                LogMissingReferenceDiagnostic(data);
                return;
            }

            if (!_hasDestination)
            {
                ApplySteeringDamping(data);
                SetStatus(EnemyMovementState.NoDestination, Vector3.zero, Vector3.zero, 0f, 0f, 0f, false, false);
                LogMovementDiagnostic(data, "Enemy vessel has no movement destination.", warning: false);
                return;
            }

            Vector3 resolvedDestination = ResolveNavigableDestination(data, _destination);
            Vector3 toDestination = resolvedDestination - DrivePosition;
            toDestination.y = 0f;
            float distance = toDestination.magnitude;
            if (distance <= _acceptanceRadius)
            {
                ApplySteeringDamping(data);
                SetStatus(EnemyMovementState.Arrived, resolvedDestination, Vector3.zero, 0f, 0f, distance, true, false);
                LogMovementDiagnostic(data, "Enemy vessel reached destination tolerance.", warning: false);
                return;
            }

            bool hasWater = WaterQuery.TrySample(_rigidbody.worldCenterOfMass, out _);
            Vector3 desiredDirection = toDestination.sqrMagnitude > 0.0001f ? toDestination.normalized : Vector3.zero;
            if (!hasWater)
            {
                ApplySteeringDamping(data);
                SetStatus(EnemyMovementState.NoWaterSample, resolvedDestination, desiredDirection, 0f, 0f, distance, false, false);
                LogMovementDiagnostic(data, "Enemy vessel cannot move because WaterQuery has no active sample at the vessel center.", warning: true);
                return;
            }

            float steeringInput = EnemyVesselSteeringUtility.ComputeSteeringInput(
                _driveTransform.forward,
                toDestination,
                data.FullSteerAngleDegrees);
            float throttleInput = EnemyVesselSteeringUtility.ComputeArriveThrottle(
                _driveTransform.forward,
                toDestination,
                data.SlowTurnAngleDegrees,
                data.SlowTurnThrottle,
                data.MinimumCruiseThrottle,
                data.ArrivalSlowdownDistance,
                _acceptanceRadius);

            EnemyTerrainAvoidanceDecision avoidanceDecision = ResolveTerrainAvoidance(data);
            if (avoidanceDecision.AnyBlocked)
            {
                steeringInput = Mathf.Clamp(steeringInput + avoidanceDecision.SteeringBias, -1f, 1f);
            }

            ApplySteeringTorque(data, steeringInput);

            if (avoidanceDecision.ShouldReverse)
            {
                throttleInput = -data.EmergencyReverseThrottle;
            }
            else if (avoidanceDecision.CenterBlocked)
            {
                ApplyTerrainBrake(data);
                throttleInput = Mathf.Min(throttleInput, data.MinimumCruiseThrottle);
            }

            ApplyThrottleForce(data, throttleInput);

            EnemyMovementState state = avoidanceDecision.AnyBlocked
                ? EnemyMovementState.TerrainBlocked
                : EnemyMovementState.Moving;
            SetStatus(
                state,
                resolvedDestination,
                desiredDirection,
                steeringInput,
                throttleInput,
                distance,
                true,
                avoidanceDecision.AnyBlocked);

            if (avoidanceDecision.CenterBlocked)
            {
                LogMovementDiagnostic(data, "Enemy vessel terrain probe is blocked; applying avoidance steering.", warning: true);
            }
            else
            {
                LogMovementDiagnostic(data, "Enemy vessel movement tick.", warning: false);
            }
        }

        protected override void OnDrawnGizmos()
        {
            if (_driveTransform == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_driveTransform.position, _driveTransform.position + (_driveTransform.forward * 5f));

            if (_hasDestination)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_destination, Mathf.Max(0.25f, _acceptanceRadius));
                Gizmos.DrawLine(_driveTransform.position, _destination);
            }

            DrawProbeGizmo(_lastCenterProbePoint, _lastCenterProbeBlocked);
            DrawProbeGizmo(_lastLeftProbePoint, _lastLeftProbeBlocked);
            DrawProbeGizmo(_lastRightProbePoint, _lastRightProbeBlocked);
        }

        public void MoveTo(Vector3 destination)
        {
            EnemyVesselData data = ResolveData();
            MoveTo(destination, data != null ? data.EngagementWaypointAcceptanceRadius : 1f);
        }

        public void MoveTo(Vector3 destination, float acceptanceRadius)
        {
            _destination = destination;
            _acceptanceRadius = Mathf.Max(0.1f, acceptanceRadius);
            _hasDestination = true;
            _lastStopReason = DefaultStopReason;
            LogMoveOrderIfUseful();
        }

        public void Stop()
        {
            Stop("unspecified");
        }

        public void Stop(string reason)
        {
            _hasDestination = false;
            _lastStopReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
            LogInfo(
                $"Enemy vessel motor stopped. reason={_lastStopReason}, speed={CurrentSpeed:0.##}, position={FormatVector(DrivePosition)}, previousDestination={FormatVector(_destination)}.");
        }

        public bool HasReached(Vector3 point, float radius)
        {
            Vector3 offset = point - DrivePosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        }

        public bool IsDestinationValid(Vector3 destination)
        {
            EnemyVesselData data = ResolveData();
            return data != null && IsNavigablePoint(destination, data);
        }

        public bool TryProjectDestination(Vector3 desiredDestination, float searchRadius, out Vector3 projectedDestination)
        {
            EnemyVesselData data = ResolveData();
            if (data == null)
            {
                projectedDestination = desiredDestination;
                return false;
            }

            return NavalWaterPointValidator.TryProjectToValidPoint(
                desiredDestination,
                searchRadius,
                data.DestinationProjectionRings,
                data.DestinationProjectionSamplesPerRing,
                candidate => IsNavigablePoint(candidate, data),
                out projectedDestination);
        }

        private void CacheReferences()
        {
            _brain ??= GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
            _rigidbody = gameObject.transform.parent.GetComponent<Rigidbody>();
            _driveTransform ??= _rigidbody != null ? _rigidbody.transform : transform;
            _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
        }

        private Vector3 DrivePosition => _driveTransform != null ? _driveTransform.position : transform.position;

        private EnemyVesselData ResolveData()
        {
            CacheReferences();
            return _enemyData != null ? _enemyData : _brain != null ? _brain.EnemyData : null;
        }

        private Vector3 ResolveNavigableDestination(EnemyVesselData data, Vector3 desiredDestination)
        {
            if (IsNavigablePoint(desiredDestination, data))
            {
                return desiredDestination;
            }

            return NavalWaterPointValidator.TryProjectToValidPoint(
                desiredDestination,
                data.DestinationProjectionSearchRadius,
                data.DestinationProjectionRings,
                data.DestinationProjectionSamplesPerRing,
                candidate => IsNavigablePoint(candidate, data),
                out Vector3 projectedDestination)
                ? projectedDestination
                : desiredDestination;
        }

        private bool IsNavigablePoint(Vector3 point, EnemyVesselData data)
        {
            if (!WaterQuery.TrySample(point, out WaterSample waterSample))
            {
                return false;
            }

            return !IsTerrainAboveWater(point, waterSample.Height, data);
        }

        private bool IsTerrainAboveWater(Vector3 point, float waterHeight, EnemyVesselData data)
        {
            if (_terrainLayerMask == 0)
            {
                _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
            }

            if (_terrainLayerMask == 0)
            {
                return false;
            }

            Vector3 probeOrigin = point + (Vector3.up * data.TerrainProbeHeight);
            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    data.TerrainProbeHeight + data.TerrainProbeDepth,
                    _terrainLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.point.y >= waterHeight - data.TerrainClearance;
        }

        private EnemyTerrainAvoidanceDecision ResolveTerrainAvoidance(EnemyVesselData data)
        {
            Vector3 forward = FlattenDirection(_driveTransform.forward);
            Vector3 left = Quaternion.AngleAxis(-data.TerrainProbeFanAngleDegrees, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(data.TerrainProbeFanAngleDegrees, Vector3.up) * forward;

            _lastCenterProbeBlocked = IsTerrainBlockingAlong(forward, data, out _lastCenterProbePoint);
            _lastLeftProbeBlocked = IsTerrainBlockingAlong(left, data, out _lastLeftProbePoint);
            _lastRightProbeBlocked = IsTerrainBlockingAlong(right, data, out _lastRightProbePoint);

            return EnemyTerrainAvoidanceUtility.ResolveFanDecision(
                _lastCenterProbeBlocked,
                _lastLeftProbeBlocked,
                _lastRightProbeBlocked,
                data.AvoidanceSteeringWeight);
        }

        private bool IsTerrainBlockingAlong(Vector3 direction, EnemyVesselData data, out Vector3 probePoint)
        {
            probePoint = DrivePosition + (FlattenDirection(direction) * data.TerrainProbeForwardDistance);
            if (!WaterQuery.TrySample(probePoint, out WaterSample waterSample))
            {
                return true;
            }

            return IsTerrainAboveWater(probePoint, waterSample.Height, data);
        }

        private void ApplyThrottleForce(EnemyVesselData data, float throttleInput)
        {
            Vector3 forward = _driveTransform.forward;
            float currentForwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, forward);
            float targetSpeed = Mathf.Clamp(throttleInput, -1f, 1f) * data.MaxForwardSpeed;
            float speedError = targetSpeed - currentForwardSpeed;
            float desiredAcceleration = Mathf.Clamp(
                speedError * data.SpeedResponse,
                -data.DriveDeceleration,
                data.DriveAcceleration);

            _rigidbody.AddForce(forward * desiredAcceleration, ForceMode.Acceleration);
        }

        private void ApplySteeringTorque(EnemyVesselData data, float steeringInput)
        {
            if (data == null)
            {
                return;
            }

            float currentForwardSpeed = Mathf.Abs(Vector3.Dot(_rigidbody.linearVelocity, _driveTransform.forward));
            float steeringAuthority = Mathf.Clamp01(Mathf.Max(currentForwardSpeed, data.MaxForwardSpeed * 0.35f) / data.SteeringAuthoritySpeed);
            float steeringTorque = Mathf.Clamp(steeringInput, -1f, 1f) * data.SteeringTorque * steeringAuthority;
            float dampingTorque = -_rigidbody.angularVelocity.y * data.SteeringDamping;

            _rigidbody.AddTorque(Vector3.up * (steeringTorque + dampingTorque), ForceMode.Acceleration);
        }

        private void ApplySteeringDamping(EnemyVesselData data)
        {
            if (_rigidbody == null || data == null)
            {
                return;
            }

            float dampingTorque = -_rigidbody.angularVelocity.y * data.SteeringDamping;
            _rigidbody.AddTorque(Vector3.up * dampingTorque, ForceMode.Acceleration);
        }

        private void ApplyTerrainBrake(EnemyVesselData data)
        {
            Vector3 forward = _driveTransform.forward;
            float currentForwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, forward);
            if (currentForwardSpeed <= 0f)
            {
                return;
            }

            float brakingAcceleration = Mathf.Min(
                currentForwardSpeed * data.TerrainBrakeAcceleration,
                data.TerrainBrakeAcceleration);
            _rigidbody.AddForce(-forward * brakingAcceleration, ForceMode.Acceleration);
        }

        private void SetStatus(
            EnemyMovementState state,
            Vector3 destination,
            Vector3 desiredDirection,
            float steeringInput,
            float throttleInput,
            float distanceToDestination,
            bool hasWaterSample,
            bool terrainBlocked)
        {
            _currentStatus = new EnemyMovementStatus(
                state,
                destination,
                desiredDirection,
                _acceptanceRadius,
                distanceToDestination,
                steeringInput,
                throttleInput,
                hasWaterSample,
                terrainBlocked,
                _lastStopReason);
        }

        private void LogMoveOrderIfUseful()
        {
            EnemyVesselData data = ResolveData();
            float interval = data != null ? data.MovementDiagnosticInterval : 1.5f;
            float now = Time.time;
            bool destinationChanged = !_hasLoggedMoveDestination
                || Vector3.Distance(_lastLoggedMoveDestination, _destination) > 1f;
            if (!destinationChanged && now < _nextMoveOrderLogTime)
            {
                return;
            }

            _hasLoggedMoveDestination = true;
            _lastLoggedMoveDestination = _destination;
            _nextMoveOrderLogTime = now + interval;
            LogInfo(
                $"Enemy vessel motor received destination. destination={FormatVector(_destination)}, acceptance={_acceptanceRadius:0.##}, current={FormatVector(DrivePosition)}, distance={PlanarDistance(DrivePosition, _destination):0.##}, status={_currentStatus.State}.");
        }

        private void LogMovementDiagnostic(EnemyVesselData data, string message, bool warning)
        {
            float now = Time.time;
            if (now < _nextDiagnosticTime)
            {
                return;
            }

            _nextDiagnosticTime = now + data.MovementDiagnosticInterval;
            string diagnostic =
                $"{message} state={_currentStatus.State}, hasDestination={_hasDestination}, destination={FormatVector(_currentStatus.Destination)}, rawDestination={FormatVector(_destination)}, distance={_currentStatus.DistanceToDestination:0.##}, acceptance={_currentStatus.AcceptanceRadius:0.##}, throttle={_currentStatus.ThrottleInput:0.##}, steering={_currentStatus.SteeringInput:0.##}, speed={CurrentSpeed:0.##}, water={_currentStatus.HasWaterSample}, terrainBlocked={_currentStatus.TerrainBlocked}, probes(center={_lastCenterProbeBlocked}, left={_lastLeftProbeBlocked}, right={_lastRightProbeBlocked}), rb={DescribeRigidbody()}, stopReason={_lastStopReason}.";
            if (warning)
            {
                LogWarning(diagnostic);
            }
            else
            {
                LogInfo(diagnostic);
            }
        }

        private void LogMissingReferenceDiagnostic(EnemyVesselData data)
        {
            float now = Time.time;
            if (now < _nextReferenceDiagnosticTime)
            {
                return;
            }

            _nextReferenceDiagnosticTime = now + 1.5f;
            LogWarning(
                $"Enemy vessel motor missing references. rigidbody={DescribeRigidbody()}, driveTransform={_driveTransform?.name ?? "None"}, data={data?.name ?? "None"}, brain={_brain?.name ?? "None"}, object={name}, root={transform.root?.name ?? "None"}.");
        }

        private static Vector3 FlattenDirection(Vector3 value)
        {
            value.y = 0f;
            if (value.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return value.normalized;
        }

        private static void DrawProbeGizmo(Vector3 probePoint, bool blocked)
        {
            if (probePoint == Vector3.zero)
            {
                return;
            }

            Gizmos.color = blocked ? Color.red : Color.green;
            Gizmos.DrawWireSphere(probePoint, 0.35f);
        }

        private string DescribeRigidbody()
        {
            if (_rigidbody == null)
            {
                return "None";
            }

            return $"{_rigidbody.name}[kinematic={_rigidbody.isKinematic}, gravity={_rigidbody.useGravity}, constraints={_rigidbody.constraints}, mass={_rigidbody.mass:0.##}, velocity={FormatVector(_rigidbody.linearVelocity)}, angular={FormatVector(_rigidbody.angularVelocity)}]";
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }
    }
}
