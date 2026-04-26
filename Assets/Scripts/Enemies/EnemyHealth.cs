using BitBox.Library;
using BitBox.Library.Eventing.DebugEvents;
using BitBox.Library.Eventing.WeaponEvents;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviourBase
    {
        [SerializeField, InlineEditor] private EnemyVesselData _enemyData;
        [SerializeField] private EnemyTargetTracker _targetTracker;
        [SerializeField] private GameObject _enemyRoot;
        [SerializeField] private EnemyHealthWorldDisplay _worldDisplay;

        private EnemyBrain _brain;
        [ShowInInspector, ReadOnly] private float _currentHealth;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public bool IsDead => _isDead;
        public EnemyHealthWorldDisplay WorldDisplay => _worldDisplay;

        protected override void OnEnabled()
        {
            CacheReferences();
            ResetHealth();
            _globalMessageBus?.Subscribe<ProjectileImpactEvent>(OnProjectileImpact);
            _globalMessageBus?.Subscribe<KillAllEnemiesEvent>(OnKillAllEnemies);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<ProjectileImpactEvent>(OnProjectileImpact);
            _globalMessageBus?.Unsubscribe<KillAllEnemiesEvent>(OnKillAllEnemies);
        }

        public void ResetHealth()
        {
            _isDead = false;
            float maxHealth = ResolveMaxHealth();
            _currentHealth = maxHealth;
            _worldDisplay?.Initialize(_currentHealth, maxHealth);
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
            ApplyDamage(impact.Damage, impact.PlayerIndex, target, "hit", impact.Point);
            return true;
        }

        public void ApplyDamage(
            float damage,
            int sourcePlayerIndex,
            PlayerVesselTarget sourceTarget,
            string reason,
            Vector3? damageTextPosition = null)
        {
            if (_isDead || damage <= 0f)
            {
                return;
            }

            float maxHealth = ResolveMaxHealth();
            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            GameObject enemyRoot = ResolveEnemyRoot();
            _worldDisplay?.ShowDamage(_currentHealth, maxHealth, damage, damageTextPosition);
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
            _worldDisplay?.HandleDeath(_currentHealth, maxHealth);
            _globalMessageBus?.Publish(new EnemyDeathEvent(enemyRoot));
        }

        private void OnProjectileImpact(ProjectileImpactEvent impact)
        {
            TryApplyProjectileImpact(impact);
        }

        private void OnKillAllEnemies(KillAllEnemiesEvent @event)
        {
            if (_isDead)
            {
                return;
            }

            float damage = _currentHealth > 0f ? _currentHealth : ResolveMaxHealth();
            ApplyDamage(damage, -1, null, "kill_all_enemies");
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
            _worldDisplay ??= GetComponentInChildren<EnemyHealthWorldDisplay>(includeInactive: true);
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
