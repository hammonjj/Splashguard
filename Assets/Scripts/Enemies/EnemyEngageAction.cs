using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyEngageAction : EnemyActionBase
    {
        private const float EngageScore = 1f;
        private int _orbitDirection = 1;

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
        }

        public override void Exit()
        {
            Context?.WeaponController?.ClearTarget();
            Context?.Motor?.Stop();
        }

        public override void Tick(float deltaTime)
        {
            if (Context?.TargetTracker == null
                || Context.Motor == null
                || Context.EnemyData == null
                || !Context.TargetTracker.HasTarget)
            {
                Context?.WeaponController?.ClearTarget();
                Context?.Motor?.Stop();
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
                && Context.Motor.HasReached(decision.Destination, Context.EnemyData.EngagementWaypointAcceptanceRadius))
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

            Context.Motor.MoveTo(decision.Destination);
            Context.WeaponController?.SetTarget(Context.TargetTracker.CurrentTarget);
        }
    }
}
