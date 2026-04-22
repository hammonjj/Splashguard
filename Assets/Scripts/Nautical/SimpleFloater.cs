using System.Collections.Generic;
using BitBox.Library;
using Bitbox.Splashguard.Nautical.Crane;
using UnityEngine;

namespace Bitbox.Toymageddon.Nautical
{
    public static class SimpleFloaterUtility
    {
        public static float CalculateSubmersionFraction(float pointY, float waterHeight, float depthBeforeSubmerged)
        {
            float submersionDepth = waterHeight - pointY;
            if (submersionDepth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(submersionDepth / Mathf.Max(0.01f, depthBeforeSubmerged));
        }

        public static float CalculateBuoyancyAcceleration(
            float gravityMagnitude,
            float displacementStrength,
            float submersionFraction,
            float pointShare)
        {
            if (displacementStrength <= 0f || submersionFraction <= 0f || pointShare <= 0f)
            {
                return 0f;
            }

            return Mathf.Abs(gravityMagnitude) * displacementStrength * submersionFraction * pointShare;
        }

        public static Vector3 CalculateWaterDragVelocityChange(
            Vector3 velocity,
            float drag,
            float submersionFraction,
            float deltaTime)
        {
            if (drag <= 0f || submersionFraction <= 0f || deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            return -velocity * drag * submersionFraction * deltaTime;
        }

        public static Vector3 CalculateUprightTorque(
            Vector3 objectUp,
            Vector3 targetUp,
            float torqueStrength,
            float submersionFraction,
            float maxTorque)
        {
            if (torqueStrength <= 0f || submersionFraction <= 0f
                || objectUp.sqrMagnitude <= 0.0001f || targetUp.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 torque = Vector3.Cross(objectUp.normalized, targetUp.normalized)
                * torqueStrength
                * submersionFraction;
            float clampedMaxTorque = Mathf.Max(0f, maxTorque);

            if (clampedMaxTorque > 0f && torque.sqrMagnitude > clampedMaxTorque * clampedMaxTorque)
            {
                return torque.normalized * clampedMaxTorque;
            }

            return torque;
        }

        public static Vector3 CalculateAnchorAcceleration(
            Vector3 holdPoint,
            Vector3 currentPosition,
            Vector3 velocity,
            float slackRadius,
            float horizontalStopTime,
            float holdSpringAcceleration,
            float maxAnchorAcceleration)
        {
            if (maxAnchorAcceleration <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 planarError = holdPoint - currentPosition;
            planarError.y = 0f;

            Vector3 springAcceleration = Vector3.zero;
            float planarDistance = planarError.magnitude;
            if (planarDistance > slackRadius && planarDistance > Mathf.Epsilon)
            {
                springAcceleration = planarError.normalized
                    * ((planarDistance - Mathf.Max(0f, slackRadius)) * Mathf.Max(0f, holdSpringAcceleration));
            }

            Vector3 horizontalVelocity = velocity;
            horizontalVelocity.y = 0f;
            Vector3 dampingAcceleration = -horizontalVelocity / Mathf.Max(0.01f, horizontalStopTime);

            Vector3 anchorAcceleration = Vector3.ClampMagnitude(
                springAcceleration + dampingAcceleration,
                Mathf.Max(0f, maxAnchorAcceleration));
            anchorAcceleration.y = 0f;
            return anchorAcceleration;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SimpleFloater : MonoBehaviourBase
    {
        [Header("Buoyancy")]
        [SerializeField, Min(0.01f)]
        private float _displacementStrength = 1.35f;

        [SerializeField, Min(0.01f)]
        private float _depthBeforeSubmerged = 0.18f;

        [Header("Water Damping")]
        [SerializeField]
        private bool _applyWaterDrag = true;

        [SerializeField, Min(0f)]
        private float _waterDrag = 1.8f;

        [SerializeField, Min(0f)]
        private float _waterAngularDrag = 1.25f;

        [Header("Stability")]
        [SerializeField]
        private bool _applyUprightTorque = true;

        [SerializeField, Min(0f)]
        private float _uprightTorque = 0.35f;

        [SerializeField, Min(0f)]
        private float _maxUprightTorque = 3f;

        [Header("Invisible Anchor")]
        [SerializeField]
        private bool _anchorInPlace;

        [SerializeField]
        private bool _releaseAnchorWhenGrabbed = true;

        [SerializeField, Min(0f)]
        private float _anchorSlackRadius = 0.35f;

        [SerializeField, Min(0.01f)]
        private float _anchorHorizontalStopTime = 4f;

        [SerializeField, Min(0f)]
        private float _anchorHoldSpringAcceleration = 1.25f;

        [SerializeField, Min(0f)]
        private float _maxAnchorAcceleration = 6.5f;

        private readonly List<Transform> _floatPoints = new();
        private Rigidbody _rigidbody;
        private CranePickupTarget _pickupTarget;
        private Vector3 _anchorHoldPoint;
        private bool _invisibleAnchorActive;
        private bool _missingRigidbodyWarningLogged;

        public bool IsInvisibleAnchorActive => _invisibleAnchorActive;
        public Vector3 InvisibleAnchorHoldPoint => _anchorHoldPoint;

        protected override void OnEnabled()
        {
            CacheReferences();
            if (_anchorInPlace)
            {
                DropInvisibleAnchor();
            }
        }

        protected override void OnFixedUpdated()
        {
            if (_rigidbody == null)
            {
                LogMissingRigidbodyWarning();
                return;
            }

            ReleaseInvisibleAnchorIfGrabbed();
            ApplyInvisibleAnchorHold();

            if (_floatPoints.Count == 0)
            {
                return;
            }

            if (!_rigidbody.useGravity)
            {
                _rigidbody.useGravity = true;
            }

            int submergedPointCount = 0;
            float totalSubmersionFraction = 0f;
            Vector3 weightedNormal = Vector3.zero;
            float pointShare = 1f / _floatPoints.Count;

            for (int i = 0; i < _floatPoints.Count; i++)
            {
                Transform floatPoint = _floatPoints[i];
                if (floatPoint == null || !WaterQuery.TrySample(floatPoint.position, out WaterSample waterSample))
                {
                    continue;
                }

                float submersionFraction = SimpleFloaterUtility.CalculateSubmersionFraction(
                    floatPoint.position.y,
                    waterSample.Height,
                    _depthBeforeSubmerged);
                if (submersionFraction <= 0f)
                {
                    continue;
                }

                submergedPointCount++;
                totalSubmersionFraction += submersionFraction;
                weightedNormal += waterSample.Normal * submersionFraction;

                float upwardAcceleration = SimpleFloaterUtility.CalculateBuoyancyAcceleration(
                    Physics.gravity.y,
                    _displacementStrength,
                    submersionFraction,
                    pointShare);

                _rigidbody.AddForceAtPosition(
                    Vector3.up * upwardAcceleration,
                    floatPoint.position,
                    ForceMode.Acceleration);
            }

            if (submergedPointCount == 0)
            {
                return;
            }

            float averageSubmersion = totalSubmersionFraction / _floatPoints.Count;
            ApplyWaterDrag(averageSubmersion);
            ApplyUprightTorque(weightedNormal, averageSubmersion);
        }

        protected override void OnDrawnGizmos()
        {
            if (_floatPoints.Count == 0)
            {
                CacheFloatPoints();
            }

            Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.75f);
            float radius = Mathf.Max(0.025f, _depthBeforeSubmerged * 0.25f);

            for (int i = 0; i < _floatPoints.Count; i++)
            {
                Transform floatPoint = _floatPoints[i];
                if (floatPoint != null)
                {
                    Gizmos.DrawSphere(floatPoint.position, radius);
                    Gizmos.DrawLine(floatPoint.position, floatPoint.position + Vector3.down * _depthBeforeSubmerged);
                }
            }
        }

        private void OnValidate()
        {
            _displacementStrength = Mathf.Max(0.01f, _displacementStrength);
            _depthBeforeSubmerged = Mathf.Max(0.01f, _depthBeforeSubmerged);
            _waterDrag = Mathf.Max(0f, _waterDrag);
            _waterAngularDrag = Mathf.Max(0f, _waterAngularDrag);
            _uprightTorque = Mathf.Max(0f, _uprightTorque);
            _maxUprightTorque = Mathf.Max(0f, _maxUprightTorque);
            _anchorSlackRadius = Mathf.Max(0f, _anchorSlackRadius);
            _anchorHorizontalStopTime = Mathf.Max(0.01f, _anchorHorizontalStopTime);
            _anchorHoldSpringAcceleration = Mathf.Max(0f, _anchorHoldSpringAcceleration);
            _maxAnchorAcceleration = Mathf.Max(0f, _maxAnchorAcceleration);
            CacheFloatPoints();
        }

        public void DropInvisibleAnchor()
        {
            if (_rigidbody == null)
            {
                CacheReferences();
            }

            if (_rigidbody == null)
            {
                return;
            }

            _anchorHoldPoint = _rigidbody.position;
            _invisibleAnchorActive = true;
        }

        public void ReleaseInvisibleAnchor()
        {
            _invisibleAnchorActive = false;
        }

        private void OnTransformChildrenChanged()
        {
            CacheFloatPoints();
        }

        private void CacheReferences()
        {
            _rigidbody = GetComponentInParent<Rigidbody>();
            _pickupTarget = GetComponentInParent<CranePickupTarget>();
            CacheFloatPoints();
            LogMissingRigidbodyWarning();
        }

        private void CacheFloatPoints()
        {
            _floatPoints.Clear();

            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate != transform)
                {
                    _floatPoints.Add(candidate);
                }
            }
        }

        private void ApplyWaterDrag(float averageSubmersion)
        {
            if (!_applyWaterDrag)
            {
                return;
            }

            _rigidbody.AddForce(
                SimpleFloaterUtility.CalculateWaterDragVelocityChange(
                    _rigidbody.linearVelocity,
                    _waterDrag,
                    averageSubmersion,
                    Time.fixedDeltaTime),
                ForceMode.VelocityChange);

            _rigidbody.AddTorque(
                SimpleFloaterUtility.CalculateWaterDragVelocityChange(
                    _rigidbody.angularVelocity,
                    _waterAngularDrag,
                    averageSubmersion,
                    Time.fixedDeltaTime),
                ForceMode.VelocityChange);
        }

        private void ApplyUprightTorque(Vector3 weightedNormal, float averageSubmersion)
        {
            if (!_applyUprightTorque)
            {
                return;
            }

            Vector3 targetUp = weightedNormal.sqrMagnitude > 0.0001f
                ? weightedNormal.normalized
                : Vector3.up;
            Vector3 torque = SimpleFloaterUtility.CalculateUprightTorque(
                transform.up,
                targetUp,
                _uprightTorque,
                averageSubmersion,
                _maxUprightTorque);

            if (torque.sqrMagnitude > 0f)
            {
                _rigidbody.AddTorque(torque, ForceMode.Acceleration);
            }
        }

        private void ApplyInvisibleAnchorHold()
        {
            if (!_invisibleAnchorActive || _rigidbody == null)
            {
                return;
            }

            Vector3 anchorAcceleration = SimpleFloaterUtility.CalculateAnchorAcceleration(
                _anchorHoldPoint,
                _rigidbody.position,
                _rigidbody.linearVelocity,
                _anchorSlackRadius,
                _anchorHorizontalStopTime,
                _anchorHoldSpringAcceleration,
                _maxAnchorAcceleration);
            if (anchorAcceleration.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            _rigidbody.AddForce(anchorAcceleration, ForceMode.Acceleration);
        }

        private void ReleaseInvisibleAnchorIfGrabbed()
        {
            if (!_releaseAnchorWhenGrabbed || !_invisibleAnchorActive)
            {
                return;
            }

            if (_pickupTarget == null)
            {
                _pickupTarget = GetComponentInParent<CranePickupTarget>();
            }

            if (_pickupTarget != null && _pickupTarget.IsGrabbedByCrane)
            {
                ReleaseInvisibleAnchor();
            }
        }

        private void LogMissingRigidbodyWarning()
        {
            if (_rigidbody != null || _missingRigidbodyWarningLogged)
            {
                return;
            }

            LogWarning($"{nameof(SimpleFloater)} on '{name}' requires a parent {nameof(Rigidbody)}.");
            _missingRigidbodyWarningLogged = true;
        }
    }
}
