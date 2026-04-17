using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyPatrolAction : EnemyActionBase
    {
        private const float PatrolScore = 0.25f;

        private Vector3 _spawnPosition;
        private Vector3 _currentWaypoint;
        private bool _hasWaypoint;
        private int _waypointSequence;
        private float _nextRepathTime;

        public override string DebugStatus => _hasWaypoint ? "Patrolling" : "Choosing patrol waypoint";

        protected override void OnContextBound()
        {
            _spawnPosition = transform.position;
            _hasWaypoint = false;
            _waypointSequence = 0;
            _nextRepathTime = 0f;
        }

        public override float Score()
        {
            return Context != null
                && Context.TargetTracker != null
                && !Context.TargetTracker.HasTarget
                ? PatrolScore
                : 0f;
        }

        public override void Enter()
        {
            EnsureWaypoint(force: true);
        }

        public override void Exit()
        {
            Context?.Motor?.Stop();
        }

        public override void Tick(float deltaTime)
        {
            if (Context?.Motor == null || Context.EnemyData == null)
            {
                return;
            }

            if (!_hasWaypoint
                || Context.Motor.HasReached(_currentWaypoint, Context.EnemyData.PatrolWaypointAcceptanceRadius)
                || Time.time >= _nextRepathTime)
            {
                EnsureWaypoint(force: true);
            }

            if (_hasWaypoint)
            {
                Context.Motor.MoveTo(_currentWaypoint);
            }
        }

        private void EnsureWaypoint(bool force)
        {
            if (!force && _hasWaypoint)
            {
                return;
            }

            EnemyVesselData data = Context?.EnemyData;
            if (data == null)
            {
                return;
            }

            bool found = EnemyPatrolPlanner.TryChooseWaypoint(
                _spawnPosition,
                transform.position,
                data.PatrolRadius,
                data.PatrolMinimumWaypointDistance,
                data.PatrolSeed,
                _waypointSequence++,
                data.PatrolCandidateAttempts,
                IsPatrolCandidateValid,
                out _currentWaypoint);

            _hasWaypoint = found;
            float repathDelay = Random.Range(data.PatrolRepathSecondsMin, data.PatrolRepathSecondsMax);
            _nextRepathTime = Time.time + repathDelay;
        }

        private static bool IsPatrolCandidateValid(Vector3 candidate)
        {
            return Bitbox.Toymageddon.Nautical.WaterQuery.TrySample(candidate, out _);
        }
    }
}
