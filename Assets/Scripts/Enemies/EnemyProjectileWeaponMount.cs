using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.Weapons;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyProjectileWeaponMount : MonoBehaviourBase
    {
        private const int EnemyProjectilePlayerIndex = -1;
        private const string FirePointName = "FirePoint.001";
        private const string FirePointPrefix = "FirePoint";
        private const string RotationPivotName = "RotationPivot";
        private const string AuthoredPitchPivotName = "PitchlPivot";
        private const string CorrectedPitchPivotName = "PitchPivot";

        [Header("Weapon")]
        [SerializeField] private WeaponDefinition _weaponDefinition;
        [SerializeField, Range(1f, 180f)] private float _arcHalfAngleDegrees = 90f;
        [SerializeField, Min(0f)] private float _rangeOverride = 0f;

        [Header("Aiming")]
        [SerializeField] private Transform _rotationPivot;
        [SerializeField] private Transform _pitchPivot;
        [SerializeField] private Transform _firePoint;

        private readonly Dictionary<ProjectileDefinition, ProjectilePool> _projectilePools = new();
        private readonly List<Collider> _ignoredCollisionColliders = new();
        private GameObject _ownerRoot;
        private EnemyVesselData _enemyData;
        private MessageBus _globalBus;
        private EnemyBurstScheduler _burstScheduler;
        private Transform _projectilePoolRoot;

        public float ArcHalfAngleDegrees => _arcHalfAngleDegrees;
        public Transform FirePoint => _firePoint;
        public WeaponDefinition WeaponDefinition => _weaponDefinition;

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnDrawnGizmos()
        {
            if (_firePoint == null)
            {
                return;
            }

            Vector3 targetPoint = _firePoint.position + _firePoint.forward * (
                _rangeOverride > 0f
                    ? _rangeOverride
                    : _enemyData != null
                        ? _enemyData.AttackRange
                        : 10f);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(_firePoint.position, targetPoint - _firePoint.position);
            Gizmos.DrawSphere(targetPoint, 0.35f);
        }

        public void Configure(GameObject ownerRoot, EnemyVesselData enemyData, MessageBus globalBus)
        {
            CacheReferences();
            _ownerRoot = ownerRoot != null ? ownerRoot : transform.root.gameObject;
            _enemyData = enemyData;
            _globalBus = globalBus;
            _arcHalfAngleDegrees = enemyData != null ? enemyData.WeaponArcHalfAngleDegrees : _arcHalfAngleDegrees;
            _burstScheduler = CreateBurstScheduler(enemyData);
            RebuildIgnoredCollisionColliders();
        }

        public void TickWeapon(Vector3 targetPoint, float time, float deltaTime)
        {
            CacheReferences();
            if (!HasValidWeaponConfiguration() || !IsTargetInRangeAndArc(targetPoint))
            {
                return;
            }

            AimAt(targetPoint, deltaTime);
            if (_burstScheduler == null)
            {
                _burstScheduler = CreateBurstScheduler(_enemyData);
            }

            if (_burstScheduler.TryConsumeShot(time))
            {
                Fire(targetPoint);
            }
        }

        public void ResetBurst()
        {
            _burstScheduler?.Reset();
        }

        public bool IsTargetInRangeAndArc(Vector3 targetPoint)
        {
            if (!EnemyWeaponMath.IsTargetInsideArc(transform.position, transform.forward, targetPoint, _arcHalfAngleDegrees))
            {
                return false;
            }

            float range = _rangeOverride > 0f
                ? _rangeOverride
                : _enemyData != null
                    ? _enemyData.AttackRange
                    : float.PositiveInfinity;
            return Vector3.Distance(transform.position, targetPoint) <= range;
        }

        private void CacheReferences()
        {
            _rotationPivot ??= FindChildByName(transform, RotationPivotName);
            _pitchPivot ??= FindChildByName(transform, AuthoredPitchPivotName) ?? FindChildByName(transform, CorrectedPitchPivotName);
            _firePoint ??= FindChildByName(transform, FirePointName) ?? FindChildByNamePrefix(transform, FirePointPrefix);

            if (_projectilePoolRoot == null)
            {
                var root = new GameObject($"{name} ProjectilePool");
                root.transform.SetParent(transform, false);
                _projectilePoolRoot = root.transform;
            }
        }

        private EnemyBurstScheduler CreateBurstScheduler(EnemyVesselData data)
        {
            int shots = data != null ? data.BurstShots : 5;
            float cooldown = data != null ? data.BurstCooldownSeconds : 1.25f;
            float secondsPerShot = _weaponDefinition != null && _weaponDefinition.FireMode != null
                ? _weaponDefinition.FireMode.SecondsPerShot
                : 0.12f;
            return new EnemyBurstScheduler(shots, secondsPerShot, cooldown);
        }

        private bool HasValidWeaponConfiguration()
        {
            return _weaponDefinition != null
                && _weaponDefinition.Ammo != null
                && _weaponDefinition.Ammo.Projectile != null
                && _weaponDefinition.Ammo.Projectile.ProjectilePrefab != null
                && _firePoint != null;
        }

        private void AimAt(Vector3 targetPoint, float deltaTime)
        {
            float aimSpeed = _enemyData != null ? _enemyData.WeaponAimDegreesPerSecond : 180f;
            if (aimSpeed <= 0f)
            {
                return;
            }

            Vector3 planarDirection = targetPoint - transform.position;
            planarDirection.y = 0f;
            if (_rotationPivot != null && planarDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredYaw = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
                _rotationPivot.rotation = Quaternion.RotateTowards(
                    _rotationPivot.rotation,
                    desiredYaw,
                    aimSpeed * deltaTime);
            }

            if (_pitchPivot != null && _firePoint != null)
            {
                Vector3 pitchDirection = targetPoint - _pitchPivot.position;
                if (pitchDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredPitch = Quaternion.LookRotation(pitchDirection.normalized, Vector3.up);
                    _pitchPivot.rotation = Quaternion.RotateTowards(
                        _pitchPivot.rotation,
                        desiredPitch,
                        aimSpeed * deltaTime);
                }
            }
        }

        private void Fire(Vector3 targetPoint)
        {
            AmmoDefinition ammo = _weaponDefinition.Ammo;
            ProjectileDefinition projectileDefinition = ammo.Projectile;
            ProjectilePool projectilePool = GetOrCreatePool(projectileDefinition);
            Vector3 direction = targetPoint - _firePoint.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = _firePoint.forward;
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            PhysicalProjectile projectile = projectilePool.Rent(_firePoint.position, rotation);
            if (projectile == null)
            {
                return;
            }

            Vector3 velocity = direction.normalized * projectileDefinition.Speed;
            projectile.Arm(
                EnemyProjectilePlayerIndex,
                _weaponDefinition,
                ammo,
                projectileDefinition,
                _ignoredCollisionColliders,
                velocity,
                _globalBus,
                projectilePool.Return);

            _globalBus?.Publish(new WeaponFiredEvent(
                EnemyProjectilePlayerIndex,
                _weaponDefinition,
                ammo,
                projectileDefinition,
                remainingAmmo: 0,
                infiniteAmmo: true));
            _globalBus?.Publish(new ProjectileSpawnedEvent(
                EnemyProjectilePlayerIndex,
                _weaponDefinition,
                ammo,
                projectileDefinition,
                projectile));
        }

        private ProjectilePool GetOrCreatePool(ProjectileDefinition projectile)
        {
            if (_projectilePools.TryGetValue(projectile, out ProjectilePool projectilePool))
            {
                return projectilePool;
            }

            projectilePool = new ProjectilePool(projectile, _projectilePoolRoot);
            projectilePool.Prewarm();
            _projectilePools.Add(projectile, projectilePool);
            return projectilePool;
        }

        private void RebuildIgnoredCollisionColliders()
        {
            _ignoredCollisionColliders.Clear();
            GameObject root = _ownerRoot != null ? _ownerRoot : transform.root.gameObject;
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !_ignoredCollisionColliders.Contains(colliders[i]))
                {
                    _ignoredCollisionColliders.Add(colliders[i]);
                }
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildByName(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindChildByNamePrefix(Transform root, string childNamePrefix)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name.StartsWith(childNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildByNamePrefix(root.GetChild(i), childNamePrefix);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private sealed class ProjectilePool
        {
            private readonly ProjectileDefinition _projectileDefinition;
            private readonly Transform _parent;
            private readonly Queue<PhysicalProjectile> _availableProjectiles = new();
            private readonly HashSet<PhysicalProjectile> _allProjectiles = new();

            public ProjectilePool(ProjectileDefinition projectileDefinition, Transform parent)
            {
                _projectileDefinition = projectileDefinition;
                _parent = parent;
            }

            public void Prewarm()
            {
                int targetCount = Mathf.Min(_projectileDefinition.PrewarmCount, _projectileDefinition.MaxPoolSize);
                while (_allProjectiles.Count < targetCount)
                {
                    PhysicalProjectile projectile = CreateProjectile();
                    if (projectile == null)
                    {
                        return;
                    }

                    Return(projectile);
                }
            }

            public PhysicalProjectile Rent(Vector3 position, Quaternion rotation)
            {
                while (_availableProjectiles.Count > 0)
                {
                    PhysicalProjectile projectile = _availableProjectiles.Dequeue();
                    if (projectile == null)
                    {
                        continue;
                    }

                    projectile.transform.SetParent(null, true);
                    projectile.transform.SetPositionAndRotation(position, rotation);
                    projectile.gameObject.SetActive(true);
                    return projectile;
                }

                if (_allProjectiles.Count >= _projectileDefinition.MaxPoolSize)
                {
                    return null;
                }

                PhysicalProjectile newProjectile = CreateProjectile();
                if (newProjectile == null)
                {
                    return null;
                }

                newProjectile.transform.SetParent(null, true);
                newProjectile.transform.SetPositionAndRotation(position, rotation);
                newProjectile.gameObject.SetActive(true);
                return newProjectile;
            }

            public void Return(PhysicalProjectile projectile)
            {
                if (projectile == null)
                {
                    return;
                }

                projectile.PrepareForPool();
                projectile.transform.SetParent(_parent, false);
                projectile.gameObject.SetActive(false);
                _availableProjectiles.Enqueue(projectile);
            }

            private PhysicalProjectile CreateProjectile()
            {
                if (_projectileDefinition == null || _projectileDefinition.ProjectilePrefab == null)
                {
                    return null;
                }

                PhysicalProjectile projectile = Instantiate(_projectileDefinition.ProjectilePrefab, _parent);
                projectile.name = _projectileDefinition.ProjectilePrefab.name;
                projectile.PrepareForPool();
                _allProjectiles.Add(projectile);
                return projectile;
            }
        }
    }
}
