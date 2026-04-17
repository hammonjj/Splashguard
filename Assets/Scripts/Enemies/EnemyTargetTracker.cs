using BitBox.Library;
using BitBox.Toymageddon.Debugging;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyTargetTracker : MonoBehaviourBase
    {
        [SerializeField] private EnemyVesselData _enemyData;
        [SerializeField] private GameObject _enemyRoot;
        [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

        private EnemyBrain _brain;
        private PlayerVesselTarget _currentTarget;
        private float _nextRefreshTime;
        private bool _alertPublishedForCurrentTarget;

        public bool HasTarget => _currentTarget != null && _currentTarget.isActiveAndEnabled;
        public PlayerVesselTarget CurrentTarget => HasTarget ? _currentTarget : null;
        public Transform CurrentTargetTransform => CurrentTarget != null ? CurrentTarget.RootTransform : null;
        public Vector3 CurrentTargetAimPoint => CurrentTarget != null ? CurrentTarget.AimPoint : transform.position;
        public float CurrentDistance => CurrentTarget != null
            ? Vector3.Distance(transform.position, CurrentTarget.AimPoint)
            : float.PositiveInfinity;

        protected override void OnEnabled()
        {
            CacheReferences();
            _globalMessageBus?.Subscribe<EnemyAlertEvent>(OnEnemyAlerted);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<EnemyAlertEvent>(OnEnemyAlerted);
            _currentTarget = null;
            _alertPublishedForCurrentTarget = false;
        }

        protected override void OnUpdated()
        {
            if (DebugContext.EnemiesFrozen)
            {
                ClearTarget();
                return;
            }

            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.time + Mathf.Max(0.05f, _refreshInterval);
            RefreshTarget();
        }

        public bool ForceSetTarget(PlayerVesselTarget target, bool publishAlert, string reason)
        {
            if (DebugContext.EnemiesFrozen)
            {
                ClearTarget();
                return false;
            }

            if (target == null)
            {
                return false;
            }

            bool changed = target != _currentTarget;
            _currentTarget = target;
            if (changed)
            {
                _alertPublishedForCurrentTarget = false;
            }

            if (publishAlert)
            {
                PublishAlert(reason);
            }

            return true;
        }

        public void ClearTarget()
        {
            _currentTarget = null;
            _alertPublishedForCurrentTarget = false;
        }

        public void PublishAlert(string reason)
        {
            if (DebugContext.EnemiesFrozen || !HasTarget || _globalMessageBus == null)
            {
                return;
            }

            _alertPublishedForCurrentTarget = true;
            float alertRadius = ResolveData()?.AlertRadius ?? 0f;
            _globalMessageBus.Publish(new EnemyAlertEvent(
                ResolveEnemyRoot(),
                _currentTarget,
                transform.position,
                alertRadius,
                reason));
        }

        private void RefreshTarget()
        {
            EnemyVesselData data = ResolveData();
            if (data == null)
            {
                return;
            }

            if (HasTarget)
            {
                if (CurrentDistance <= data.DetectionRange || _alertPublishedForCurrentTarget)
                {
                    return;
                }

                _currentTarget = null;
                _alertPublishedForCurrentTarget = false;
            }

            if (!PlayerVesselTarget.TryFindNearest(transform.position, data.DetectionRange, out PlayerVesselTarget target))
            {
                return;
            }

            ForceSetTarget(target, publishAlert: true, reason: "spotted");
        }

        private void OnEnemyAlerted(EnemyAlertEvent @event)
        {
            if (DebugContext.EnemiesFrozen)
            {
                ClearTarget();
                return;
            }

            if (@event == null || @event.Target == null || @event.SourceEnemyRoot == ResolveEnemyRoot())
            {
                return;
            }

            if (!EnemyAlertUtility.IsWithinAlertRadius(transform.position, @event.SourcePosition, @event.Radius))
            {
                return;
            }

            ForceSetTarget(@event.Target, publishAlert: false, reason: "ally_alert");
        }

        private void CacheReferences()
        {
            _brain ??= GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
        }

        private EnemyVesselData ResolveData()
        {
            CacheReferences();
            return _enemyData != null ? _enemyData : _brain != null ? _brain.EnemyData : null;
        }

        private GameObject ResolveEnemyRoot()
        {
            if (_enemyRoot != null)
            {
                return _enemyRoot;
            }

            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }
    }
}
