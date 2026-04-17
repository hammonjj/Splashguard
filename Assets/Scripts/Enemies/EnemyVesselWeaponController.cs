using BitBox.Library;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyVesselWeaponController : MonoBehaviourBase
    {
        [SerializeField] private EnemyVesselData _enemyData;
        [SerializeField] private EnemyTargetTracker _targetTracker;
        [SerializeField] private GameObject _ownerRoot;
        [SerializeField] private EnemyProjectileWeaponMount[] _weaponMounts;
        [SerializeField, Min(0.05f)] private float _targetGizmoRadius = 0.75f;

        private EnemyBrain _brain;
        private PlayerVesselTarget _explicitTarget;

        protected override void OnEnabled()
        {
            CacheReferences();
            ConfigureMounts();
        }

        protected override void OnUpdated()
        {
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
            for (int i = 0; i < _weaponMounts.Length; i++)
            {
                _weaponMounts[i]?.ResetBurst();
            }
        }

        private void CacheReferences()
        {
            _brain ??= GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
            _targetTracker ??= GetComponent<EnemyTargetTracker>() ?? GetComponentInParent<EnemyTargetTracker>();
            if (_weaponMounts == null || _weaponMounts.Length == 0)
            {
                _weaponMounts = ResolveOwnerRoot().GetComponentsInChildren<EnemyProjectileWeaponMount>(includeInactive: true);
            }
        }

        private void ConfigureMounts()
        {
            EnemyVesselData data = ResolveData();
            for (int i = 0; i < _weaponMounts.Length; i++)
            {
                _weaponMounts[i]?.Configure(ResolveOwnerRoot(), data, _globalMessageBus);
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

        private GameObject ResolveOwnerRoot()
        {
            if (_ownerRoot != null)
            {
                return _ownerRoot;
            }

            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }
    }
}
