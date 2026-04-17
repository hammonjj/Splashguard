using BitBox.Library;
using BitBox.Library.Constants;
using Bitbox.Toymageddon.Nautical;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyVesselMotor : MonoBehaviourBase
    {
        private const string TerrainLayerName = "Terrain";

        [SerializeField, InlineEditor] private EnemyVesselData _enemyData;
        [SerializeField] private Transform _driveTransform;

        private EnemyBrain _brain;
        private Rigidbody _rigidbody;
        private Vector3 _destination;
        private bool _hasDestination;
        private int _terrainLayerMask;

        public bool HasDestination => _hasDestination;
        public Vector3 CurrentDestination => _destination;

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnFixedUpdated()
        {
            EnemyVesselData data = ResolveData();
            if (!_hasDestination || _rigidbody == null || data == null)
            {
                ApplySteeringDamping(data);
                return;
            }

            Vector3 toDestination = _destination - DrivePosition;
            toDestination.y = 0f;
            if (toDestination.magnitude <= data.EngagementWaypointAcceptanceRadius)
            {
                Stop();
                ApplySteeringDamping(data);
                return;
            }

            bool hasWater = WaterQuery.TrySample(_rigidbody.worldCenterOfMass, out _);
            float steeringInput = EnemyVesselSteeringUtility.ComputeSteeringInput(
                _driveTransform.forward,
                toDestination,
                data.FullSteerAngleDegrees);
            float throttleInput = EnemyVesselSteeringUtility.ComputeThrottleInput(
                _driveTransform.forward,
                toDestination,
                data.SlowTurnAngleDegrees,
                data.SlowTurnThrottle);

            ApplySteeringTorque(data, steeringInput);

            if (!hasWater)
            {
                return;
            }

            if (IsTerrainBlockingAhead(data))
            {
                ApplyTerrainBrake(data);
                return;
            }

            ApplyThrottleForce(data, throttleInput);
        }

        public void MoveTo(Vector3 destination)
        {
            _destination = destination;
            _hasDestination = true;
        }

        public void Stop()
        {
            _hasDestination = false;
        }

        public bool HasReached(Vector3 point, float radius)
        {
            Vector3 offset = point - DrivePosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        }

        private void CacheReferences()
        {
            _brain ??= GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
            _rigidbody ??= GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
            _driveTransform ??= _rigidbody != null ? _rigidbody.transform : transform;
            _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
        }

        private Vector3 DrivePosition => _driveTransform != null ? _driveTransform.position : transform.position;

        private EnemyVesselData ResolveData()
        {
            CacheReferences();
            return _enemyData != null ? _enemyData : _brain != null ? _brain.EnemyData : null;
        }

        private void ApplyThrottleForce(EnemyVesselData data, float throttleInput)
        {
            Vector3 forward = _driveTransform.forward;
            float currentForwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, forward);
            float targetSpeed = Mathf.Clamp01(throttleInput) * data.MaxForwardSpeed;
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

        private bool IsTerrainBlockingAhead(EnemyVesselData data)
        {
            if (_terrainLayerMask == 0)
            {
                _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
            }

            Vector3 probePoint = _driveTransform.position + (_driveTransform.forward * data.TerrainProbeForwardDistance);
            Vector3 probeOrigin = probePoint + (Vector3.up * data.TerrainProbeHeight);

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

            if (!WaterQuery.TrySample(probePoint, out WaterSample waterSample))
            {
                return true;
            }

            return hit.point.y >= waterSample.Height - data.TerrainClearance;
        }
    }
}
