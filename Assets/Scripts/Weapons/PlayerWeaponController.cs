using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.DebugEvents;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponController : MonoBehaviourBase
    {
        private const string FirePointName = "FirePoint.001";
        private const string FirePointPrefix = "FirePoint";

        private readonly Dictionary<ProjectileDefinition, ProjectilePool> _projectilePools = new();
        private readonly List<Collider> _ignoredCollisionColliders = new();
        private readonly HashSet<Collider> _ignoredCollisionLookup = new();

        [Header("Weapon")]
        [SerializeField, Required, InlineEditor] private WeaponDefinition _weaponDefinition;
        [SerializeField] private bool _useDebugWeaponSelection = true;
        [SerializeField] private WeaponDefinition[] _debugWeaponDefinitions;

        [Header("Spawn")]
        [SerializeField, Required] private Transform _firePoint;

        private MessageBus _localMessageBus;
        private Transform _projectilePoolRoot;
        private WeaponDefinition _activeWeapon;
        private GameObject _controllingPlayerRoot;
        private GameObject _weaponOwnerRoot;
        private int _controllingPlayerIndex = -1;
        private int _currentAmmo;
        private bool _hasControl;
        private bool _fireHeld;
        private bool _spinupActive;
        private bool _hasFiredSinceTriggerHeld;
        private bool _dryFirePublishedSinceTriggerHeld;
        private bool _isPaused;
        private bool _infiniteAmmo;
        private bool _configurationErrorLogged;
        private float _spinupElapsed;
        private float _nextShotTime;

        public Transform FirePoint => _firePoint;
        public int CurrentAmmo => _currentAmmo;
        public int ClipCapacity => _activeWeapon != null && _activeWeapon.Magazine != null
            ? _activeWeapon.Magazine.ClipCapacity
            : 0;
        public bool InfiniteAmmo => _infiniteAmmo;
        public WeaponDefinition ActiveWeapon => _activeWeapon;

        protected override void OnAwakened()
        {
            CacheReferences();
            RefreshActiveWeapon(resetMagazine: true);
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            RefreshActiveWeapon(resetMagazine: _activeWeapon == null);
            PrewarmProjectilePool();

            _localMessageBus.Subscribe<WeaponControlAcquiredEvent>(OnWeaponControlAcquired);
            _localMessageBus.Subscribe<WeaponControlReleasedEvent>(OnWeaponControlReleased);
            _localMessageBus.Subscribe<WeaponFireInputEvent>(OnWeaponFireInput);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<InfiniteAmmoEvent>(OnInfiniteAmmo);

            _infiniteAmmo = DebugContext.InfiniteAmmo;
            LogInfo($"Weapon controller enabled. weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}, localFireSubscribers={_localMessageBus.GetSubscriberCount<WeaponFireInputEvent>()}.");
            PublishAmmoChanged();
        }

        protected override void OnDisabled()
        {
            ResetTrigger(publishSpinupCancelled: false);
            _localMessageBus?.Unsubscribe<WeaponControlAcquiredEvent>(OnWeaponControlAcquired);
            _localMessageBus?.Unsubscribe<WeaponControlReleasedEvent>(OnWeaponControlReleased);
            _localMessageBus?.Unsubscribe<WeaponFireInputEvent>(OnWeaponFireInput);
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus?.Unsubscribe<InfiniteAmmoEvent>(OnInfiniteAmmo);
        }

        protected override void OnUpdated()
        {
            if (!_hasControl || !_fireHeld || _isPaused || !HasValidWeaponConfiguration())
            {
                return;
            }

            if (!_hasFiredSinceTriggerHeld)
            {
                TickSpinup();
                return;
            }

            TickAutomaticFire();
        }

        private void CacheReferences()
        {
            _localMessageBus ??= GetComponent<MessageBus>();
            if (_localMessageBus == null)
            {
                _localMessageBus = gameObject.AddComponent<MessageBus>();
            }

            EnsureFirePoint();

            if (_projectilePoolRoot == null)
            {
                var root = new GameObject("ProjectilePool");
                root.transform.SetParent(transform, false);
                _projectilePoolRoot = root.transform;
            }
        }

        private void EnsureFirePoint()
        {
            _firePoint ??= FindPreferredFirePoint(transform);
        }

        private void RefreshActiveWeapon(bool resetMagazine)
        {
            WeaponDefinition resolvedWeapon = ResolveWeaponDefinition();
            if (resolvedWeapon == _activeWeapon && !resetMagazine)
            {
                return;
            }

            _activeWeapon = resolvedWeapon;
            if (_activeWeapon != null && _activeWeapon.Magazine != null)
            {
                _currentAmmo = _activeWeapon.Magazine.StartsFull
                    ? _activeWeapon.Magazine.ClipCapacity
                    : 0;
            }
            else
            {
                _currentAmmo = 0;
            }
        }

        private WeaponDefinition ResolveWeaponDefinition()
        {
            if (!_useDebugWeaponSelection)
            {
                return _weaponDefinition;
            }

            DebugWeaponType requestedWeaponType = DebugContext.RequestedWeaponType;
            if (_debugWeaponDefinitions != null)
            {
                for (int index = 0; index < _debugWeaponDefinitions.Length; index++)
                {
                    WeaponDefinition candidate = _debugWeaponDefinitions[index];
                    if (candidate != null && candidate.WeaponType == requestedWeaponType)
                    {
                        return candidate;
                    }
                }
            }

            return _weaponDefinition;
        }

        private void OnWeaponControlAcquired(WeaponControlAcquiredEvent @event)
        {
            _hasControl = true;
            _controllingPlayerIndex = @event.PlayerIndex;
            _controllingPlayerRoot = @event.PlayerRoot;
            _weaponOwnerRoot = @event.OwnerRoot;
            _infiniteAmmo = DebugContext.InfiniteAmmo;
            RefreshActiveWeapon(resetMagazine: false);
            RebuildIgnoredCollisionColliders(@event.PlayerRoot, @event.WeaponRoot, @event.OwnerRoot);
            LogInfo($"Weapon control acquired. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}, ignoredColliders={_ignoredCollisionColliders.Count}.");
            PublishAmmoChanged();
        }

        private void OnWeaponControlReleased(WeaponControlReleasedEvent @event)
        {
            if (!_hasControl || @event.PlayerIndex != _controllingPlayerIndex)
            {
                return;
            }

            ResetTrigger(publishSpinupCancelled: true);
            _hasControl = false;
            _controllingPlayerIndex = -1;
            _controllingPlayerRoot = null;
            _weaponOwnerRoot = null;
            _ignoredCollisionColliders.Clear();
            _ignoredCollisionLookup.Clear();
        }

        private void OnWeaponFireInput(WeaponFireInputEvent @event)
        {
            if (!_hasControl || @event.PlayerIndex != _controllingPlayerIndex)
            {
                return;
            }

            if (@event.IsHeld)
            {
                LogInfo($"Weapon fire input held. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}.");
                BeginTriggerHold();
                return;
            }

            LogInfo($"Weapon fire input released. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}.");
            ResetTrigger(publishSpinupCancelled: true);
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
            if (_isPaused)
            {
                ResetTrigger(publishSpinupCancelled: true);
            }
        }

        private void OnInfiniteAmmo(InfiniteAmmoEvent @event)
        {
            _infiniteAmmo = @event.IsEnabled;
            DebugContext.InfiniteAmmo = @event.IsEnabled;
            PublishAmmoChanged();
        }

        private void BeginTriggerHold()
        {
            if (_fireHeld || _isPaused)
            {
                return;
            }

            _fireHeld = true;
            _spinupActive = false;
            _hasFiredSinceTriggerHeld = false;
            _dryFirePublishedSinceTriggerHeld = false;
            _spinupElapsed = 0f;
            _nextShotTime = 0f;

            AutomaticFireModeDefinition fireMode = _activeWeapon != null ? _activeWeapon.FireMode : null;
            if (fireMode == null || fireMode.SpinUpSeconds <= 0f)
            {
                LogInfo($"Weapon firing immediately. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}.");
                FireFirstShotNow();
                return;
            }

            _spinupActive = true;
            LogInfo($"Weapon spin-up started. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, spinUpSeconds={fireMode.SpinUpSeconds:0.###}.");
            _globalMessageBus.Publish(new WeaponSpinupStartedEvent(
                _controllingPlayerIndex,
                _activeWeapon,
                gameObject,
                fireMode.SpinUpSeconds));
        }

        private void ResetTrigger(bool publishSpinupCancelled)
        {
            if (publishSpinupCancelled && _spinupActive && !_hasFiredSinceTriggerHeld)
            {
                LogInfo($"Weapon spin-up cancelled. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, elapsed={_spinupElapsed:0.###}.");
                _globalMessageBus?.Publish(new WeaponSpinupCancelledEvent(
                    _controllingPlayerIndex,
                    _activeWeapon,
                    gameObject));
            }

            _fireHeld = false;
            _spinupActive = false;
            _hasFiredSinceTriggerHeld = false;
            _dryFirePublishedSinceTriggerHeld = false;
            _spinupElapsed = 0f;
            _nextShotTime = 0f;
        }

        private void TickSpinup()
        {
            AutomaticFireModeDefinition fireMode = _activeWeapon.FireMode;
            if (fireMode == null)
            {
                return;
            }

            _spinupElapsed += Time.deltaTime;
            if (_spinupElapsed < fireMode.SpinUpSeconds)
            {
                return;
            }

            _spinupActive = false;
            FireFirstShotNow();
        }

        private void FireFirstShotNow()
        {
            _hasFiredSinceTriggerHeld = true;
            if (TryFireShot())
            {
                LogInfo($"Weapon first shot fired. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}, firePoint={DescribeTransform(_firePoint)}, firePointPosition={_firePoint.position.ToString("F2")}, firePointForward={_firePoint.forward.ToString("F2")}.");
            }

            _nextShotTime = Time.time + (_activeWeapon != null && _activeWeapon.FireMode != null
                ? _activeWeapon.FireMode.SecondsPerShot
                : 0f);
        }

        private void TickAutomaticFire()
        {
            AutomaticFireModeDefinition fireMode = _activeWeapon.FireMode;
            if (fireMode == null)
            {
                return;
            }

            if (_nextShotTime <= 0f)
            {
                _nextShotTime = Time.time;
            }

            int shotsThisFrame = 0;
            while (Time.time >= _nextShotTime && shotsThisFrame < fireMode.MaxCatchUpShotsPerFrame)
            {
                TryFireShot();
                _nextShotTime += fireMode.SecondsPerShot;
                shotsThisFrame++;
            }
        }

        private bool TryFireShot()
        {
            if (!HasValidWeaponConfiguration())
            {
                return false;
            }

            MagazineDefinition magazine = _activeWeapon.Magazine;
            AmmoDefinition ammo = _activeWeapon.Ammo;
            ProjectileDefinition projectile = ammo.Projectile;

            if (!_infiniteAmmo && _currentAmmo < magazine.AmmoConsumedPerShot)
            {
                PublishDryFireOnce();
                return false;
            }

            ProjectilePool projectilePool = GetOrCreatePool(projectile);
            PhysicalProjectile projectileInstance = projectilePool.Rent(_firePoint.position, _firePoint.rotation);
            if (projectileInstance == null)
            {
                LogWarning($"Unable to fire weapon because projectile pool is exhausted. weapon={_activeWeapon.DisplayName}, projectile={projectile.name}.");
                return false;
            }

            Vector3 velocity = _firePoint.forward * projectile.Speed;
            projectileInstance.Arm(
                _controllingPlayerIndex,
                _activeWeapon,
                ammo,
                projectile,
                _ignoredCollisionColliders,
                velocity,
                _globalMessageBus,
                projectilePool.Return);

            if (!_infiniteAmmo)
            {
                _currentAmmo = Mathf.Max(0, _currentAmmo - magazine.AmmoConsumedPerShot);
            }

            _globalMessageBus.Publish(new WeaponFiredEvent(
                _controllingPlayerIndex,
                _activeWeapon,
                ammo,
                projectile,
                _currentAmmo,
                _infiniteAmmo));

            _globalMessageBus.Publish(new ProjectileSpawnedEvent(
                _controllingPlayerIndex,
                _activeWeapon,
                ammo,
                projectile,
                projectileInstance));

            LogDebug($"Weapon shot fired. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}, projectile={projectile.name}.");
            PublishAmmoChanged();
            return true;
        }

        private bool HasValidWeaponConfiguration()
        {
            bool isValid = _activeWeapon != null
                && _activeWeapon.FireMode != null
                && _activeWeapon.Magazine != null
                && _activeWeapon.Ammo != null
                && _activeWeapon.Ammo.Projectile != null
                && _activeWeapon.Ammo.Projectile.ProjectilePrefab != null
                && _firePoint != null;

            if (!isValid && !_configurationErrorLogged)
            {
                _configurationErrorLogged = true;
                LogError($"Weapon configuration is incomplete on {name}.");
            }

            return isValid;
        }

        private void PublishDryFireOnce()
        {
            if (_dryFirePublishedSinceTriggerHeld)
            {
                return;
            }

            _dryFirePublishedSinceTriggerHeld = true;
            LogInfo($"Weapon dry fire. playerIndex={_controllingPlayerIndex}, weapon={DescribeWeapon(_activeWeapon)}, ammo={_currentAmmo}, infiniteAmmo={_infiniteAmmo}.");
            _globalMessageBus.Publish(new WeaponDryFireEvent(_controllingPlayerIndex, _activeWeapon));
        }

        private void PublishAmmoChanged()
        {
            if (_globalMessageBus == null || _activeWeapon == null || _activeWeapon.Magazine == null)
            {
                return;
            }

            _globalMessageBus.Publish(new WeaponAmmoChangedEvent(
                _controllingPlayerIndex,
                _activeWeapon,
                _currentAmmo,
                _activeWeapon.Magazine.ClipCapacity,
                _infiniteAmmo));
        }

        private void PrewarmProjectilePool()
        {
            if (_activeWeapon == null || _activeWeapon.Ammo == null || _activeWeapon.Ammo.Projectile == null)
            {
                return;
            }

            GetOrCreatePool(_activeWeapon.Ammo.Projectile).Prewarm();
        }

        private ProjectilePool GetOrCreatePool(ProjectileDefinition projectile)
        {
            if (_projectilePools.TryGetValue(projectile, out ProjectilePool projectilePool))
            {
                return projectilePool;
            }

            projectilePool = new ProjectilePool(projectile, _projectilePoolRoot);
            _projectilePools.Add(projectile, projectilePool);
            return projectilePool;
        }

        private void RebuildIgnoredCollisionColliders(params GameObject[] roots)
        {
            _ignoredCollisionColliders.Clear();
            _ignoredCollisionLookup.Clear();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider candidate = colliders[colliderIndex];
                    if (candidate != null && _ignoredCollisionLookup.Add(candidate))
                    {
                        _ignoredCollisionColliders.Add(candidate);
                    }
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

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform child = FindChildByName(root.GetChild(childIndex), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindPreferredFirePoint(Transform root)
        {
            return FindChildByName(root, FirePointName)
                ?? FindChildByNamePrefix(root, FirePointPrefix);
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

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform child = FindChildByNamePrefix(root.GetChild(childIndex), childNamePrefix);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static string DescribeTransform(Transform candidate)
        {
            return candidate != null ? candidate.name : "None";
        }

        private static string DescribeWeapon(WeaponDefinition weapon)
        {
            return weapon != null ? weapon.DisplayName : "None";
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
