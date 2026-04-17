using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class PlayerVesselTarget : MonoBehaviour
    {
        private static readonly List<PlayerVesselTarget> ActiveTargets = new();

        [SerializeField] private Transform _rootTransform;
        [SerializeField] private Transform _aimTransform;
        [SerializeField] private bool _useColliderBoundsAimPoint = true;
        [SerializeField, Range(0f, 1f)] private float _colliderAimHeightNormalized = 0.45f;
        [SerializeField] private float _aimVerticalWorldOffset = 0f;
        [SerializeField] private Vector3 _aimLocalOffset = new(0f, 0.45f, 0f);

        private Collider[] _aimColliders = Array.Empty<Collider>();

        public Transform RootTransform => _rootTransform != null ? _rootTransform : transform;
        public Transform AimTransform => _aimTransform != null ? _aimTransform : RootTransform;
        public Vector3 AimPoint => TryGetColliderBoundsAimPoint(out Vector3 aimPoint)
            ? aimPoint
            : AimTransform.TransformPoint(_aimLocalOffset);
        public GameObject RootObject => RootTransform.gameObject;

        private void OnEnable()
        {
            CacheAimColliders();
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        private void OnTransformChildrenChanged()
        {
            CacheAimColliders();
        }

        public static bool TryFindNearest(Vector3 position, float maxRange, out PlayerVesselTarget target)
        {
            target = null;
            float bestDistanceSq = maxRange >= 0f && !float.IsPositiveInfinity(maxRange)
                ? maxRange * maxRange
                : float.PositiveInfinity;

            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                PlayerVesselTarget candidate = ActiveTargets[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                float distanceSq = (candidate.AimPoint - position).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        private void CacheAimColliders()
        {
            Transform rootTransform = RootTransform;
            _aimColliders = rootTransform != null
                ? rootTransform.GetComponentsInChildren<Collider>(includeInactive: false)
                : Array.Empty<Collider>();
        }

        private bool TryGetColliderBoundsAimPoint(out Vector3 aimPoint)
        {
            aimPoint = default;
            if (!_useColliderBoundsAimPoint)
            {
                return false;
            }

            if (_aimColliders == null || _aimColliders.Length == 0)
            {
                CacheAimColliders();
            }

            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < _aimColliders.Length; i++)
            {
                Collider targetCollider = _aimColliders[i];
                if (targetCollider == null
                    || !targetCollider.enabled
                    || targetCollider.isTrigger
                    || !targetCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetCollider.bounds);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            aimPoint = new Vector3(
                bounds.center.x,
                Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(_colliderAimHeightNormalized)) + _aimVerticalWorldOffset,
                bounds.center.z);
            return true;
        }
    }
}
