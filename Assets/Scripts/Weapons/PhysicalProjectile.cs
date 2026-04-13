using System;
using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.WeaponEvents;
using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class PhysicalProjectile : MonoBehaviourBase
    {
        private readonly List<Collider> _ignoredColliders = new();

        private Rigidbody _rigidbody;
        private Collider _projectileCollider;
        private TrailRenderer[] _trailRenderers;
        private MessageBus _globalBus;
        private Action<PhysicalProjectile> _returnToPool;
        private WeaponDefinition _weapon;
        private AmmoDefinition _ammo;
        private ProjectileDefinition _projectile;
        private int _playerIndex;
        private float _expiresAt;
        private bool _armed;

        protected override void OnAwakened()
        {
            CacheReferences();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnUpdated()
        {
            if (!_armed)
            {
                return;
            }

            if (Time.time >= _expiresAt)
            {
                Despawn();
            }
        }

        protected override void OnTriggerEntered(Collider other)
        {
            if (!_armed || other == null || other == _projectileCollider || !IsLayerInCollisionMask(other.gameObject.layer))
            {
                return;
            }

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 normal = -transform.forward;
            PublishImpact(other.gameObject, point, normal);
            Despawn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_armed || collision == null || collision.collider == null || !IsLayerInCollisionMask(collision.collider.gameObject.layer))
            {
                return;
            }

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;

            Vector3 point = collision.contactCount > 0 ? contact.point : transform.position;
            Vector3 normal = collision.contactCount > 0 ? contact.normal : -transform.forward;
            PublishImpact(collision.collider.gameObject, point, normal);
            Despawn();
        }

        public void Arm(
            int playerIndex,
            WeaponDefinition weapon,
            AmmoDefinition ammo,
            ProjectileDefinition projectile,
            IReadOnlyList<Collider> ignoredColliders,
            Vector3 velocity,
            MessageBus globalBus,
            Action<PhysicalProjectile> returnToPool)
        {
            CacheReferences();
            ResetIgnoredCollisions();

            _playerIndex = playerIndex;
            _weapon = weapon;
            _ammo = ammo;
            _projectile = projectile;
            _globalBus = globalBus;
            _returnToPool = returnToPool;
            _expiresAt = Time.time + (projectile != null ? projectile.LifetimeSeconds : 3f);
            _armed = true;

            ResetTrails(emitting: true);
            ApplyIgnoredCollisions(ignoredColliders);

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        public void PrepareForPool()
        {
            CacheReferences();
            _armed = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            ResetTrails(emitting: false);
            ResetIgnoredCollisions();
            _globalBus = null;
            _returnToPool = null;
            _weapon = null;
            _ammo = null;
            _projectile = null;
            _playerIndex = -1;
        }

        private void CacheReferences()
        {
            _rigidbody ??= GetComponent<Rigidbody>();
            _projectileCollider ??= GetComponent<Collider>();
            _trailRenderers ??= GetComponentsInChildren<TrailRenderer>(includeInactive: true);
        }

        private void ResetTrails(bool emitting)
        {
            if (_trailRenderers == null)
            {
                return;
            }

            for (int index = 0; index < _trailRenderers.Length; index++)
            {
                TrailRenderer trailRenderer = _trailRenderers[index];
                if (trailRenderer == null)
                {
                    continue;
                }

                trailRenderer.Clear();
                trailRenderer.emitting = emitting;
            }
        }

        private void ApplyIgnoredCollisions(IReadOnlyList<Collider> ignoredColliders)
        {
            if (_projectileCollider == null || ignoredColliders == null)
            {
                return;
            }

            for (int index = 0; index < ignoredColliders.Count; index++)
            {
                Collider ignoredCollider = ignoredColliders[index];
                if (ignoredCollider == null || ignoredCollider == _projectileCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(_projectileCollider, ignoredCollider, true);
                _ignoredColliders.Add(ignoredCollider);
            }
        }

        private void ResetIgnoredCollisions()
        {
            if (_projectileCollider == null)
            {
                _ignoredColliders.Clear();
                return;
            }

            for (int index = 0; index < _ignoredColliders.Count; index++)
            {
                Collider ignoredCollider = _ignoredColliders[index];
                if (ignoredCollider != null)
                {
                    Physics.IgnoreCollision(_projectileCollider, ignoredCollider, false);
                }
            }

            _ignoredColliders.Clear();
        }

        private bool IsLayerInCollisionMask(int layer)
        {
            if (_projectile == null)
            {
                return true;
            }

            return (_projectile.CollisionMask.value & (1 << layer)) != 0;
        }

        private void PublishImpact(GameObject hitObject, Vector3 point, Vector3 normal)
        {
            _globalBus?.Publish(new ProjectileImpactEvent(
                _playerIndex,
                _weapon,
                _ammo,
                _projectile,
                this,
                hitObject,
                point,
                normal));
        }

        private void Despawn()
        {
            if (!_armed)
            {
                return;
            }

            Action<PhysicalProjectile> returnToPool = _returnToPool;
            PrepareForPool();
            returnToPool?.Invoke(this);
        }
    }
}
