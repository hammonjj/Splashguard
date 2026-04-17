using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyEngageAction : EnemyActionBase
    {
        private const float EngageScore = 1f;
        private int _orbitDirection = 1;
        private float _nextDiagnosticTime;

        public override string DebugStatus
        {
            get
            {
                if (Context?.TargetTracker == null || !Context.TargetTracker.HasTarget)
                {
                    return "No target";
                }

                return $"Engaging {Context.TargetTracker.CurrentTarget.name}";
            }
        }

        public override float Score()
        {
            return Context != null
                && Context.TargetTracker != null
                && Context.TargetTracker.HasTarget
                ? EngageScore
                : 0f;
        }

        public override void Enter()
        {
            _orbitDirection = Random.value >= 0.5f ? 1 : -1;
            _nextDiagnosticTime = 0f;
            LogInfo(
                $"Enemy engage entered. orbitDirection={_orbitDirection}, hasMovementAgent={Context?.MovementAgent != null}, target={Context?.TargetTracker?.CurrentTarget?.name ?? "None"}.");
        }

        public override void Exit()
        {
            Context?.WeaponController?.ClearTarget();
            Context?.MovementAgent?.Stop("engage_exit");
        }

        public override void Tick(float deltaTime)
        {
            if (Context?.TargetTracker == null
                || Context.MovementAgent == null
                || Context.EnemyData == null
                || !Context.TargetTracker.HasTarget)
            {
                Context?.WeaponController?.ClearTarget();
                Context?.MovementAgent?.Stop("engage_no_target");
                return;
            }

            Vector3 targetPoint = Context.TargetTracker.CurrentTargetAimPoint;
            EnemyEngagementDecision decision = EnemyEngagementPlanner.ResolveDestination(
                transform.position,
                transform.forward,
                targetPoint,
                Context.EnemyData.AttackRange,
                Context.EnemyData.IdealStandoffDistance,
                Context.EnemyData.RetreatDistance,
                Context.EnemyData.OrbitStepDegrees,
                _orbitDirection);

            if (decision.Mode == EnemyEngagementMode.Orbit
                && Context.MovementAgent.HasReached(decision.Destination, Context.EnemyData.EngagementWaypointAcceptanceRadius))
            {
                _orbitDirection *= -1;
                decision = EnemyEngagementPlanner.ResolveDestination(
                    transform.position,
                    transform.forward,
                    targetPoint,
                    Context.EnemyData.AttackRange,
                    Context.EnemyData.IdealStandoffDistance,
                    Context.EnemyData.RetreatDistance,
                    Context.EnemyData.OrbitStepDegrees,
                    _orbitDirection);
            }

            Vector3 destination = Context.MovementAgent.TryProjectDestination(
                decision.Destination,
                Context.EnemyData.DestinationProjectionSearchRadius,
                out Vector3 projectedDestination)
                ? projectedDestination
                : decision.Destination;

            Context.MovementAgent.MoveTo(destination, Context.EnemyData.EngagementWaypointAcceptanceRadius);
            Context.WeaponController?.SetTarget(Context.TargetTracker.CurrentTarget);
            LogEngageDiagnostic(decision, targetPoint, destination);
        }

        private void LogEngageDiagnostic(
            EnemyEngagementDecision decision,
            Vector3 targetPoint,
            Vector3 destination)
        {
            float now = Time.time;
            float interval = Context.EnemyData.MovementDiagnosticInterval;
            if (now < _nextDiagnosticTime)
            {
                return;
            }

            _nextDiagnosticTime = now + interval;
            LogInfo(
                $"Enemy engage movement order. mode={decision.Mode}, target={Context.TargetTracker.CurrentTarget?.name ?? "None"}, targetPoint={FormatVector(targetPoint)}, destination={FormatVector(destination)}, distanceToTarget={Context.TargetTracker.CurrentDistance:0.##}, movementState={Context.MovementAgent.CurrentStatus.State}, speed={Context.MovementAgent.CurrentSpeed:0.##}.");
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }
    }
}
