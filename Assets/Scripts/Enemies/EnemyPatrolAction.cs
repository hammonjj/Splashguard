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
        private float _lingerUntilTime;
        private Vector3 _lastLoggedWaypoint;
        private bool _hasLoggedWaypoint;

        public override string DebugStatus => _hasWaypoint ? "Patrolling" : "Choosing patrol waypoint";

        protected override void OnContextBound()
        {
            _spawnPosition = transform.position;
            _hasWaypoint = false;
            _waypointSequence = 0;
            _nextRepathTime = 0f;
            _lingerUntilTime = 0f;
            _hasLoggedWaypoint = false;
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
            LogInfo(
                $"Enemy patrol entered. spawn={FormatVector(_spawnPosition)}, hasMovementAgent={Context?.MovementAgent != null}, data={Context?.EnemyData?.name ?? "None"}.");
            EnsureWaypoint(force: true);
        }

        public override void Exit()
        {
            Context?.MovementAgent?.Stop("patrol_exit");
        }

        public override void Tick(float deltaTime)
        {
            if (Context?.MovementAgent == null || Context.EnemyData == null)
            {
                return;
            }

            bool reachedWaypoint = _hasWaypoint
                && Context.MovementAgent.HasReached(_currentWaypoint, Context.EnemyData.PatrolWaypointAcceptanceRadius);
            if (reachedWaypoint)
            {
                if (Context.EnemyData.PatrolLingerSeconds > 0f && _lingerUntilTime <= 0f)
                {
                    _lingerUntilTime = Time.time + Context.EnemyData.PatrolLingerSeconds;
                    Context.MovementAgent.Stop("patrol_linger");
                    return;
                }

                if (Time.time < _lingerUntilTime)
                {
                    return;
                }
            }

            if (!_hasWaypoint || reachedWaypoint || Time.time >= _nextRepathTime)
            {
                EnsureWaypoint(force: true);
            }

            if (_hasWaypoint)
            {
                LogWaypointOrderIfChanged();
                Context.MovementAgent.MoveTo(_currentWaypoint, Context.EnemyData.PatrolWaypointAcceptanceRadius);
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
            _lingerUntilTime = 0f;
            _hasLoggedWaypoint = false;
            float repathDelay = Random.Range(data.PatrolRepathSecondsMin, data.PatrolRepathSecondsMax);
            _nextRepathTime = Time.time + repathDelay;
            if (!found)
            {
                LogWarning(
                    $"Enemy patrol failed to find a valid water waypoint. spawn={FormatVector(_spawnPosition)}, current={FormatVector(transform.position)}, radius={data.PatrolRadius:0.##}, attempts={data.PatrolCandidateAttempts}.");
                Context?.MovementAgent?.Stop("no_valid_patrol_waypoint");
            }
            else
            {
                LogInfo(
                    $"Enemy patrol selected waypoint. waypoint={FormatVector(_currentWaypoint)}, current={FormatVector(transform.position)}, nextRepathIn={repathDelay:0.##}s.");
            }
        }

        private bool IsPatrolCandidateValid(Vector3 candidate)
        {
            return Context?.MovementAgent == null || Context.MovementAgent.IsDestinationValid(candidate);
        }

        private void LogWaypointOrderIfChanged()
        {
            if (_hasLoggedWaypoint && Vector3.Distance(_lastLoggedWaypoint, _currentWaypoint) <= 0.1f)
            {
                return;
            }

            _hasLoggedWaypoint = true;
            _lastLoggedWaypoint = _currentWaypoint;
            LogInfo(
                $"Enemy patrol ordering movement. waypoint={FormatVector(_currentWaypoint)}, acceptance={Context.EnemyData.PatrolWaypointAcceptanceRadius:0.##}, movementState={Context.MovementAgent.CurrentStatus.State}.");
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }
    }
}
