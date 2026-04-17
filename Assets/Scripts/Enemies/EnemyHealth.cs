using BitBox.Library;
using BitBox.Library.Eventing.WeaponEvents;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviourBase
    {
        [SerializeField] private EnemyVesselData _enemyData;
        [SerializeField] private EnemyTargetTracker _targetTracker;
        [SerializeField] private GameObject _enemyRoot;

        private EnemyBrain _brain;
        private float _currentHealth;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public bool IsDead => _isDead;

        protected override void OnEnabled()
        {
            CacheReferences();
            ResetHealth();
            _globalMessageBus?.Subscribe<ProjectileImpactEvent>(OnProjectileImpact);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<ProjectileImpactEvent>(OnProjectileImpact);
        }

        public void ResetHealth()
        {
            _isDead = false;
            _currentHealth = ResolveMaxHealth();
        }

        public bool TryApplyProjectileImpact(ProjectileImpactEvent impact)
        {
            if (impact == null
                || impact.PlayerIndex < 0
                || impact.Damage <= 0
                || impact.HitObject == null
                || !IsHitPartOfEnemy(impact.HitObject))
            {
                return false;
            }

            PlayerVesselTarget.TryFindNearest(ResolveEnemyRoot().transform.position, float.PositiveInfinity, out PlayerVesselTarget target);
            ApplyDamage(impact.Damage, impact.PlayerIndex, target, "hit");
            return true;
        }

        public void ApplyDamage(float damage, int sourcePlayerIndex, PlayerVesselTarget sourceTarget, string reason)
        {
            if (_isDead || damage <= 0f)
            {
                return;
            }

            float maxHealth = ResolveMaxHealth();
            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            GameObject enemyRoot = ResolveEnemyRoot();
            _globalMessageBus?.Publish(new EnemyDamagedEvent(enemyRoot, _currentHealth, maxHealth, damage, sourcePlayerIndex));

            if (sourceTarget != null && _targetTracker != null)
            {
                _targetTracker.ForceSetTarget(sourceTarget, publishAlert: true, reason);
            }

            if (_currentHealth > 0f)
            {
                return;
            }

            _isDead = true;
            _globalMessageBus?.Publish(new EnemyDeathEvent(enemyRoot));
        }

        private void OnProjectileImpact(ProjectileImpactEvent impact)
        {
            TryApplyProjectileImpact(impact);
        }

        private void CacheReferences()
        {
            GameObject enemyRoot = ResolveEnemyRoot();
            _brain ??= GetComponent<EnemyBrain>()
                ?? GetComponentInParent<EnemyBrain>()
                ?? enemyRoot.GetComponentInChildren<EnemyBrain>(includeInactive: true);
            _targetTracker ??= GetComponent<EnemyTargetTracker>()
                ?? GetComponentInParent<EnemyTargetTracker>()
                ?? enemyRoot.GetComponentInChildren<EnemyTargetTracker>(includeInactive: true);
        }

        private float ResolveMaxHealth()
        {
            EnemyVesselData data = _enemyData != null ? _enemyData : _brain != null ? _brain.EnemyData : null;
            return data != null ? data.MaxHealth : 1f;
        }

        private bool IsHitPartOfEnemy(GameObject hitObject)
        {
            GameObject enemyRoot = ResolveEnemyRoot();
            return hitObject == enemyRoot || hitObject.transform.IsChildOf(enemyRoot.transform);
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
