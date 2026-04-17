using System;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public enum EnemyEngagementMode
    {
        Hold,
        Chase,
        Orbit,
        Retreat
    }

    public readonly struct EnemyEngagementDecision
    {
        public EnemyEngagementDecision(EnemyEngagementMode mode, Vector3 destination)
        {
            Mode = mode;
            Destination = destination;
        }

        public EnemyEngagementMode Mode { get; }
        public Vector3 Destination { get; }
    }

    public static class EnemyPatrolPlanner
    {
        public static bool TryChooseWaypoint(
            Vector3 anchor,
            Vector3 currentPosition,
            float patrolRadius,
            float minimumDistance,
            int seed,
            int sequence,
            int candidateAttempts,
            Func<Vector3, bool> isCandidateValid,
            out Vector3 waypoint)
        {
            waypoint = anchor;
            patrolRadius = Mathf.Max(0f, patrolRadius);
            minimumDistance = Mathf.Max(0f, minimumDistance);
            candidateAttempts = Mathf.Max(1, candidateAttempts);

            if (patrolRadius <= 0.01f)
            {
                return false;
            }

            var random = new System.Random(seed ^ (sequence * 73856093));
            for (int attempt = 0; attempt < candidateAttempts; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                float radius = patrolRadius * Mathf.Sqrt((float)random.NextDouble());
                Vector3 candidate = anchor + new Vector3(
                    Mathf.Cos((float)angle) * radius,
                    0f,
                    Mathf.Sin((float)angle) * radius);

                if (Vector3.Distance(currentPosition, candidate) < minimumDistance)
                {
                    continue;
                }

                if (isCandidateValid != null && !isCandidateValid(candidate))
                {
                    continue;
                }

                waypoint = candidate;
                return true;
            }

            return false;
        }
    }

    public static class EnemyEngagementPlanner
    {
        public static EnemyEngagementDecision ResolveDestination(
            Vector3 selfPosition,
            Vector3 selfForward,
            Vector3 targetPosition,
            float attackRange,
            float idealStandoffDistance,
            float retreatDistance,
            float orbitStepDegrees,
            int orbitDirection)
        {
            Vector3 toTarget = Flatten(targetPosition - selfPosition);
            float distance = toTarget.magnitude;
            if (distance <= 0.01f)
            {
                return new EnemyEngagementDecision(EnemyEngagementMode.Hold, selfPosition);
            }

            Vector3 targetDirection = toTarget / distance;
            attackRange = Mathf.Max(0f, attackRange);
            idealStandoffDistance = Mathf.Max(0f, idealStandoffDistance);
            retreatDistance = Mathf.Max(0f, Mathf.Min(retreatDistance, idealStandoffDistance));

            if (distance > attackRange)
            {
                Vector3 chaseDestination = targetPosition - (targetDirection * idealStandoffDistance);
                return new EnemyEngagementDecision(EnemyEngagementMode.Chase, KeepY(chaseDestination, selfPosition.y));
            }

            if (distance < retreatDistance)
            {
                Vector3 retreatDestination = targetPosition - (targetDirection * idealStandoffDistance);
                return new EnemyEngagementDecision(EnemyEngagementMode.Retreat, KeepY(retreatDestination, selfPosition.y));
            }

            int direction = orbitDirection < 0 ? -1 : 1;
            float step = Mathf.Clamp(orbitStepDegrees, 0f, 180f) * direction;
            Vector3 radial = Quaternion.AngleAxis(step, Vector3.up) * (-targetDirection);
            Vector3 orbitDestination = targetPosition + (radial.normalized * idealStandoffDistance);
            return new EnemyEngagementDecision(EnemyEngagementMode.Orbit, KeepY(orbitDestination, selfPosition.y));
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static Vector3 KeepY(Vector3 value, float y)
        {
            value.y = y;
            return value;
        }
    }

    public static class EnemyVesselSteeringUtility
    {
        public static float ComputeSteeringInput(Vector3 forward, Vector3 toDestination, float fullSteerAngleDegrees)
        {
            forward.y = 0f;
            toDestination.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toDestination.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float signedAngle = Vector3.SignedAngle(forward.normalized, toDestination.normalized, Vector3.up);
            return Mathf.Clamp(signedAngle / Mathf.Max(1f, fullSteerAngleDegrees), -1f, 1f);
        }

        public static float ComputeThrottleInput(Vector3 forward, Vector3 toDestination, float slowTurnAngleDegrees, float slowTurnThrottle)
        {
            forward.y = 0f;
            toDestination.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toDestination.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float angle = Vector3.Angle(forward.normalized, toDestination.normalized);
            if (angle <= Mathf.Max(0f, slowTurnAngleDegrees))
            {
                return 1f;
            }

            return Mathf.Clamp01(slowTurnThrottle);
        }
    }

    public static class EnemyWeaponMath
    {
        public static bool IsTargetInsideArc(Vector3 origin, Vector3 forward, Vector3 target, float halfAngleDegrees)
        {
            forward.y = 0f;
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float angle = Vector3.Angle(forward.normalized, toTarget.normalized);
            return angle <= Mathf.Clamp(halfAngleDegrees, 0f, 180f);
        }
    }

    public static class EnemyAlertUtility
    {
        public static bool IsWithinAlertRadius(Vector3 listenerPosition, Vector3 sourcePosition, float radius)
        {
            radius = Mathf.Max(0f, radius);
            return (listenerPosition - sourcePosition).sqrMagnitude <= radius * radius;
        }
    }

    public sealed class EnemyBurstScheduler
    {
        private readonly int _shotsPerBurst;
        private readonly float _secondsPerShot;
        private readonly float _cooldownSeconds;
        private int _shotsRemaining;
        private float _nextShotTime;
        private float _cooldownUntil;

        public EnemyBurstScheduler(int shotsPerBurst, float secondsPerShot, float cooldownSeconds)
        {
            _shotsPerBurst = Mathf.Max(1, shotsPerBurst);
            _secondsPerShot = Mathf.Max(0.01f, secondsPerShot);
            _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }

        public bool TryConsumeShot(float time)
        {
            if (time < _cooldownUntil || time < _nextShotTime)
            {
                return false;
            }

            if (_shotsRemaining <= 0)
            {
                _shotsRemaining = _shotsPerBurst;
            }

            _shotsRemaining--;
            if (_shotsRemaining <= 0)
            {
                _cooldownUntil = time + _cooldownSeconds;
                _nextShotTime = _cooldownUntil;
            }
            else
            {
                _nextShotTime = time + _secondsPerShot;
            }

            return true;
        }

        public void Reset()
        {
            _shotsRemaining = 0;
            _nextShotTime = 0f;
            _cooldownUntil = 0f;
        }
    }
}
