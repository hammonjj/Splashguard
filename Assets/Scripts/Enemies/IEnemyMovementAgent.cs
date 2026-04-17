using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public enum EnemyMovementState
    {
        Idle,
        Moving,
        Arrived,
        NoDestination,
        NoWaterSample,
        TerrainBlocked,
        MissingReferences
    }

    public readonly struct EnemyMovementStatus
    {
        public EnemyMovementStatus(
            EnemyMovementState state,
            Vector3 destination,
            Vector3 desiredDirection,
            float acceptanceRadius,
            float distanceToDestination,
            float steeringInput,
            float throttleInput,
            bool hasWaterSample,
            bool terrainBlocked,
            string stopReason)
        {
            State = state;
            Destination = destination;
            DesiredDirection = desiredDirection;
            AcceptanceRadius = acceptanceRadius;
            DistanceToDestination = distanceToDestination;
            SteeringInput = steeringInput;
            ThrottleInput = throttleInput;
            HasWaterSample = hasWaterSample;
            TerrainBlocked = terrainBlocked;
            StopReason = stopReason;
        }

        public EnemyMovementState State { get; }
        public Vector3 Destination { get; }
        public Vector3 DesiredDirection { get; }
        public float AcceptanceRadius { get; }
        public float DistanceToDestination { get; }
        public float SteeringInput { get; }
        public float ThrottleInput { get; }
        public bool HasWaterSample { get; }
        public bool TerrainBlocked { get; }
        public string StopReason { get; }
    }

    public interface IEnemyMovementAgent
    {
        bool HasDestination { get; }
        Vector3 CurrentDestination { get; }
        EnemyMovementStatus CurrentStatus { get; }
        float CurrentSpeed { get; }

        void MoveTo(Vector3 destination, float acceptanceRadius);
        void Stop(string reason);
        bool HasReached(Vector3 point, float radius);
        bool IsDestinationValid(Vector3 destination);
        bool TryProjectDestination(Vector3 desiredDestination, float searchRadius, out Vector3 projectedDestination);
    }
}
