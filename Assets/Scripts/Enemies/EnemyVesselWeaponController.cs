using BitBox.Library;
using BitBox.Toymageddon.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyVesselWeaponController : MonoBehaviourBase
    {
        [SerializeField, InlineEditor] private EnemyVesselData _enemyData;
        [SerializeField] private EnemyTargetTracker _targetTracker;
        [SerializeField] private EnemyProjectileWeaponMount[] _weaponMounts;
        [SerializeField, Min(0.05f)] private float _targetGizmoRadius = 0.5f;

        private Rigidbody _rigidBody;
        private EnemyBrain _brain;
        private PlayerVesselTarget _explicitTarget;

        protected override void OnEnabled()
        {
            CacheReferences();
            ConfigureMounts();
        }

        protected override void OnUpdated()
        {
            if (_weaponMounts == null || _weaponMounts.Length == 0)
            {
                return;
            }

            if (DebugContext.EnemiesPassive)
            {
                ResetMountBursts();
                return;
            }

            PlayerVesselTarget target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            EnemyVesselData data = ResolveData();
            if (data == null || Vector3.Distance(transform.position, target.AimPoint) > data.AttackRange)
            {
                return;
            }

            for (int i = 0; i < _weaponMounts.Length; i++)
            {
                EnemyProjectileWeaponMount mount = _weaponMounts[i];
                if (mount != null && mount.enabled)
                {
                    mount.TickWeapon(target.AimPoint, Time.time, Time.deltaTime);
                }
            }
        }

        protected override void OnDrawnGizmos()
        {
            if (!_explicitTarget)
            {
                return;
            }

            Vector3 aimPoint = _explicitTarget.AimPoint;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(aimPoint, _targetGizmoRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimPoint, _targetGizmoRadius * 1.35f);
        }

        public void SetTarget(PlayerVesselTarget target)
        {
            _explicitTarget = target;
        }

        public void ClearTarget()
        {
            _explicitTarget = null;
            ResetMountBursts();
        }

        private void ResetMountBursts()
        {
            if (_weaponMounts == null)
            {
                return;
            }

            for (int i = 0; i < _weaponMounts.Length; i++)
            {
                _weaponMounts[i]?.ResetBurst();
            }
        }

        private void CacheReferences()
        {
            if (_brain == null)
            {
                _brain = GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
            }

            if (_targetTracker == null)
            {
                _targetTracker = GetComponent<EnemyTargetTracker>() ?? GetComponentInParent<EnemyTargetTracker>();
            }

            if (_rigidBody == null)
            {
                _rigidBody = ResolveRootRigidbody();
            }

            if (_rigidBody == null)
            {
                LogError(
                    $"Enemy weapon controller could not resolve a parent Rigidbody. object={name}, root={transform.root?.name ?? "None"}.");
                enabled = false;
                return;
            }

            if (_weaponMounts == null || _weaponMounts.Length == 0)
            {
                _weaponMounts = _rigidBody.gameObject.GetComponentsInChildren<EnemyProjectileWeaponMount>(includeInactive: true);
            }
        }

        private void ConfigureMounts()
        {
            if (_rigidBody == null)
            {
                return;
            }

            EnemyVesselData data = ResolveData();
            if (_weaponMounts == null || _weaponMounts.Length == 0)
            {
                LogWarning(
                    $"Enemy weapon controller has no weapon mounts. owner={_rigidBody.gameObject.name}, controller={name}.");
                return;
            }

            LogInfo(
                $"Enemy weapon controller configured. owner={_rigidBody.gameObject.name}, mounts={_weaponMounts.Length}, data={data?.name ?? "None"}.");

            for (int i = 0; i < _weaponMounts.Length; i++)
            {
                _weaponMounts[i]?.Configure(_rigidBody.gameObject, data, _globalMessageBus);
            }
        }

        private PlayerVesselTarget ResolveTarget()
        {
            if (_explicitTarget != null && _explicitTarget.isActiveAndEnabled)
            {
                return _explicitTarget;
            }

            return _targetTracker != null && _targetTracker.HasTarget
                ? _targetTracker.CurrentTarget
                : null;
        }

        private EnemyVesselData ResolveData()
        {
            return _enemyData != null ? _enemyData : _brain != null ? _brain.EnemyData : null;
        }

        private Rigidbody ResolveRootRigidbody()
        {
            Rigidbody[] parentRigidbodies = GetComponentsInParent<Rigidbody>(includeInactive: true);
            for (int i = 0; i < parentRigidbodies.Length; i++)
            {
                Rigidbody candidate = parentRigidbodies[i];
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
