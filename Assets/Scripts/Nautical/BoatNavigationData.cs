using UnityEngine;

namespace Bitbox
{
    [CreateAssetMenu(fileName = "BoatNavigationData", menuName = "Nautical/Boat Navigation Data")]
    public sealed class BoatNavigationData : ScriptableObject
    {
        [Header("Throttle")]
        [SerializeField, Range(0.1f, 2f)] private float _throttleChangeRate = 0.45f;
        [SerializeField, Min(0.1f)] private float _maxForwardSpeed = 10f;
        [SerializeField, Min(0.1f)] private float _maxReverseSpeed = 4f;
        [SerializeField, Min(0.1f)] private float _speedResponse = 1.6f;
        [SerializeField, Min(0.1f)] private float _driveAcceleration = 6f;
        [SerializeField, Min(0.1f)] private float _driveDeceleration = 8f;

        [Header("Steering")]
        [SerializeField, Min(0.1f)] private float _steeringTorque = 4f;
        [SerializeField, Min(0.1f)] private float _steeringDamping = 2.5f;
        [SerializeField, Min(0.1f)] private float _steeringAuthoritySpeed = 4f;

        [Header("Terrain Avoidance")]
        [SerializeField, Min(0.1f)] private float _terrainProbeForwardDistance = 2.5f;
        [SerializeField, Min(0.1f)] private float _terrainProbeHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float _terrainProbeDepth = 5f;
        [SerializeField, Min(0f)] private float _terrainClearance = 0.1f;
        [SerializeField, Min(0.1f)] private float _terrainBrakeAcceleration = 10f;

        public float ThrottleChangeRate => _throttleChangeRate;
        public float MaxForwardSpeed => _maxForwardSpeed;
        public float MaxReverseSpeed => _maxReverseSpeed;
        public float SpeedResponse => _speedResponse;
        public float DriveAcceleration => _driveAcceleration;
        public float DriveDeceleration => _driveDeceleration;
        public float SteeringTorque => _steeringTorque;
        public float SteeringDamping => _steeringDamping;
        public float SteeringAuthoritySpeed => _steeringAuthoritySpeed;
        public float TerrainProbeForwardDistance => _terrainProbeForwardDistance;
        public float TerrainProbeHeight => _terrainProbeHeight;
        public float TerrainProbeDepth => _terrainProbeDepth;
        public float TerrainClearance => _terrainClearance;
        public float TerrainBrakeAcceleration => _terrainBrakeAcceleration;
    }
}
