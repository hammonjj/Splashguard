using System.Collections.Generic;
using BitBox.Library;
using Bitbox.Toymageddon.Nautical;
using UnityEngine;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public class BoyancyController : MonoBehaviourBase
    {
        [SerializeField] private float _waterDrag = 0.99f;
        [SerializeField] private float _waterAngularDrag = 0.5f;
        [SerializeField] private float _displacementAmount = 3f;
        [SerializeField] private float _depthBeforeSubmerged = 2f;

        private readonly List<Transform> _floatPoints = new();

        private Rigidbody _rigidbody;
        private bool _runtimeBuoyancyDiagnosticsLogged;

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnFixedUpdated()
        {
            if (_rigidbody == null || _floatPoints.Count == 0)
            {
                return;
            }

            if (!_rigidbody.useGravity)
            {
                ApplyGravity();
            }

            int submergedPointCount = 0;
            float totalDisplacementModifier = 0f;
            float totalSubmersion = 0f;
            float totalSubmersionFraction = 0f;
            float buoyancyShare = 1f / _floatPoints.Count;

            for (int i = 0; i < _floatPoints.Count; i++)
            {
                Transform floatPoint = _floatPoints[i];
                if (!floatPoint || !TryGetWaterSample(floatPoint.position, out WaterSample waterSample))
                {
                    continue;
                }

                float submersionDepth = waterSample.Height - floatPoint.position.y;
                if (submersionDepth <= 0f)
                {
                    continue;
                }

                submergedPointCount++;
                totalSubmersion += submersionDepth;
                float submersionFraction = Mathf.Clamp01(submersionDepth / Mathf.Max(0.01f, _depthBeforeSubmerged));
                totalSubmersionFraction += submersionFraction;

                float displacementModifier = submersionFraction * _displacementAmount;
                totalDisplacementModifier += displacementModifier;

                _rigidbody.AddForceAtPosition(
                    new Vector3(0f, Mathf.Abs(Physics.gravity.y) * displacementModifier * buoyancyShare, 0f),
                    floatPoint.position,
                    ForceMode.Acceleration);
            }

            if (submergedPointCount == 0)
            {
                return;
            }

            MaybeLogRuntimeBuoyancyDiagnostics(submergedPointCount, totalSubmersion, buoyancyShare);
            float averageSubmersionFraction = totalSubmersionFraction / _floatPoints.Count;
            ApplyWaterDrag(averageSubmersionFraction);
        }

        protected override void OnDrawnGizmos()
        {
            if (_floatPoints.Count == 0)
            {
                CacheFloatPoints();
            }

            Gizmos.color = new Color(0f, 0f, 1f, 0.5f);
            float radius = Mathf.Max(0.05f, _depthBeforeSubmerged);

            for (int i = 0; i < _floatPoints.Count; i++)
            {
                Transform floatPoint = _floatPoints[i];
                if (floatPoint)
                {
                    Gizmos.DrawSphere(floatPoint.position, radius);
                }
            }
        }

        private void OnValidate()
        {
            _waterDrag = Mathf.Max(0f, _waterDrag);
            _waterAngularDrag = Mathf.Max(0f, _waterAngularDrag);
            _displacementAmount = Mathf.Max(0.01f, _displacementAmount);
            _depthBeforeSubmerged = Mathf.Max(0.01f, _depthBeforeSubmerged);
            CacheFloatPoints();
        }

        private void OnTransformChildrenChanged()
        {
            CacheFloatPoints();
        }

        private void CacheReferences()
        {
            _rigidbody = GetComponentInParent<Rigidbody>();
            Assert.IsNotNull(_rigidbody, $"{nameof(BoyancyController)} requires a parent {nameof(Rigidbody)}.");

            CacheFloatPoints();
            LogSetupWarnings();
            _runtimeBuoyancyDiagnosticsLogged = false;
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

        private void ApplyGravity()
        {
            _rigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);
        }

        private void ApplyWaterDrag(float dragFactor)
        {
            _rigidbody.AddForce(
                -_rigidbody.linearVelocity * (_waterDrag * dragFactor) * Time.fixedDeltaTime,
                ForceMode.VelocityChange);

            _rigidbody.AddTorque(
                -_rigidbody.angularVelocity * (_waterAngularDrag * dragFactor) * Time.fixedDeltaTime,
                ForceMode.VelocityChange);
        }

        private static bool TryGetWaterSample(Vector3 worldPoint, out WaterSample waterSample)
        {
            if (WaterQuery.TrySample(worldPoint, out waterSample))
            {
                return true;
            }

            waterSample = default;
            return false;
        }

        private void MaybeLogRuntimeBuoyancyDiagnostics(int submergedPointCount, float totalSubmersion, float buoyancyShare)
        {
            if (_runtimeBuoyancyDiagnosticsLogged)
            {
                return;
            }

            float averageSubmersion = totalSubmersion / Mathf.Max(1, submergedPointCount);
            float equilibriumSubmersion = _depthBeforeSubmerged / Mathf.Max(0.01f, _displacementAmount);

            LogInfo(
                $"Buoyancy diagnostics. rigidbody={_rigidbody.name}, floatPoints={_floatPoints.Count}, submergedPoints={submergedPointCount}, buoyancyShare={buoyancyShare:0.###}, avgSubmersion={averageSubmersion:0.##}, expectedEquilibriumSubmersion={equilibriumSubmersion:0.##}, rigidbodyY={_rigidbody.position.y:0.##}, velocity=({_rigidbody.linearVelocity.x:0.##}, {_rigidbody.linearVelocity.y:0.##}, {_rigidbody.linearVelocity.z:0.##}).");

            if (averageSubmersion > equilibriumSubmersion * 1.5f)
            {
                LogWarning(
                    $"Boat starts heavily submerged relative to its buoyancy tuning. rigidbody={_rigidbody.name}, avgSubmersion={averageSubmersion:0.##}, expectedEquilibriumSubmersion={equilibriumSubmersion:0.##}. Raise the boat root or move float points upward if it still launches or flips.");
            }

            _runtimeBuoyancyDiagnosticsLogged = true;
        }

        private void LogSetupWarnings()
        {
            if (_rigidbody == null)
            {
                return;
            }

            if (_floatPoints.Count == 0)
            {
                LogWarning($"No float points found under {name}. Add child transforms for buoyancy sampling.");
                return;
            }

            Collider hullCollider = _rigidbody.GetComponent<Collider>();
            Vector3 min = transform.InverseTransformPoint(_floatPoints[0].position);
            Vector3 max = min;

            for (int i = 1; i < _floatPoints.Count; i++)
            {
                Vector3 localPosition = transform.InverseTransformPoint(_floatPoints[i].position);
                min = Vector3.Min(min, localPosition);
                max = Vector3.Max(max, localPosition);
            }

            Vector3 spread = max - min;
            Vector3 colliderSize = hullCollider != null ? hullCollider.bounds.size : Vector3.zero;

            LogInfo(
                $"Buoyancy setup. rigidbody={_rigidbody.name}, floatPoints={_floatPoints.Count}, mass={_rigidbody.mass:0.##}, colliderSize=({colliderSize.x:0.##}, {colliderSize.y:0.##}, {colliderSize.z:0.##}), floatPointSpreadLocal=({spread.x:0.##}, {spread.y:0.##}, {spread.z:0.##}).");

            if (hullCollider != null
                && (spread.x > hullCollider.bounds.size.x * 1.5f
                    || spread.z > hullCollider.bounds.size.z * 1.5f))
            {
                LogWarning(
                    $"Float point layout is much larger than the hull collider. rigidbody={_rigidbody.name}, colliderSize=({colliderSize.x:0.##}, {colliderSize.y:0.##}, {colliderSize.z:0.##}), floatPointSpreadLocal=({spread.x:0.##}, {spread.y:0.##}, {spread.z:0.##}). This can make boats unstable after scaling.");
            }
        }
    }
}
