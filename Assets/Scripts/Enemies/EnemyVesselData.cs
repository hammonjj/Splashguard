using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [CreateAssetMenu(fileName = "EnemyVesselData", menuName = "Enemies/Naval Enemy Vessel Data")]
    public sealed class EnemyVesselData : ScriptableObject
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float _maxHealth = 150f;

        [Header("Targeting")]
        [SerializeField, Min(0f)] private float _detectionRange = 85f;
        [SerializeField, Min(0f)] private float _alertRadius = 120f;

        [Header("Patrol")]
        [SerializeField, Min(0f)] private float _patrolRadius = 40f;
        [SerializeField, Min(0f)] private float _patrolMinimumWaypointDistance = 12f;
        [SerializeField, Min(0.1f)] private float _patrolWaypointAcceptanceRadius = 6f;
        [SerializeField, Min(0f)] private float _patrolRepathSecondsMin = 8f;
        [SerializeField, Min(0f)] private float _patrolRepathSecondsMax = 14f;
        [SerializeField, Min(1)] private int _patrolCandidateAttempts = 12;
        [SerializeField] private int _patrolSeed = 1337;

        [Header("Engagement")]
        [SerializeField, Min(0f)] private float _attackRange = 70f;
        [SerializeField, Min(0f)] private float _idealStandoffDistance = 35f;
        [SerializeField, Min(0f)] private float _retreatDistance = 22f;
        [SerializeField, Min(0f)] private float _engagementWaypointAcceptanceRadius = 4.5f;
        [SerializeField, Range(0f, 180f)] private float _orbitStepDegrees = 65f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float _maxForwardSpeed = 10.5f;
        [SerializeField, Min(0.1f)] private float _speedResponse = 1.8f;
        [SerializeField, Min(0.1f)] private float _driveAcceleration = 7f;
        [SerializeField, Min(0.1f)] private float _driveDeceleration = 8f;
        [SerializeField, Min(0.1f)] private float _steeringTorque = 4f;
        [SerializeField, Min(0.1f)] private float _steeringDamping = 2.25f;
        [SerializeField, Min(0.1f)] private float _steeringAuthoritySpeed = 4f;
        [SerializeField, Range(1f, 180f)] private float _fullSteerAngleDegrees = 70f;
        [SerializeField, Range(0f, 180f)] private float _slowTurnAngleDegrees = 45f;
        [SerializeField, Range(0f, 1f)] private float _slowTurnThrottle = 0.65f;

        [Header("Terrain Avoidance")]
        [SerializeField, Min(0.1f)] private float _terrainProbeForwardDistance = 4f;
        [SerializeField, Min(0.1f)] private float _terrainProbeHeight = 3f;
        [SerializeField, Min(0.1f)] private float _terrainProbeDepth = 6f;
        [SerializeField, Min(0f)] private float _terrainClearance = 0.15f;
        [SerializeField, Min(0.1f)] private float _terrainBrakeAcceleration = 10f;

        [Header("Weapons")]
        [SerializeField, Min(1)] private int _burstShots = 5;
        [SerializeField, Min(0f)] private float _burstCooldownSeconds = 1.25f;
        [SerializeField, Range(1f, 180f)] private float _weaponArcHalfAngleDegrees = 90f;
        [SerializeField, Min(0f)] private float _weaponAimDegreesPerSecond = 180f;

        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float DetectionRange => Mathf.Max(0f, _detectionRange);
        public float AlertRadius => Mathf.Max(0f, _alertRadius);
        public float PatrolRadius => Mathf.Max(0f, _patrolRadius);
        public float PatrolMinimumWaypointDistance => Mathf.Max(0f, _patrolMinimumWaypointDistance);
        public float PatrolWaypointAcceptanceRadius => Mathf.Max(0.1f, _patrolWaypointAcceptanceRadius);
        public float PatrolRepathSecondsMin => Mathf.Max(0f, Mathf.Min(_patrolRepathSecondsMin, _patrolRepathSecondsMax));
        public float PatrolRepathSecondsMax => Mathf.Max(PatrolRepathSecondsMin, _patrolRepathSecondsMax);
        public int PatrolCandidateAttempts => Mathf.Max(1, _patrolCandidateAttempts);
        public int PatrolSeed => _patrolSeed;
        public float AttackRange => Mathf.Max(0f, _attackRange);
        public float IdealStandoffDistance => Mathf.Max(0f, _idealStandoffDistance);
        public float RetreatDistance => Mathf.Max(0f, _retreatDistance);
        public float EngagementWaypointAcceptanceRadius => Mathf.Max(0.1f, _engagementWaypointAcceptanceRadius);
        public float OrbitStepDegrees => Mathf.Clamp(_orbitStepDegrees, 0f, 180f);
        public float MaxForwardSpeed => Mathf.Max(0.1f, _maxForwardSpeed);
        public float SpeedResponse => Mathf.Max(0.1f, _speedResponse);
        public float DriveAcceleration => Mathf.Max(0.1f, _driveAcceleration);
        public float DriveDeceleration => Mathf.Max(0.1f, _driveDeceleration);
        public float SteeringTorque => Mathf.Max(0.1f, _steeringTorque);
        public float SteeringDamping => Mathf.Max(0.1f, _steeringDamping);
        public float SteeringAuthoritySpeed => Mathf.Max(0.1f, _steeringAuthoritySpeed);
        public float FullSteerAngleDegrees => Mathf.Clamp(_fullSteerAngleDegrees, 1f, 180f);
        public float SlowTurnAngleDegrees => Mathf.Clamp(_slowTurnAngleDegrees, 0f, 180f);
        public float SlowTurnThrottle => Mathf.Clamp01(_slowTurnThrottle);
        public float TerrainProbeForwardDistance => Mathf.Max(0.1f, _terrainProbeForwardDistance);
        public float TerrainProbeHeight => Mathf.Max(0.1f, _terrainProbeHeight);
        public float TerrainProbeDepth => Mathf.Max(0.1f, _terrainProbeDepth);
        public float TerrainClearance => Mathf.Max(0f, _terrainClearance);
        public float TerrainBrakeAcceleration => Mathf.Max(0.1f, _terrainBrakeAcceleration);
        public int BurstShots => Mathf.Max(1, _burstShots);
        public float BurstCooldownSeconds => Mathf.Max(0f, _burstCooldownSeconds);
        public float WeaponArcHalfAngleDegrees => Mathf.Clamp(_weaponArcHalfAngleDegrees, 1f, 180f);
        public float WeaponAimDegreesPerSecond => Mathf.Max(0f, _weaponAimDegreesPerSecond);

        private void OnValidate()
        {
            if (_patrolRepathSecondsMin > _patrolRepathSecondsMax)
            {
                (_patrolRepathSecondsMin, _patrolRepathSecondsMax) = (_patrolRepathSecondsMax, _patrolRepathSecondsMin);
            }

            if (_retreatDistance > _idealStandoffDistance)
            {
                _retreatDistance = _idealStandoffDistance;
            }

            if (_idealStandoffDistance > _attackRange)
            {
                _idealStandoffDistance = _attackRange;
            }
        }
    }
}
